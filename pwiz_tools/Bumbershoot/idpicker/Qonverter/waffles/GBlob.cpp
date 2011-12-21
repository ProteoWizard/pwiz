/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#include "GBlob.h"
#include "GBits.h"

using namespace GClasses;
using std::string;

GBlobIncoming::GBlobIncoming()
{
	m_pBuffer = NULL;
	m_nBufferSize = 0;
	m_nBufferPos = 0;
	m_bDeleteBuffer = false;
}

GBlobIncoming::GBlobIncoming(unsigned char* pBuffer, size_t nSize, bool bDeleteBuffer)
{
	m_bDeleteBuffer = false;
	setBlob(pBuffer, nSize, bDeleteBuffer);
}

GBlobIncoming::~GBlobIncoming()
{
	if(m_bDeleteBuffer)
		delete[] m_pBuffer;
}

void GBlobIncoming::setBlob(unsigned char* pBuffer, size_t nSize, bool bDeleteBuffer)
{
	if(m_bDeleteBuffer)
	{
		GAssert(pBuffer != m_pBuffer); // You gave me ownership of the same buffer twice
		delete(m_pBuffer);
	}
	m_pBuffer = pBuffer;
	m_nBufferSize = nSize;
	m_nBufferPos = 0;
	m_bDeleteBuffer = bDeleteBuffer;
}

void GBlobIncoming::get(unsigned char* pData, size_t nSize)
{
	if(m_nBufferSize - m_nBufferPos < nSize)
		ThrowError("GBlobIncoming blob is too small to contain the expected data");
	memcpy(pData, &m_pBuffer[m_nBufferPos], nSize);
	m_nBufferPos += nSize;
}

void GBlobIncoming::get(wchar_t* pwc)
{
	get((unsigned char*)pwc, sizeof(wchar_t));
#ifdef WINDOWS
	GAssert(sizeof(wchar_t) == 2);
	*pwc = GBits::littleEndianToN16((unsigned short)*pwc);
#else
	GAssert(sizeof(wchar_t) == 4);
	*pwc = GBits::littleEndianToN32(*pwc);
#endif
}

void GBlobIncoming::get(char* pc)
{
	get((unsigned char*)pc, sizeof(char));
}

void GBlobIncoming::get(int* pn)
{
	get((unsigned char*)pn, sizeof(int));
	*pn = GBits::littleEndianToN32(*pn);
}

void GBlobIncoming::get(unsigned int* pui)
{
	get((unsigned char*)pui, sizeof(unsigned int));
	*pui = GBits::littleEndianToN32(*pui);
}

void GBlobIncoming::get(unsigned long long* pull)
{
	get((unsigned char*)pull, sizeof(unsigned long long));
	*pull = GBits::littleEndianToN64(*pull);
}

void GBlobIncoming::get(unsigned char* puc)
{
	get(puc, sizeof(unsigned char));
}

void GBlobIncoming::get(float* pf)
{
	get((unsigned char*)pf, sizeof(float));
	*pf = GBits::littleEndianToR32(*pf);
}

void GBlobIncoming::get(double* pd)
{
	get((unsigned char*)pd, sizeof(double));
	*pd = GBits::littleEndianToR64(*pd);
}

void GBlobIncoming::get(string* pString)
{
	unsigned int nLen;
	get(&nLen);
	if(m_nBufferSize - m_nBufferPos < nLen)
		ThrowError("GBlobIncoming blob is too small to contain the expected data");
	pString->assign((const char*)&m_pBuffer[m_nBufferPos], nLen);
	m_nBufferPos += nLen;
}

void GBlobIncoming::peek(size_t nIndex, unsigned char* pData, size_t nSize)
{
	if(nIndex + nSize > m_nBufferSize)
		ThrowError("GBlobIncoming peek out of range");
	memcpy(pData, &m_pBuffer[nIndex], nSize);
}

// ----------------------------------------------------------------------

GBlobOutgoing::GBlobOutgoing(size_t nBufferSize, bool bOkToResizeBuffer)
{
	if(nBufferSize > 0)
		m_pBuffer = new unsigned char[nBufferSize];
	else
		m_pBuffer = NULL;
	m_nBufferSize = nBufferSize;
	m_nBufferPos = 0;
	m_bOkToResizeBuffer = bOkToResizeBuffer;
}

GBlobOutgoing::~GBlobOutgoing()
{
	delete[] m_pBuffer;
}

void GBlobOutgoing::resizeBuffer(size_t nRequiredSize)
{
	if(m_bOkToResizeBuffer)
	{
		size_t nNewSize = std::max((size_t)3 * m_nBufferSize, std::max((size_t)1024, nRequiredSize));
		unsigned char* pNewBuffer = new unsigned char[nNewSize];
		memcpy(pNewBuffer, m_pBuffer, m_nBufferPos);
		delete[] m_pBuffer;
		m_pBuffer = pNewBuffer;
		m_nBufferSize = nNewSize;
	}
	else
		ThrowError("GBlobOutgoing buffer too small to hold blob");
}

void GBlobOutgoing::add(const unsigned char* pData, size_t nSize)
{
	if(m_nBufferSize - m_nBufferPos < nSize)
		resizeBuffer(m_nBufferPos + nSize);
	memcpy(&m_pBuffer[m_nBufferPos], pData, nSize);
	m_nBufferPos += nSize;
}

void GBlobOutgoing::add(const wchar_t wc)
{
#ifdef WINDOWS
	GAssert(sizeof(wchar_t) == 2);
	wchar_t tmp = GBits::n16ToLittleEndian((unsigned short)wc);
	add((const unsigned char*)&tmp, sizeof(wchar_t));
#else
	GAssert(sizeof(wchar_t) == 4);
	wchar_t tmp = GBits::n32ToLittleEndian(wc);
	add((const unsigned char*)&tmp, sizeof(wchar_t));
#endif
}

void GBlobOutgoing::add(const char c)
{
	add((const unsigned char*)&c, sizeof(char));
}

void GBlobOutgoing::add(const int n)
{
	int i = GBits::n32ToLittleEndian(n);
	add((const unsigned char*)&i, sizeof(int));
}

void GBlobOutgoing::add(const unsigned int n)
{
	unsigned int i = GBits::n32ToLittleEndian(n);
	add((const unsigned char*)&i, sizeof(unsigned int));
}

void GBlobOutgoing::add(const unsigned long long n)
{
	unsigned long long i = GBits::n64ToLittleEndian(n);
	add((const unsigned char*)&i, sizeof(unsigned long long));
}

void GBlobOutgoing::add(const unsigned char uc)
{
	add((const unsigned char*)&uc, sizeof(unsigned char));
}

void GBlobOutgoing::add(const float f)
{
	float f2 = GBits::r32ToLittleEndian(f);
	add((const unsigned char*)&f2, sizeof(float));
}

void GBlobOutgoing::add(const double d)
{
	double d2 = GBits::r64ToLittleEndian(d);
	add((const unsigned char*)&d2, sizeof(double));
}

void GBlobOutgoing::add(const char* szString)
{
	size_t nLen = strlen(szString);
	add((unsigned int)nLen);
	add((const unsigned char*)szString, nLen);
}

void GBlobOutgoing::poke(size_t nIndex, const unsigned char* pData, size_t nSize)
{
	if(nIndex + nSize > m_nBufferPos)
		ThrowError("GBlobOutgoing poke out of range");
	memcpy(&m_pBuffer[nIndex], pData, nSize);
}

void GBlobOutgoing::poke(size_t nIndex, const int n)
{
	int tmp = GBits::n32ToLittleEndian(n);
	poke(nIndex, (const unsigned char*)&tmp, sizeof(int));
}








GBlobQueue::GBlobQueue()
: m_pBuffer(NULL), m_bufferAllocation(0), m_bufferBytes(0), m_pPos(NULL), m_bytesRemaining(0)
{
}

GBlobQueue::~GBlobQueue()
{
	delete[] m_pBuffer;
}

void GBlobQueue::reallocBuffer(size_t newSize)
{
	char* pNewBuffer = new char[newSize];
	memcpy(pNewBuffer, m_pBuffer, m_bufferBytes);
	m_bufferAllocation = newSize;
	delete[] m_pBuffer;
	m_pBuffer = pNewBuffer;
}

void GBlobQueue::enqueue(const char* pBlob, size_t len)
{
	if(m_bytesRemaining > 0)
		ThrowError("There are still bytes remainging to be processed. You should call dequeue until it returns NULL");
	m_pPos = pBlob;
	m_bytesRemaining = len;
}

const char* GBlobQueue::dequeue(size_t len)
{
	if(m_bufferBytes > 0)
	{
		if(m_bufferAllocation < len)
			reallocBuffer(len);
		size_t bytesNeeded = len - m_bufferBytes;
		if(m_bytesRemaining >= bytesNeeded)
		{
			memcpy(m_pBuffer + m_bufferBytes, m_pPos, bytesNeeded);
			m_pPos += bytesNeeded;
			m_bytesRemaining -= bytesNeeded;
			m_bufferBytes = 0;
			return m_pBuffer;
		}
		else
		{
			memcpy(m_pBuffer + m_bufferBytes, m_pPos, m_bytesRemaining);
			m_bufferBytes += m_bytesRemaining;
			m_bytesRemaining = 0;
			return NULL;
		}
	}
	else
	{
		if(m_bytesRemaining >= len)
		{
			const char* pChunk = m_pPos;
			m_pPos += len;
			m_bytesRemaining -= len;
			return pChunk;
		}
		else
		{
			if(m_bufferAllocation < len)
				reallocBuffer(len);
			memcpy(m_pBuffer, m_pPos, m_bytesRemaining);
			m_bufferBytes = m_bytesRemaining;
			m_bytesRemaining = 0;
			return NULL;
		}
	}
}

