@rem git clone --recurse-submodules https://github.com/devizer/Universe.SqlTrace

type Prepare-NuGet-And-build-tools.ps1 | powershell -c -
call ~local-build-tools.cmd
"%NUGET_EXE%" restore

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

goto skip1
"%DOTCOVER_EXE%" analyse /TargetExecutable="%NUNIT_RUNNER_EXE%" ^
  /TargetArguments="Universe.SqlTrace.Tests.exe" ^
  /Output="tests-report\App-Coverage-Report.html" ^
  /TargetWorkingDir=V:\_GIT\Universe.SqlTrace\Universe.SqlTrace.Tests\bin\Debug\ ^
  /ReportType="HTML"
:skip1

"%NUNIT_RUNNER_EXE%" --workers=1 Universe.SqlTrace.Tests.exe
"%REPORT_UNIT_EXE%" .\ tests-report\ 1>report_unit.log 2>&1
popd
