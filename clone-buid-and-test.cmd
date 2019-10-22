mkdir C:\Lab >/nul 2>&1
pushd C:\Lab
rd /q /s Universe.SqlTrace
git clone --recurse-submodules https://github.com/devizer/Universe.SqlTrace
cd Universe.SqlTrace
call build-for-appveyor.cmd 2>&1 | tee log.log
popd
