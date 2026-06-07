; Inno Setup script for ExtrabbitCode Modern UI.
; Two selectable components:
;   * app   - the standalone Gallery WPF app (self-contained; no .NET prerequisite)
;   * addin - the Inventor 2025+ add-in (deployed to ProgramData + the Inventor Addins folder)
;
; Build payloads first (installer\build.ps1 does this), then compile with:
;   ISCC.exe /DAppVersion=1.2.3 installer\ModernUi.iss

#define AppName "ExtrabbitCode Modern UI"
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#define Publisher "ExtrabbitCode"
#define AppExe "ExtrabbitCode.Inventor.ModernUi.Gallery.exe"
#define AddinDeploy "{commonappdata}\ExtrabbitCode\ExtrabbitCode.Inventor.ModernUi.Demo.AddIn"
#define InventorAddins "{commonappdata}\Autodesk\Inventor Addins"

[Setup]
AppId={{B7F4E1B2-3C7A-4D8E-9F2A-6D1C5E0A77AA}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#Publisher}
DefaultDirName={autopf}\ExtrabbitCode\Modern UI Gallery
DefaultGroupName=ExtrabbitCode Modern UI
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=ExtrabbitCode.Inventor.ModernUi-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\resources\ModernUi.ico
WizardImageFile=wizard-large.bmp
WizardSmallImageFile=wizard-small.bmp
UninstallDisplayIcon={app}\ModernUi.ico
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Writing to Program Files and ProgramData\Autodesk requires elevation.
PrivilegesRequired=admin
UninstallDisplayName={#AppName}

[Components]
Name: "app";   Description: "Modern UI Gallery (standalone app)";        Types: full custom
Name: "addin"; Description: "Inventor add-in (requires Inventor 2025+)"; Types: full custom

[Files]
; Brand icon, always installed -> used as the Add/Remove Programs icon (any component selection).
Source: "..\resources\ModernUi.ico"; DestDir: "{app}"; Flags: ignoreversion

; Standalone, self-contained Gallery app -> Program Files.
Source: "publish\app\*"; DestDir: "{app}"; Components: app; Excludes: "*.pdb"; Flags: recursesubdirs ignoreversion

; Inventor add-in payload -> ProgramData (the .addin's <Assembly> points here).
Source: "publish\addin\*"; DestDir: "{#AddinDeploy}"; Components: addin; Excludes: "*.pdb"; Flags: recursesubdirs ignoreversion

; .addin manifest -> Inventor's add-ins folder so Inventor discovers it.
Source: "..\ExtrabbitCode.Inventor.ModernUi.Demo.AddIn\Addin\ExtrabbitCode.Inventor.ModernUi.Demo.AddIn.addin"; DestDir: "{#InventorAddins}"; Components: addin; Flags: ignoreversion

[Icons]
Name: "{group}\Modern UI Gallery"; Filename: "{app}\{#AppExe}"; Components: app
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Modern UI Gallery"; Filename: "{app}\{#AppExe}"; Components: app; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Components: app; Flags: unchecked

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch Modern UI Gallery"; Flags: nowait postinstall skipifsilent; Components: app

[UninstallDelete]
; Remove the deploy folder we created (and any leftovers) on uninstall.
Type: filesandordirs; Name: "{#AddinDeploy}"
