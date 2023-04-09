SET CVGEN_PATH=".\build-nt-x86\pwiz\data\common\msvc-14.2\rls\adrs-mdl-64\async-excpt-on\lnk-sttc\thrd-mlt"
SET OBO_PATH=".\pwiz\data\common"

%CVGEN_PATH%\cvgen.exe %OBO_PATH%\unit.obo %OBO_PATH%\unimod.obo %OBO_PATH%\psi-ms.obo %OBO_PATH%\imagingMS.obo

copy %CVGEN_PATH%\cv.hpp %OBO_PATH%
copy %CVGEN_PATH%\cv.cpp %OBO_PATH%