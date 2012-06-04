/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#include "GPriorityQueue.h"
#include "GError.h"
#include "GHolders.h"
#include "GRand.h"

namespace GClasses {

GPriorityQueue::GPriorityQueue(PointerComparer pCompareFunc, void* pThis)
: m_pCompareFunc(pCompareFunc), m_pThis(pThis)
{
	GPriorityQueueEntry tmp;
	m_entries.push_back(tmp);
}

GPriorityQueue::~GPriorityQueue()
{
}

int GPriorityQueue::size()
{
	return (int)m_entries.size() - 1;
}

void GPriorityQueue::minHeapSwap(int a, int b)
{
	GPriorityQueueEntry* pA = &m_entries[a];
	GPriorityQueueEntry* pB = &m_entries[b];

	// swap the min indexes
	GPriorityQueueEntry* pAOther = &m_entries[pA->m_maxIndex];
	GPriorityQueueEntry* pBOther = &m_entries[pB->m_maxIndex];
	pAOther->m_minIndex = b;
	pBOther->m_minIndex = a;

	// swap the max indexes
	int t = pA->m_maxIndex;
	pA->m_maxIndex = pB->m_maxIndex;
	pB->m_maxIndex = t;

	// swap the payloads
	void* pTmp = pA->m_pPayload;
	pA->m_pPayload = pB->m_pPayload;
	pB->m_pPayload = pTmp;
}

void GPriorityQueue::maxHeapSwap(int a, int b)
{
	GPriorityQueueEntry* pA = &m_entries[a];
	GPriorityQueueEntry* pB = &m_entries[b];

	// swap the max indexes
	GPriorityQueueEntry* pAOther = &m_entries[pA->m_minIndex];
	GPriorityQueueEntry* pBOther = &m_entries[pB->m_minIndex];
	pAOther->m_maxIndex = b;
	pBOther->m_maxIndex = a;

	// swap the in indexes
	int t = pA->m_minIndex;
	pA->m_minIndex = pB->m_minIndex;
	pB->m_minIndex = t;
}

void GPriorityQueue::minHeapBubbleUp(int index)
{
	int parIndex;
	GPriorityQueueEntry* pPar;
	GPriorityQueueEntry* pChild = &m_entries[index];
	while(index > 1)
	{
		parIndex = index / 2;
		pPar = &m_entries[parIndex];
		if(m_pCompareFunc(m_pThis, pChild->m_pPayload, pPar->m_pPayload) >= 0)
			break;
		minHeapSwap(index, parIndex);
		index = parIndex;
		pChild = pPar;
	}
}

void GPriorityQueue::maxHeapBubbleUp(int index)
{
	int parIndex;
	GPriorityQueueEntry* pPar;
	GPriorityQueueEntry* pChild = &m_entries[index];
	void* pChildValue;
	void* pParValue;
	while(index > 1)
	{
		parIndex = index / 2;
		pPar = &m_entries[parIndex];
		pChildValue = m_entries[pChild->m_minIndex].m_pPayload;
		pParValue = m_entries[pPar->m_minIndex].m_pPayload;
		if(m_pCompareFunc(m_pThis, pChildValue, pParValue) <= 0)
			break;
		maxHeapSwap(index, parIndex);
		index = parIndex;
		pChild = pPar;
	}
}

void GPriorityQueue::minHeapBubbleDown(int index)
{
	int last = (int)m_entries.size() - 1;
	int childIndex;
	while(true)
	{
		childIndex = index * 2;
		if(childIndex >= last)
			break;
		if(m_pCompareFunc(m_pThis, m_entries[childIndex].m_pPayload, m_entries[childIndex + 1].m_pPayload) < 0)
		{
			minHeapSwap(index, childIndex);
			index = childIndex;
		}
		else
		{
			minHeapSwap(index, childIndex + 1);
			index = childIndex + 1;
		}
	}
	if(index >= last)
		return;
	minHeapSwap(index, last);
	minHeapBubbleUp(index);
}

void GPriorityQueue::maxHeapBubbleDown(int index)
{
	int last = (int)m_entries.size() - 1;
	int childIndex;
	while(true)
	{
		childIndex = index * 2;
		if(childIndex >= last)
			break;
		if(
			m_pCompareFunc(
					m_pThis,
					m_entries[m_entries[childIndex].m_minIndex].m_pPayload,
					m_entries[m_entries[childIndex + 1].m_minIndex].m_pPayload
				) > 0)
		{
			maxHeapSwap(index, childIndex);
			index = childIndex;
		}
		else
		{
			maxHeapSwap(index, childIndex + 1);
			index = childIndex + 1;
		}
	}
	if(index >= last)
		return;
	maxHeapSwap(index, last);
	maxHeapBubbleUp(index);
}

void GPriorityQueue::insert(void* pObj)
{
	int index = (int)m_entries.size();
	GPriorityQueueEntry entry(index, index, pObj);
	m_entries.push_back(entry);
	minHeapBubbleUp(index);
	maxHeapBubbleUp(index);
}

void* GPriorityQueue::minimum()
{
	return m_entries[1].m_pPayload;
}

void* GPriorityQueue::maximum()
{
	return m_entries[m_entries[1].m_minIndex].m_pPayload;
}

void GPriorityQueue::removeMin()
{
	int maxIndex = m_entries[1].m_maxIndex;
	minHeapBubbleDown(1);
	maxHeapBubbleDown(maxIndex);
	m_entries.pop_back();
}

void GPriorityQueue::removeMax()
{
	int minIndex = m_entries[1].m_minIndex;
	maxHeapBubbleDown(1);
	minHeapBubbleDown(minIndex);
	m_entries.pop_back();
}

#ifndef NO_TEST_CODE
int GPriorityQueueTestComparer(void* pThis, void* pA, void* pB)
{
	int a = *(int*)pA;
	int b = *(int*)pB;
	if(a < b)
		return -1;
	if(a > b)
		return 1;
	return 0;
}

#define TEST_SIZE 4096
void GPriorityQueue::test()
{
	int* buf = new int[TEST_SIZE];
	ArrayHolder<int> hBuf(buf);
	int i, t, r;
	for(i = 0; i < TEST_SIZE; i++)
		buf[i] = i;
	GRand prng(0);
	for(i = TEST_SIZE; i > 2; i--)
	{
		r = (int)prng.next(i);
		t = buf[r];
		buf[r] = buf[i - 1];
		buf[i - 1] = t;
	}
	GPriorityQueue q(GPriorityQueueTestComparer, NULL);
	for(i = 0; i < TEST_SIZE; i++)
		q.insert(buf + i);
	int* pInt;
	for(i = 0; i < (TEST_SIZE / 2); i++)
	{
		pInt = (int*)q.minimum();
		q.removeMin();
		if(*pInt != i)
			ThrowError("wrong answer");
		pInt = (int*)q.maximum();
		q.removeMax();
		if(*pInt != (TEST_SIZE - 1) - i)
			ThrowError("wrong answer");
	}
}
#endif

} // namespace GClasses

