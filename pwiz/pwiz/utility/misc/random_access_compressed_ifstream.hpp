// $Id$
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


#include "pwiz/utility/misc/Export.hpp"
#ifdef WIN32
#ifdef max //trouble with windef.h max(a,b) colliding with <limits> datatype.max()
#undef max
#undef min
#endif
#endif
#include "boost/iostreams/positioning.hpp"
#include <fstream>


namespace pwiz {
namespace util {

typedef boost::iostreams::stream_offset random_access_compressed_ifstream_off_t;
//class random_access_compressed_streambuf; // forward ref

class PWIZ_API_DECL random_access_compressed_ifstream : public std::istream {
public:
	random_access_compressed_ifstream(); // default ctor
	random_access_compressed_ifstream(const char *fname); // optional arg to learn compression state
	virtual ~random_access_compressed_ifstream(); // destructor
	void open(const char *fname); // for ease of use as ifstream replacement
	bool is_open() const; // for ease of use as ifstream replacement
	void close(); // for ease of use as ifstream replacement
	enum eCompressionType {NONE, GZIP}; // maybe add bz2 etc later?
	eCompressionType getCompressionType() const {
		return compressionType;
	}
private:
	eCompressionType compressionType;
};


} // namespace util
} // namespace pwiz 

#endif // RANDOM_ACCESS_COMPRESSED_IFSTREAM_INCL
