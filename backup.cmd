pushd "%LOCALAPPDATA%"
echo [System.DateTime]::Now.ToString("yyyy-MM-dd,HH-mm-ss") | powershell -command - > .backup.timestamp
for /f %%i in (.backup.timestamp) do set datetime=%%i
popd

rem MAX: -mx=9 -mfb=128 -md=128m
"C:\Program Files\7-Zip\7zG.exe" a -t7z -mx=9 -ms=on -xr!.git -xr!bin -xr!obj -xr!packages -xr!.vs -xr!*.nupkg ^
  "C:\Users\Backups on Google Drive\Universe.SqlTrace (%datetime%).7z" .

