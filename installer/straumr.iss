#define MyAppName "Straumr"
#define MyAppPublisher "Albin Alm"
#define MyAppExeName "straumr.exe"
#define MyAppUrl "https://github.com/albinalm/straumr"

[Setup]
AppId={{E7A3F2B1-9C4D-4E5F-8A6B-1D2E3F4A5B6C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppUrl}
AppSupportURL={#MyAppUrl}/issues
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
LicenseFile={#MyLicenseFile}
OutputDir={#MyOutputDir}
OutputBaseFilename=straumr-{#MyAppVersion}-win-x64-setup
SetupIconFile={#MyIconFile}
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
ChangesEnvironment=yes
PrivilegesRequired=lowest
DisableProgramGroupPage=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#MySourceDir}\straumr.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
    Path: string;
    AppDir: string;
begin
    if CurStep = ssPostInstall then
    begin
        AppDir := ExpandConstant('{app}');
        if IsAdminInstallMode then
            RegQueryStringValue(HKLM, 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment', 'Path', Path)
        else
            RegQueryStringValue(HKCU, 'Environment', 'Path', Path);

        if Pos(Uppercase(AppDir), Uppercase(Path)) = 0 then
        begin
            Path := Path + ';' + AppDir;
            if IsAdminInstallMode then
                RegWriteStringValue(HKLM, 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment', 'Path', Path)
            else
                RegWriteStringValue(HKCU, 'Environment', 'Path', Path);
        end;
    end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
    Path: string;
    AppDir: string;
    P: Integer;
begin
    if CurUninstallStep = usPostUninstall then
    begin
        AppDir := ExpandConstant('{app}');
        if IsAdminInstallMode then
            RegQueryStringValue(HKLM, 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment', 'Path', Path)
        else
            RegQueryStringValue(HKCU, 'Environment', 'Path', Path);

        P := Pos(';' + Uppercase(AppDir), Uppercase(Path));
        if P > 0 then
            Delete(Path, P, Length(AppDir) + 1)
        else begin
            P := Pos(Uppercase(AppDir) + ';', Uppercase(Path));
            if P > 0 then
                Delete(Path, P, Length(AppDir) + 1)
            else begin
                P := Pos(Uppercase(AppDir), Uppercase(Path));
                if P > 0 then
                    Delete(Path, P, Length(AppDir));
            end;
        end;

        if IsAdminInstallMode then
            RegWriteStringValue(HKLM, 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment', 'Path', Path)
        else
            RegWriteStringValue(HKCU, 'Environment', 'Path', Path);
    end;
end;
