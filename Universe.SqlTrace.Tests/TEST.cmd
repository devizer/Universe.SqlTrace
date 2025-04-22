msbuild /t:Build /p:Configuration=Release
pushd bin\Release
nunit3-console-v3.12.exe Universe.SqlTrace.Tests.exe 
popd
