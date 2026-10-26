; Voxora — Windows kurulum paketi (Inno Setup 6)
; Derleme: ISCC.exe installer\Voxora.iss  →  dist\Voxora-Setup.exe

#define MyAppName "Voxora"
#define MyAppVersion "1.1.0"
#define MyAppPublisher "Voxora"
#define MyAppExeName "Voxora.exe"

[Setup]
AppId={{8B7C2E1A-64D4-4E9F-9C77-2A5F41C9D3B0}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=Voxora-Setup
SetupIconFile=..\src\KayoneZSoundpad\Assets\app.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "autostart"; Description: "Windows açılışında başlat"; GroupDescription: "Ek seçenekler"; Flags: unchecked

[Files]
Source: "..\dist\Voxora\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{#MyAppName} Kaldır"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Otomatik başlatma görevi işaretlenirse HKCU Run anahtarına yaz (kullanıcı seviyesi)
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Voxora"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{#MyAppName}'ı şimdi başlat"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Kullanıcı verileri (%APPDATA%\KayoneZSoundpad) kaldırmada SİLİNMEZ — bilinçli karar.
; Yalnızca kurulum dizinindeki uygulama dosyaları kaldırılır.
Type: filesandordirs; Name: "{app}\logs"
