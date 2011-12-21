/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __GFILE_H__
#define __GFILE_H__

#include <sys/types.h>
#include <sys/stat.h>
#include <fcntl.h>
#include <istream>
#include <vector>

namespace GClasses {

/// Helper struct to hold the results from GFile::ParsePath
struct PathData
{
	int dirStart;
	int fileStart;
	int extStart;
	int len;
};


/// Contains some useful routines for manipulating files
class GFile
{
public:
	/// returns true if the file exists
	static bool doesFileExist(const char *filename);

	/// returns true if the directory exists
	static bool doesDirExist(const char* szDir);

	/// Deletes the specified file. Returns true iff successful.
	static bool deleteFile(const char* szFilename);

	/// Removes the specified directory. Fails if it is not empty.
	/// Returns true iff successful.
	static bool removeDir(const char* szDir);

	/// This finds the last slash in szBuff and returns a
	/// pointer to the char past that.  (If there are no
	/// slashes or back-slashes, it returns szBuff)
	static const char* clipPath(const char* szBuff);

	/// This finds the last slash in szBuff and sets it
	/// to '\0' and returns szBuff.
	static char* clipFilename(char* szBuff);

	/// returns a user's home directory for the various OS's
	static bool localStorageDirectory(char *toHere);

	/// This copies a file.  It doesn't check to see if it is
	/// overwriting--it just does the copying.  On success it
	/// returns true.  On error it returns false.  It won't
	/// work with a file bigger than 2GB.  Both paths must
	/// include the filename.
	static bool copyFile(const char* szSrcPath, const char* szDestPath);

	/// Loads a file into memory and returns a pointer to the
	/// memory.  You must delete the buffer it returns.
	static char* loadFile(const char* szFilename, size_t* pnSize);

	/// Saves a buffer as a file.  Returns true on success
	static void saveFile(const char* pBuf, size_t nSize, const char* szFilename);

	/// Fills "list" with the names of all the files (excluding folders)
	/// in the specified directory.
	static void fileList(std::vector<std::string>& list, const char* dir = ".");

	/// Fills "list" with the names of all the folders in the specified directory.
	/// If excludeDots is true, then folders named "." or ".." will be excluded.
	static void folderList(std::vector<std::string>& list, const char* dir = ".", bool excludeDots = true);

	/// This is a brute force way to make a directory.  It
	/// iterates through each subdir in szDir and calls mkdir
	/// until it has created the complete set of nested directories.
	static bool makeDir(const char* szDir);

	/// Remove extra ".." folders in the path
	static void condensePath(char* szPath);

	/// This returns the number of seconds since 1970 UTC
	static time_t modifiedTime(const char* szFilename);

	/// Set the last modified time of a file
	static void setModifiedTime(const char *filename, time_t t);
/*
	/// This only writes one pass of random numbers over the file, so it may still be
	/// possible for the file to be recovered with expensive hardware that takes
	/// advantage of the fact that the hard disk write head may drift slightly while
	/// writing in order to read older data that may still be encoded along the edge
	/// of the path on the platter.
	static bool shredFile(const char* szFilename);

	/// Delete a folder and recursively shred all it's contents. Returns true if successful
	/// This only writes one pass of random numbers over the file--see the warning on
	/// ShredFile.
	static bool shredFolder(const char* szPath);
*/
	/// Identifies the folder, file, extension, and total length from a path
	static void parsePath(const char* szPath, struct PathData* pData);

	/// returns a temporary filename
	static void tempFilename(char* pBuf);
};


/// This implements a simple compression/decompression algorithm
class GCompressor
{
public:
	/// Compress pIn. You are responsible to delete[] pOut. The new length is guaranteed to be at
	/// most len+5, and typically will be much smaller. Also, the first 4 bytes in the compressed
	/// data will be len (the size when uncompressed).
	static unsigned char* compress(unsigned char* pIn, unsigned int len, unsigned int* pOutNewLen);

	/// Uncompress pIn. You are responsible to delete[] pOut.
	static unsigned char* uncompress(unsigned char* pIn, unsigned int len, unsigned int* pOutUncompressedLen);

#ifndef NO_TEST_CODE
	static void test();
#endif
};



} // namespace GClasses

#endif // __GFILE_H__
