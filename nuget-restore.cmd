set url=https://dist.nuget.org/win-x86-commandline/latest/nuget.exe
set outfile=%AppData%\Temp\nuget.exe
mkdir "%AppData%\Temp" 1>nul 2>&1
echo [System.Net.ServicePointManager]::ServerCertificateValidationCallback={$true}; $d=new-object System.Net.WebClient; $d.DownloadFile("$Env:url","$Env:outfile") | powershell -command -
"%outfile%" restore


