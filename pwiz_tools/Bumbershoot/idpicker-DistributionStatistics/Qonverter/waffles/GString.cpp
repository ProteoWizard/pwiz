/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#include "GString.h"
#include "GError.h"
#include <string.h>
#include <stdlib.h>
#include <stdarg.h>

namespace GClasses {

size_t safe_strcpy(char* szDest, const char* szSrc, size_t nMaxSize)
{
	if(nMaxSize == 0)
		return 0;
	nMaxSize--;
	size_t n;
	for(n = 0; szSrc[n] != '\0' && n < nMaxSize; n++)
		szDest[n] = szSrc[n];
	szDest[n] = '\0';
	return n;
}

// ----------------------------------------------------------------------------------

GStringChopper::GStringChopper(const char* szString, size_t nMinLength, size_t nMaxLength, bool bDropLeadingWhitespace)
{
	GAssert(nMinLength > 0 && nMaxLength >= nMinLength); // lengths out of range
	m_bDropLeadingWhitespace = bDropLeadingWhitespace;
	if(nMinLength < 1)
		nMinLength = 1;
	if(nMaxLength < nMinLength)
		nMaxLength = nMinLength;
	m_nMinLen = nMinLength;
	m_nMaxLen = nMaxLength;
	m_szString = szString;
	m_nLen = strlen(szString);
	if(m_nLen > nMaxLength)
		m_pBuf = new char[nMaxLength + 1];
	else
		m_pBuf = NULL;
}

GStringChopper::~GStringChopper()
{
	delete[] m_pBuf;
}

void GStringChopper::reset(const char* szString)
{
	m_szString = szString;
	m_nLen = strlen(szString);
	if(!m_pBuf && m_nLen > m_nMaxLen)
		m_pBuf = new char[m_nMaxLen + 1];
}

const char* GStringChopper::next()
{
	if(m_nLen <= 0)
		return NULL;
	if(m_nLen <= m_nMaxLen)
	{
		m_nLen = 0;
		return m_szString;
	}
	size_t i;
	for(i = m_nMaxLen; i >= m_nMinLen && m_szString[i] > ' '; i--)
	{
	}
	if(i < m_nMinLen)
		i = m_nMaxLen;
	memcpy(m_pBuf, m_szString, i);
	m_pBuf[i] = '\0';
	m_szString += i;
	m_nLen -= i;
	if(m_bDropLeadingWhitespace)
	{
		while(m_nLen > 0 && m_szString[0] <= ' ')
		{
			m_szString++;
			m_nLen--;
		}
	}
	return m_pBuf;
}

} // namespace GClasses

