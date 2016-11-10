#!/bin/bash -ex
export "PATH=$PATH:/cygdrive/c/Program Files/Windows Kits/8.1/bin/x86"
for f in Release Debug;
do
  signtool sign /f winsw_cert.pfx /t http://timestamp.verisign.com/scripts/timestamp.dll bin/$f/winsw.exe
  signtool verify /v /pa bin/$f/winsw.exe
done
echo success
