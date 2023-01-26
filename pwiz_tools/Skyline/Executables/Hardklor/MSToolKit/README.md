# MSToolkit

The MSToolkit is a light-weight C++ library for reading, writing, and manipulating mass spectrometry data. The MSToolkit is easily linked to virtually any C++ algorithm for simple, fast file reading and analysis.

### Version 83.0.1, June 4 2020

### Supported File Formats
  * *mzML* including internal compression (zlib and numpress) and external compression (.mzML.gz) _read/write\*_
  * *mzXML* including internal compression (zlib) and external compression (.mzXML.gz) _read-only_
  * *mzID* _read/write_ (a.k.a. mzIdentML)
  * *mgf* _read/write_
  * *ms1* _read/write_
  * *ms2* _read/write_
  * *bms1* _read/write_
  * *bms2* _read/write_
  * *cms1* _read/write_
  * *cms2* _read/write_
  * *RAW* Thermo proprietary file format (Windows only, requires Xcalibur/MSFileReader) _read-only_
  
  _\* Note: .mzML writing produces funtional files, but currently does not export all meta data. Spectral peak data is complete. .mzML.gz files
  are not produced by the toolkit, and must be gzipped externally._
  _\* Note: .mzID reading and writing is in active development to support all the weird unexpected stuff people do with that format._


### Simple Interface
  * Open any file format from a single function call.
  * Store any spectrum in a simple, comprehensive data structure.
  * Sequential or random-access file reading.


### Easy Integration
  * All headers included from a single location.
  * Single library file easily linked by the compiler.

### Compiling
A few hints:
 * On GNU/Linux, try "make all" to build the library. On VS/Windows, you'll have to build your own solution.
 * Add _NOSQLITE to build smaller library without SQLite. SQLite is only required for special case usage.
 * Add _NO_THERMORAW to build without Thermo file support in Windows. This is not necessary in GNU/Linux, where Thermo support is disabled by default.
 * Older versions of MSVC may require building with XML_STATIC declared.
 * Declaring WIN32 may still be required for compiling 64-bit libraries with MSVC.
 * MAKEFILE.nmake is for MSVC builds without creating your own solution file. Use the x64 Native Tools Command Prompt in VS (tested on VS2019) and type nmake /f MAKEFILE.nmake all
 
### License
Code written for the MSToolkit uses the Apache License, Version 2.0. All 3rd party software included in the MSToolkit library retains its original license.
