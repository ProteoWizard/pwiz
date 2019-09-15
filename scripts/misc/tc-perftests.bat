@echo off
set SKYLINE_DOWNLOAD_PATH=z:\download

REM Remove C++ build artifacts to free up space
rmdir /s /q build-nt-x86

pwiz_tools\Skyline\bin\x64\Release\TestRunner.exe test=TestTutorial.dll loop=1 language=en perftests=on teamcitytestdecoration=on
rmdir /s /q %SKYLINE_DOWNLOAD_PATH%

FOR %%I IN (AgilentIMSImportTest,AgilentSpectrumMillRampedIMSImportTest,AgilentSpectrumMillSpectralLibTest,BrukerPasefMascotImportTest,ElectronIonizationAllIonsPerfTest,ImportResultsHugeTest,MeasuredDriftValuesPerfTest,MeasuredInverseK0ValuesPerfTest,NegativeIonDIATest,PeakSortingTest,TestAreaCVHistogramQValuesAndRatios,TestDriftTimePredictorSmallMolecules,TestDriftTimePredictorTutorial,TestHiResMetabolomicsTutorial,TestImportMassOnlyMolecules,TestMinimizeResultsPerformance,TestMs3Chromatograms,TestThermoFAIMS,UniquePeptides0PerfTest,UniquePeptides1PerfTest,UniquePeptides2PerfTest,UniquePeptides3PerfTest,UniquePeptides4PerfTest,UniquePeptides5PerfTest,WatersIMSImportTest,WatersLockmassCmdlinePerfTest,WatersLockmassPerfTest) DO (
pwiz_tools\Skyline\bin\x64\Release\TestRunner.exe test=%%I loop=1 language=en perftests=on teamcitytestdecoration=on
rmdir /s /q %SKYLINE_DOWNLOAD_PATH%
)
