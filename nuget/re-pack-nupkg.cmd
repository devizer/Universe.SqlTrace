rem REBUILD NUPKG
call ..\set-version.cmd
for %%f in (*.nuspec) do set srcfile=%%f
echo SRC: %srcfile%
nuget.exe pack %srcfile% -Version %PKG_VERSION%
