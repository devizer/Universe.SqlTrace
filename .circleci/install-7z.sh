#!/usr/bin/env bash
sudo apt-get purge p7zip -y
sudo apt-get purge p7zip-full -y


mkdir -p ~/src/7zip-16.02
pushd ~/src/7zip-16.02
url=https://downloads.sourceforge.net/p7zip/p7zip_16.02_src_all.tar.bz2
url=https://netcologne.dl.sourceforge.net/project/p7zip/p7zip/16.02/p7zip_16.02_src_all.tar.bz2
file=$(basename $url)
wget -O _$file --no-check-certificate $url
tar xjf _$file 
cd p7zip*
sed -i 's/OPTFLAGS=-O /OPTFLAGS=-O3 /g' makefile.machine
time make test_7z && sudo make install
/usr/local/bin/7z || echo "7Z not found"
popd

