call ..\set-version.cmd
for %%f in (*.nuspec) do set srcfile=%%f
echo SRC: %srcfile%
nuget.exe pack %srcfile% -Version %PKG_VERSION%
nuget push *.%PKG_VERSION%.nupkg %NUGET_UNIVERSE_SQLTRACE% -Timeout 600 -Source https://www.nuget.org/api/v2/package
