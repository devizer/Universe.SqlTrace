    function Say { param( [string] $message )
        Write-Host "$(Get-Elapsed) " -NoNewline -ForegroundColor Magenta
        Write-Host "$message" -ForegroundColor Yellow
    }
    
    function Get-Elapsed
    {
        if ($Global:startAt -eq $null) { $Global:startAt = [System.Diagnostics.Stopwatch]::StartNew(); }
        [System.String]::Concat("[", (new-object System.DateTime(0)).AddMilliseconds($Global:startAt.ElapsedMilliseconds).ToString("HH:mm:ss"), "]");
    }; $_=Get-Elapsed;

    function MsiUninstallArgs { param($x)
        
        $trimmed=$x -Replace "msiexec.exe","" -Replace "/I","" -Replace "/X",""
        "/X $trimmed /qn"
    }

    function Uninstall-SqlLocalDB { param([string] $version)
        Say "Deleting LocalDB $version"
        $apps = Find-Apps LocalDB | ? { $_.DisplayName -like $version -and $_.DisplayName -like "Microsoft" }
        if ($apps.Length -gr 0) { 
            # DisplayName, DisplayVersion, PSPath, PSChildName, UninstallString, Guid, MsiUninstallArgs
            $msi=$apps[0]; $msi_args=$msi.MsiUninstallArgs + "/L*v `"Uninstall $($msi.DesplayName).log`""
            Say "Deleting MSI Package $($msi.DesplayName) (Version [$($msi.DesplayName)]) using args: [$msi_args]"
            start-process "msiexec.exe" -arg "/X $uninstall64 /qb" -Wait

    }

    
    # https://stackoverflow.com/questions/113542/how-can-i-uninstall-an-application-using-powershell
    function Find-Apps { param([string] $pattern)
        # $pattern="SQL"
        $apps = @(); 
        $path32="HKLM:\SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        $path64="HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"
        foreach($path in @($path64, $path32)) {
            $u = gci $path | foreach { gp $_.PSPath } | ? { $_ -like "*$($pattern)*" } 
            # $u = gci $path | where { $_.DisplayName -like "*$($pattern)*" } | foreach { gp $_.PSPath; Write-Host "$($_.PSPath)" } | ? { $_ -like "*$($pattern)*" } 
            if ($u.Length -gt 0) { $apps += $u }
        }

        $apps | ? { $_.DisplayName -like '*LocalDB*' } | sort -Property DisplayName | fl
        $apps | ? { "$_.DisplayName".Length -gt 0 } | sort -Property DisplayName | ft DisplayName, DisplayVersion, *

#            select -Property DisplayName, DisplayVersion, PSPath, PSChildName, UninstallString, Guid, MsiUninstallArgs
        $apps = $apps | 
            foreach { $_ | Add-Member Guid $_.PSChildName; $_ } |
            foreach { $_ | Add-Member MsiUninstallArgs ("/X " + ($_.UninstallString -Replace "msiexec.exe","" -Replace "/I","" -Replace "/X","" ).ToString().Trim() + " /qn"); $_ }

        $apps | 
            ? { $_.DisplayName -like '*LocalDB*' } | 
            sort -Property DisplayName  | 
            fl

        $uninstall | ? { $_.DisplayName -like '*LocalDB*' } | sort -Property DisplayName  | select { $_ | Add-Member -MemberType CodeProperty -Name "KeyName" -Value { "$([System.IO.Path]::GetFileName($this.PSPath))" } } | fl
        
        # $uninstall32 = gci "HKLM:\SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall" | foreach { gp $_.PSPath } | ? { $_ -like "*$($pattern)*" } 
        # $uninstall64 = gci "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall" | foreach { gp $_.PSPath } | ? { $_ -like "*$($pattern)*" }


    }


Find-App SQL
exit;

"[" + "XXX" + "]"

AuthorizedCDFPrefix :
Comments            :
Contact             :
DisplayVersion      : 13.2.5101.9
HelpLink            : http://go.microsoft.com/fwlink/?LinkId=230480
HelpTelephone       :
InstallDate         : 20190726
InstallLocation     :
InstallSource       : C:\Program Files\Microsoft SQL Server\130\Setup Bootstrap\Update Cache\KB4505220\GDR\1033_ENU_LP\x64\setup\x64\
ModifyPath          : MsiExec.exe /I{044A540B-5CBB-405A-8B73-0CEEEB213C6E}
Publisher           : Microsoft Corporation
Readme              :
Size                :
EstimatedSize       : 196897
UninstallString     : MsiExec.exe /I{044A540B-5CBB-405A-8B73-0CEEEB213C6E}
URLInfoAbout        :
URLUpdateInfo       :
VersionMajor        : 13
VersionMinor        : 2
WindowsInstaller    : 1
Version             : 218239981
Language            : 1033
DisplayName         : Microsoft SQL Server 2016 LocalDB
sEstimatedSize2     : 238108
PSPath              : Microsoft.PowerShell.Core\Registry::HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{044A540B-5CBB-405A-8B73-0CEEEB213C6E}
PSParentPath        : Microsoft.PowerShell.Core\Registry::HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall
PSChildName         : {044A540B-5CBB-405A-8B73-0CEEEB213C6E}
PSProvider          : Microsoft.PowerShell.Core\Registry

AuthorizedCDFPrefix :
Comments            :
Contact             :
DisplayVersion      : 14.0.2027.2
HelpLink            : https://go.microsoft.com/fwlink/?LinkId=230480
HelpTelephone       :
InstallDate         : 20190726
InstallLocation     :
InstallSource       : C:\Program Files\Microsoft SQL Server\140\Setup Bootstrap\Update Cache\KB4505224\GDR\1033_ENU_LP\x64\setup\x64\
ModifyPath          : MsiExec.exe /I{58180BC0-0DA3-4341-A41F-9A3CF7207EE1}
Publisher           : Microsoft Corporation
Readme              :
Size                :
EstimatedSize       : 174808
UninstallString     : MsiExec.exe /I{58180BC0-0DA3-4341-A41F-9A3CF7207EE1}
URLInfoAbout        :
URLUpdateInfo       :
VersionMajor        : 14
VersionMinor        : 0
WindowsInstaller    : 1
Version             : 234883051
Language            : 1033
DisplayName         : Microsoft SQL Server 2017 LocalDB
sEstimatedSize2     : 247124
PSPath              : Microsoft.PowerShell.Core\Registry::HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{58180BC0-0DA3-4341-A41F-9A3CF7207EE1}
PSParentPath        : Microsoft.PowerShell.Core\Registry::HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall
PSChildName         : {58180BC0-0DA3-4341-A41F-9A3CF7207EE1}
PSProvider          : Microsoft.PowerShell.Core\Registry


Say "Find using WMI Filter"
get-wmiobject win32_product -Filter 'Name Like "%LocalDB%"' | sort -Property Vendor, Name, Version | ft IdentifyingNumber, Version, Name, Vendor, InstallState

Say "Find using Client Side Filter"
get-wmiobject win32_product | where { $_.Name -like "*LocalDB*" } | sort -Property Vendor, Name, Version | ft IdentifyingNumber, Version, Name, Vendor, InstallState

Say Done