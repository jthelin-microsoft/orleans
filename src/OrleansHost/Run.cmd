@setlocal
@echo OFF

set CMDHOME=%~dp0

set siloName=Primary
if not "%1" == "" (
    set siloName=%1
)

@echo -- Starting Orleans node "%siloName%" on localhost
cd /d %CMDHOME% && OrleansHost.exe %siloName%
