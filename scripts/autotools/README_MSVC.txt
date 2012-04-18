Building the ProteoWizard library for your MSVC-based projects

The ProteoWizard developers use the bjam build system to build the various ProteoWizard tools like msconvert, but for your own projects you will probably find it more convenient to use normal MSVC project files to build and link the ProteoWizard library.  For that purpose we provide the zip file whose contents you are now viewing - it's a special subset of the complete ProteoWizard project.

Note that use of vendor DLLs for reading native mass spec formats isn't supported, for that you're going to have to go ahead and use the bjam build system.  For that, go to proteowizard.org to get the full project.

But if you just want mzML, mzXML. MGF etc handling then you can use the libpwiz project files found in the msvc8, msvc9, and msvc10 directories.  There you'll find an .sln for building the pwiz library and several example and test programs to get you moving.


