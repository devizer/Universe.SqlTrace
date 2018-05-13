type Prepare-NuGet-And-build-tools.ps1 | powershell -c -
call ~local-build-tools.cmd

pushd include\Universe.SqlServerJam\src
set TEST_SQL_NET_DURATION_OF_Ping=42
set TEST_SQL_NET_DURATION_OF_Upload=42
set TEST_SQL_NET_DURATION_OF_Download=42
"%NUGET_EXE%" restore
call Modern-Build-and-Test.cmd
popd

echo START TRACE
call build-only.cmd
set work=Universe.SqlTrace.Tests\bin\Debug\
pushd "%work%"
"%NUNIT_RUNNER_EXE%" --workers=1 Universe.SqlTrace.Tests.exe
"%REPORT_UNIT_EXE%" .\ 1>report_unit.log 2>&1
popd
