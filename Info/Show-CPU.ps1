$cpu=Get-WmiObject Win32_Processor; $cpuName="$($cpu.Name), $([System.Environment]::ProcessorCount) Cores"
$cpuName
