@echo off
@setlocal
@echo off

set FILENAME=%~nx1
set FILE_PARENT_PATH=%~dp1
set FILE_EXT=%~x1
set FILE_BASENAME=%~n1
REM set PROTEIN_DATABASE="cow.protein.fasta"
set PROTEIN_DATABASE="cow.protein.PRG2012-subset.fasta"
set MZML_FILENAME=%FILE_BASENAME%.mzML

pushd "%FILE_PARENT_PATH%"

IF NOT EXIST "%FILENAME%" (echo Input file does not exist. & exit /b 1)

FOR %%I IN (msconvert.exe) DO (set MSCONVERT=%%~$PATH:I)
IF NOT EXIST "%MSCONVERT%" (echo msconvert not found; add the location of msconvert.exe to your PATH environment variable. & exit /b 1)

FOR %%I IN (myrimatch.exe) DO (set MYRIMATCH=%%~$PATH:I)
IF NOT EXIST "%MYRIMATCH%" (echo MyriMatch not found; add the location of myrimatch.exe to your PATH environment variable. & exit /b 1)

FOR %%I IN (msgfplus.jar) DO (set MSGF=%%~$PATH:I)
IF NOT EXIST "%MSGF%" (echo MS-GF+ not found; add the location of the MSGFPlus.jar to your PATH environment variable. & exit /b 1)

FOR %%I IN (comet.exe) DO (set COMET=%%~$PATH:I)
IF NOT EXIST "%COMET%" (echo Comet not found; add the location of comet.exe to your PATH environment variable. & exit /b 1)

set JAVA=""
IF EXIST "%PROGRAMFILES(X86)%\Java\jre7\bin\java.exe" set JAVA="%PROGRAMFILES(X86)%\Java\jre7\bin\java.exe"
IF EXIST "%PROGRAMFILES%\Java\jre7\bin\java.exe" set JAVA="%PROGRAMFILES%\Java\jre7\bin\java.exe"
IF NOT EXIST %JAVA% goto :nojava


REM Convert to mzML
REM IF NOT EXIST "%MZML_FILENAME%" "%MSCONVERT%" "%FILENAME%" --outfile "%MZML_FILENAME%" --mzML -z -v --filter "msLevel 2-" --filter "peakPicking true 1-"
IF NOT EXIST "%MZML_FILENAME%" "%MSCONVERT%" "%FILENAME%" --outfile "%MZML_FILENAME%" --mzML -z -v --filter "msLevel 2-" --filter "peakPicking cwt msLevel=1-"


REM Search with MyriMatch, using a different config for Thermo than for other vendors
IF NOT EXIST "%FILE_BASENAME%-mm.pepXML" (
	IF /I "%FILE_EXT%"==".RAW" (
		"%MYRIMATCH%" -cfg "myrimatch-thermo.cfg" "%MZML_FILENAME%" -ProteinDatabase "%PROTEIN_DATABASE%" -OutputSuffix "-mm"
	) ELSE (
		"%MYRIMATCH%" -cfg "myrimatch.cfg" "%MZML_FILENAME%" -ProteinDatabase "%PROTEIN_DATABASE%" -OutputSuffix "-mm"
	)
)


REM Search with MS-GF+, using a different config for Thermo than for other vendors
IF NOT EXIST "%FILE_BASENAME%-msgf.mzid" (
	IF /I "%FILE_EXT%"==".RAW" (
		%JAVA% -jar "%MSGF%" -mod "Mods.txt" -inst 0 -m 0 -protocol 0 -tda 1 -t 20ppm -ti -1,2 -ntt 0 -thread 8 -addFeatures 1 -s "%MZML_FILENAME%" -o "%FILE_BASENAME%-msgf.mzid" -d "%PROTEIN_DATABASE%"
	) ELSE (
		%JAVA% -jar "%MSGF%" -mod "Mods.txt" -inst 2 -m 3 -protocol 0 -tda 1 -t 50ppm -ti -1,2 -ntt 1 -thread 8 -addFeatures 1 -s "%MZML_FILENAME%" -o "%FILE_BASENAME%-msgf.mzid" -d "%PROTEIN_DATABASE%"
	)
)

REM Search with X-Tandem!

REM Search with Comet, using a different config for Thermo than for other vendors
IF NOT EXIST "%FILE_BASENAME%-cm.pep.xml" (
	IF /I "%FILE_EXT%"==".RAW" (
		%COMET% "%MZML_FILENAME%" -Pcomet.params.high-low -D"%PROTEIN_DATABASE%"
	) ELSE (
		%COMET% "%MZML_FILENAME%" -Pcomet.params.high-high -D"%PROTEIN_DATABASE%"
	)
)

REM Search with Pepitome

REM Search with DirecTag/TagRecon

popd

exit /b 0

:nojava
echo Could not find Java. Install it now or edit this script to add its location on your machine.
exit /b 1