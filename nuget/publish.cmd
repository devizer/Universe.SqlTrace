call re-pack-nupkg.cmd
nuget push *.%PKG_VERSION%.nupkg %NUGET_UNIVERSE_SQLTRACE% -Timeout 600 -Source https://www.nuget.org/api/v2/package