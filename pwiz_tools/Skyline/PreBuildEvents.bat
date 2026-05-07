REM folders where the .resx files get processed by SortRESX.exe so that all of the elements are sorted by name.
REM paths are relative to pwiz_tools\Skyline
for %%f in ( 
"%~dp0Controls\AuditLog"
"%~dp0Executables\DevTools\SortRESX
"%~dp0Model\Databinding\Entities"
"%~dp0Properties"
) do (
echo "%~dp0Executables\DevTools\SortRESX\SortRESX.exe" --preserveOrderInResourcesResx %%~f 
"%~dp0Executables\DevTools\SortRESX\SortRESX.exe" --preserveOrderInResourcesResx %%~f
)

echo "%~dp0ProtocolBuffers\generatecode.bat"
"%~dp0ProtocolBuffers\generatecode.bat"
