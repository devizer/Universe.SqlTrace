for %%c in (Debug Release) DO (
  %SystemRoot%\Microsoft.NET\Framework\v4.0.30319\msbuild Universe.SqlTrace.sln /t:Rebuild /p:Configuration=%%c
)
