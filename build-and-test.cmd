pushd include\Universe.SqlServerJam\src
set TEST_SQL_NET_DURATION_OF_Ping=42
set TEST_SQL_NET_DURATION_OF_Upload=42
set TEST_SQL_NET_DURATION_OF_Download=42
call Modern-Build-and-Test.cmd
popd

echo START TRACE
call build-only.cmd
set work=Universe.SqlTrace.Tests\bin\Debug\
pushd "%work%"
..\..\..\packages\NUnit.ConsoleRunner.3.8.0\tools\nunit3-console.exe --workers=1 Universe.SqlTrace.Tests.exe
popd
