@echo off
setlocal
@echo off

REM # Get the location of quickbuild.bat and drop trailing slash
set PWIZ_ROOT=%~dp0
set PWIZ_ROOT=%PWIZ_ROOT:~0,-1%
pushd %PWIZ_ROOT%

IF EXIST bin rmdir /s /q bin
IF EXIST obj rmdir /s /q obj
IF EXIST Microsoft.VC90.MFC rmdir /s /q Microsoft.VC90.MFC
IF EXIST Microsoft.VC100.MFC rmdir /s /q Microsoft.VC100.MFC
IF EXIST ClearCore.dll del /q ClearCore.dll
IF EXIST ClearCore.Storage.dll del /q ClearCore.Storage.dll
IF EXIST EULA.MHDAC del /q EULA.MHDAC
IF EXIST EULA.MSFileReader del /q EULA.MSFileReader
IF EXIST Interop.EDAL.SxS.manifest del /q Interop.EDAL.SxS.manifest
IF EXIST MassLynxRaw.dll del /q MassLynxRaw.dll
IF EXIST Skyline.sln.cache del /q Skyline.sln.cache
IF EXIST Model\Prosit\Config\PrositConfig.xml del /q Model\Prosit\Config\PrositConfig.xml
IF EXIST Test\bin rmdir /s /q Test\bin
IF EXIST Test\obj rmdir /s /q Test\obj
IF EXIST TestA\bin rmdir /s /q TestA\bin
IF EXIST TestA\obj rmdir /s /q TestA\obj
IF EXIST TestFunctional\bin rmdir /s /q TestFunctional\bin
IF EXIST TestFunctional\obj rmdir /s /q TestFunctional\obj
IF EXIST TestTutorial\bin rmdir /s /q TestTutorial\bin
IF EXIST TestTutorial\obj rmdir /s /q TestTutorial\obj
IF EXIST TestUtil\bin rmdir /s /q TestUtil\bin
IF EXIST TestUtil\obj rmdir /s /q TestUtil\obj
IF EXIST TestResults rmdir /s /q TestResults
IF EXIST "SkylineTester Results" rmdir /s /q "SkylineTester Results"
IF EXIST ..\Shared\ProteomeDb\bin rmdir /s /q ..\Shared\ProteomeDb\bin
IF EXIST ..\Shared\ProteomeDb\obj rmdir /s /q ..\Shared\ProteomeDb\obj
IF EXIST ..\Shared\ProteowizardWrapper\Interop.EDAL.SxS.manifest del /q ..\Shared\ProteowizardWrapper\Interop.EDAL.SxS.manifest
IF EXIST ..\Shared\ProteowizardWrapper\bin rmdir /s /q ..\Shared\ProteowizardWrapper\bin
IF EXIST ..\Shared\ProteowizardWrapper\obj rmdir /s /q ..\Shared\ProteowizardWrapper\obj
IF EXIST ..\Shared\ProteowizardWrapper\Microsoft.VC90.MFC rmdir /s /q ..\Shared\ProteowizardWrapper\Microsoft.VC90.MFC
IF EXIST ..\Shared\MSGraph\bin rmdir /s /q ..\Shared\MSGraph\bin
IF EXIST ..\Shared\MSGraph\obj rmdir /s /q ..\Shared\MSGraph\obj
IF EXIST ..\Shared\Crawdad\obj rmdir /s /q ..\Shared\Crawdad\obj
IF EXIST ProtocolBuffers\tmp rmdir /s /q ProtocolBuffers\tmp
IF EXIST ProtocolBuffers\GeneratedCode\*.cs del /q ProtocolBuffers\GeneratedCode\*.cs
IF EXIST Test\ProtocolBuffers\tmp rmdir /s /q Test\ProtocolBuffers\tmp
IF EXIST Test\ProtocolBuffers\GeneratedCode\*.cs del /q Test\ProtocolBuffers\GeneratedCode\*.cs
IF EXIST TestSettings_x64.testsettings del /q TestSettings_x64.testsettings
IF EXIST TestSettings_x86.testsettings del /q TestSettings_x86.testsettings
IF EXIST Executables\Installer\FileList64.txt del /q Executables\Installer\FileList64.txt
IF EXIST Executables\Hardklor\obj rmdir /s /q Executables\Hardklor\obj
IF EXIST Executables\Hardklor\x64 rmdir /s /q Executables\Hardklor\x64
IF EXIST Executables\SkylineBatch\SkylineBatch\Properties\AssemblyInfo.cs del /q Executables\SkylineBatch\SkylineBatch\Properties\AssemblyInfo.cs
IF EXIST Executables\DevTools\TutorialLocalizer\obj rmdir /s /q Executables\DevTools\TutorialLocalizer\obj 
IF EXIST Executables\DevTools\TutorialLocalizer\bin rmdir /s /q Executables\DevTools\TutorialLocalizer\bin
IF EXIST Properties\AssemblyInfo.cs del /q Properties\AssemblyInfo.cs
IF EXIST SkylineCmd\Properties\AssemblyInfo.cs del /q SkylineCmd\Properties\AssemblyInfo.cs
IF EXIST SkylineNightly\Properties\AssemblyInfo.cs del /q SkylineNightly\Properties\AssemblyInfo.cs
IF EXIST SkylineNightlyShim\Properties\AssemblyInfo.cs del /q SkylineNightlyShim\Properties\AssemblyInfo.cs
IF EXIST SkylineTester\Properties\AssemblyInfo.cs del /q SkylineTester\Properties\AssemblyInfo.cs
IF EXIST TestRunner\Properties\AssemblyInfo.cs del /q TestRunner\Properties\AssemblyInfo.cs
popd