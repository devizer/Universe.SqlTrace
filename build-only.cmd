call set-version.cmd
echo [assembly: System.Reflection.AssemblyVersion("%SqlTrace_Version%")] > Universe.SqlTrace\Properties\AssemblyVersion.cs 
echo [assembly: System.Reflection.AssemblyFileVersion("%SqlTrace_Version%")] >> Universe.SqlTrace\Properties\AssemblyVersion.cs 

for %%c in (Debug Release) DO (
  %SystemRoot%\Microsoft.NET\Framework\v4.0.30319\msbuild Universe.SqlTrace.sln /t:Rebuild /p:Configuration=%%c
)
