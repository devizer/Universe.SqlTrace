call build-only.cmd
set work=Universe.SqlTrace.Tests\bin\Debug\
pushd "%work%"
..\..\..\packages\NUnit.ConsoleRunner.3.6.1\tools\nunit3-console.exe --workers=1 Universe.SqlTrace.Tests.exe
popd
