$files = Get-ChildItem -Path 'C:\Program Files (x86)\Microsoft SDKs\Windows\' -Filter 'sn.exe' -Recurse | Select-Object -Property FullName
$files
& $files[0].FullName -k winsw_key.snk
