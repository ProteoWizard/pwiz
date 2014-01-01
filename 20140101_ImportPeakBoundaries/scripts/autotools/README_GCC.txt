Building the ProteoWizard library for your GCC-based projects

The ProteoWizard developers use the bjam build system to build the various ProteoWizard tools like msconvert, but for your own projects you will probably find it more convenient to use normal GNU Makefiles files to build and link the ProteoWizard library "libpwiz".

Just do the usual autotools steps:
./autotools/configure
make
make check  (optional, runs some tests to ensure a proper build)
make install