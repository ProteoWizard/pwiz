pushd %~dp0
if exist PrositConfig_production.xml (
	copy PrositConfig_production.xml PrositConfig.xml
) else (
	copy PrositConfig_development.xml PrositConfig.xml
)
