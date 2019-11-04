mkdir ..\ARTIFACT 1>nul 2>&1
diskspd.exe -d15 -c11000M -b1M -Sw -Su -t1 -w0 -s1b "io-perf.tmp" 
diskspd.exe -d15 -c11000M -b1M -Sw -Su -t1 -w100 -s1b "io-perf.tmp" 

diskspd.exe -d15 -c11000M -b4K -Sw -Su -t8 -w0 -r1b "io-perf.tmp" 
diskspd.exe -d15 -c11000M -b4K -Sw -Su -t8 -w100 -r1b "io-perf.tmp" 

del "io-perf.tmp"
