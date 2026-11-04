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
Filename: "ms-settings:defaultapps"; Description: "打开 Windows「默认应用」设置，把 ImageViewer 设为图片默认程序"; Flags: postinstall runasoriginaluser shellexec nowait skipifsilent; Tasks: fileassociations

[Code]
// 安装阶段的注册必须放在代码里执行：静默安装没有向导步骤，只有 ExecAsOriginalUser 能稳定拿到原始用户令牌，
// 否则用户级候选与默认程序会写进管理员/SYSTEM 的 HKCU。
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    if WizardIsTaskSelected('fileassociations') then
    begin
      // 机器级注册：当前进程已是提权上下文。
      Exec(ExpandConstant('{app}\ImageViewer.exe'), '--register-file-associations', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
      // 用户级候选 + 直接设为默认：必须在原始用户上下文执行。
      ExecAsOriginalUser(ExpandConstant('{app}\ImageViewer.exe'), '--register-user-file-association-candidates', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    end;
  end;
end;

[Messages]
WelcomeLabel2=这将在您的电脑上安装 [name/ver]。建议在继续前关闭其他应用程序。
FinishedLabel=ImageViewer 已安装到您的电脑。点击“完成”退出安装向导。

