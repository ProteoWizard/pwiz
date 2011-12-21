/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#include "GDirList.h"
#include "GError.h"
#include "GFile.h"
#include "GBlob.h"
#include "GHolders.h"
#include <string.h>
#ifdef WINDOWS
#	include <direct.h> // for "getcwd", "chdir", etc.
#	include <io.h> // for "access"
#endif

using std::string;

namespace GClasses {

GDirList::GDirList()
{
	char buf[300];
	if(!getcwd(buf, 300))
		ThrowError("getcwd failed");
	GFile::folderList(m_folders, buf, true);
	GFile::fileList(m_files, buf);
}




#define BUF_SIZE 2048
#define COMPRESS_BUF_SIZE 65536

GFolderSerializer::GFolderSerializer(const char* szPath, bool compress)
{
	m_szPath = szPath;
	m_szOrigPath = new char[300];
	if(!getcwd(m_szOrigPath, 300))
		ThrowError("getcwd failed");
	m_pBuf = new char[BUF_SIZE];
	m_pPos = m_pBuf;
	m_size = BUF_SIZE;
	m_state = 0;
	m_pInStream = NULL;
	m_pCompressedBuf = NULL;
	if(compress)
		m_pUncompressedBuf = new char[COMPRESS_BUF_SIZE];
	else
		m_pUncompressedBuf = NULL;
	m_compressedSize = 0;
	m_uncompressedPos = 0;
	m_compressedBufReady = false;
	m_bytesOut = 0;
}

GFolderSerializer::~GFolderSerializer()
{
	if(chdir(m_szOrigPath) != 0)
		ThrowError("Failed to restore original path");
	delete[] m_szOrigPath;
	delete[] m_pBuf;
	delete(m_pInStream);
	delete[] m_pCompressedBuf;
	delete[] m_pUncompressedBuf;
}

char* GFolderSerializer::nextPiece(size_t* pOutSize)
{
	switch(m_state)
	{
	case 0: // uncompressed header
		memcpy(m_pPos, "ugfs", 4);
		m_pPos += 4;
		m_size -= 4;
		m_state = 4;
		break;
	case 1: // figure out what to do next
		if(m_dirStack.size() > 0)
			continueDir();
		else
			return NULL;
		break;
	case 2: // continue reading file
		continueFile();
		break;
	case 3: // continue reading dir
		continueDir();
		break;
	case 4: // get started
		{
			// Change to the directory with the file or folder
			string sPath = m_szPath;
			if(sPath.length() > 0 && (sPath[sPath.length() - 1] == '/' || sPath[sPath.length() - 1] == '\\'))
				sPath.erase(sPath.length() - 1);
			PathData pd;
			GFile::parsePath(sPath.c_str(), &pd);
			if(pd.fileStart > 0)
			{
				string s;
				s.assign(m_szPath, pd.fileStart);
				if(chdir(s.c_str()) != 0)
					ThrowError("Failed to change dir to ", s.c_str());
			}

			// Add the file or folder
			if(access(m_szPath, 0) != 0)
				ThrowError("The file or folder ", m_szPath, " does not seem to exist");
			struct stat status;
			stat(m_szPath, &status);
			if(status.st_mode & S_IFDIR)
				startDir(m_szPath + pd.fileStart);
			else
				startFile(m_szPath + pd.fileStart);
		}
		break;
	default:
		ThrowError("Unexpected state");
	}
	*pOutSize = (m_pPos - m_pBuf);
	m_pPos = m_pBuf;
	m_size = BUF_SIZE;
	return m_pBuf;
}

char* GFolderSerializer::next(size_t* pOutSize)
{
	if(m_pUncompressedBuf) // If we are supposed to compress the output...
	{
		if(m_state == 0)
		{
			memcpy(m_pBuf, "cgfs", 4);
			*pOutSize = 4;
			m_state = 4;
			m_bytesOut += 4;
			return m_pBuf;
		}
		if(m_compressedBufReady) // If there is a compressed buffer queued up...
		{
			// Return the compressed buffer
			*pOutSize = m_compressedSize;
			m_bytesOut += m_compressedSize;
			m_compressedBufReady = false;
			return (char*)m_pCompressedBuf;
		}
		while(true) // Fill up a buffer until we have enough stuff to compress
		{
			size_t size;
			char* pChunk = nextPiece(&size);
			if(pChunk)
			{
				// Add the chunk to the uncompressed buffer
				size_t partLen = std::min(size, COMPRESS_BUF_SIZE - m_uncompressedPos);
				memcpy(m_pUncompressedBuf + m_uncompressedPos, pChunk, partLen);
				m_uncompressedPos += partLen;
				if(m_uncompressedPos >= COMPRESS_BUF_SIZE) // if the uncompressed buffer is full...
				{
					// Compress it, and return the compressed bytes
					delete[] m_pCompressedBuf;
					m_pCompressedBuf = GCompressor::compress((unsigned char*)m_pUncompressedBuf, COMPRESS_BUF_SIZE, &m_compressedSize);
					memcpy(m_pUncompressedBuf, pChunk + partLen, size - partLen);
					m_uncompressedPos = size - partLen;
					m_compressedBufReady = true;
					*pOutSize = sizeof(unsigned int);
					m_bytesOut += sizeof(unsigned int);
					memcpy(m_pBuf, (char*)&m_compressedSize, sizeof(unsigned int));
					return m_pBuf;
				}
			}
			else
			{
				// There is nothing left that needs to go in
				if(m_uncompressedPos > 0)
				{
					// Compress whatever is left
					delete[] m_pCompressedBuf;
					m_pCompressedBuf = GCompressor::compress((unsigned char*)m_pUncompressedBuf, (unsigned int)m_uncompressedPos, &m_compressedSize);
					m_compressedBufReady = true;
					*pOutSize = sizeof(unsigned int);
					m_bytesOut += sizeof(unsigned int);
					m_uncompressedPos = 0;
					memcpy(m_pBuf, (char*)&m_compressedSize, sizeof(unsigned int));
					return m_pBuf;
				}
				else
					return NULL;
			}
		}
	}
	else
	{
		char* pRet = nextPiece(pOutSize);
		m_bytesOut += *pOutSize;
		return pRet;
	}
}

void GFolderSerializer::addName(const char* szName)
{
	unsigned int len = (unsigned int)strlen(szName);
	memcpy(m_pPos, &len, sizeof(unsigned int));
	m_pPos += sizeof(unsigned int);
	m_size -= sizeof(unsigned int);
	memcpy(m_pPos, szName, len);
	m_pPos += len;
	m_size -= len;
}

void GFolderSerializer::startFile(const char* szFilename)
{
	// File indicator
	*m_pPos = 'f';
	m_pPos++;
	m_size--;
	addName(szFilename);

	// The file size
	m_pInStream = new std::ifstream();
	unsigned long long size = 0;
	try
	{
		m_pInStream->exceptions(std::ios::failbit|std::ios::badbit);
		m_pInStream->open(szFilename, std::ios::binary);
		m_pInStream->seekg(0, std::ios::end);
		size = m_pInStream->tellg();
		m_pInStream->seekg(0, std::ios::beg);
	}
	catch(const std::exception e)
	{
		ThrowError("Error opening file: ", szFilename);
	}
	memcpy(m_pPos, &size, sizeof(unsigned long long));
	m_pPos += sizeof(unsigned long long);
	m_size -= sizeof(unsigned long long);
	m_remaining = (size_t)size;

	// The file
	continueFile();
}

void GFolderSerializer::continueFile()
{
	size_t chunk = std::min(m_size, m_remaining);
	try
	{
		m_pInStream->read(m_pPos, chunk);
	}
	catch(const std::exception&)
	{
		ThrowError("Error while reading from file");
	}
	m_remaining -= chunk;
	m_pPos += chunk;
	m_size -= chunk;
	if(m_remaining > 0)
		m_state = 2; // continue reading file
	else
		m_state = 1; // figure out what to do next
}

void GFolderSerializer::startDir(const char* szDirName)
{
	// File indicator
	*m_pPos = 'd';
	m_pPos++;
	m_size--;
	addName(szDirName);

	// Create the GDirList
	if(chdir(szDirName) != 0)
		ThrowError("Failed to change dir to ", szDirName);
	m_dirStack.push(new GDirList());
	m_state = 3; // continue reading dir
}

void GFolderSerializer::continueDir()
{
	GDirList* pDL = m_dirStack.top();
	if(pDL->m_folders.size() > 0)
	{
		startDir(pDL->m_folders.back().c_str());
		pDL->m_folders.pop_back();
	}
	else if(pDL->m_files.size() > 0)
	{
		startFile(pDL->m_files.back().c_str());
		pDL->m_files.pop_back();
	}
	else
	{
		// End of dir indicator
		*m_pPos = 'e';
		m_pPos++;
		m_size--;

		// Move out of the dir
		delete(pDL);
		m_dirStack.pop();
		if(chdir("..") != 0)
			ThrowError("Failed to chdir to ..");
		m_state = 1;
	}
}








GFolderDeserializer::GFolderDeserializer(std::string* pBaseName)
: m_pBaseName(pBaseName)
{
	m_pBQ1 = new GBlobQueue();
	m_pBQ2 = NULL;
	m_compressedBlockSize = 0;
	m_state = 0;
	m_pOutStream = NULL;
	m_depth = 0;
	if(m_pBaseName)
		m_pBaseName->clear();
}

GFolderDeserializer::~GFolderDeserializer()
{
	delete(m_pBQ1);
	delete(m_pBQ2);
	delete(m_pOutStream);
}

void GFolderDeserializer::pump1()
{
	while(m_pBQ1->readyBytes() > 0)
	{
		switch(m_state)
		{
		case 0: // the header
			{
				const char* pChunk = m_pBQ1->dequeue(4); if(!pChunk) return;
				if(memcmp(pChunk, "ugfs", 4) == 0)
					m_state = 1;
				else if(memcmp(pChunk, "cgfs", 4) == 0)
				{
					delete(m_pBQ2);
					m_pBQ2 = new GBlobQueue();
					size_t readyBytes = m_pBQ1->readyBytes();
					m_pBQ2->enqueue(m_pBQ1->dequeue(readyBytes), readyBytes);
					m_state = 1;
					return;
				}
				else
					ThrowError("Unrecognized format");
			}
			break;
		case 1: // the file type
			{
				const char* pChunk = m_pBQ1->dequeue(1); if(!pChunk) return;
				if(*pChunk == 'f')
					m_state = 2;
				else if(*pChunk == 'd')
					m_state = 6;
				else if(*pChunk == 'e')
				{
					if(m_depth-- == 0)
						ThrowError("corruption issue");
					if(chdir("..") != 0)
						ThrowError("Failed to move up one dir");
				}
				else
					ThrowError("Unexpected value");
			}
			break;
		case 2: // filename len
			{
				const char* pChunk = m_pBQ1->dequeue(sizeof(unsigned int)); if(!pChunk) return;
				m_nameLen = *(unsigned int*)pChunk;
				m_state = 3;
			}
			break;
		case 3: // filename
			{
				const char* pChunk = m_pBQ1->dequeue(m_nameLen); if(!pChunk) return;
				string sFilename;
				sFilename.assign(pChunk, m_nameLen);
				if(m_pBaseName && m_pBaseName->length() == 0)
					m_pBaseName->assign(sFilename);
				GAssert(m_pOutStream == NULL);
				if(m_depth == 0 && access(sFilename.c_str(), 0 ) == 0)
					ThrowError("There is already a file or folder named \"", sFilename.c_str(), "\"");
				m_pOutStream = new std::ofstream();
				m_pOutStream->exceptions(std::ios::failbit|std::ios::badbit);
				m_pOutStream->open(sFilename.c_str(), std::ios::binary);
				m_state = 4;
			}
			break;
		case 4: // file size
			{
				const char* pChunk = m_pBQ1->dequeue(sizeof(unsigned long long)); if(!pChunk) return;
				m_fileLen = *(unsigned long long*)pChunk;
				m_state = 5;
			}
			break;
		case 5: // file bytes
			{
				size_t writeAmount = std::min((size_t)m_fileLen, m_pBQ1->readyBytes());
				const char* pChunk = m_pBQ1->dequeue(writeAmount);
				if(!pChunk)
					ThrowError("Expected this call to succeed");
				m_pOutStream->write(pChunk, writeAmount);
				m_fileLen -= writeAmount;
				if(m_fileLen == 0)
				{
					delete(m_pOutStream);
					m_pOutStream = NULL;
					m_state = 1;
				}
				else
					return;
			}
			break;
		case 6: // dirname len
			{
				const char* pChunk = m_pBQ1->dequeue(sizeof(unsigned int)); if(!pChunk) return;
				m_nameLen = *(unsigned int*)pChunk;
				m_state = 7;
			}
			break;
		case 7: // dirname
			{
				const char* pChunk = m_pBQ1->dequeue(m_nameLen); if(!pChunk) return;
				string sDirname;
				sDirname.assign(pChunk, m_nameLen);
				if(m_pBaseName && m_pBaseName->length() == 0)
					m_pBaseName->assign(sDirname);
				if(m_depth == 0 && access(sDirname.c_str(), 0) == 0)
					ThrowError("There is already a file or folder named \"", sDirname.c_str(), "\"");
#ifdef WINDOWS
				bool bOK = (mkdir(sDirname.c_str()) == 0);
#else
				bool bOK = (mkdir(sDirname.c_str(), S_IRWXU | S_IRWXG | S_IROTH | S_IXOTH) == 0); // read/write/search permissions for owner and group, and with read/search permissions for others
#endif
				if(!bOK)
					ThrowError("Failed to create dir");
				if(chdir(sDirname.c_str()) != 0)
					ThrowError("Failed to change dir to ", sDirname.c_str());
				m_depth++;
				m_state = 1;
			}
			break;
		default:
			ThrowError("Unrecognized state");
		}
	}
}

void GFolderDeserializer::pump2()
{
	while(m_pBQ2->readyBytes() > 0)
	{
		if(m_compressedBlockSize == 0)
		{
			const char* pChunk = m_pBQ2->dequeue(sizeof(unsigned int)); if(!pChunk) return;
			unsigned int compressedBlockSize = *(unsigned int*)pChunk;
			m_compressedBlockSize = (size_t)compressedBlockSize;
			if(compressedBlockSize > COMPRESS_BUF_SIZE + 5)
				ThrowError("out of range");
		}
		else
		{
			const char* pChunk = m_pBQ2->dequeue(m_compressedBlockSize); if(!pChunk) return;
			unsigned int uncompressedLen;
			unsigned char* pUncompressed = GCompressor::uncompress((unsigned char*)pChunk, (unsigned int)m_compressedBlockSize, &uncompressedLen);
			ArrayHolder<unsigned char> hUncompressed(pUncompressed);
			m_pBQ1->enqueue((char*)pUncompressed, (size_t)uncompressedLen);
			pump1();
			m_compressedBlockSize = 0;
		}
	}
}

void GFolderDeserializer::doNext(const char* pBuf, size_t bufLen)
{
	if(m_pBQ2)
	{
		m_pBQ2->enqueue(pBuf, bufLen);
		pump2();
	}
	else
	{
		m_pBQ1->enqueue(pBuf, bufLen);
		pump1();
		if(m_pBQ2)
			pump2();
	}
}

} // namespace GClasses
