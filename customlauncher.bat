@echo off
setlocal EnableExtensions EnableDelayedExpansion

REM ====== Config ======
set "GAME_EXE=%APPDATA%\Tempo Launcher - Beta\game\buildx64\TheBazaar.exe"
set "SCREEN_WIDTH=2560"
set "SCREEN_HEIGHT=1440"
set "SOCKET_URL=wss://server.playthebazaar.com/game"
set "TEMPO_URL=https://enter.playthebazaar.com"
set "DESIGN_URL=https://cdn.playthebazaar.com/bazaardesigndataprod"
set "LAUNCHER_VER=1.0.12"
REM ====================

REM --- Ask for email (plain console prompt) ---
set "EMAIL="
set /p "EMAIL=Email: "
if "%EMAIL%"=="" (
  echo [Error] Email is required.
  pause
  exit /b 11
)

REM --- Ask for password (hidden), hash to SHA-256 (lowercase hex) ---
set "HASH="
for /f "usebackq delims=" %%H in (`powershell -NoProfile -Command ^
  "$p = Read-Host -Prompt 'Password' -AsSecureString; " ^
  "if(-not $p){ exit 12 } " ^
  "$b=[Runtime.InteropServices.Marshal]::SecureStringToBSTR($p); " ^
  "try{ $s=[Runtime.InteropServices.Marshal]::PtrToStringUni($b) } finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($b) } " ^
  "$bytes=[Text.Encoding]::UTF8.GetBytes($s); $sha=[Security.Cryptography.SHA256]::Create(); " ^
  "($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString('x2') }) -join ''"`) do (
  set "HASH=%%H"
)

if "%HASH%"=="" (
  echo [Error] No password entered or hashing failed.
  pause
  exit /b 12
)

REM --- Prepare temp files ---
set "REQ=%TEMP%\baz_login_req.json"
set "RES=%TEMP%\baz_login_res.json"
if exist "%REQ%" del /q "%REQ%" >nul 2>&1
if exist "%RES%" del /q "%RES%" >nul 2>&1

REM --- Build JSON body (to avoid escaping hell in the command line) ---
> "%REQ%" (
  echo {
  echo   "email":"%EMAIL%",
  echo   "password":"%HASH%",
  echo   "includeAlternateRefreshToken": true
  echo }
)

REM --- POST to login endpoint with curl ---
echo Logging in...
echo About to run curl command...
echo Request file contents:
type "%REQ%"
echo.
curl -s -S -X POST -H "Accept: application/json" -H "Content-Type: application/json" -H "User-Agent: axios/1.6.8" --data-binary "@%REQ%" "https://www.playthebazaar.com/api/auth/login/user" -D "%TEMP%\baz_headers.txt" > "%RES%"
type "%RES%"
REM --- Extract tokens using simple findstr ---

set "ACCESS="
for /f "delims=" %%a in ('findstr "accessToken" "%RES%"') do (
  set "LINE=%%a"
  set "LINE=!LINE:*accessToken:=!"
  set "LINE=!LINE:~2!"
  for /f "delims=," %%b in ("!LINE!") do set "ACCESS=%%b"
  set "ACCESS=!ACCESS:~18,-2!"
)

set "REFRESH="
for /f "delims=" %%a in ('findstr /c:"refreshToken" "%RES%"') do (
  set "LINE=%%a"
  echo Found refreshToken line: !LINE!
  
  REM Find the position of refreshToken
  set "POS=0"
  set "TEMP=!LINE!"
  :find_refresh_loop
  if "!TEMP!"=="" goto :found_refresh
  if "!TEMP:~0,12!"=="refreshToken" goto :found_refresh
  set "TEMP=!TEMP:~1!"
  set /a "POS+=1"
  goto :find_refresh_loop
  
  :found_refresh
  echo Position found: !POS!
  set "LINE=!LINE:~%POS%!"
  echo After position trim: !LINE!
  set "LINE=!LINE:*refreshToken:=!"
  echo After removing prefix: !LINE!
  for /f "delims=," %%b in ("!LINE!") do set "REFRESH=%%b"
  echo Before final trim: !REFRESH!
  set "REFRESH=!REFRESH:~17,-2!"
)

echo Extracted accessToken: !ACCESS!
echo Extracted refreshToken: !REFRESH!

if "!ACCESS!"=="" (
  echo [Error] accessToken missing.
  pause
  exit /b 3
)
if "!REFRESH!"=="" (
  echo [Error] refreshToken missing.
  pause
  exit /b 4
)

REM --- Launch the game ---

echo Starting The Bazaar...
start "" "%GAME_EXE%" ^
  -screen-width %SCREEN_WIDTH% ^
  -screen-height %SCREEN_HEIGHT% ^
  -logintoken="%ACCESS%" ^
  -refreshtoken="%REFRESH%" ^
  -socketServerUrl=%SOCKET_URL% ^
  -tempoNetUrl=%TEMPO_URL% ^
  -designDataUrl=%DESIGN_URL% ^
  -launcherversion=%LAUNCHER_VER%

REM (Optional) Clean temp files
REM del /q "%REQ%" "%RES%" >nul 2>&1

endlocal
exit /b 0
