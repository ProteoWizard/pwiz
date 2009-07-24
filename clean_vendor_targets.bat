@echo off
echo Cleaning project...
IF EXIST build\msvc-release rmdir /s /q build\msvc-release
IF EXIST build\msvc-debug rmdir /s /q build\msvc-debug
IF EXIST build\gcc-release rmdir /s /q build\gcc-release
IF EXIST build\gcc-debug rmdir /s /q build\gcc-debug
IF EXIST build\pwiz\data\vendor_readers rmdir /s /q build\pwiz\data\vendor_readers
IF EXIST build\pwiz\utility\vendor_api rmdir /s /q build\pwiz\utility\vendor_api
IF EXIST build\pwiz\utility\bindings rmdir /s /q build\pwiz\utility\bindings
IF EXIST build\pwiz_aux\msrc\data\vendor_readers rmdir /s /q build\pwiz_aux\msrc\data\vendor_readers
IF EXIST build\pwiz_aux\msrc\utility\vendor_api rmdir /s /q build\pwiz_aux\msrc\utility\vendor_api
IF EXIST build\pwiz\analysis\spectrum_processing rmdir /s /q build\pwiz\analysis\spectrum_processing
IF EXIST build\pwiz\analysis\chromatogram_processing rmdir /s /q build\pwiz\analysis\chromatogram_processing
IF EXIST pwiz\data\vendor_readers\Thermo\Reader_Thermo_Test.data rmdir /s /q pwiz\data\vendor_readers\Thermo\Reader_Thermo_Test.data
IF EXIST pwiz\data\vendor_readers\Agilent\Reader_Agilent_Test.data rmdir /s /q pwiz\data\vendor_readers\Agilent\Reader_Agilent_Test.data
IF EXIST pwiz_aux\msrc\data\vendor_readers\ABI\Reader_ABI_Test.data rmdir /s /q pwiz_aux\msrc\data\vendor_readers\ABI\Reader_ABI_Test.data
IF EXIST pwiz_aux\msrc\data\vendor_readers\Waters\Reader_Waters_Test.data rmdir /s /q pwiz_aux\msrc\data\vendor_readers\Waters\Reader_Waters_Test.data
IF EXIST pwiz_aux\msrc\data\vendor_readers\Bruker\Reader_Bruker_Test.data rmdir /s /q pwiz_aux\msrc\data\vendor_readers\Bruker\Reader_Bruker_Test.data