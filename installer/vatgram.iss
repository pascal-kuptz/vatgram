; ─────────────────────────────────────────────────────────────────────────────
;  vatgram – installer
;  Inno Setup 6 script. Compiles to setup\vatgram-setup.exe.
;
;  Build pipeline: see installer\build.ps1 — does dotnet publish + iscc.
;
;  Requires: Inno Setup 6 (https://jrsoftware.org/isdl.php)
; ─────────────────────────────────────────────────────────────────────────────

#define AppName "vatGram"
#define AppVersion "1.0.0"
#define AppPublisher "vatGram"
#define AppURL "https://github.com/vatgram"
#define AppExeName "vatgram.exe"
; Stable AppId — DO NOT change between versions; uninstaller uses this.
#define AppId "{{B7B5C5D2-7F2A-4B3C-9F1A-A474B24C0001}"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline
OutputDir=setup
OutputBaseFilename=vatgram-setup
SetupIconFile=..\assets\vatgram.ico
UninstallDisplayIcon={app}\{#AppExeName}
Compression=lzma2/ultra
SolidCompression=yes
WizardStyle=modern
ShowLanguageDialog=no
DisableWelcomePage=no
DisableReadyMemo=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardSizePercent=120
MinVersion=10.0.17763
AppCopyright=© {#AppPublisher}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"
Name: "autostart"; Description: "Launch vatGram automatically when Windows starts"; GroupDescription: "Startup:"

[Files]
; Tray app + dependencies (output of `dotnet publish src/Vatgram.Tray ... -r win-x64`)
Source: "..\src\Vatgram.Tray\bin\Release\net10.0-windows\win-x64\publish\*"; \
    DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; Plugin DLLs into vPilot\Plugins\
Source: "..\src\Vatgram.Plugin\bin\Release\*.dll"; \
    DestDir: "{code:GetVPilotPluginsDir}"; Flags: ignoreversion; \
    Check: VPilotFolderValid

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{userdesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon
Name: "{userstartup}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: autostart

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch vatGram now"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Remove plugin DLLs we copied; do NOT touch the user's settings (%APPDATA%\vatgram\)
Type: files; Name: "{code:GetVPilotPluginsDir}\Vatgram.Plugin.dll"
Type: files; Name: "{code:GetVPilotPluginsDir}\Vatgram.Plugin.pdb"
Type: files; Name: "{code:GetVPilotPluginsDir}\Vatgram.Shared.dll"
Type: files; Name: "{code:GetVPilotPluginsDir}\System.Text.Json.dll"
Type: files; Name: "{code:GetVPilotPluginsDir}\System.Buffers.dll"
Type: files; Name: "{code:GetVPilotPluginsDir}\System.Memory.dll"
Type: files; Name: "{code:GetVPilotPluginsDir}\System.Numerics.Vectors.dll"
Type: files; Name: "{code:GetVPilotPluginsDir}\System.Runtime.CompilerServices.Unsafe.dll"
Type: files; Name: "{code:GetVPilotPluginsDir}\System.Text.Encodings.Web.dll"
Type: files; Name: "{code:GetVPilotPluginsDir}\System.Threading.Tasks.Extensions.dll"
Type: files; Name: "{code:GetVPilotPluginsDir}\System.ValueTuple.dll"
Type: files; Name: "{code:GetVPilotPluginsDir}\Microsoft.Bcl.AsyncInterfaces.dll"

[Code]
var
  VPilotPathPage: TInputDirWizardPage;
  ResolvedVPilotRoot: string;

function IsVPilotRunning(): Boolean;
var
  ResultCode: Integer;
begin
  // tasklist | find — exit 0 if found, 1 if not
  Result := False;
  if Exec(ExpandConstant('{cmd}'), '/C tasklist /FI "IMAGENAME eq vPilot.exe" /NH | find /I "vPilot.exe" >nul', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    Result := (ResultCode = 0);
end;

function FindVPilotInRegistry(): string;
var
  Names: TArrayOfString;
  DisplayName, InstallLocation, SubKeyPath: string;
  i, j: Integer;
  RootKeys: array[0..1] of Integer;
begin
  Result := '';
  RootKeys[0] := HKLM;
  RootKeys[1] := HKCU;
  for i := 0 to 1 do
  begin
    if RegGetSubkeyNames(RootKeys[i], 'Software\Microsoft\Windows\CurrentVersion\Uninstall', Names) then
    begin
      for j := 0 to GetArrayLength(Names) - 1 do
      begin
        SubKeyPath := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\' + Names[j];
        if RegQueryStringValue(RootKeys[i], SubKeyPath, 'DisplayName', DisplayName) then
        begin
          if Pos('vpilot', LowerCase(DisplayName)) > 0 then
          begin
            if RegQueryStringValue(RootKeys[i], SubKeyPath, 'InstallLocation', InstallLocation) then
            begin
              if (InstallLocation <> '') and DirExists(InstallLocation) then
              begin
                Result := RemoveBackslashUnlessRoot(InstallLocation);
                Exit;
              end;
            end;
          end;
        end;
      end;
    end;
  end;
end;

function FindVPilotInCommonPaths(): string;
var
  Drives: array[0..3] of string;
  Suffixes: array[0..4] of string;
  Drive, Suffix: Integer;
  Candidate: string;
begin
  Result := '';
  Drives[0] := 'C:\';
  Drives[1] := 'D:\';
  Drives[2] := 'E:\';
  Drives[3] := ExpandConstant('{localappdata}') + '\';
  Suffixes[0] := 'Microsoft Flight Simulator 2024 Utilities\vPilot';
  Suffixes[1] := 'Program Files\vPilot';
  Suffixes[2] := 'Program Files (x86)\vPilot';
  Suffixes[3] := 'vPilot';
  Suffixes[4] := 'Microsoft Flight Simulator\vPilot';
  for Drive := 0 to 3 do
  begin
    for Suffix := 0 to 4 do
    begin
      Candidate := Drives[Drive] + Suffixes[Suffix];
      if FileExists(Candidate + '\vPilot.exe') and FileExists(Candidate + '\RossCarlson.Vatsim.Vpilot.Plugins.dll') then
      begin
        Result := Candidate;
        Exit;
      end;
    end;
  end;
end;

function DetectVPilotPath(): string;
begin
  Result := FindVPilotInRegistry();
  if (Result <> '') and FileExists(Result + '\vPilot.exe') then Exit;
  Result := FindVPilotInCommonPaths();
end;

procedure InitializeWizard();
var
  Detected: string;
begin
  VPilotPathPage := CreateInputDirPage(
    wpSelectDir,
    'Locate vPilot',
    'Where is vPilot installed?',
    'vatGram needs to copy a small plugin DLL into vPilot''s Plugins folder. ' +
    'Setup tried to detect it automatically — please verify the path below or browse to your vPilot install folder.',
    False,
    '');
  VPilotPathPage.Add('vPilot install folder:');
  Detected := DetectVPilotPath();
  if Detected <> '' then VPilotPathPage.Values[0] := Detected
  else VPilotPathPage.Values[0] := ExpandConstant('{commonpf}') + '\vPilot';
end;

function VPilotFolderValid(): Boolean;
begin
  Result := FileExists(ResolvedVPilotRoot + '\vPilot.exe')
        and FileExists(ResolvedVPilotRoot + '\RossCarlson.Vatsim.Vpilot.Plugins.dll');
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  Path: string;
begin
  Result := True;
  if CurPageID = VPilotPathPage.ID then
  begin
    Path := RemoveBackslashUnlessRoot(VPilotPathPage.Values[0]);
    if not FileExists(Path + '\vPilot.exe') then
    begin
      MsgBox('vPilot.exe was not found in:' + #13#10 + Path + #13#10 + #13#10 +
             'Pick the folder that contains vPilot.exe.', mbError, MB_OK);
      Result := False;
      Exit;
    end;
    if not FileExists(Path + '\RossCarlson.Vatsim.Vpilot.Plugins.dll') then
    begin
      MsgBox('vPilot was found, but the plugin SDK DLL is missing:' + #13#10 +
             'RossCarlson.Vatsim.Vpilot.Plugins.dll' + #13#10 + #13#10 +
             'You may have an unsupported vPilot version.', mbError, MB_OK);
      Result := False;
      Exit;
    end;
    if IsVPilotRunning() then
    begin
      MsgBox('vPilot is currently running. Please close it before installing — ' +
             'the plugin DLL cannot be replaced while vPilot has it loaded.',
             mbError, MB_OK);
      Result := False;
      Exit;
    end;
    ResolvedVPilotRoot := Path;
  end;
end;

function GetVPilotPluginsDir(Param: string): string;
begin
  Result := ResolvedVPilotRoot + '\Plugins';
  if not DirExists(Result) then
    ForceDirectories(Result);
end;

procedure CleanStrayVPilotSdk();
var
  pluginsDir, sdkDll, sdkXml, sdkPdb: string;
  removed: Boolean;
begin
  // The vPilot SDK (RossCarlson.Vatsim.Vpilot.Plugins.dll) belongs ONLY in vPilot's
  // main folder where vPilot loads it on startup. If a copy ALSO sits in Plugins\
  // (e.g. left by an older plugin installer or copied manually by the user) it
  // creates an assembly-identity conflict — vPilot loads two copies of IPlugin /
  // IBroker with different assembly identities, so when the loader checks
  // `type implements IPlugin`, every plugin compiled against the canonical SDK
  // silently fails the check and gets skipped. No error log, no UI, just no plugins.
  pluginsDir := ResolvedVPilotRoot + '\Plugins';
  sdkDll := pluginsDir + '\RossCarlson.Vatsim.Vpilot.Plugins.dll';
  sdkXml := pluginsDir + '\RossCarlson.Vatsim.Vpilot.Plugins.xml';
  sdkPdb := pluginsDir + '\RossCarlson.Vatsim.Vpilot.Plugins.pdb';
  removed := False;
  if FileExists(sdkDll) then begin DeleteFile(sdkDll); removed := True; end;
  if FileExists(sdkXml) then DeleteFile(sdkXml);
  if FileExists(sdkPdb) then DeleteFile(sdkPdb);
  if removed then
    MsgBox('Cleanup: removed a stray copy of the vPilot SDK from your Plugins folder.' + #13#10 + #13#10 +
           'It was preventing plugins (including vatGram) from loading. Your main vPilot install is unaffected.',
           mbInformation, MB_OK);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssInstall then
  begin
    // Run before our DLLs land in the Plugins folder so vPilot picks up a clean state.
    CleanStrayVPilotSdk();
  end;
  if CurStep = ssPostInstall then
  begin
    // Best-effort: kill any stale vatgram process so the new exe isn't blocked.
    Exec(ExpandConstant('{cmd}'), '/C taskkill /F /IM vatgram.exe >nul 2>&1', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;

function InitializeUninstall(): Boolean;
var
  ResultCode: Integer;
begin
  // Stop running vatgram so files can be removed
  Exec(ExpandConstant('{cmd}'), '/C taskkill /F /IM vatgram.exe >nul 2>&1', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := True;
end;
