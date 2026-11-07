; SteelMate AI installer. Per-user install: no admin rights needed.
Unicode true
!define APP "SteelMate AI"
!define VER "1.9.2"
!define EXE "SteelMateAI.exe"
!define UNKEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\SteelMateAI"

Name "${APP} ${VER}"
OutFile "SteelMateAI-Setup-${VER}.exe"
InstallDir "$LOCALAPPDATA\Programs\SteelMateAI"
InstallDirRegKey HKCU "Software\SteelMateAI" "InstallDir"
RequestExecutionLevel user
SetCompressor /SOLID lzma
VIProductVersion "${VER}.0"
VIAddVersionKey "ProductName" "${APP}"
VIAddVersionKey "ProductVersion" "${VER}"
VIAddVersionKey "FileVersion" "${VER}"
VIAddVersionKey "FileDescription" "${APP} installer"
VIAddVersionKey "LegalCopyright" ""

!include "MUI2.nsh"
!define MUI_FINISHPAGE_RUN "$INSTDIR\${EXE}"
!define MUI_FINISHPAGE_SHOWREADME "$INSTDIR\CHANGELOG.txt"
!define MUI_FINISHPAGE_SHOWREADME_TEXT "Show what's new"
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"

Function .onInit
  ; Close a running copy so files can be replaced.
  nsExec::Exec 'taskkill /IM ${EXE} /F'
  ReadRegStr $0 HKCU "${UNKEY}" "DisplayVersion"
  StrCmp $0 "" done
  MessageBox MB_OKCANCEL|MB_ICONINFORMATION "${APP} $0 is installed. It will be updated to ${VER}.$\r$\nYour settings, API keys and saved chats are kept." IDOK done
  Abort
  done:
FunctionEnd

Section "Install"
  SetOutPath "$INSTDIR"
  ; From 1.7.0 the Tekla API files come from the installed Tekla (2025, 2026, ...).
  ; Remove copies left by older versions, or they would pin one Tekla version.
  Delete "$INSTDIR\Tekla.Structures*.dll"
  ; Keep tools you built yourself (tools\*.dll and tools\src) when updating.
  File /r /x "tools" "app\*.*"
  SetOverwrite off
  File /r "app\tools"
  SetOverwrite on
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  WriteRegStr HKCU "Software\SteelMateAI" "InstallDir" "$INSTDIR"
  WriteRegStr HKCU "Software\SteelMateAI" "Version" "${VER}"
  WriteRegStr HKCU "${UNKEY}" "DisplayName" "${APP}"
  WriteRegStr HKCU "${UNKEY}" "DisplayVersion" "${VER}"
  WriteRegStr HKCU "${UNKEY}" "Publisher" "SteelMate AI"
  WriteRegStr HKCU "${UNKEY}" "DisplayIcon" "$INSTDIR\${EXE}"
  WriteRegStr HKCU "${UNKEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UNKEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegDWORD HKCU "${UNKEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNKEY}" "NoRepair" 1
  CreateDirectory "$SMPROGRAMS\${APP}"
  CreateShortcut "$SMPROGRAMS\${APP}\${APP}.lnk" "$INSTDIR\${EXE}"
  CreateShortcut "$SMPROGRAMS\${APP}\Uninstall ${APP}.lnk" "$INSTDIR\Uninstall.exe"
  CreateShortcut "$DESKTOP\${APP}.lnk" "$INSTDIR\${EXE}"
SectionEnd

Section "Uninstall"
  nsExec::Exec 'taskkill /IM ${EXE} /F'
  ; Safety: only wipe the folder if it really is the SteelMate AI folder.
  IfFileExists "$INSTDIR\${EXE}" 0 +2
    RMDir /r "$INSTDIR"
  Delete "$DESKTOP\${APP}.lnk"
  RMDir /r "$SMPROGRAMS\${APP}"
  DeleteRegKey HKCU "${UNKEY}"
  DeleteRegKey HKCU "Software\SteelMateAI"
  MessageBox MB_YESNO|MB_ICONQUESTION "Also delete your settings, saved API keys, chat history and logs?$\r$\n(Choose No to keep them for a later reinstall. Reports in Documents are never deleted.)" IDNO keep
    RMDir /r "$APPDATA\TeklaChat"
    RMDir /r "$LOCALAPPDATA\SteelMateAI"
  keep:
SectionEnd
