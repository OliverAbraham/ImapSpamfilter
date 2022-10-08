@ECHO off
@ECHO -----------------------------------------------------------------------------------
@ECHO Deploy-Skript
@ECHO Quelle 1                  : %SOURCE1%
@ECHO Quelle 2                  : %SOURCE2%
@ECHO Quelle 3                  : %SOURCE3%
@ECHO Quelle 4                  : %SOURCE4%
@ECHO Zielanwendung             : %DESTINATION%
@ECHO Interprozess-Kommunikation: %COMMUNICATION_DIR%
@ECHO -----------------------------------------------------------------------------------
rem pause


del %COMMUNICATION_DIR%\Force_application_close.dat
del %COMMUNICATION_DIR%\Application_is_updated.dat


echo .
echo Erst die Dateien kopieren...
xcopy %SOURCE4%\*             			%DESTINATION%\bin /s /Y /D

echo .
echo Anwendung auffordern. sich zu beenden
echo .>%COMMUNICATION_DIR%\Force_application_close.dat


echo .
echo Warten, bis Anwendung sich beendet und upgedated hat
:wait
@CHOICE /T 2 /M "Warten,bis Anwendung sich upgedated hat...  2 druecken zum uebergehen" /C:123 /CS /D 1
IF ERRORLEVEL 2 GOTO dontwait
if not exist %COMMUNICATION_DIR%\Application_is_updated.dat goto wait
:dontwait


echo .
echo Aufraeumen...
if exist  %COMMUNICATION_DIR%\Force_application_close.dat   del %COMMUNICATION_DIR%\Force_application_close.dat
if exist  %COMMUNICATION_DIR%\Application_is_updated.dat	del %COMMUNICATION_DIR%\Application_is_updated.dat
if exist  %COMMUNICATION_DIR%\Application_is_closed.dat	    del %COMMUNICATION_DIR%\Application_is_closed.dat
rem pause