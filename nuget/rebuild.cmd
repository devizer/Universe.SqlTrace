rem REBUILD RELEASE
net start mssql$sql2005
pushd ..
call nuget-restore.cmd
call build-and-test.cmd
popd
net stop mssql$sql2005

call re-pack-nupkg.cmd