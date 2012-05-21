generate_msvc.py %*
REM prepare for test build
cd ..\..
rmdir /s /q msvc_test
mkdir msvc_test
cd msvc_test
..\libraries\7za.exe x ..\build-nt-x86\libpwiz_msvc.zip