Building the ProteoWizard library for your MSVC-based projects

The ProteoWizard developers use the bjam build system to build the various ProteoWizard tools like msconvert, but for your own projects you will probably find it more convenient to use normal MSVC project files to build and link the ProteoWizard library.

Note that use of vendor DLLs for reading native mass spec formats isn't supported, for that you're going to have to go ahead and use the bjam build system.  But if you just want mzML handling then you can use the libpwiz project files found in the msvc8, msvc9, and msvc10 directories.

The pwiz.sln file in this directory is a wrapper for the bjam build system, you should look in the msvc8, msvc9, and msvc10 directories instead.
