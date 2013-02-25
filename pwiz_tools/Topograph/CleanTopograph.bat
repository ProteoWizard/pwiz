@echo off
setlocal
@echo off

REM # Get the location of quickbuild.bat and drop trailing slash
set PWIZ_ROOT=%~dp0
set PWIZ_ROOT=%PWIZ_ROOT:~0,-1%
pushd %PWIZ_ROOT%

REM # TODO(nicksh):12/22/2011 Remove lines relating to "turnover" after it has been deleted from people's machines.

IF EXIST bin rmdir /s /q bin
IF EXIST obj rmdir /s /q obj
IF EXIST topograph.sln.cache del /q topograph.sln.cache
IF EXIST turnover\Microsoft.VC90.MFC rmdir /s /q turnover\Microsoft.VC90.MFC
IF EXIST turnover\ClearCore.dll del /q turnover\ClearCore.dll
IF EXIST turnover\ClearCore.Storage.dll del /q turnover\ClearCore.Storage.dll
IF EXIST turnover\EULA.MHDAC del /q turnover\EULA.MHDAC
IF EXIST turnover\EULA.MSFileReader del /q turnover\EULA.MSFileReader
IF EXIST turnover\Interop.EDAL.SxS.manifest del /q turnover\Interop.EDAL.SxS.manifest
IF EXIST turnover\MassLynxRaw.dll del /q turnover\MassLynxRaw.dll
IF EXIST TopographApp\Microsoft.VC90.MFC rmdir /s /q TopographApp\Microsoft.VC90.MFC
IF EXIST TopographApp\ClearCore.dll del /q TopographApp\ClearCore.dll
IF EXIST TopographApp\ClearCore.Storage.dll del /q TopographApp\ClearCore.Storage.dll
IF EXIST TopographApp\EULA.MHDAC del /q TopographApp\EULA.MHDAC
IF EXIST TopographApp\EULA.MSFileReader del /q TopographApp\EULA.MSFileReader
IF EXIST TopographApp\Interop.EDAL.SxS.manifest del /q TopographApp\Interop.EDAL.SxS.manifest
IF EXIST TopographApp\MassLynxRaw.dll del /q TopographApp\MassLynxRaw.dll
IF EXIST TestResults rmdir /s /q TestResults
IF EXIST TopographTestProject\bin rmdir /s /q TopographTestProject\bin
IF EXIST TopographTestProject\obj rmdir /s /q TopographTestProject\obj
IF EXIST turnover\bin rmdir /s /q turnover\bin
IF EXIST turnover\obj rmdir /s /q turnover\obj
IF EXIST turnover_lib\bin rmdir /s /q turnover_lib\bin
IF EXIST turnover_lib\obj rmdir /s /q turnover_lib\obj
IF EXIST TopographApp\bin rmdir /s /q TopographApp\bin
IF EXIST TopographApp\obj rmdir /s /q TopographApp\obj
IF EXIST TopographApp\publish rmdir /s /q TopographApp\publish
IF EXIST TopographApp\publish64 rmdir /s /q TopographApp\publish64
IF EXIST TopographLib\bin rmdir /s /q TopographLib\bin
IF EXIST TopographLib\obj rmdir /s /q TopographLib\obj
IF EXIST ..\Shared\Common\bin rmdir /s /q ..\Shared\Common\bin
IF EXIST ..\Shared\Common\obj rmdir /s /q ..\Shared\Common\obj
IF EXIST ..\Shared\ProteomeDb\bin rmdir /s /q ..\Shared\ProteomeDb\bin
IF EXIST ..\Shared\ProteomeDb\obj rmdir /s /q ..\Shared\ProteomeDb\obj
IF EXIST ..\Shared\ProteowizardWrapper\Interop.EDAL.SxS.manifest del /q ..\Shared\ProteowizardWrapper\Interop.EDAL.SxS.manifest
IF EXIST ..\Shared\ProteowizardWrapper\bin rmdir /s /q ..\Shared\ProteowizardWrapper\bin
IF EXIST ..\Shared\ProteowizardWrapper\obj rmdir /s /q ..\Shared\ProteowizardWrapper\obj
IF EXIST ..\Shared\ProteowizardWrapper\Microsoft.VC90.MFC rmdir /s /q ..\Shared\ProteowizardWrapper\Microsoft.VC90.MFC
IF EXIST ..\Shared\MSGraph\bin rmdir /s /q ..\Shared\MSGraph\bin
IF EXIST ..\Shared\MSGraph\obj rmdir /s /q ..\Shared\MSGraph\obj

popd
