// random_access_compressed_ifstream.hpp
//
// This is just an istream which chooses its streambuf implementation
// based on whether the file is gzipped or not.  Could be extended
// to work with other compression formats, too.
//
// What makes it interesting compared to the classic gzstream implementation
// is the ability to perform seeks in a reasonably efficient manner.  In the
// event that a seek is requested (other than a rewind, or tellg()) the file 
// is decompressed once and snapshots of the compression state are made 
// every 1MB or so.  Further seeks are then quite efficient since they don't
// have to begin at the head of the file.
//
// Copyright (C) Insilicos LLC 2008, ALl Rights Reserved.
//
// draws heavily on example code from the zlib distro, so
// for conditions of distribution and use, see copyright notice in zlib.h
//
// based on:
/* gzio.c -- IO on .gz files
* Copyright (C) 1995-2005 Jean-loup Gailly.
* For conditions of distribution and use, see copyright notice in zlib.h
*/

// efficient random access stuff based on
/* zran.c -- example of zlib/gzip stream indexing and random access
* Copyright (C) 2005 Mark Adler
* For conditions of distribution and use, see copyright notice in zlib.h
Version 1.0  29 May 2005  Mark Adler */



#ifndef RANDOM_ACCESS_COMPRESSED_IFSTREAM_INCL
#define RANDOM_ACCESS_COMPRESSED_IFSTREAM_INCL


#if defined(_MSC_VER) || defined(__MINGW32__)  // MSVC or MinGW
#include <winsock2.h>
#include <sys/types.h>
#include <fcntl.h>
#include <io.h>
#else
#include <stdint.h>
#include <netinet/in.h>
#endif
#include <sys/stat.h>
#include <fstream>
#include "boost/iostreams/positioning.hpp"


namespace pwiz {
namespace util {

typedef boost::iostreams::stream_offset random_access_compressed_ifstream_off_t;
class random_access_compressed_streambuf; // forward ref

class random_access_compressed_ifstream : public std::istream {
public:
	random_access_compressed_ifstream(const char *fname, bool *isCompressed=NULL); // optional arg to learn compression state
	virtual ~random_access_compressed_ifstream(); // destructor

};


} // namespace util
} // namespace pwiz 

#endif // RANDOM_ACCESS_COMPRESSED_IFSTREAM_INCL
