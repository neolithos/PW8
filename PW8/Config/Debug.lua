const CryptProtectorInit = [[C:\Tools\PW8\Data\Crypt.lua]];
const BackupTest = [[C:\Tools\PW8\Data\Backup.lua]];
const File typeof System.IO.File;

AddResolvePath[[..\..\..\Extern\Calc\bin\Debug]];
AddResolvePath[[..\..\..\Extern\Guid\bin\Debug]];
AddResolvePath[[..\..\..\Extern\Lua\bin\Debug]];
AddResolvePath[[..\..\..\Extern\Cred\bin\Debug]];
AddResolvePath[[..\..\..\Extern\Backup\bin\Debug\net48]];
AddResolvePath[[..\..\..\Extern\QuickConnect\bin\Debug]];

creds = package("PW.Cred.dll;CredPackage");

package("PW.Calc.dll;CalcPackage");
package("PW.Guid.dll;GuidPackage");
quickConnect = package("PW.QuickConnect.dll;QuickConnectPackage");
backup = package("PW.Backup.dll;BackupPackage");

if File:Exists(BackupTest) then
	backupTest = package("backupTest", BackupTest);
end;

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
	Background = "#1C1C1C",
	Foreground = "#FFFFFF",
	Border = "#6DB8F2",
	Opaciy = 0.95,
	
	Margin = "12";

	d:Text {
		Text = "Perfect Working"
	},

	d:Border {
		BorderThickness = "0,1,0,1",
		Margin = "0,3,0,3",
		Padding = "0,3,0,3",
		d:Log {
		}
	},

	d:NetworkInterface {
		InterfaceName = "LAN"
	},
	d:NetworkInterface {
		InterfaceName = "OpenVPN",
		IpLookup = function (addr) : string
			if IsNetwork(24, addr, "10.0.58.0") then
				return "TecWare";
			end;
			return nil;
		end
	}
};

package("test",
	function (self)
		self.passwordStore = creds:CreateFileCredentialProvider("pwds", cryptKey.Protector);
		-- self.tecwareCreds = creds:CreateFileCredentialProvider('C:\\Temp\\Test\\tecware.xcreds', cryptKey.twDes, true);


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
