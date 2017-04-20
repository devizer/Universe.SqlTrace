call set-version.cmd
echo [assembly: System.Reflection.AssemblyVersion("%SqlTrace_Version%")] > Universe.SqlTrace\Properties\AssemblyVersion.cs 
echo [assembly: System.Reflection.AssemblyFileVersion("%SqlTrace_Version%")] >> Universe.SqlTrace\Properties\AssemblyVersion.cs 

echo for($v=15; $v -ge 11; $v--) { $p="HKLM:\Software\Microsoft\MSBuild\$v.0"; $i1=(Get-Item -ErrorAction SilentlyContinue -Path $p); if ($i1) { $ret=$i1.GetValue("MSBuildOverrideTasksPath"); if ($ret) { $exe = "${ret}msbuild.exe"; if (Test-Path $exe) { Write-Host "$exe"; Exit 0; } } } } Exit 1; | powershell -OutputFormat Text -command - > "%USERPROFILE%\.lastmsbuild"
for /F "delims=" %%v in (%USERPROFILE%\.lastmsbuild) DO set LAST_MSBUILD=%%v
echo Last MSBUILD: "%LAST_MSBUILD%"

for %%c in (Debug Release) DO (
  "%LAST_MSBUILD%" Universe.SqlTrace.sln /t:Rebuild /p:Configuration=%%c
)
