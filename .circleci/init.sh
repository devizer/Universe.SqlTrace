#!/usr/bin/env bash
set -e

echo Configure apt
echo 'Acquire::Check-Valid-Until "0";' | sudo tee /etc/apt/apt.conf.d/10no--check-valid-until 
echo 'APT::Get::Assume-Yes "true";' | sudo tee /etc/apt/apt.conf.d/11assume-yes               
echo 'APT::Get::AllowUnauthenticated "true";' | sudo tee /etc/apt/apt.conf.d/12allow-unauth   

sudo apt -qq update >/dev/null ; sudo apt install -y -qq git sudo jq tar bzip2 gzip curl lsb-release procps
