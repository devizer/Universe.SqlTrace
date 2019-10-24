    function Say { param( [string] $message )
        Write-Host "$(Get-Elapsed) " -NoNewline -ForegroundColor Magenta
        Write-Host "$message" -ForegroundColor Yellow
    }
    
    function Get-Elapsed
    {
        if ($Global:startAt -eq $null) { $Global:startAt = [System.Diagnostics.Stopwatch]::StartNew(); }
        [System.String]::Concat("[", (new-object System.DateTime(0)).AddMilliseconds($Global:startAt.ElapsedMilliseconds).ToString("HH:mm:ss"), "]");
    }; $_=Get-Elapsed;

    $Env:ARTIFACT = "C:\projects\universe-sqltrace\Artifact"
    
    Say "Store All The Logs"
    Get-ChildItem -Path "C:\" -force -directory | % { $folder=$_;
        Say "Store logs for $($folder.FullName)"
        $files=(Get-ChildItem -Path $folder.FullName -recurse -force -file -Include "*.log" 2> "$Env:ARTIFACT\Access Denied ($($folder.Name)).txt")
        if ( @($files).Count -gt 0 ) {
            Say "Pack $(@($files).Count) log files from $($folder.FullName)"
            @($files) | % { $_.FullName } | sort -unique | % { $_.substring(3) } | Out-File "$Env:ARTIFACT\log-files-$($folder.Name).list" utf8
            pushd "C:\"
            7z a -t7z -mx=3 -ms=on -spf "$Env:ARTIFACT\All The Logs for $($folder.Name).7z" "@$($Env:ARTIFACT)\log-files-$($folder.Name).list"
            popd
        }
    }
