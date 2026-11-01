#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "."
#endif
#ifndef OutputDirectory
  #define OutputDirectory "."
#endif

[Setup]
AppId={{E4A60292-839A-4C2B-B811-C22BBD3AA634}
AppName=ImageViewer
AppVersion={#AppVersion}
AppPublisher=ImageViewer
DefaultDirName={autopf}\ImageViewer
DefaultGroupName=ImageViewer
DisableProgramGroupPage=yes
DisableWelcomePage=no
DisableDirPage=no
DisableReadyPage=no
PrivilegesRequired=admin
UninstallFilesDir={app}\_uninstall
UsePreviousAppDir=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17134
SetupIconFile=..\ImageViewer.ico
UninstallDisplayIcon={app}\ImageViewer.exe
OutputDir={#OutputDirectory}
OutputBaseFilename=ImageViewer-{#AppVersion}-win-x64-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern dynamic
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "chinesesimplified"; MessagesFile: "ChineseSimplified.isl"

[Tasks]
Name: "fileassociations"; Description: "注册 ImageViewer 为图片打开方式"; GroupDescription: "附加选项："
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加选项："; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "ImageViewer.exe.config,Configuration\*,Logs\*"

[Icons]
Name: "{autoprograms}\ImageViewer"; Filename: "{app}\ImageViewer.exe"
Name: "{autodesktop}\ImageViewer"; Filename: "{app}\ImageViewer.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\ImageViewer.exe"; Parameters: "--register-file-associations"; Flags: runhidden waituntilterminated; Tasks: fileassociations
Filename: "{app}\ImageViewer.exe"; Parameters: "--register-user-file-association-candidates"; Flags: runasoriginaluser runhidden waituntilterminated; Tasks: fileassociations

[Messages]
WelcomeLabel2=这将在您的电脑上安装 [name/ver]。建议在继续前关闭其他应用程序。
FinishedLabel=ImageViewer 已安装到您的电脑。点击“完成”退出安装向导。

