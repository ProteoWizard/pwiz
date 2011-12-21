/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#include <stdio.h>
#include "GHashTable.h"
#include "GError.h"
#include "GHolders.h"
#include <wchar.h>

using namespace GClasses;

GHashTableBase::GHashTableBase(size_t nInitialBucketCount)
{
	m_nBucketCount = 0;
	m_pBuckets = NULL;
	m_nCount = 0;
	m_nModCount = 0;
	_Resize(nInitialBucketCount);
}

GHashTableBase::~GHashTableBase()
{
	delete[] m_pBuckets;
}

inline bool IsObviousNonPrime(size_t n)
{
	if((n % 3) == 0)
		return true;
	if((n % 5) == 0)
		return true;
	if((n % 7) == 0 && n != 7)
		return true;
	if((n % 11) == 0 && n != 11)
		return true;
	if((n % 13) == 0 && n != 13)
		return true;
	if((n % 17) == 0 && n != 17)
		return true;
	return false;
}

void GHashTableBase::_Resize(size_t nNewSize)
{
	// Find a good size
	if(nNewSize < m_nCount * 3)
		nNewSize = m_nCount * 3;
	if(nNewSize < 7)
		nNewSize = 7;
	if((nNewSize & 1) == 0)
		nNewSize++;
	while(IsObviousNonPrime(nNewSize))
		nNewSize += 2;

	// Allocate the new buckets
	struct HashBucket* pOldBuckets = m_pBuckets;
	m_pBuckets = new struct HashBucket[nNewSize];
	size_t nOldCount = m_nBucketCount;
	m_nBucketCount = nNewSize;
	m_nCount = 0;

	// Init the new buckets
	m_pBuckets[0].pPrev = NULL;
	m_pBuckets[0].pNext = &m_pBuckets[1];
	m_pBuckets[0].pKey = NULL;
	size_t n;
	size_t nNewSizeMinusOne = nNewSize - 1;
	for(n = 1; n < nNewSizeMinusOne; n++)
	{
		m_pBuckets[n].pPrev = &m_pBuckets[n - 1];
		m_pBuckets[n].pNext = &m_pBuckets[n + 1];
		m_pBuckets[n].pKey = NULL;
	}
	m_pBuckets[nNewSizeMinusOne].pPrev = &m_pBuckets[nNewSizeMinusOne - 1];
	m_pBuckets[nNewSizeMinusOne].pNext = NULL;
	m_pBuckets[nNewSizeMinusOne].pKey = NULL;
	m_pFirstEmpty = &m_pBuckets[0];

	// Copy the old data
	for(n = 0; n < nOldCount; n++)
	{
		if(pOldBuckets[n].pKey)
			_Add(pOldBuckets[n].pKey, pOldBuckets[n].pValue);
	}

	// delete the old buckets
	delete[] pOldBuckets;
	m_nModCount++;
}

void GHashTableBase::_Add(const char* pKey, const void* pValue)
{
	// Check inputs
	GAssert(pKey);

	// Resize if necessary
	if(m_nCount * 2 > m_nBucketCount)
		_Resize(m_nBucketCount * 2);
	else
		m_nModCount++;

	// Determine which bucket
	size_t nPos = hash(pKey, m_nBucketCount);
	GAssert(nPos < m_nBucketCount); // Out of range

	// Insert it
	m_nCount++;
	if(m_pBuckets[nPos].pKey)
	{
		// The bucket is occupied, so either boot them out or rent a place yourself
		if(m_pBuckets[nPos].pPrev)
		{
			// The bucket is being rented by someone else.  Boot them out to the next available empty spot.
			struct HashBucket* pRenter = m_pFirstEmpty;
			m_pFirstEmpty = pRenter->pNext;
			m_pFirstEmpty->pPrev = NULL;
			*pRenter = m_pBuckets[nPos];
			pRenter->pPrev->pNext = pRenter;
			if(pRenter->pNext)
				pRenter->pNext->pPrev = pRenter;
		
			// Move in
			m_pBuckets[nPos].pPrev = NULL;
			m_pBuckets[nPos].pNext = NULL;
			m_pBuckets[nPos].pKey = pKey;
			m_pBuckets[nPos].pValue = pValue;
		}
		else
		{
			// The bucket is already owned, so just rent the first available empty spot.
			struct HashBucket* pNewBucket = m_pFirstEmpty;
			m_pFirstEmpty = pNewBucket->pNext;
			m_pFirstEmpty->pPrev = NULL;
			pNewBucket->pKey = pKey;
			pNewBucket->pValue = pValue;
			pNewBucket->pNext = m_pBuckets[nPos].pNext;
			pNewBucket->pPrev = &m_pBuckets[nPos];
			m_pBuckets[nPos].pNext = pNewBucket;
			if(pNewBucket->pNext)
				pNewBucket->pNext->pPrev = pNewBucket;
		}
	}
	else
	{
		// The bucket is empty.  Move in.
		if(m_pBuckets[nPos].pPrev)
			m_pBuckets[nPos].pPrev->pNext = m_pBuckets[nPos].pNext;
		else
		{
			GAssert(m_pFirstEmpty == &m_pBuckets[nPos]); // Orphaned empty bucket!  Only m_pFirstEmpty and non-empty buckets should have a NULL value for pPrev.
			m_pFirstEmpty = m_pBuckets[nPos].pNext;
		}
		if(m_pBuckets[nPos].pNext)
			m_pBuckets[nPos].pNext->pPrev = m_pBuckets[nPos].pPrev;
		m_pBuckets[nPos].pPrev = NULL;
		m_pBuckets[nPos].pNext = NULL;
		m_pBuckets[nPos].pKey = pKey;
		m_pBuckets[nPos].pValue = pValue;
	}
	GAssert(m_pFirstEmpty && m_pFirstEmpty->pNext); // Less than two empty slots left!
}


size_t GHashTableBase::_Count(const char* pKey)
{
	GAssert(pKey != NULL);
	size_t nPos = hash(pKey, m_nBucketCount);
	GAssert(nPos < m_nBucketCount); // Out of range
	if(!m_pBuckets[nPos].pKey || m_pBuckets[nPos].pPrev)
		return 0;
	size_t nCount = 0;
	struct HashBucket* pBucket;
	for(pBucket = &m_pBuckets[nPos]; pBucket; pBucket = pBucket->pNext)
	{
		if(areKeysEqual(pBucket->pKey, pKey))
			nCount++;
	}
	return nCount;
}

void GHashTableBase::_Remove(const char* pKey)
{
	GAssert(pKey != NULL);
	size_t nPos = hash(pKey, m_nBucketCount);
	GAssert(nPos < m_nBucketCount); // Out of range
	if(!m_pBuckets[nPos].pKey || m_pBuckets[nPos].pPrev)
		return;
	struct HashBucket* pBucket;
	for(pBucket = &m_pBuckets[nPos]; pBucket; pBucket = pBucket->pNext)
	{
		GAssert(pBucket->pKey); // empty bucket should not be in a chain!
		if(areKeysEqual(pBucket->pKey, pKey))
		{
			if(pBucket->pPrev)
			{
				// It's just a renter, so unlink it and delete the bucket
				GAssert(pBucket != &m_pBuckets[nPos]); // The landlord bucket shouldn't have a prev
				pBucket->pPrev->pNext = pBucket->pNext;
				if(pBucket->pNext)
					pBucket->pNext->pPrev = pBucket->pPrev;
				pBucket->pPrev = NULL;
				pBucket->pNext = m_pFirstEmpty;
				pBucket->pKey = NULL;
				m_pFirstEmpty->pPrev = pBucket;
				m_pFirstEmpty = pBucket;
			}
			else
			{
				// It's a landlord
				GAssert(pBucket == &m_pBuckets[nPos]); // Renters should have a prev
				if(pBucket->pNext)
				{
					// Move the next renter into the landlord bucket
					struct HashBucket* pOldBucket = pBucket->pNext;
					pBucket->pNext = pOldBucket->pNext;
					pBucket->pKey = pOldBucket->pKey;
					pBucket->pValue = pOldBucket->pValue;
					if(pBucket->pNext)
						pBucket->pNext->pPrev = pBucket;

					// Delete the former-renter's old bucket
					pOldBucket->pNext = m_pFirstEmpty;
					pOldBucket->pPrev = NULL;
					pOldBucket->pKey = NULL;
					m_pFirstEmpty->pPrev = pOldBucket;
					m_pFirstEmpty = pOldBucket;
				}
				else
				{
					// Just delete the landlord bucket
					pBucket->pNext = m_pFirstEmpty;
					pBucket->pKey = NULL;
					m_pFirstEmpty->pPrev = pBucket;
					m_pFirstEmpty = pBucket;
				}
			}
			m_nCount--;
			m_nModCount++;
			return;
		}
	}
}

#ifndef NO_TEST_CODE

#define TEST_HASH_TABLE_ELEMENTS 32000

bool VerifyBucketCount(GHashTableBase* pHT)
{
	GHashTableEnumerator hte(pHT);
	void* pValue;
	size_t n = 0;
	while(hte.next(&pValue))
		n++;
	if(n != pHT->size())
		return false;
	return true;
}

// static
void GHashTable::test()
{
	size_t nElements = TEST_HASH_TABLE_ELEMENTS;
	GHashTable ht(13);
	size_t* pNothing = new size_t[nElements];
	ArrayHolder<size_t> hNothing(pNothing);
	size_t n;
	for(n = 0; n < nElements; n++)
		ht.add(&pNothing[n], (const void*)&pNothing[n]);
	for(n = 0; n < nElements; n += 7)
		ht.remove(&pNothing[n]);
	if(!VerifyBucketCount(&ht))
		ThrowError("failed");
	void* pVal = NULL;
	for(n = 0; n < nElements; n++)
	{
		if(n % 7 == 0)
		{
			if(ht.get(&pNothing[n], &pVal))
				ThrowError("failed");
		}
		else
		{
			if(!ht.get(&pNothing[n], &pVal))
				ThrowError("failed");
			if(pVal != &pNothing[n])
				ThrowError("failed");
		}
	}
}
#endif // !NO_TEST_CODE

// ------------------------------------------------------------------------------

const char* GHashTableEnumerator::next(void** ppValue)
{
	GAssert(m_pHashTable->revisionNumber() == m_nModCount); // The HashTable was modified since this enumerator was constructed!
	const void* pValue;
	while(m_nPos < m_pHashTable->m_nBucketCount)
	{
		const char* pKey = m_pHashTable->m_pBuckets[m_nPos].pKey;
		pValue = m_pHashTable->m_pBuckets[m_nPos].pValue;
		m_nPos++;
		if(pKey)
		{
			*ppValue = (void*)pValue;
			return pKey;
		}
	}
	return NULL;
}

void* GHashTableEnumerator::currentValue()
{
	if(m_nPos <= 0)
		return NULL;
	return (void*)m_pHashTable->m_pBuckets[m_nPos - 1].pValue;
}

// ------------------------------------------------------------------------------

bool GConstStringHashTable::get(const char* pKey, size_t nLen, void** ppOutValue)
{
	GTEMPBUF(char, szKey, nLen + 1);
	memcpy(szKey, pKey, nLen);
	szKey[nLen] = '\0';
	return get(szKey, ppOutValue);
}

bool GConstStringToIndexHashTable::get(const char* pKey, size_t nLen, size_t* pValue)
{
	GTEMPBUF(char, szKey, nLen + 1);
	memcpy(szKey, pKey, nLen);
	szKey[nLen] = '\0';
	return get(szKey, pValue);
}

// ------------------------------------------------------------------------------

GNodeHashTable::GNodeHashTable(bool bOwnNodes, size_t nInitialBucketCount)
: GHashTableBase(nInitialBucketCount)
{
	if(bOwnNodes)
		m_pNodes = new std::vector<HashTableNode*>();
	else
		m_pNodes = NULL;
}

// virtual
GNodeHashTable::~GNodeHashTable()
{
	if(m_pNodes)
	{
		size_t nCount = m_pNodes->size();
		for(size_t i = 0; i < nCount; i++)
			delete((*m_pNodes)[i]);
		delete(m_pNodes);
	}
}

void GNodeHashTable::add(HashTableNode* pRec)
{
	if(m_pNodes)
		m_pNodes->push_back(pRec);
	_Add((const char*)pRec, pRec);
}
