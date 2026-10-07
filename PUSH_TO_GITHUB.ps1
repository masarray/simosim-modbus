# Execute in the extracted source root after configuring Git credentials.
$ErrorActionPreference = 'Stop'
git init -b main
git add .
git commit -m "Import SimoSim Modbus source"
git remote add origin https://github.com/masarray/simosim-modbus.git
git push -u origin main
