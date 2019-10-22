@rem git clone --recurse-submodules https://github.com/devizer/Universe.SqlTrace

type Prepare-NuGet-And-build-tools.ps1 | powershell -c -
call ~local-build-tools.cmd
"%NUGET_EXE%" restore

call build-only.cmd

