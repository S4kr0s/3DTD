@echo off
setlocal EnableDelayedExpansion

set file1=031.asset
set file2=031.asset.meta
set num=32

:loop
if %num% leq 132 (
    set file1num=00%num%
    set file1num=!file1num:~-3!
    set file2num=!file1num!

    copy "%file1%" "!file1num!.asset"
    copy "%file2%" "!file2num!.asset.meta"

    set /a num+=1
    goto loop
)

echo All files copied.