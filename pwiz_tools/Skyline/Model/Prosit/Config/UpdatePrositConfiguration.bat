REM If the file "PrositConfig_production.xml" exists, then copy
REM it into "PrositConfig.xml". Otherwise, copy "PrositConfig_development.xml"
REM The product.xml file contains private keys and might not be present in the source tree

pushd %~dp0
if exist PrositConfig_production.xml (
	copy PrositConfig_production.xml PrositConfig.xml
) else (
	copy PrositConfig_development.xml PrositConfig.xml
)
