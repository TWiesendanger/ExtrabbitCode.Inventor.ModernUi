@echo on
SET ProjectDir=%~1
SET TargetName=%~2
SET TargetPath=%~3
SET TargetDir=%~4

SET DeployDir=C:\ProgramData\ExtrabbitCode\ExtrabbitCode.Inventor.ModernUi.Demo.AddIn
SET AddinsDir=C:\ProgramData\Autodesk\Inventor Addins

echo - Copy the .addin manifest into the Inventor add-ins folder so Inventor discovers it.
if not exist "%AddinsDir%" mkdir "%AddinsDir%"
XCopy "%ProjectDir%Addin\ExtrabbitCode.Inventor.ModernUi.Demo.AddIn.addin" "%AddinsDir%" /y

echo - Mirror the build output to the deploy folder. /MIR purges stale files (no accumulation).
robocopy "%TargetDir%." "%DeployDir%" /MIR /NJH /NJS /NDL /NP /R:1 /W:1
REM robocopy exit codes < 8 are success (0=no change, 1=copied, 2=purged, 3=both, ...).
if %ERRORLEVEL% GEQ 8 exit /b 1
exit /b 0
