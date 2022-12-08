REM folders where the .resx files get processed by SortRESX.exe so that all of the elements are sorted by name
for %%f in ( 
"%~dp0..\..\pwiz_tools\Skyline\Model\Databinding\Entities"
"%~dp0..\..\pwiz_tools\Skyline\Executables\SortRESX
) do (
echo "%~dp0Executables\SortRESX\SortRESX.exe" %%~f 
"%~dp0Executables\SortRESX\SortRESX.exe" %%~f
)

REM folders where the .resx files get processed by SortRESX.exe but the element order is preserved
REM so that it just normalizes whitespace and removes elements such as "TrayLocation" or "Type"
for %%f in (
"%~dp0..\..\pwiz_tools\Skyline"
) do (
echo "%~dp0Executables\SortRESX\SortRESX.exe" --preserveElementOrder %%~f 
"%~dp0Executables\SortRESX\SortRESX.exe" --preserveElementOrder %%~f
)

echo "%~dp0ProtocolBuffers\generatecode.bat"
"%~dp0ProtocolBuffers\generatecode.bat"
