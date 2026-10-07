Third-party tools and archives the build extracts or runs in place.

7za.exe / 7zz            7-Zip command-line extractors (Windows / Linux) for the vendor SDK
                         archives in vendor-archives/ and the msparser archives below.
bsdtar.exe               libarchive tar used by build/ExtractTestData.targets to unpack the
  + archive.dll, bzip2.dll, libbz2.dll, zlib1.dll (its runtime dependencies, loaded from this directory)
                         vendor reader test data (pwiz/data/vendor_readers/*/Reader_*_Test.data.tar.bz2)
                         and by Hardklor.vcxproj for the zlib/expat sources.
msparser_3_1_0_x86_*.7z  Matrix Science Mascot Parser (Windows x64 / Linux x64), extracted here
                         by pwiz_tools/BiblioSpec/src/BiblioSpec/BiblioSpec.csproj for .dat support.
zlib-1.2.3.tar.bz2       Extracted and compiled by pwiz_tools/Skyline/Executables/Hardklor/Hardklor.vcxproj.
expat-2.0.1.tar.bz2      Same.

The extracted directories are gitignored (see the /libraries/ entries in .gitignore).
