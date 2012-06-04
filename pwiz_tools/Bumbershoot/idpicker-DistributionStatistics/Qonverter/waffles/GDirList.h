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

#include <fstream>
#include <stack>
#include <vector>

namespace GClasses {

class GBlobQueue;

/// This class contains a list of files and a list of folders.
/// The constructor populates these lists with the names of files and folders in
/// the current working directory
class GDirList
{
public:
	GDirList();
	~GDirList() {}

	std::vector<std::string> m_folders;
	std::vector<std::string> m_files;
};

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
