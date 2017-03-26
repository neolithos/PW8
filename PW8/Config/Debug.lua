
resolvePath[[..\..\..\Extern\Calc\bin\Debug]];
resolvePath[[..\..\..\Extern\Lua\bin\Debug]];
resolvePath[[..\..\..\Extern\Cred\bin\Debug]];
resolvePath[[..\..\..\Extern\QuickConnect\bin\Debug]];

creds = package("PW.Cred.dll;CredPackage");

package("PW.Calc.dll;CalcPackage");
package("PW.Lua.dll;LuaPackage");
quickConnect = package("PW.QuickConnect.dll;QuickConnectPackage");

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

package("test",
	function (self)
		--self.passwordStore = creds:CreateCredentialProvider("pwds");
		self.connectTecWareHome = quickConnect:CreateConnection("TecWare Home", [[\\Garten\Stein$]], "M:", "pwd://garten/stein");

		self.testProgress = CreateAction(title = "Test Progress", label = "Zeigt die Progressbar", func = executeTestProgress);

		for i = 1,3,1 do
			self.RegisterObject("btn{0}":Format(i), CreateAction(title = "Test {0}":Format(i), label = "Zeigt eine Textbox an.", func = executeMessageNotify));
		end;

	end
);


-- package("assebly qualified name")