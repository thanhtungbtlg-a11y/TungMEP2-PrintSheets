#define MyAppName "TungMEP2 PrintSheets"
#define MyAppVersion "2026.10.03.1"
#ifndef MyBuildId
#define MyBuildId "2026.10.03.1"
#endif
[Setup]
AppId={{50BBBA72-ECD7-4A41-99FB-991679A32F4C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=TungMEP2
VersionInfoVersion=2026.10.3.1
VersionInfoDescription=Manual STT PrintSheets for Autodesk Revit 2024
DefaultDirName={localappdata}\TungMEP2-PrintSheets\Revit2024\{#MyBuildId}
UsePreviousAppDir=no
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\dist
OutputBaseFilename=TungMEP2-PrintSheets-Setup-{#MyAppVersion}-Revit2024
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
UninstallFilesDir={app}\Uninstall
SetupLogging=yes
CloseApplications=no
RestartApplications=no
[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
[Files]
Source: "..\release\TungMEP2-PrintSheets\net48\TungMEP2.PrintSheets.dll"; DestDir: "{app}\net48"; Flags: ignoreversion
Source: "README.txt"; DestDir: "{app}"; Flags: ignoreversion
[Code]
function XmlEscape(Value: String): String;
begin
  Result := Value;
  StringChangeEx(Result, '&', '&amp;', True);
  StringChangeEx(Result, '<', '&lt;', True);
  StringChangeEx(Result, '>', '&gt;', True);
  StringChangeEx(Result, '"', '&quot;', True);
end;
function GetManifestPath(): String;
begin
  Result := ExpandConstant('{userappdata}\Autodesk\Revit\Addins\2024\TungMEP2-PrintSheets.addin');
end;
function InstalledAssembly(): String;
begin
  Result := ExpandConstant('{app}\net48\TungMEP2.PrintSheets.dll');
end;
function InitializeSetup(): Boolean;
var Locator, Service, Processes: Variant;
begin
  Result := False;
  try
    Locator := CreateOleObject('WbemScripting.SWbemLocator');
    Service := Locator.ConnectServer('.', 'root\CIMV2');
    Processes := Service.ExecQuery('SELECT Name FROM Win32_Process WHERE Name="Revit.exe"');
    if Processes.Count > 0 then begin
      MsgBox('Close Revit before installing PrintSheets.', mbError, MB_OK);
      Exit;
    end;
  except
    MsgBox('Could not check Revit processes. Close Revit and use the portable installer instead.', mbError, MB_OK);
    Exit;
  end;
  Result := True;
end;
procedure CurStepChanged(CurStep: TSetupStep);
var Manifest, Backup: String;
begin
  if CurStep = ssPostInstall then begin
    ForceDirectories(ExtractFileDir(GetManifestPath()));
    if FileExists(GetManifestPath()) then begin
      Backup := ExpandConstant('{app}\manifest-before-') + GetDateTimeString('yyyymmdd-hhnnss', '-', ':') + '.xml';
      if not FileCopy(GetManifestPath(), Backup, True) then RaiseException('Could not back up previous manifest.');
    end;
    Manifest := '<?xml version="1.0" encoding="utf-8"?>' + #13#10 +
      '<RevitAddIns><AddIn Type="Application">' + #13#10 +
      '<Name>TungMEP2 PrintSheets</Name>' + #13#10 +
      '<Assembly>' + XmlEscape(InstalledAssembly()) + '</Assembly>' + #13#10 +
      '<AddInId>8E5C88F4-0C70-4CB7-A2CF-C792E7BF762D</AddInId>' + #13#10 +
      '<FullClassName>TungMEP2.PrintSheets.Application</FullClassName>' + #13#10 +
      '<VendorId>TM2P</VendorId>' + #13#10 +
      '<VendorDescription>PrintSheets 2026.10.03.1 - Revit 2024</VendorDescription>' + #13#10 +
      '</AddIn></RevitAddIns>' + #13#10;
    if not SaveStringToFile(GetManifestPath(), Utf8Encode(Manifest), False) then RaiseException('Could not register PrintSheets.');
  end;
end;
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var Manifest: AnsiString;
begin
  if CurUninstallStep = usUninstall then
    if LoadStringFromFile(GetManifestPath(), Manifest) then
      if Pos(XmlEscape(InstalledAssembly()), Utf8Decode(Manifest)) > 0 then DeleteFile(GetManifestPath());
end;
