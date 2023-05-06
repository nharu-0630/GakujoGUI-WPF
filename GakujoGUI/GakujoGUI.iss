; -- CodeDependencies.iss --
;
; This script shows how to download and install any dependency such as .NET,
; Visual C++ or SQL Server during your application's installation process.
;
; contribute: https://github.com/DomGries/InnoDependencyInstaller


; -----------
; SHARED CODE
; -----------
[Code]
// types and variables
type
  TDependency_Entry = record
    Filename: String;
    Parameters: String;
    Title: String;
    URL: String;
    Checksum: String;
    ForceSuccess: Boolean;
    RestartAfter: Boolean;
  end;

var
  Dependency_Memo: String;
  Dependency_List: array of TDependency_Entry;
  Dependency_NeedRestart, Dependency_ForceX86: Boolean;
  Dependency_DownloadPage: TDownloadWizardPage;

procedure Dependency_Add(const Filename, Parameters, Title, URL, Checksum: String; const ForceSuccess, RestartAfter: Boolean);
var
  Dependency: TDependency_Entry;
  DependencyCount: Integer;
begin
  Dependency_Memo := Dependency_Memo + #13#10 + '%1' + Title;

  Dependency.Filename := Filename;
  Dependency.Parameters := Parameters;
  Dependency.Title := Title;

  if FileExists(ExpandConstant('{tmp}{\}') + Filename) then begin
    Dependency.URL := '';
  end else begin
    Dependency.URL := URL;
  end;

  Dependency.Checksum := Checksum;
  Dependency.ForceSuccess := ForceSuccess;
  Dependency.RestartAfter := RestartAfter;

  DependencyCount := GetArrayLength(Dependency_List);
  SetArrayLength(Dependency_List, DependencyCount + 1);
  Dependency_List[DependencyCount] := Dependency;
end;

<event('InitializeWizard')>
procedure Dependency_Internal1;
begin
  Dependency_DownloadPage := CreateDownloadPage(SetupMessage(msgWizardPreparing), SetupMessage(msgPreparingDesc), nil);
end;

<event('PrepareToInstall')>
function Dependency_Internal2(var NeedsRestart: Boolean): String;
var
  DependencyCount, DependencyIndex, ResultCode: Integer;
  Retry: Boolean;
  TempValue: String;
begin
  DependencyCount := GetArrayLength(Dependency_List);

  if DependencyCount > 0 then begin
    Dependency_DownloadPage.Show;

    for DependencyIndex := 0 to DependencyCount - 1 do begin
      if Dependency_List[DependencyIndex].URL <> '' then begin
        Dependency_DownloadPage.Clear;
        Dependency_DownloadPage.Add(Dependency_List[DependencyIndex].URL, Dependency_List[DependencyIndex].Filename, Dependency_List[DependencyIndex].Checksum);

        Retry := True;
        while Retry do begin
          Retry := False;

          try
            Dependency_DownloadPage.Download;
          except
            if Dependency_DownloadPage.AbortedByUser then begin
              Result := Dependency_List[DependencyIndex].Title;
              DependencyIndex := DependencyCount;
            end else begin
              case SuppressibleMsgBox(AddPeriod(GetExceptionMessage), mbError, MB_ABORTRETRYIGNORE, IDIGNORE) of
                IDABORT: begin
                  Result := Dependency_List[DependencyIndex].Title;
                  DependencyIndex := DependencyCount;
                end;
                IDRETRY: begin
                  Retry := True;
                end;
              end;
            end;
          end;
        end;
      end;
    end;

    if Result = '' then begin
      for DependencyIndex := 0 to DependencyCount - 1 do begin
        Dependency_DownloadPage.SetText(Dependency_List[DependencyIndex].Title, '');
        Dependency_DownloadPage.SetProgress(DependencyIndex + 1, DependencyCount + 1);

        while True do begin
          ResultCode := 0;
          if ShellExec('', ExpandConstant('{tmp}{\}') + Dependency_List[DependencyIndex].Filename, Dependency_List[DependencyIndex].Parameters, '', SW_SHOWNORMAL, ewWaitUntilTerminated, ResultCode) then begin
            if Dependency_List[DependencyIndex].RestartAfter then begin
              if DependencyIndex = DependencyCount - 1 then begin
                Dependency_NeedRestart := True;
              end else begin
                NeedsRestart := True;
                Result := Dependency_List[DependencyIndex].Title;
              end;
              break;
            end else if (ResultCode = 0) or Dependency_List[DependencyIndex].ForceSuccess then begin // ERROR_SUCCESS (0)
              break;
            end else if ResultCode = 1641 then begin // ERROR_SUCCESS_REBOOT_INITIATED (1641)
              NeedsRestart := True;
              Result := Dependency_List[DependencyIndex].Title;
              break;
            end else if ResultCode = 3010 then begin // ERROR_SUCCESS_REBOOT_REQUIRED (3010)
              Dependency_NeedRestart := True;
              break;
            end;
          end;

          case SuppressibleMsgBox(FmtMessage(SetupMessage(msgErrorFunctionFailed), [Dependency_List[DependencyIndex].Title, IntToStr(ResultCode)]), mbError, MB_ABORTRETRYIGNORE, IDIGNORE) of
            IDABORT: begin
              Result := Dependency_List[DependencyIndex].Title;
              break;
            end;
            IDIGNORE: begin
              break;
            end;
          end;
        end;

        if Result <> '' then begin
          break;
        end;
      end;

      if NeedsRestart then begin
        TempValue := '"' + ExpandConstant('{srcexe}') + '" /restart=1 /LANG="' + ExpandConstant('{language}') + '" /DIR="' + WizardDirValue + '" /GROUP="' + WizardGroupValue + '" /TYPE="' + WizardSetupType(False) + '" /COMPONENTS="' + WizardSelectedComponents(False) + '" /TASKS="' + WizardSelectedTasks(False) + '"';
        if WizardNoIcons then begin
          TempValue := TempValue + ' /NOICONS';
        end;
        RegWriteStringValue(HKA, 'SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce', '{#SetupSetting("AppName")}', TempValue);
      end;
    end;

    Dependency_DownloadPage.Hide;
  end;
end;

<event('UpdateReadyMemo')>
function Dependency_Internal3(const Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo, MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String): String;
begin
  Result := '';
  if MemoUserInfoInfo <> '' then begin
    Result := Result + MemoUserInfoInfo + Newline + NewLine;
  end;
  if MemoDirInfo <> '' then begin
    Result := Result + MemoDirInfo + Newline + NewLine;
  end;
  if MemoTypeInfo <> '' then begin
    Result := Result + MemoTypeInfo + Newline + NewLine;
  end;
  if MemoComponentsInfo <> '' then begin
    Result := Result + MemoComponentsInfo + Newline + NewLine;
  end;
  if MemoGroupInfo <> '' then begin
    Result := Result + MemoGroupInfo + Newline + NewLine;
  end;
  if MemoTasksInfo <> '' then begin
    Result := Result + MemoTasksInfo;
  end;

  if Dependency_Memo <> '' then begin
    if MemoTasksInfo = '' then begin
      Result := Result + SetupMessage(msgReadyMemoTasks);
    end;
    Result := Result + FmtMessage(Dependency_Memo, [Space]);
  end;
end;

<event('NeedRestart')>
function Dependency_Internal4: Boolean;
begin
  Result := Dependency_NeedRestart;
end;

function Dependency_IsX64: Boolean;
begin
  Result := not Dependency_ForceX86 and IsWin64;
end;

function Dependency_String(const x86, x64: String): String;
begin
  if Dependency_IsX64 then begin
    Result := x64;
  end else begin
    Result := x86;
  end;
end;

function Dependency_ArchSuffix: String;
begin
  Result := Dependency_String('', '_x64');
end;

function Dependency_ArchTitle: String;
begin
  Result := Dependency_String(' (x86)', ' (x64)');
end;

function Dependency_IsNetCoreInstalled(const Version: String): Boolean;
var
  ResultCode: Integer;
begin
  // source code: https://github.com/dotnet/deployment-tools/tree/master/src/clickonce/native/projects/NetCoreCheck
  if not FileExists(ExpandConstant('{tmp}{\}') + 'netcorecheck' + Dependency_ArchSuffix + '.exe') then begin
    ExtractTemporaryFile('netcorecheck' + Dependency_ArchSuffix + '.exe');
  end;
  Result := ShellExec('', ExpandConstant('{tmp}{\}') + 'netcorecheck' + Dependency_ArchSuffix + '.exe', Version, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

procedure Dependency_AddDotNet60Desktop;
begin
  // https://dotnet.microsoft.com/download/dotnet/6.0
  if not Dependency_IsNetCoreInstalled('Microsoft.WindowsDesktop.App 6.0.4') then begin
    Dependency_Add('dotnet60desktop' + Dependency_ArchSuffix + '.exe',
      '/lcid ' + IntToStr(GetUILanguage) + ' /passive /install /quiet /norestart',
      '.NET Desktop Runtime 6.0.4' + Dependency_ArchTitle,
      Dependency_String('https://download.visualstudio.microsoft.com/download/pr/2a392287-fd51-4ee8-9c15-a672ab9bc55d/03d4784b3a543a0fb9ce5677ed13a9a3/windowsdesktop-runtime-6.0.11-win-x86.exe', 'https://download.visualstudio.microsoft.com/download/pr/0192a249-3ec8-4374-a827-e186dd58d55d/cec046575f3eb2247a10ba3d50f5cf6c/windowsdesktop-runtime-6.0.11-win-x64.exe'),
      '', False, False);
  end;
end;

function Dependency_IsWebviewInstalled(): Boolean;
begin
  if Dependency_IsX64 then begin
    Result := RegKeyExists(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}') or RegKeyExists(HKCU, 'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}')
  end else begin
    Result := RegKeyExists(HKLM, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}') or RegKeyExists(HKCU, 'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}')
  end;
end;

procedure Dependency_AddWebview;
begin
  if not Dependency_IsWebviewInstalled() then begin
    Dependency_Add('MicrosoftEdgeWebView2RuntimeInstaller' + Dependency_ArchSuffix + '.exe',
      '/silent /install',
      'WebView2 Runtime' + Dependency_ArchTitle,
      Dependency_String('https://msedge.sf.dl.delivery.mp.microsoft.com/filestreamingservice/files/a6eb8fdb-ffe3-4886-a1a3-14c7f72627a3/MicrosoftEdgeWebView2RuntimeInstallerX86.exe', 'https://msedge.sf.dl.delivery.mp.microsoft.com/filestreamingservice/files/7e516d67-e834-4e72-ae7b-fe18b0ea75bb/MicrosoftEdgeWebView2RuntimeInstallerX64.exe'),
      '', False, False);
  end;
end;


[Setup]
; -------------
; EXAMPLE SETUP
; -------------
#ifndef Dependency_NoExampleSetup

; comment out dependency defines to disable installing them

; requires netcorecheck.exe and netcorecheck_x64.exe (see download link below)
#define UseNetCoreCheck
#ifdef UseNetCoreCheck
  #define UseDotNet60Desktop
#endif

#define UseWebview

#define MyAppName "GakujoGUI"
#define MyAppVersion "1.3.9.3"
#define MyAppPublisher "xyzyxJP"
#define MyAppURL "https://xyzyxjp.github.io/GakujoGUI/"
#define MyAppExeName "GakujoGUI.exe"

[Setup]
; NOTE: The value of AppId uniquely identifies this application. Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{F58EE923-10BC-4E90-A927-588BBF57535E}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
AppCopyright={#MyAppPublisher}
DefaultDirName={localappdata}\{#MyAppName}
DisableProgramGroupPage=yes
; Remove the following line to run in administrative install mode (install for all users.)
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=commandline
OutputDir=bin\Setup
OutputBaseFilename=GakujoGUI_Setup
SetupIconFile=Resources\GakujoGUI.ico
Compression=lzma
SolidCompression=yes
VersionInfoVersion={#MyAppVersion}
WizardStyle=modern
DisableDirPage=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: jp; MessagesFile: "compiler:Languages\Japanese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
#ifdef UseNetCoreCheck
; download netcorecheck.exe: https://go.microsoft.com/fwlink/?linkid=2135256
; download netcorecheck_x64.exe: https://go.microsoft.com/fwlink/?linkid=2135504
Source: "netcorecheck.exe"; Flags: dontcopy noencryption
Source: "netcorecheck_x64.exe"; Flags: dontcopy noencryption
#endif

#ifdef UseDirectX
Source: "dxwebsetup.exe"; Flags: dontcopy noencryption
#endif

Source: "bin\Release\net6.0-windows10.0.18362.0\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net6.0-windows10.0.18362.0\Discord.Net.Core.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net6.0-windows10.0.18362.0\Discord.Net.Rest.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net6.0-windows10.0.18362.0\Discord.Net.WebSocket.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net6.0-windows10.0.18362.0\GakujoGUI.deps.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net6.0-windows10.0.18362.0\GakujoGUI.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net6.0-windows10.0.18362.0\GakujoGUI.runtimeconfig.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net6.0-windows10.0.18362.0\Hardcodet.NotifyIcon.Wpf.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net6.0-windows10.0.18362.0\HtmlAgilityPack.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net6.0-windows10.0.18362.0\Markdig.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net6.0-windows10.0.18362.0\Markdig.Wpf.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net6.0-windows10.0.18362.0\Microsoft.Toolkit.Uwp.Notifications.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net6.0-windows10.0.18362.0\Microsoft.Web.WebView2.Core.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net6.0-windows10.0.18362.0\Microsoft.Web.WebView2.WinForms.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net6.0-windows10.0.18362.0\Microsoft.Web.WebView2.Wpf.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net6.0-windows10.0.18362.0\Microsoft.Windows.SDK.NET.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net6.0-windows10.0.18362.0\ModernWpf.Controls.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net6.0-windows10.0.18362.0\ModernWpf.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net6.0-windows10.0.18362.0\ModernWpf.MessageBox.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net6.0-windows10.0.18362.0\Newtonsoft.Json.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net6.0-windows10.0.18362.0\NLog.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net6.0-windows10.0.18362.0\NLog.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net6.0-windows10.0.18362.0\ReverseMarkdown.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net6.0-windows10.0.18362.0\System.Interactive.Async.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net6.0-windows10.0.18362.0\System.Linq.Async.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net6.0-windows10.0.18362.0\Todoist.Net.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net6.0-windows10.0.18362.0\WinRT.Runtime.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net6.0-windows10.0.18362.0\runtimes\*"; DestDir: "{app}\runtimes"; Flags: ignoreversion recursesubdirs createallsubdirs
; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall

[Code]
function InitializeSetup: Boolean;
begin
(* DeleteFile(ExpandConstant('{userappdata}\{#MyAppName}\Account.json')); *)
(* DeleteFile(ExpandConstant('{userappdata}\{#MyAppName}\Tokens.json')); *)
#ifdef UseDotNet60Desktop
  Dependency_AddDotNet60Desktop;
#endif
#ifdef UseWebview
  Dependency_AddWebview;
#endif
  Result := True;
end;

#endif
