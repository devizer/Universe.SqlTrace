pushd ..
call set-version.cmd
call build-only.cmd
popd
dir ..\Universe.SqlTrace\bin\Release\*.%PKG_VERSION%.*nupkg
for %%f in (..\Universe.SqlTrace\bin\Release\*.%PKG_VERSION%.*nupkg) DO (
   echo PUSH %%f
   nuget push %%f %NUGET_UNIVERSE_SQLTRACE% -Timeout 600 -Source https://www.nuget.org/api/v2/package
)
