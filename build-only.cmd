call set-version.cmd
echo [assembly: System.Reflection.AssemblyVersion("%SqlTrace_Version%.0")]     >  Universe.SqlTrace\Properties\AssemblyVersion.cs 
echo [assembly: System.Reflection.AssemblyFileVersion("%SqlTrace_Version%.0")] >> Universe.SqlTrace\Properties\AssemblyVersion.cs 

rem *** BUILD ***
type Prepare-NuGet-And-build-tools.ps1 | powershell -c -
call ~local-build-tools.cmd
"%NUGET_EXE%" restore
"%MSBUILD_EXE%" Universe.SqlTrace.sln /t:Rebuild /v:m /p:Configuration=Debug
"%MSBUILD_EXE%" Universe.SqlTrace.sln /t:Rebuild /v:m /p:Configuration=Release
