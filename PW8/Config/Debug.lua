const CryptProtectorInit = [[C:\Tools\PW8\Data\Crypt.lua]];
const File typeof System.IO.File;

AddResolvePath[[..\..\..\Extern\Calc\bin\Debug]];
AddResolvePath[[..\..\..\Extern\Guid\bin\Debug]];
AddResolvePath[[..\..\..\Extern\Lua\bin\Debug]];
AddResolvePath[[..\..\..\Extern\Cred\bin\Debug]];
AddResolvePath[[..\..\..\Extern\QuickConnect\bin\Debug]];

creds = package("PW.Cred.dll;CredPackage");

package("PW.Calc.dll;CalcPackage");
package("PW.Guid.dll;GuidPackage");
quickConnect = package("PW.QuickConnect.dll;QuickConnectPackage");

-- register my crypt protectors
if File:Exists(CryptProtectorInit) then
	cryptKey = package("cryptKey", CryptProtectorInit);
else
	cryptKey = package("dbgcrypt",
		function(self)
			self.myPwd = creds:NoProtector;
		end
	);
end;

local sleep = clr.System.Threading.Thread.Sleep;

local function executeTestProgress(button)

	local maxSteps : int = 50;

	for i = 1,maxSteps,1 do
		button.Label = "Schritt {0} von {1}...":Format(i, maxSteps);
		button.UpdateProgress(i, maxSteps);
		sleep(100);
	end;
end; -- executeTestProgress

local function executeMessageNotify(button)

	UI.ShowNotification("Hallo Welt");

end; -- executeMessageNotify

local function sendKeyCommand()
	SendKeyData("[Hallo]");
end;


local d = NewDashboard();
d {
	Columns = {
		"*"
	},
	Rows = {
		"*",
		"*"
	},

	--[[d:NetworkInterface {
		Row = 2,
		Column = 1
	},
	d:NetworkInterface {
		Row = 2,
		Column = 2
	},
	d:NetworkInterface {
		Row = 2,
		Column = 3
	},]]
	d:Log {
		Row = 1
	},
	d:Text {
		Row = 2,
		Text = "Hallo Welt"
	}
};

package("test",
	function (self)
		self.passwordStore = creds:CreateFileCredentialProvider("pwds", cryptKey.Protector);

		self.testProgress = CreateAction(title = "Test Progress", label = "Zeigt die Progressbar", func = executeTestProgress);
		self.sendKey = CreateHotKey(title = "Send",  key = "Ctrl+Win+T", command = sendKeyCommand);

		for i = 1,3,1 do
			self.RegisterObject("btn{0}":Format(i), CreateAction(title = "Test {0}":Format(i), label = "Zeigt eine Textbox an.", func = executeMessageNotify));
		end;

		foreach cur in quickConnect:GetVpnConfigurations() do
			self.RegisterObject("vpn://" .. cur.EventName, cur);
		end;

	end
);
