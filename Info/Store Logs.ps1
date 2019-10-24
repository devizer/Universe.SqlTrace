$files=(Get-ChildItem -Path "C:\" -recurse -force -file -Include "*.log")
@($files) | % { $_.FullName } | select -unique | % { $_.substring(3) } | Out-File log-files.list utf8
7z a -spf logs.7z "@log-files.list"