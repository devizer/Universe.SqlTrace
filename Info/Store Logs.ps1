$files=(Get-ChildItem -Path "C:\" -recurse -force -Include "*.log")
@($files) | Select FullName | sort -unique | Out-File files.list utf8
7z a logs.7z "@files.list"