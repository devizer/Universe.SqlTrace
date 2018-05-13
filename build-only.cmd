call set-version.cmd
echo [assembly: System.Reflection.AssemblyVersion("%SqlTrace_Version%.0")]     >  Universe.SqlTrace\Properties\AssemblyVersion.cs 
echo [assembly: System.Reflection.AssemblyFileVersion("%SqlTrace_Version%.0")] >> Universe.SqlTrace\Properties\AssemblyVersion.cs 

rem *** BUILD ***
call Prepare-Nuget-and-Build-Tools.cmd
"%BUILD_TOOLS_ONLINE%\nuget.exe" restore
"%MS_BUILD_2017%" Universe.SqlTrace.sln /t:Rebuild /v:m /p:Configuration=Debug
"%MS_BUILD_2017%" Universe.SqlTrace.sln /t:Rebuild /v:m /p:Configuration=Release
