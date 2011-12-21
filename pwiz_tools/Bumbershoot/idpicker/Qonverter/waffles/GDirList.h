/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __GDIRLIST_H__
#define __GDIRLIST_H__

#ifdef WINDOWS


/// ----------------------
///  WINDOWS VERSION
/// ----------------------
#include <sstream>
#include <string>
#include <stack>

namespace GClasses {

class GFileFinder;
class GBlobQueue;

/// Iterates through the files and/or folders in the current directory.
class GDirList
{
protected:
	GFileFinder* m_pFinder[256]; // it won't search more than 256 dir nests deep
	int m_nNests;
	std::ostringstream m_buffer;
	std::string m_tempBuf;
	char m_szOldDir[256];
	bool m_bReportFiles;
	bool m_bReportDirs;
	bool m_bRecurseSubDirs;
	bool m_bReportPaths;

public:
	GDirList(bool bRecurseSubDirs = true, bool bReportFiles = true, bool bReportDirs = false, bool bReportPaths = true);
	virtual ~GDirList();

	/// Returns true iff this object will recurse sub dirs
	bool recurseSubDirs() { return m_bRecurseSubDirs; }

	/// Returns true iff this object will report files
	bool reportFiles() { return m_bReportFiles; }

	/// Returns true iff this object will report dirs
	bool reportDirs() { return m_bReportDirs; }

	/// Returns true iff this object will report full paths
	bool reportPaths() { return m_bReportPaths; }

	/// Returns the next filename or dirname, or NULL if there are no more
	const char* GetNext();
};

} // namespace GClasses

#else // WINDOWS

// ----------------------
//  LINUX VERSION
// ----------------------
#include <sys/types.h>
#include <sys/dir.h>
#include <dirent.h>
#include <sstream>
#include <string>
#include <stack>

namespace GClasses {

class GBlobQueue;

class GDirList
{
protected:
	DIR* m_pDirs[256]; // it won't search more than 256 dir nests deep
	DIR* m_pCurDir;
	int m_nNests;
	std::ostringstream m_buffer;
	std::string m_tempBuf;
	char m_szOldDir[256];
	bool m_bReportFiles;
	bool m_bReportDirs;
	bool m_bRecurseSubDirs;
	bool m_bReportPaths;

public:
	GDirList(bool bRecurseSubDirs = true, bool bReportFiles = true, bool bReportDirs = false, bool bReportPaths = true);
	virtual ~GDirList();

	/// Returns true iff this object will recurse sub dirs
	bool recurseSubDirs() { return m_bRecurseSubDirs; }

	/// Returns true iff this object will report files
	bool reportFiles() { return m_bReportFiles; }

	/// Returns true iff this object will report dirs
	bool reportDirs() { return m_bReportDirs; }

	/// Returns true iff this object will report full paths
	bool reportPaths() { return m_bReportPaths; }

	/// Returns the next filename or dirname, or NULL if there are no more
	const char* GetNext();
};

} // namespace GClasses

#endif // !WIN32


namespace GClasses {

/// This turns a file or a folder (and its contents recursively) into a stream of bytes
class GFolderSerializer
{
protected:
	const char* m_szPath;
	char* m_szOrigPath;
	char* m_pBuf;
	char* m_pPos;
	size_t m_size;
	size_t m_state;
	size_t m_remaining;
	std::ifstream* m_pInStream;
	std::stack<GDirList*> m_dirStack;
	unsigned char* m_pCompressedBuf;
	char* m_pUncompressedBuf;
	size_t m_uncompressedPos;
	unsigned int m_compressedSize;
	bool m_compressedBufReady;
	size_t m_bytesOut;

public:
	/// szPath can be a filename or a foldername
	GFolderSerializer(const char* szPath, bool compress);
	~GFolderSerializer();

	/// Returns a pointer to the next chunk of bytes. Returns NULL
	/// if it is done.
	char* next(size_t* pOutSize);

	/// Returns the number of bytes that have been sent out so far
	size_t bytesOut() { return m_bytesOut; }

protected:
	char* nextPiece(size_t* pOutSize);
	void addName(const char* szName);
	void startFile(const char* szFilename);
	void continueFile();
	void startDir(const char* szDirName);
	void continueDir();
};

/// This class complements GFolderSerializer
class GFolderDeserializer
{
protected:
	GBlobQueue* m_pBQ1;
	GBlobQueue* m_pBQ2;
	size_t m_compressedBlockSize;
	size_t m_state;
	unsigned int m_nameLen;
	unsigned long long m_fileLen;
	std::ofstream* m_pOutStream;
	size_t m_depth;
	std::string* m_pBaseName;

public:
	GFolderDeserializer(std::string* pBaseName = NULL);
	~GFolderDeserializer();

	void doNext(const char* pBuf, size_t bufLen);

protected:
	void pump1();
	void pump2();
};


} // namespace GClasses

#endif // __GDIRLIST_H__
