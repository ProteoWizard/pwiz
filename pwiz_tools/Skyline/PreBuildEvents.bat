REM folders where the .resx files get processed by SortRESX.exe so that all of the elements are sorted by name.
REM paths are relative to pwiz_tools\Skyline
for %%f in ( 
"%~dp0Model\Databinding\Entities"
"%~dp0Executables\SortRESX
) do (
echo "%~dp0Executables\SortRESX\SortRESX.exe" %%~f 
"%~dp0Executables\SortRESX\SortRESX.exe" %%~f
)

REM folders where the .resx files get processed by SortRESX.exe but the element order is preserved
REM so that it just normalizes whitespace and removes elements such as "TrayLocation" or "Type"
for %%f in (
"%~dp0Properties"
"%~dp0Controls\AuditLog"
) do (
echo "%~dp0Executables\SortRESX\SortRESX.exe" --preserveElementOrder %%~f 
"%~dp0Executables\SortRESX\SortRESX.exe" --preserveElementOrder %%~f
)

echo "%~dp0ProtocolBuffers\generatecode.bat"
"%~dp0ProtocolBuffers\generatecode.bat"
