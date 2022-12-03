for %%f in ( 
"%~dp0..\..\pwiz_tools\Skyline\Model\Databinding\Entities"
"%~dp0..\..\pwiz_tools\Skyline\Executables\SortRESX
) do (
echo "%~dp0Executables\SortRESX\SortRESX.exe" %%~f 
"%~dp0Executables\SortRESX\SortRESX.exe" %%~f
)

echo "%~dp0ProtocolBuffers\generatecode.bat"
"%~dp0ProtocolBuffers\generatecode.bat"
