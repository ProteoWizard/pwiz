@echo off
CALL "%VS120COMNTOOLS%vsvars32.bat"
signtool sign /t http://timestamp.verisign.com/scripts/timstamp.dll /v /f %1 /p %2 %3