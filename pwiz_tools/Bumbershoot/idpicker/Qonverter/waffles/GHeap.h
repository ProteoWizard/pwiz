/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __GHEAP_H__
#define __GHEAP_H__

#include <stddef.h>
#include <string.h>
#include "GError.h"

namespace GClasses {

#define BITS_PER_POINTER (sizeof(void*) * 8)
#define ALIGN_DOWN(p) (((p) / BITS_PER_POINTER) * BITS_PER_POINTER)
#define ALIGN_UP(p) ALIGN_DOWN((p) + BITS_PER_POINTER - 1)

/// Provides a heap in which to put strings or whatever
/// you need to store. If you need to allocate space for
/// a lot of small objects, it's much more efficient to
/// use this class than the C++ heap. Plus, you can
/// delete them all by simply deleting the heap. You can't,
/// however, reuse the space for individual objects in
/// this heap.
class GHeap
{
protected:
	char* m_pCurrentBlock;
	size_t m_nMinBlockSize;
	size_t m_nCurrentPos;

public:
	GHeap(size_t nMinBlockSize)
	{
		m_pCurrentBlock = NULL;
		m_nMinBlockSize = nMinBlockSize;
		m_nCurrentPos = nMinBlockSize;
	}

	virtual ~GHeap();

	/// Allocate space in the heap and copy a string to it.  Returns
	/// a pointer to the string
	char* add(const char* szString)
	{
		return add(szString, (int)strlen(szString));
	}

	/// Allocate space in the heap and copy a string to it.  Returns
	/// a pointer to the string
	char* add(const char* pString, size_t nLength)
	{
		char* pNewString = allocate(nLength + 1);
		memcpy(pNewString, pString, nLength);
		pNewString[nLength] = '\0';
		return pNewString;
	}

	/// Allocate space in the heap and return a pointer to it
	char* allocate(size_t nLength)
	{
		if(m_nCurrentPos + nLength > m_nMinBlockSize)
		{
			char* pNewBlock = new char[sizeof(char*) + std::max(nLength, m_nMinBlockSize)];
			*(char**)pNewBlock = m_pCurrentBlock;
			m_pCurrentBlock = pNewBlock;
			m_nCurrentPos = 0;
		}
		char* pNewBytes = m_pCurrentBlock + sizeof(char*) + m_nCurrentPos;
		m_nCurrentPos += nLength;
		return pNewBytes;
	}

	/// Allocate space in the heap and return a pointer to it
	char* allocAligned(size_t nLength)
	{
		size_t nAlignedCurPos = ALIGN_UP(m_nCurrentPos);
		if(nAlignedCurPos + nLength > m_nMinBlockSize)
		{
			char* pNewBlock = new char[sizeof(char*) + std::max(nLength, m_nMinBlockSize)];
			*(char**)pNewBlock = m_pCurrentBlock;
			m_pCurrentBlock = pNewBlock;
			m_nCurrentPos = 0;
			nAlignedCurPos = 0;
		}
		char* pNewBytes = m_pCurrentBlock + sizeof(char*) + nAlignedCurPos;
		m_nCurrentPos = nAlignedCurPos + nLength;
		return pNewBytes;
	}
};

} // namespace GClasses

#endif // __GHEAP_H__
