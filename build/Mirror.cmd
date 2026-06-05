@echo off
REM Mirror %1 -> %2 (purges stale files). robocopy exit codes 0-7 are success; >=8 is failure.
robocopy %1 %2 /MIR /NJH /NJS /NDL /NP /R:1 /W:1
set ROBOCOPY_RC=%ERRORLEVEL%
if %ROBOCOPY_RC% GEQ 8 exit /b 1
exit /b 0
