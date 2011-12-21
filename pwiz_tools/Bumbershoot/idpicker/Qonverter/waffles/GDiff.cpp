/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#include <stddef.h>
#include "GDiff.h"
#include "GHashTable.h"
#include "GHeap.h"

using namespace GClasses;

GDiff::GDiff(const char* szFile1, const char* szFile2)
{
	m_pFile1 = szFile1;
	m_pFile2 = szFile2;
	m_nPos1 = 0;
	m_nPos2 = 0;
	m_nLine1 = 1;
	m_nLine2 = 1;
	m_nNextMatchLen = findNextMatchingLine(&m_nNextMatch1, &m_nNextMatch2);
}

GDiff::~GDiff()
{
}

// static
size_t GDiff::measureLineLength(const char* pLine)
{
	size_t i;
	for(i = 0; pLine[i] != '\0' && pLine[i] != '\n' && pLine[i] != '\r'; i++)
	{
	}
	return i;
}

size_t GDiff::findNextMatchingLine(size_t* pPos1, size_t* pPos2)
{
	size_t pos1 = m_nPos1;
	size_t pos2 = m_nPos2;
	GConstStringToIndexHashTable ht1(53, true);
	GConstStringToIndexHashTable ht2(53, true);
	GHeap heap(2000);
	size_t nMatch;
	size_t len1 = 0;
	size_t len2 = 0;
	const char* szLine1 = NULL;
	const char* szLine2 = NULL;
	while(true)
	{
		// Add the next line to the hash table
		if(m_pFile1[pos1] != '\0')
		{
			len1 = measureLineLength(&m_pFile1[pos1]);
			szLine1 = heap.add(&m_pFile1[pos1], len1);
			ht1.add(szLine1, pos1);
		}
		if(m_pFile2[pos2] != '\0')
		{
			len2 = measureLineLength(&m_pFile2[pos2]);
			szLine2 = heap.add(&m_pFile2[pos2], len2);
			ht2.add(szLine2, pos2);
		}

		// Check for a match
		if(m_pFile1[pos1] != '\0')
		{
			if(ht2.get(szLine1, &nMatch))
			{
				*pPos1 = pos1;
				*pPos2 = nMatch;
				return len1;
			}
			pos1 += len1;
			if(m_pFile1[pos1] == '\r')
				pos1++;
			if(m_pFile1[pos1] == '\n')
				pos1++;
		}
		if(m_pFile2[pos2] != '\0')
		{
			if(ht1.get(szLine2, &nMatch))
			{
				*pPos1 = nMatch;
				*pPos2 = pos2;
				return len2;
			}
			pos2 += len2;
			if(m_pFile2[pos2] == '\r')
				pos2++;
			if(m_pFile2[pos2] == '\n')
				pos2++;
		}

		// Check for the end of the file
		if(m_pFile1[pos1] == '\0' && m_pFile2[pos2] == '\0')
		{
			*pPos1 = pos1;
			*pPos2 = pos2;
			return INVALID_INDEX;
		}
	}
}

bool GDiff::nextLine(struct GDiffLine* pDiffLine)
{
	// Handle unique lines
	if(m_nPos1 < m_nNextMatch1)
	{
		pDiffLine->nLineNumber1 = m_nLine1++;
		pDiffLine->nLineNumber2 = INVALID_INDEX;
		pDiffLine->pLine = &m_pFile1[m_nPos1];
		pDiffLine->nLength = measureLineLength(pDiffLine->pLine);
		m_nPos1 += pDiffLine->nLength;
		if(m_pFile1[m_nPos1] == '\r')
			m_nPos1++;
		if(m_pFile1[m_nPos1] == '\n')
			m_nPos1++;
		return true;
	}
	if(m_nPos2 < m_nNextMatch2)
	{
		pDiffLine->nLineNumber1 = INVALID_INDEX;
		pDiffLine->nLineNumber2 = m_nLine2++;
		pDiffLine->pLine = &m_pFile2[m_nPos2];
		pDiffLine->nLength = measureLineLength(pDiffLine->pLine);
		m_nPos2 += pDiffLine->nLength;
		if(m_pFile2[m_nPos2] == '\r')
			m_nPos2++;
		if(m_pFile2[m_nPos2] == '\n')
			m_nPos2++;
		return true;
	}

	// Handle the end of file
	if(m_pFile1[m_nPos1] == '\0' && m_pFile2[m_nPos2] == '\0')
		return false;

	// Handle matching lines
	pDiffLine->nLineNumber1 = m_nLine1++;
	pDiffLine->nLineNumber2 = m_nLine2++;
	pDiffLine->pLine = &m_pFile1[m_nPos1];
	pDiffLine->nLength = m_nNextMatchLen;
	m_nPos1 += m_nNextMatchLen;
	if(m_pFile1[m_nPos1] == '\r')
		m_nPos1++;
	if(m_pFile1[m_nPos1] == '\n')
		m_nPos1++;
	m_nPos2 += m_nNextMatchLen;
	if(m_pFile2[m_nPos2] == '\r')
		m_nPos2++;
	if(m_pFile2[m_nPos2] == '\n')
		m_nPos2++;
	m_nNextMatchLen = findNextMatchingLine(&m_nNextMatch1, &m_nNextMatch2);
	return true;
}

#ifndef NO_TEST_CODE
// static
void GDiff::test()
{
	const char* pA = "eenie\nmeenie\nmeiny\nmo"; // Linux line endings
	const char* pB = "wham\r\nmeenie\r\nfroopy\r\nmeiny\r\nmo\r\ngwobble\r\n\r\n"; // Windows line endings
	struct GDiffLine dl;
	GDiff differ(pA, pB);

	// eenie
	if(!differ.nextLine(&dl))
		ThrowError("failed");
	if(dl.nLength != 5)
		ThrowError("wrong");
	if(dl.nLineNumber1 != 1 || dl.nLineNumber2 != INVALID_INDEX)
		ThrowError("wrong");

	// wham
	if(!differ.nextLine(&dl))
		ThrowError("failed");
	if(dl.nLength != 4)
		ThrowError("wrong");
	if(dl.nLineNumber1 != INVALID_INDEX || dl.nLineNumber2 != 1)
		ThrowError("wrong");

	// meenie
	if(!differ.nextLine(&dl))
		ThrowError("failed");
	if(dl.nLength != 6)
		ThrowError("wrong");
	if(dl.nLineNumber1 != 2 || dl.nLineNumber2 != 2)
		ThrowError("wrong");

	// froopy
	if(!differ.nextLine(&dl))
		ThrowError("failed");

	// meiny
	if(!differ.nextLine(&dl))
		ThrowError("failed");
	if(dl.nLineNumber1 != 3 || dl.nLineNumber2 != 4)
		ThrowError("wrong");
	if(strncmp(dl.pLine, "meiny", 5) != 0)
		ThrowError("wrong");

	// mo
	if(!differ.nextLine(&dl))
		ThrowError("failed");

	// gwobble
	if(!differ.nextLine(&dl))
		ThrowError("failed");

	// one blank line at the end of the second file
	if(!differ.nextLine(&dl))
		ThrowError("failed");

	// that's all folks
	if(differ.nextLine(&dl))
		ThrowError("That should have been the end");
}
#endif // !NO_TEST_CODE
