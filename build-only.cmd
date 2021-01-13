call set-version.cmd
rem echo [assembly: System.Reflection.AssemblyVersion("%SqlTrace_Version%.0")]     >  Universe.SqlTrace\Properties\AssemblyVersion.cs 
rem echo [assembly: System.Reflection.AssemblyFileVersion("%SqlTrace_Version%.0")] >> Universe.SqlTrace\Properties\AssemblyVersion.cs 

rem *** BUILD ***
type Prepare-NuGet-And-build-tools.ps1 | powershell -c -
call ~local-build-tools.cmd
"%NUGET_EXE%" restore
"%MSBUILD_EXE%" Universe.SqlTrace.sln /t:Rebuild /v:q /p:Configuration=Debug   /p:AssemblyVersion=%SqlTrace_Version% /p:PackageVersion=%SqlTrace_Version%
"%MSBUILD_EXE%" Universe.SqlTrace.sln /t:Rebuild /v:q /p:Configuration=Release /p:AssemblyVersion=%SqlTrace_Version% /p:PackageVersion=%SqlTrace_Version%
