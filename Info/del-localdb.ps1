    function Say { param( [string] $message )
        Write-Host "$(Get-Elapsed) " -NoNewline -ForegroundColor Magenta
        Write-Host "$message" -ForegroundColor Yellow
    }
    
    function Get-Elapsed
    {
        if ($Global:startAt -eq $null) { $Global:startAt = [System.Diagnostics.Stopwatch]::StartNew(); }
        [System.String]::Concat("[", (new-object System.DateTime(0)).AddMilliseconds($Global:startAt.ElapsedMilliseconds).ToString("HH:mm:ss"), "]");
    }; $_=Get-Elapsed;

    function Get-Preinstalled-SqlServers
    {
        $names = @();
        foreach($path in @('HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server', 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Microsoft SQL Server')) {
            try { $v = (get-itemproperty $path).InstalledInstances; $names += $v } catch {}
        }
        $names | sort | where { "$_".Length -gt 0 }
    }

    function Disable-SqlServers { param( [array] $names ) 
        foreach($sqlname in $names) {
            Say "Disable MSSQL`$$sqlname"
            Stop-Service "MSSQL`$$sqlname" -ErrorAction SilentlyContinue
            Set-Service "MSSQL`$$sqlname" -StartupType Disabled
        }
    }

    function Delete-SqlServers { param( [array] $names ) 
        foreach($sqlname in $names) {
            Say "Delete MSSQL`$$sqlname"
            Stop-Service "MSSQL`$$sqlname" -ErrorAction SilentlyContinue
            Set-Service "MSSQL`$$sqlname" -StartupType Disabled
            & cmd /c sc delete "MSSQL`$$sqlname"
        }
    }

    function Hide-LocalDB-Servers {
        Say "Hide SQL Server LocalDB"; 
        Remove-Item -Path "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server Local DB\Installed Versions" -Recurse -Force
    }

    # return empty string if SqlLocalDB.exe is not found. always returns latest installed SqlLocalDB.exe
    function Find-SqlLocalDB-Exe {
        if ($Global:LocalDbExe -eq $null) {
            $Global:LocalDbExe=(Get-ChildItem -Path "C:\Program Files\Microsoft SQL Server" -Filter "SqlLocalDB.exe" -Recurse -ErrorAction SilentlyContinue -Force | Sort-Object -Property "FullName" -Descending)[0].FullName
            if ($Global:LocalDbExe) { Write-Host "$(Get-Elapsed) Found SqlLocalDB.exe full path: [$($Global:LocalDbExe)]" } else { Write-Host "$(Get-Elapsed) SqlLocalDB.exe NOT Found" }
        }
        "$($Global:LocalDbExe)"
    }

    function Uninstall-SqlLocalDB { param([string] $version)
        Say "Deleting LocalDB $version"
        $apps = Find-Apps "LocalDB" | ? { $($_.DisplayName -like "*$($version)*") -and ($_.DisplayName -like "*Microsoft*")  }
        if ($apps -and $apps[0]) { 
            # DisplayName, DisplayVersion, PSPath, PSChildName, UninstallString, Guid, MsiUninstallArgs
            $msi=@($apps)[0]; $msi_args=$msi.MsiUninstallArgs + " /L*v `"Uninstall $($msi.DisplayName).log`""
            Say "Deleting MSI Package $($msi.DisplayName) (version [$($msi.DisplayVersion)]) using args: [$msi_args]"
            # start-process "msiexec.exe" -arg $msi_args -Wait
        } else {
            Say "LocalDB $version is not found"
        }
    }

    # https://stackoverflow.com/questions/113542/how-can-i-uninstall-an-application-using-powershell
    function Find-Apps { param([string] $pattern)
        $apps = @();
        $path32="HKLM:\SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        $path64="HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"
        foreach($path in @($path64, $path32)) {
            $u = gci $path | foreach { gp $_.PSPath } | ? { $_ -like "*$($pattern)*" }
            if ($u.Length -gt 0) { $apps += $u }
        }
        $apps |
            foreach { $_ | Add-Member Guid $_.PSChildName; $_ } |
            foreach { $_.DisplayName = "$($_.DisplayName)".Trim(); $_ } |
            foreach { $_ | Add-Member MsiUninstallArgs ("/X " + ($_.UninstallString -Replace "msiexec.exe","" -Replace "/I","" -Replace "/X","" ).ToString().Trim() + " /qn"); $_ }
    }

    function Show-SqlServers {
        get-wmiobject win32_service | where {$_.Name.ToLower().IndexOf("sql") -ge 0 } | sort-object -Property "DisplayName" | ft State, Name, DisplayName, StartMode
    }

# Find-Apps LocalDB
Uninstall-SqlLocalDB 2016
