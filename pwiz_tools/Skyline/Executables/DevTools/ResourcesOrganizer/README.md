ResourcesOrganizer reads resources from .resx files and puts them into a SQLite database.

The Visual Studio solution is:
pwiz_tools\Skyline\Executables\DevTools\ResourcesOrganizer\ResourcesOrganizer.sln

The project requires .Net 8 to build.

>The project has been published to the folder:
>
> `pwiz_tools\Skyline\Executables\DevTools\ResourcesOrganizer\ResourcesOrganizer\scripts\exe`

### The "scripts" folder contains the following scripts which are intended to be run from the root of the project:

>`pwiz_tools\Skyline\Executables\DevTools\ResourcesOrganizer\scripts\readResxFiles.bat`
>
>creates a file called "resources.db" which contains information from all of the .resx files

> `pwiz_tools\Skyline\Executables\DevTools\ResourcesOrganizer\scripts\generateLocalizationCsvFiles.bat`
>
> *creates files "localization.ja.csv" and "localization.zh-CHS.csv" containing strings that have "NeedsReview:" comments in them*

### Additionally, the following commands will be useful after getting updated copies of these .csv files back from the localizers:

>  `pwiz_tools\Skyline\Executables\DevTools\ResourcesOrganizer\scripts\exe\ResourcesOrganizer.exe importLocalizationCsv`
>
> *Imports the contents of "localization.ja.csv" and "localization.zh-CHS.csv" back into resources.db*

> `pwiz_tools\Skyline\Executables\DevTools\ResourcesOrganizer\scripts\exe\ResourcesOrganizer.exe exportResx resxFiles.zip`
>
> *Outputs the contents of `resources.db` into zipfiles in "resxFiles.zip". You can then extract the contents of that zip file to the root of the project and normalize the resx files, making sure that the contents of the resx files are in the same order across the languages.*

