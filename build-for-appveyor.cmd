@rem git clone --recurse-submodules https://github.com/devizer/Universe.SqlTrace

type Prepare-NuGet-And-build-tools.ps1 | powershell -c -
call ~local-build-tools.cmd
"%NUGET_EXE%" restore

pushd include\Universe.SqlServerJam\src
"%NUGET_EXE%" restore
popd

call build-only.cmd

