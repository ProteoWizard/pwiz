#Localization Helper V1.0
#Author: Yuval Boss <yuval .at. u.washington.edu>, 
#MacCoss Lab, Department of Genome Sciences, UW

Requirments:
* Visual Studios 2010
* Resharper 8.2+

To install the plugin close Visual Studios, run install-VS2010.cmd, then reopen Visual Studios.
If for some reason you do not see the plugin in ReSharper > Options > Plugins, check to make sure the dll was successfully coppied to
C:%AppData%\Local\JetBrains\ReSharper\v8.2\vs10.0\plugins\LocalizaitonHelper.dll

This R# plugin highlights and throws warnings for all strings which are not marked as '// Not L10N' or are not in resources.

This plugin confilcts with 'Element is localizable' r# warning.  To fix open Visual Studios > Resharper > Options > Inspection Severity:
Search 'Element is localizable' and change from Warning to Do not show.
***
To mark a line as not localizable add '// Not L10N' to the end of the line.
To maRk a block of code as not localizable add:
// ReSharper disable NonLocalizedString
	code()
	{
	 ...
	}
// ReSharper restore NonLocalizedString
You can change the severity level of the issue in ReSharper > Options > Inspection Severity

