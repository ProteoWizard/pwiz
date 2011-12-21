/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#include "GNeighborFinder.h"
#include "GVec.h"
#include "GRand.h"
#include "GPlot.h"
#include <stdlib.h>
#include <vector>
#include <queue>
#include <set>
#include "GOptimizer.h"
#include "GHillClimber.h"
#include <string.h>
#include "GGraph.h"
#include "GBitTable.h"
#include <deque>
#include "GDom.h"
#include "GKNN.h"
#include "GTransform.h"
#include <sstream>
#include <string>
#include <iostream>
#include "GNeuralNet.h"
#include "GDistance.h"
#include <cmath>
#include <map>

namespace GClasses {

//using std::cerr;
using std::vector;
using std::priority_queue;
using std::set;
using std::deque;
using std::make_pair;
using std::pair;
using std::string;
using std::multimap;


void GNeighborFinder_InsertionSortNeighbors(size_t neighborCount, size_t* pNeighbors, double* pDistances)
{
	size_t tt;
	double t;
	for(size_t i = 1; i < neighborCount; i++)
	{
		for(size_t j = i; j > 0; j--)
		{
			if(pNeighbors[j] == INVALID_INDEX)
				break;
			if(pNeighbors[j - 1] != INVALID_INDEX && pDistances[j] >= pDistances[j - 1])
				break;

			// Swap
			tt = pNeighbors[j - 1];
			pNeighbors[j - 1] = pNeighbors[j];
			pNeighbors[j] = tt;
			t = pDistances[j - 1];
			pDistances[j - 1] = pDistances[j];
			pDistances[j] = t;
		}
	}
}

void GNeighborFinder::sortNeighbors(size_t neighborCount, size_t* pNeighbors, double* pDistances)
{
	// Use insertion sort if the list is small
	if(neighborCount < 7)
	{
		GNeighborFinder_InsertionSortNeighbors(neighborCount, pNeighbors, pDistances);
		return;
	}
	double t;
	size_t tt;
	size_t beg = 0;
	size_t end = neighborCount - 1;

	// Pick a pivot (using the median of 3 technique)
	double pivA = pDistances[0];
	double pivB = pDistances[neighborCount / 2];
	double pivC = pDistances[neighborCount - 1];
	double pivot;
	if(pivA < pivB)
	{
		if(pivB < pivC)
			pivot = pivB;
		else if(pivA < pivC)
			pivot = pivC;
		else
			pivot = pivA;
	}
	else
	{
		if(pivA < pivC)
			pivot = pivA;
		else if(pivB < pivC)
			pivot = pivC;
		else
			pivot = pivB;
	}

	// Do Quick Sort
	while(true)
	{
		while(beg < end && pNeighbors[beg] != INVALID_INDEX && pDistances[beg] < pivot)
			beg++;
		while(end > beg && (pNeighbors[end] == INVALID_INDEX || pDistances[end] > pivot))
			end--;
		if(beg >= end)
			break;
		t = pDistances[beg];
		pDistances[beg] = pDistances[end];
		pDistances[end] = t;
		tt = pNeighbors[beg];
		pNeighbors[beg] = pNeighbors[end];
		pNeighbors[end] = tt;
		beg++;
		end--;
	}

	// Recurse
	if(pNeighbors[beg] != INVALID_INDEX && pDistances[beg] < pivot)
		beg++;
	else if(beg == 0) // This could happen if they're all -1 (bad neighbors)
	{
		GNeighborFinder_InsertionSortNeighbors(neighborCount, pNeighbors, pDistances);
		return;
	}
	GNeighborFinder::sortNeighbors(beg, pNeighbors, pDistances);
	GNeighborFinder::sortNeighbors(neighborCount - beg, pNeighbors + beg, pDistances + beg);
}

void GNeighborFinder::sortNeighbors(size_t* pNeighbors, double* pDistances)
{
	GNeighborFinder::sortNeighbors(m_neighborCount, pNeighbors, pDistances);
}









GNeighborFinderCacheWrapper::GNeighborFinderCacheWrapper(GNeighborFinder* pNF, bool own)
: GNeighborFinder(pNF->data(), pNF->neighborCount()), m_pNF(pNF), m_own(own)
{
	m_pCache = new size_t[m_pData->rows() * m_neighborCount];
	m_pDissims = new double[m_pData->rows() * m_neighborCount];
	for(size_t i = 0; i < m_pData->rows(); i++)
		m_pCache[i * m_neighborCount] = m_pData->rows();
}

// virtual
GNeighborFinderCacheWrapper::~GNeighborFinderCacheWrapper()
{
	delete[] m_pCache;
	delete[] m_pDissims;
	if(m_own)
		delete(m_pNF);
}

// virtual
void GNeighborFinderCacheWrapper::neighbors(size_t* pOutNeighbors, size_t index)
{
	size_t* pCache = m_pCache + m_neighborCount * index;
	if(*pCache == m_pData->rows())
	{
		double* pDissims = m_pDissims + m_neighborCount * index;
		((GNeighborFinder*)m_pNF)->neighbors(pCache, pDissims, index);
	}
	memcpy(pOutNeighbors, pCache, sizeof(size_t) * m_neighborCount);
}

// virtual
void GNeighborFinderCacheWrapper::neighbors(size_t* pOutNeighbors, double* pOutDistances, size_t index)
{
	size_t* pCache = m_pCache + m_neighborCount * index;
	double* pDissims = m_pDissims + m_neighborCount * index;
	if(*pCache == m_pData->rows())
		((GNeighborFinder*)m_pNF)->neighbors(pCache, pDissims, index);
	memcpy(pOutNeighbors, pCache, sizeof(size_t) * m_neighborCount);
	memcpy(pOutDistances, pDissims, sizeof(double) * m_neighborCount);
}

void GNeighborFinderCacheWrapper::fillCache()
{
	size_t rowCount = m_pData->rows();
	size_t* pCache = m_pCache;
	double* pDissims = m_pDissims;
	for(size_t i = 0; i < rowCount; i++)
	{
		if(*pCache == m_pData->rows())
			((GNeighborFinder*)m_pNF)->neighbors(pCache, pDissims, i);
		pCache += m_neighborCount;
		pDissims += m_neighborCount;
	}
}

void GNeighborFinderCacheWrapper::fillDistances(GDistanceMetric* pMetric)
{
	pMetric->init(m_pData->relation());
	double* pDissim = m_pDissims;
	size_t* pHood = m_pCache;
	for(size_t i = 0; i < m_pData->rows(); i++)
	{
		double* pA = m_pData->row(i);
		for(size_t j = 0; j < m_neighborCount; j++)
		{
			double* pB = m_pData->row(pHood[j]);
			*pDissim = pMetric->squaredDistance(pA, pB);
			pDissim++;
		}
		pHood += m_neighborCount;
	}
}

size_t GNeighborFinderCacheWrapper::cutShortcuts(size_t cycleLen)
{
	GCycleCut cc(m_pCache, m_pData, m_neighborCount);
	cc.setCycleThreshold(cycleLen);
	return cc.cut();
}

void GNeighborFinderCacheWrapper::patchMissingSpots(GRand* pRand)
{
	size_t rowCount = m_pData->rows();
	size_t* pCache = m_pCache;
	double* pDissims = m_pDissims;
	for(size_t i = 0; i < rowCount; i++)
	{
		if(*pCache == m_pData->rows())
			ThrowError("cache not filled out");
		for(size_t j = 0; j < m_neighborCount; j++)
		{
			if(pCache[j] >= m_pData->rows())
			{
				size_t k = (size_t)pRand->next(m_neighborCount);
				size_t l;
				for(l = k; l < m_neighborCount; l++)
				{
					if(pCache[l] < m_pData->rows())
						break;
				}
				if(l >= m_neighborCount)
				{
					for(l = 0; l < k; l++)
					{
						if(pCache[l] < m_pData->rows())
							break;
					}
				}
				if(pCache[l] >= m_pData->rows())
					ThrowError("row has zero valid neighbors");
				if(pDissims)
					pDissims[j] = pDissims[l];
				pCache[j] = pCache[l];
			}
		}
		pCache += m_neighborCount;
		pDissims += m_neighborCount;
	}
}

void GNeighborFinderCacheWrapper::normalizeDistances()
{
	size_t rowCount = m_pData->rows();
	size_t* pCache = m_pCache;
	double* pDissims = m_pDissims;
	double total = 0.0;
	for(size_t i = 0; i < rowCount; i++)
	{
		if(*pCache == m_pData->rows())
			ThrowError("cache not filled out");
		for(size_t j = 0; j < m_neighborCount; j++)
		{
			pDissims[j] = sqrt(pDissims[j]);
			total += pDissims[j];
		}
		pCache += m_neighborCount;
		pDissims += m_neighborCount;
	}
	pDissims = m_pDissims;
	for(size_t i = 0; i < rowCount; i++)
	{
		double s = 0;
		for(size_t j = 0; j < m_neighborCount; j++)
			s += pDissims[j];
		s = 1.0 / s;
		for(size_t j = 0; j < m_neighborCount; j++)
			pDissims[j] *= s;
		pDissims += m_neighborCount;
	}
	total /= rowCount;
	pDissims = m_pDissims;
	for(size_t i = 0; i < rowCount; i++)
	{
		for(size_t j = 0; j < m_neighborCount; j++)
		{
			double d = pDissims[j] * total;
			pDissims[j] = (d * d);
		}
		pDissims += m_neighborCount;
	}
}

bool GNeighborFinderCacheWrapper::isConnected()
{
	// Make a table containing bi-directional neighbor connections
	vector< vector<size_t> > bidirTable;
	bidirTable.resize(m_pData->rows());
	size_t* pHood = m_pCache;
	for(size_t i = 0; i < m_pData->rows(); i++)
		bidirTable[i].reserve(m_neighborCount * 2);
	for(size_t i = 0; i < m_pData->rows(); i++)
	{
		vector<size_t>& row = bidirTable[i];
		for(size_t j = 0; j < (size_t)m_neighborCount; j++)
		{
			if(*pHood < m_pData->rows())
			{
				row.push_back(*pHood);
				bidirTable[*pHood].push_back(i);
			}
			pHood++;
		}
	}

	// Use a breadth-first search to determine of the graph is fully connected
	GBitTable bt(m_pData->rows());
	deque<size_t> q;
	bt.set(0);
	q.push_back(0);
	size_t count = 1;
	while(q.size() > 0)
	{
		size_t n = q.front();
		q.pop_front();
		vector<size_t>& hood = bidirTable[n];
		for(vector<size_t>::iterator it = hood.begin(); it != hood.end(); it++)
		{
			if(!bt.bit(*it))
			{
				bt.set(*it);
				count++;
				if(count >= m_pData->rows())
					return true;
				q.push_back(*it);
			}
		}
	}
	return false;
}

// --------------------------------------------------------------------

/// This helper class keeps neighbors sorted as a binary heap, such that the most dissimilar
/// of the k-current-neighbors is always at the front of the heap.
class GClosestNeighborFindingHelper
{
protected:
	size_t m_found;
	size_t m_neighbors;
	size_t* m_pNeighbors;
	double* m_pDistances;

public:
	GClosestNeighborFindingHelper(size_t neighbors, size_t* pNeighbors, double* pDistances)
	: m_found(0), m_neighbors(neighbors), m_pNeighbors(pNeighbors), m_pDistances(pDistances)
	{
		GAssert(m_neighbors >= 1);
		for(size_t i = 0; i < m_neighbors; i++)
		{
			m_pNeighbors[i] = size_t(-1);
			m_pDistances[i] = 1e308;
		}
	}

	~GClosestNeighborFindingHelper()
	{
	}

	// Adds a point to the set of current neighbors if it is closer than the
	// most dissimilar of the k-current-neighbors
	void TryPoint(size_t index, double distance)
	{
		double* pHeapDist = m_pDistances - 1;
		size_t* pHeapNeigh = m_pNeighbors - 1;
		size_t heapPos;
		if(m_found < m_neighbors)
			heapPos = ++m_found;
		else
		{
			// Compare with the front of the heap, which holds the most dissimilar of the k-current-neighbors
			if(distance >= m_pDistances[0])
				return;

			// Release the most dissimilar of the k-current neighbors
			heapPos = 1;
			while(2 * heapPos <= m_neighbors)
			{
				if(2 * heapPos == m_neighbors || pHeapDist[2 * heapPos] > pHeapDist[2 * heapPos + 1])
				{
					pHeapDist[heapPos] = pHeapDist[2 * heapPos];
					pHeapNeigh[heapPos] = pHeapNeigh[2 * heapPos];
					heapPos = 2 * heapPos;
				}
				else
				{
					pHeapDist[heapPos] = pHeapDist[2 * heapPos + 1];
					pHeapNeigh[heapPos] = pHeapNeigh[2 * heapPos + 1];
					heapPos = 2 * heapPos + 1;
				}
			}
		}

		// Insert into heap
		pHeapDist[heapPos] = distance;
		pHeapNeigh[heapPos] = index;
		while(heapPos > 1 && pHeapDist[heapPos / 2] < pHeapDist[heapPos])
		{
			std::swap(pHeapDist[heapPos / 2], pHeapDist[heapPos]);
			std::swap(pHeapNeigh[heapPos / 2], pHeapNeigh[heapPos]);
			heapPos /= 2;
		}
	}

	double GetWorstDist()
	{
		return m_found >= m_neighbors ? m_pDistances[0] : 1e308;
	}

#ifndef NO_TEST_CODE
#	define TEST_NEIGHBOR_COUNT 33
	static void test()
	{
		size_t neighbors[TEST_NEIGHBOR_COUNT];
		double distances[TEST_NEIGHBOR_COUNT];
		GMatrix values1(0, 1);
		GMatrix values2(0, 1);
		GClosestNeighborFindingHelper ob(TEST_NEIGHBOR_COUNT, neighbors, distances);
		GRand prng(0);
		for(size_t i = 0; i < 300; i++)
		{
			double d = prng.uniform();
			ob.TryPoint(i, d);
			values1.newRow()[0] = d;
			values1.sort(0);
			values2.flush();
			for(size_t j = 0; j < std::min((size_t)TEST_NEIGHBOR_COUNT, values1.rows()); j++)
				values2.newRow()[0] = distances[j];
			values2.sort(0);
			for(size_t j = 0; j < std::min((size_t)TEST_NEIGHBOR_COUNT, values1.rows()); j++)
			{
				if(std::abs(values1[j][0] - values2[j][0]) > 1e-12)
					ThrowError("something is wrong");
			}
		}
	}
#endif
};

// --------------------------------------------------------------------------------

GNeighborFinderGeneralizing::GNeighborFinderGeneralizing(GMatrix* pData, size_t neighborCount, GDistanceMetric* pMetric, bool ownMetric)
: GNeighborFinder(pData, neighborCount), m_pMetric(pMetric), m_ownMetric(ownMetric)
{
	if(!m_pMetric)
	{
		m_pMetric = new GRowDistance();
		m_ownMetric = true;
	}
	m_pMetric->init(pData->relation());
}

// virtual
GNeighborFinderGeneralizing::~GNeighborFinderGeneralizing()
{
	if(m_ownMetric)
		delete(m_pMetric);
}

// --------------------------------------------------------------------------------

GBruteForceNeighborFinder::GBruteForceNeighborFinder(GMatrix* pData, size_t neighborCount, GDistanceMetric* pMetric, bool ownMetric)
: GNeighborFinderGeneralizing(pData, neighborCount, pMetric, ownMetric)
{
}

GBruteForceNeighborFinder::~GBruteForceNeighborFinder()
{
}

size_t GBruteForceNeighborFinder::addCopy(const double* pVector)
{
	size_t index = m_pData->rows();
	GVec::copy(m_pData->newRow(), pVector, m_pData->cols());
	return index;
}

double* GBruteForceNeighborFinder::releaseVector(size_t nIndex)
{
	return m_pData->releaseRow(nIndex);
}

// virtual
void GBruteForceNeighborFinder::reoptimize()
{
}

// virtual
void GBruteForceNeighborFinder::neighbors(size_t* pOutNeighbors, size_t index)
{
	GTEMPBUF(double, distances, m_neighborCount);
	neighbors(pOutNeighbors, distances, index);
}

// virtual
void GBruteForceNeighborFinder::neighbors(size_t* pOutNeighbors, double* pOutDistances, size_t index)
{
	GClosestNeighborFindingHelper helper(m_neighborCount, pOutNeighbors, pOutDistances);
	double* pCand;
	double dist;
	double* pInputVector = m_pData->row(index);
	for(size_t i = 0; i < m_pData->rows(); i++)
	{
		if(i == index)
			continue;
		pCand = m_pData->row(i);
		dist = m_pMetric->squaredDistance(pInputVector, pCand);
		helper.TryPoint(i, dist);
	}
}

// virtual
void GBruteForceNeighborFinder::neighbors(size_t* pOutNeighbors, double* pOutDistances, const double* pInputVector)
{
	GClosestNeighborFindingHelper helper(m_neighborCount, pOutNeighbors, pOutDistances);
	double* pCand;
	double dist;
	for(size_t i = 0; i < m_pData->rows(); i++)
	{
		pCand = m_pData->row(i);
		dist = m_pMetric->squaredDistance(pInputVector, pCand);
		helper.TryPoint(i, dist);
	}
}

// --------------------------------------------------------------------------------

class GKdNode
{
protected:
	double m_minDist;
	double* m_pOffset;
	size_t m_dims;

public:
	GKdNode(size_t dims)
	{
		m_dims = dims;
		m_pOffset = new double[dims];
		GVec::setAll(m_pOffset, 0.0, dims);
		m_minDist = 0;
	}

	virtual ~GKdNode()
	{
		delete[] m_pOffset;
	}

	virtual bool IsLeaf() = 0;

	// Builds an array of all the indexes in all of the leaf nodes that descend from me
	virtual size_t Gather(size_t* pOutIndexes) = 0;

	virtual void Insert(GKdTree* pTree, size_t index, double* pRow) = 0;

	virtual void Remove(GKdTree* pTree, size_t index, double* pRow) = 0;

	virtual void Rename(GKdTree* pTree, size_t oldIndex, size_t newIndex, double* pRow) = 0;

	double GetMinDist()
	{
		return m_minDist;
	}

	size_t GetDims()
	{
		return m_dims;
	}

	void CopyOffset(GKdNode* pParent)
	{
		GVec::copy(m_pOffset, pParent->m_pOffset, m_dims);
		m_minDist = pParent->m_minDist;
	}

	void AdjustOffset(size_t attr, double offset, const double* m_pScaleFactors)
	{
		if(offset > m_pOffset[attr])
		{
			if(m_pScaleFactors)
			{
				m_minDist -= (m_pOffset[attr] * m_pOffset[attr] * m_pScaleFactors[attr] * m_pScaleFactors[attr]);
				m_pOffset[attr] = offset;
				m_minDist += (m_pOffset[attr] * m_pOffset[attr] * m_pScaleFactors[attr] * m_pScaleFactors[attr]);
			}
			else
			{
				m_minDist -= (m_pOffset[attr] * m_pOffset[attr]);
				m_pOffset[attr] = offset;
				m_minDist += (m_pOffset[attr] * m_pOffset[attr]);
			}
		}
	}
};


class GKdInteriorNode : public GKdNode
{
protected:
	GKdNode* m_pLess;
	GKdNode* m_pGreaterOrEqual;
	size_t m_size;
	size_t m_attr;
	double m_pivot;

public:
	size_t m_timeLeft;

	GKdInteriorNode(size_t dims, GKdNode* pLess, GKdNode* pGreaterOrEqual, size_t size, size_t attr, double pivot)
	 : GKdNode(dims), m_pLess(pLess), m_pGreaterOrEqual(pGreaterOrEqual), m_size(size), m_attr(attr), m_pivot(pivot)
	{
		m_timeLeft = (size_t)std::min((double)0x7fffffff, ((double)size * size) / 36 + 6);
	}

	virtual ~GKdInteriorNode()
	{
		delete(m_pLess);
		delete(m_pGreaterOrEqual);
	}

	virtual bool IsLeaf() { return false; }

	GKdNode* Rebuild(GKdTree* pTree)
	{
		size_t* pIndexes = new size_t[m_size];
		ArrayHolder<size_t> hIndexes(pIndexes);
		size_t used = Gather(pIndexes);
		GAssert(used == m_size); // m_size is wrong. This may corrupt memory.
		return pTree->buildTree(used, pIndexes);
	}

	virtual void Insert(GKdTree* pTree, size_t index, double* pRow)
	{
		m_timeLeft--;
		if(pTree->isGreaterOrEqual(pRow, m_attr, m_pivot))
		{
			m_pGreaterOrEqual->Insert(pTree, index, pRow);
			m_size++;
			if(!m_pGreaterOrEqual->IsLeaf() && ((GKdInteriorNode*)m_pGreaterOrEqual)->m_timeLeft == 0 && m_timeLeft >= m_size / 4)
			{
				GKdNode* pNewNode = ((GKdInteriorNode*)m_pGreaterOrEqual)->Rebuild(pTree);
				delete(m_pGreaterOrEqual);
				m_pGreaterOrEqual = pNewNode;
			}
		}
		else
		{
			m_pLess->Insert(pTree, index, pRow);
			m_size++;
			if(!m_pLess->IsLeaf() && ((GKdInteriorNode*)m_pLess)->m_timeLeft == 0 && m_timeLeft >= m_size / 4)
			{
				GKdNode* pNewNode = ((GKdInteriorNode*)m_pLess)->Rebuild(pTree);
				delete(m_pLess);
				m_pLess = pNewNode;
			}
		}
	}

	virtual void Remove(GKdTree* pTree, size_t index, double* pRow)
	{
		m_timeLeft--;
		if(pTree->isGreaterOrEqual(pRow, m_attr, m_pivot))
			m_pGreaterOrEqual->Remove(pTree, index, pRow);
		else
			m_pLess->Remove(pTree, index, pRow);
		m_size--;
	}

	virtual void Rename(GKdTree* pTree, size_t oldIndex, size_t newIndex, double* pRow)
	{
		if(pTree->isGreaterOrEqual(pRow, m_attr, m_pivot))
			m_pGreaterOrEqual->Rename(pTree, oldIndex, newIndex, pRow);
		else
			m_pLess->Rename(pTree, oldIndex, newIndex, pRow);
	}

	GKdNode* GetLess() { return m_pLess; }
	GKdNode* GetGreaterOrEqual() { return m_pGreaterOrEqual; }
	size_t GetSize() { return m_size; }

	void GetDivision(size_t* pAttr, double* pPivot)
	{
		*pAttr = m_attr;
		*pPivot = m_pivot;
	}

	size_t Gather(size_t* pOutIndexes)
	{
		size_t n = m_pLess->Gather(pOutIndexes);
		return m_pGreaterOrEqual->Gather(pOutIndexes + n) + n;
	}
};


class GKdLeafNode : public GKdNode
{
protected:
	vector<size_t> m_indexes;

public:
	GKdLeafNode(size_t count, size_t* pIndexes, size_t dims, size_t maxLeafSize)
	 : GKdNode(dims)
	{
		m_indexes.reserve(std::max(count, maxLeafSize));
		for(size_t i = 0; i < count; i++)
			m_indexes.push_back(pIndexes[i]);
	}

	virtual ~GKdLeafNode()
	{
	}

	virtual bool IsLeaf() { return true; }

	size_t GetSize()
	{
		return m_indexes.size();
	}

	virtual void Insert(GKdTree* pTree, size_t index, double* pRow)
	{
		m_indexes.push_back(index);
	}

	virtual void Remove(GKdTree* pTree, size_t index, double* pRow)
	{
		size_t count = m_indexes.size();
		for(size_t i = 0; i < count; i++)
		{
			if(m_indexes[i] == index)
			{
				m_indexes[i] = m_indexes[count - 1];
				m_indexes.pop_back();
				return;
			}
		}
		GAssert(false); // failed to find index. Did the row change?
	}

	virtual void Rename(GKdTree* pTree, size_t oldIndex, size_t newIndex, double* pRow)
	{
		size_t count = m_indexes.size();
		for(size_t i = 0; i < count; i++)
		{
			if(m_indexes[i] == oldIndex)
			{
				m_indexes[i] = newIndex;
				return;
			}
		}
		GAssert(false); // failed to find index. Did the row change?
	}

	vector<size_t>* GetIndexes() { return &m_indexes; }

	size_t Gather(size_t* pOutIndexes)
	{
		for(vector<size_t>::iterator i = m_indexes.begin(); i < m_indexes.end(); i++)
		{
			*pOutIndexes = *i;
			pOutIndexes++;
		}
		return m_indexes.size();
	}
};


// --------------------------------------------------------------------------------------------------------

GKdTree::GKdTree(GMatrix* pData, size_t neighborCount, GDistanceMetric* pMetric, bool ownMetric)
: GNeighborFinderGeneralizing(pData, neighborCount, pMetric, ownMetric)
{
	m_maxLeafSize = 6;
	size_t count = pData->rows();
	GTEMPBUF(size_t, tmp, count);
	for(size_t i = 0; i < count; i++)
		tmp[i] = i;
	m_pRoot = buildTree(count, tmp);
}

// virtual
GKdTree::~GKdTree()
{
	delete(m_pRoot);
}

void GKdTree::computePivotAndGoodness(size_t count, size_t* pIndexes, size_t attr, double* pOutPivot, double* pOutGoodness)
{
	size_t valueCount = m_pMetric->relation()->valueCount(attr);
	if(valueCount > 0)
	{
		// Count the ocurrences of each value
		double* pPat;
		GTEMPBUF(size_t, counts, valueCount);
		memset(counts, '\0', sizeof(size_t) * valueCount);
		for(size_t i = 0; i < count; i++)
		{
			pPat = m_pData->row(pIndexes[i]);
			if((int)pPat[attr] >= 0)
			{
				GAssert((unsigned int)pPat[attr] < (unsigned int)valueCount); // out of range
				if((unsigned int)pPat[attr] < (unsigned int)valueCount)
					counts[(int)pPat[attr]]++;
			}
		}

		// Total up the entropy
		size_t max = 0;
		size_t maxcount = 0;
		double entropy = 0;
		double ratio;
		for(size_t i = 0; i < valueCount; i++)
		{
			if(counts[i] > maxcount || i == 0)
			{
				maxcount = counts[i];
				max = i;
			}
			if(counts[i] > 0)
			{
				ratio = (double)counts[i] / count;
				entropy -= ratio * log(ratio);
			}
		}
		const double* pScaleFactors = m_pMetric->scaleFactors();
		if(pScaleFactors)
			entropy *= (pScaleFactors[attr] * pScaleFactors[attr]);

		*pOutPivot = (double)max;
		*pOutGoodness = entropy;
	}
	else
	{
		// Compute the mean
		size_t missing = 0;
		double mean = 0;
		double* pPat;
		for(size_t i = 0; i < count; i++)
		{
			GAssert(pIndexes[i] < m_pData->rows());
			pPat = m_pData->row(pIndexes[i]);
			if(pPat[attr] != UNKNOWN_REAL_VALUE)
				mean += pPat[attr];
			else
				missing++;
		}
		mean /= (count - missing);

		// Compute the scaled variance
		double var = 0;
		double d;
		const double* pScaleFactors = m_pMetric->scaleFactors();
		if(pScaleFactors)
		{
			for(size_t i = 0; i < count; i++)
			{
				pPat = m_pData->row(pIndexes[i]);
				if(pPat[attr] != UNKNOWN_REAL_VALUE)
				{
					d = (pPat[attr] - mean) * pScaleFactors[attr];
					var += (d * d);
				}
			}
		}
		else
		{
			for(size_t i = 0; i < count; i++)
			{
				pPat = m_pData->row(pIndexes[i]);
				if(pPat[attr] != UNKNOWN_REAL_VALUE)
				{
					d = (pPat[attr] - mean);
					var += (d * d);
				}
			}
		}
		var /= (count - missing); // (the biased estimator of variance is better for this purpose)

		*pOutPivot = mean;
		*pOutGoodness = var;
	}
}

size_t GKdTree::splitIndexes(size_t count, size_t* pIndexes, size_t attr, double pivot)
{
	double* pPat;
	size_t t;
	size_t beg = 0;
	size_t end = count - 1;
	if(m_pMetric->relation()->valueCount(attr) == 0)
	{
		while(end >= beg && end < count)
		{
			pPat = m_pData->row(pIndexes[beg]);
			if(pPat[attr] >= pivot)
			{
				t = pIndexes[beg];
				pIndexes[beg] = pIndexes[end];
				pIndexes[end] = t;
				end--;
			}
			else
				beg++;
		}
	}
	else
	{
		while(end >= beg && end < count)
		{
			pPat = m_pData->row(pIndexes[beg]);
			if((int)pPat[attr] == (int)pivot)
			{
				t = pIndexes[beg];
				pIndexes[beg] = pIndexes[end];
				pIndexes[end] = t;
				end--;
			}
			else
				beg++;
		}
	}
	return beg;
}

// static
bool GKdTree::isGreaterOrEqual(const double* pPat, size_t attr, double pivot)
{
	if(m_pMetric->relation()->valueCount(attr) == 0)
		return (pPat[attr] >= pivot);
	else
		return ((int)pPat[attr] == (int)pivot);
}

GKdNode* GKdTree::buildTree(size_t count, size_t* pIndexes)
{
	size_t dims = m_pMetric->relation()->size();
	if(count <= (size_t)m_maxLeafSize)
		return new GKdLeafNode(count, pIndexes, dims, m_maxLeafSize);

	// Find a good place to split
	double pivot, goodness, p, g;
	size_t attr = 0;
	computePivotAndGoodness(count, pIndexes, 0, &pivot, &goodness);
	for(size_t i = 1; i < dims; i++)
	{
		computePivotAndGoodness(count, pIndexes, i, &p, &g);
		if(g > goodness)
		{
			pivot = p;
			goodness = g;
			attr = i;
		}
	}

	// Split the data
	size_t lessCount = splitIndexes(count, pIndexes, attr, pivot);
	size_t greaterOrEqualCount = count - lessCount;
	if(lessCount == 0 || greaterOrEqualCount == 0)
		return new GKdLeafNode(count, pIndexes, dims, m_maxLeafSize);

	// Make an interior node
	GKdNode* pLess = buildTree(lessCount, pIndexes);
	GKdNode* greaterOrEqual = buildTree(greaterOrEqualCount, pIndexes + lessCount);
	return new GKdInteriorNode(dims, pLess, greaterOrEqual, count, attr, pivot);
}

// virtual
size_t GKdTree::addCopy(const double* pVector)
{
	size_t index = m_pData->rows();
	double* pVec = m_pData->newRow();
	GVec::copy(pVec, pVector, m_pData->cols());
	m_pRoot->Insert(this, index, pVec);
	if(m_pRoot->IsLeaf())
	{
		if(((GKdLeafNode*)m_pRoot)->GetSize() > m_maxLeafSize)
		{
			size_t* pIndexes = new size_t[((GKdLeafNode*)m_pRoot)->GetSize()];
			ArrayHolder<size_t> hIndexes(pIndexes);
			size_t used = m_pRoot->Gather(pIndexes);
			GAssert(used == ((GKdLeafNode*)m_pRoot)->GetSize()); // m_size is wrong. This may corrupt memory.
			GKdNode* pNewNode = buildTree(used, pIndexes);
			delete(m_pRoot);
			m_pRoot = pNewNode;
		}
	}
	else
	{
		if(((GKdInteriorNode*)m_pRoot)->m_timeLeft <= 0)
		{
			GKdNode* pNewNode = ((GKdInteriorNode*)m_pRoot)->Rebuild(this);
			delete(m_pRoot);
			m_pRoot = pNewNode;
		}
	}
	return index;
}

// virtual
double* GKdTree::releaseVector(size_t index)
{
	double* pPat = m_pData->row(index);
	m_pRoot->Remove(this, index, pPat);
	size_t last = m_pData->rows() - 1;
	if(index != last)
	{
		double* pPatLast = m_pData->row(last);
		m_pRoot->Rename(this, last, index, pPatLast);
	}
	return m_pData->releaseRow(index); // (releaseRow moves the last row to the index position)
}

class KdTree_Compare_Nodes_Functor
{
public:
	bool operator() (GKdNode* pA, GKdNode* pB) const
	{
		double a = pA->GetMinDist();
		double b = pB->GetMinDist();
		return (a > b);
	}
};

void GKdTree::findNeighbors(size_t* pOutNeighbors, double* pOutSquaredDistances, const double* pInputVector, size_t nExclude)
{
	GClosestNeighborFindingHelper helper(m_neighborCount, pOutNeighbors, pOutSquaredDistances);
	KdTree_Compare_Nodes_Functor comparator;
	priority_queue< GKdNode*, vector<GKdNode*>, KdTree_Compare_Nodes_Functor > q(comparator);
	q.push(m_pRoot);
	while(q.size() > 0)
	{
		GKdNode* pNode = q.top();
		q.pop();
		if(pNode->GetMinDist() >= helper.GetWorstDist())
			break;
		if(pNode->IsLeaf())
		{
			double squaredDist;
			double* pCand;
			vector<size_t>* pIndexes = ((GKdLeafNode*)pNode)->GetIndexes();
			size_t count = pIndexes->size();
			for(size_t i = 0; i < count; i++)
			{
				size_t index = (*pIndexes)[i];
				if(index == nExclude)
					continue;
				pCand = m_pData->row(index);
				squaredDist = m_pMetric->squaredDistance(pInputVector, pCand);
				helper.TryPoint(index, squaredDist);
			}
		}
		else
		{
			size_t attr;
			double pivot;
			GKdInteriorNode* pParent = (GKdInteriorNode*)pNode;
			pParent->GetDivision(&attr, &pivot);
			GKdNode* pLess = pParent->GetLess();
			pLess->CopyOffset(pParent);
			GKdNode* pGreaterOrEqual = pParent->GetGreaterOrEqual();
			pGreaterOrEqual->CopyOffset(pParent);
			if(isGreaterOrEqual(pInputVector, attr, pivot))
				pLess->AdjustOffset(attr, pInputVector[attr] - pivot, m_pMetric->scaleFactors());
			else
				pGreaterOrEqual->AdjustOffset(attr, pivot - pInputVector[attr], m_pMetric->scaleFactors());
			q.push(pLess);
			q.push(pGreaterOrEqual);
		}
	}
}

// virtual
void GKdTree::neighbors(size_t* pOutNeighbors, size_t index)
{
	GTEMPBUF(double, distances, m_neighborCount);
	neighbors(pOutNeighbors, distances, index);
}

// virtual
void GKdTree::neighbors(size_t* pOutNeighbors, double* pOutDistances, size_t index)
{
	findNeighbors(pOutNeighbors, pOutDistances, m_pData->row(index), index);
}

// virtual
void GKdTree::neighbors(size_t* pOutNeighbors, double* pOutDistances, const double* pInputVector)
{
	findNeighbors(pOutNeighbors, pOutDistances, pInputVector, INVALID_INDEX);
}

// virtual
void GKdTree::reoptimize()
{
	if(!m_pRoot->IsLeaf())
	{
		GKdNode* pNewNode = ((GKdInteriorNode*)m_pRoot)->Rebuild(this);
		delete(m_pRoot);
		m_pRoot = pNewNode;
	}
}

// static
double GKdTree::medianDistanceToNeighbor(GMatrix& data, size_t n)
{
	if(n < 1)
		return 0.0; // 0 is the point itself

	// Fill a vector with the distances to the n^th neighbor of each point
	GKdTree kdtree(&data, n, NULL, false);
	vector<double> vals;
	vals.reserve(data.rows());
	GTEMPBUF(double, distances, n);
	GTEMPBUF(size_t, indexes, n);
	for(size_t i = 0; i < data.rows(); i++)
	{
		kdtree.neighbors(indexes, distances, i);
		GNeighborFinder::sortNeighbors(n, indexes, distances);
		if(indexes[n - 1] < data.rows())
			vals.push_back(sqrt(distances[n - 1]));
	}

	// Find the median value
	if(vals.size() < 1)
		ThrowError("at least one value is required to compute a median");
	if(vals.size() & 1)
	{
		vector<double>::iterator med = vals.begin() + (vals.size() / 2);
		std::nth_element(vals.begin(), med, vals.end());
		return *med;
	}
	else
	{
		vector<double>::iterator a = vals.begin() + (vals.size() / 2 - 1);
		std::nth_element(vals.begin(), a, vals.end());
		vector<double>::iterator b = std::min_element(a + 1, vals.end());
		return 0.5 * (*a + *b);
	}
}

#ifndef NO_TEST_CODE
#	include "GImage.h"
#	include "GHeap.h"

void MeasureBounds(GMatrix* pData, GKdNode* pNode, size_t attr, double* pMin, double* pMax)
{
	if(pNode->IsLeaf())
	{
		double min = 1e200;
		double max = -1e200;
		vector<size_t>* pIndexes = ((GKdLeafNode*)pNode)->GetIndexes();
		double* pPat;
		for(size_t i = 0; i < pIndexes->size(); i++)
		{
			pPat = pData->row((*pIndexes)[i]);
			min = std::min(pPat[attr], min);
			max = std::max(pPat[attr], max);
		}
		*pMin = min;
		*pMax = max;
	}
	else
	{
		double min1, min2, max1, max2;
		GKdNode* pChild = ((GKdInteriorNode*)pNode)->GetLess();
		MeasureBounds(pData, pChild, attr, &min1, &max1);
		pChild = ((GKdInteriorNode*)pNode)->GetGreaterOrEqual();
		MeasureBounds(pData, pChild, attr, &min2, &max2);
		*pMin = std::min(min1, min2);
		*pMax = std::max(max1, max2);
	}
}

void DrawKdNode(GPlotWindow* pw, GKdNode* pNode, GMatrix* pData)
{
	if(pNode->IsLeaf())
	{
		vector<size_t>* pIndexes = ((GKdLeafNode*)pNode)->GetIndexes();
		double* pPat;
		for(size_t i = 0; i < pIndexes->size(); i++)
		{
			pPat = pData->row((*pIndexes)[i]);
			pw->dot(pPat[0], pPat[1], 5, 0xff00ff00, 0xff000000);
			std::ostringstream os;
			os << (*pIndexes)[i];
			string tmp = os.str();
			pw->label(pPat[0], pPat[1], tmp.c_str(), 1.0f, 0xffffffff);
		}
	}
	else
	{
		size_t attr;
		double pivot, min, max;
		((GKdInteriorNode*)pNode)->GetDivision(&attr, &pivot);
		if(attr == 0)
		{
			MeasureBounds(pData, pNode, 1, &min, &max);
			pw->line(pivot, min, pivot, max, 0xffff0000);
		}
		else
		{
			GAssert(attr == 1); // unsupported value
			MeasureBounds(pData, pNode, 0, &min, &max);
			pw->line(min, pivot, max, pivot, 0xffff0000);
		}
		GKdNode* pChild = ((GKdInteriorNode*)pNode)->GetLess();
		DrawKdNode(pw, pChild, pData);
		pChild = ((GKdInteriorNode*)pNode)->GetGreaterOrEqual();
		DrawKdNode(pw, pChild, pData);
	}
}

class GDontGoFarMetric : public GDistanceMetric
{
public:
	double m_squaredMaxDist;

	GDontGoFarMetric(double maxDist)
	: GDistanceMetric(), m_squaredMaxDist(maxDist * maxDist)
	{
	}

	virtual ~GDontGoFarMetric()
	{
	}

	virtual GDomNode* serialize(GDom* pDoc)
	{
		ThrowError("not implemented");
		return NULL;
	}

	virtual void init(sp_relation& pRelation)
	{
		m_pRelation = pRelation;
	}

	virtual double squaredDistance(const double* pA, const double* pB) const
	{
		double squaredDist = GVec::squaredDistance(pA, pB, m_pRelation->size());
		if(squaredDist > m_squaredMaxDist)
			ThrowError("a kd-tree shouldn't have to look this far away");
		return squaredDist;
	}
};

void GKdTree_testThatItDoesntLookFar()
{
	GRand prng(0);
	GMatrix tmp(100000, 2);
	for(size_t i = 0; i < tmp.rows(); i++)
	{
		double* pRow = tmp[i];
		pRow[0] = prng.uniform();
		pRow[1] = prng.uniform();
	}
	GDontGoFarMetric metric(0.05);
	GKdTree kdTree(&tmp, 5, &metric, false);
	double row[2];
	size_t neighs[5];
	double dists[5];
	for(size_t i = 0; i < 100; i++)
	{
		row[0] = prng.uniform();
		row[1] = prng.uniform();
		kdTree.neighbors(neighs, dists, row);
	}
}

#	define TEST_DIMS 4
#	define TEST_PATTERNS 1000
#	define TEST_NEIGHBORS 24
// static
void GKdTree::test()
{
	GClosestNeighborFindingHelper::test();
	GKdTree_testThatItDoesntLookFar();

	sp_relation rel;
	rel = new GUniformRelation(TEST_DIMS, 0);
	GHeap heap(2048);
	GMatrix data(rel, &heap);
	GRand prng(0);
	for(size_t i = 0; i < TEST_PATTERNS; i++)
	{
		double* pPat = data.newRow();
		prng.spherical(pPat, TEST_DIMS);
	}
	GBruteForceNeighborFinder bf(&data, TEST_NEIGHBORS, NULL, true);
	GKdTree kd(&data, TEST_NEIGHBORS, NULL, true);
/*
	GAssert(TEST_DIMS == 2); // You must change TEST_DIMS to 2 if you're going to plot the tree
	GImage image;
	image.SetSize(1000, 1000);
	image.Clear(0xff000000);
	GPlotWindow pw(&image, -1.1, -1.1, 1.1, 1.1);
	DrawKdNode(&pw, kd.GetRoot(), &data);
	image.SavePNGFile("kdtree.png");
*/
	size_t bfNeighbors[TEST_NEIGHBORS];
	size_t kdNeighbors[TEST_NEIGHBORS];
	double bfDistances[TEST_NEIGHBORS];
	double kdDistances[TEST_NEIGHBORS];
	for(size_t i = 0; i < TEST_PATTERNS; i++)
	{
		bf.neighbors(bfNeighbors, bfDistances, i);
		bf.sortNeighbors(bfNeighbors, bfDistances);
		kd.neighbors(kdNeighbors, kdDistances, i);
		kd.sortNeighbors(kdNeighbors, kdDistances);
		for(size_t j = 0; j < TEST_DIMS; j++)
		{
			if(bfNeighbors[j] != kdNeighbors[j])
				ThrowError("wrong answer!");
		}
	}
}
#endif // !NO_TEST_CODE

// --------------------------------------------------------------------------------------------------------





















class GShortcutPrunerAtomicCycleDetector : public GAtomicCycleFinder
{
protected:
	GShortcutPruner* m_pThis;
	size_t m_thresh;

public:
	GShortcutPrunerAtomicCycleDetector(size_t nodes, GShortcutPruner* pThis, size_t thresh) : GAtomicCycleFinder(nodes), m_pThis(pThis), m_thresh(thresh)
	{
	}

	virtual ~GShortcutPrunerAtomicCycleDetector()
	{
	}

	virtual bool onDetectAtomicCycle(vector<size_t>& cycle)
	{
		if(cycle.size() >= (size_t)m_thresh)
		{
			m_pThis->onDetectBigAtomicCycle(cycle);
			return false;
		}
		else
			return true;
	}
};

GShortcutPruner::GShortcutPruner(size_t* pNeighborhoods, size_t n, size_t k)
: m_pNeighborhoods(pNeighborhoods), m_n(n), m_k(k), m_cycleThresh(10), m_subGraphRange(6), m_cuts(0)
{
}

GShortcutPruner::~GShortcutPruner()
{
}

bool GShortcutPruner::isEveryNodeReachable()
{
	GBitTable visited(m_n);
	deque<size_t> q;
	visited.set(0);
	q.push_back(0);
	while(q.size() > 0)
	{
		size_t cur = q.front();
		q.pop_front();
		for(size_t j = 0; j < m_k; j++)
		{
			size_t neigh = m_pNeighborhoods[m_k * cur + j];
			if(neigh < m_n && !visited.bit(neigh))
			{
				visited.set(neigh);
				q.push_back(neigh);
			}
		}
	}
	for(size_t i = 0; i < m_n; i++)
	{
		if(!visited.bit(i))
			return false;
	}
	return true;
}

size_t GShortcutPruner::prune()
{
	while(true)
	{
		bool everyNodeReachable = isEveryNodeReachable();
		GShortcutPrunerAtomicCycleDetector g(m_n, this, m_cycleThresh);
		size_t* pHood = m_pNeighborhoods;
		for(size_t i = 0; i < m_n; i++)
		{
			for(size_t j = 0; j < m_k; j++)
			{
				if(pHood[j] < m_n)
					g.addEdgeIfNotDupe(i, pHood[j]);
			}
			pHood += m_k;
		}
		size_t oldCuts = m_cuts;
		g.compute();
		if(everyNodeReachable && !isEveryNodeReachable())
			ThrowError("Cutting shortcuts should not segment the graph");
		if(m_cuts == oldCuts)
			break;
	}
	return m_cuts;
}

void GShortcutPruner::onDetectBigAtomicCycle(vector<size_t>& cycle)
{
	// Make a subgraph containing only nodes close to the cycle
	size_t* mapIn = new size_t[m_n];
	ArrayHolder<size_t> hMapIn(mapIn);
	vector<size_t> mapOut;
	GBitTable visited(m_n);
	deque<size_t> q;
	for(vector<size_t>::iterator it = cycle.begin(); it != cycle.end(); it++)
	{
		q.push_back(*it);
		q.push_back(1);
	}
	while(q.size() > 0)
	{
		size_t cur = q.front();
		q.pop_front();
		size_t depth = q.front();
		q.pop_front();
		mapIn[cur] = mapOut.size();
		mapOut.push_back(cur);
		if(depth <= (size_t)m_subGraphRange)
		{
			for(size_t j = 0; j < m_k; j++)
			{
				size_t neigh = m_pNeighborhoods[cur * m_k + j];
				if(neigh < m_n && !visited.bit(neigh))
				{
					visited.set(neigh);
					q.push_back(neigh);
					q.push_back(depth + 1);
				}
			}
		}
	}

	// Compute betweenness of all edges
	GBrandesBetweennessCentrality g(mapOut.size());
	for(size_t i = 0; i < mapOut.size(); i++)
	{
		size_t* pHood = m_pNeighborhoods + mapOut[i] * m_k;
		for(size_t j = 0; j < m_k; j++)
		{
			size_t neigh = pHood[j];
			if(neigh < m_n && visited.bit(neigh))
			{
				g.addDirectedEdgeIfNotDupe(i, mapIn[neigh]);
				g.addDirectedEdgeIfNotDupe(mapIn[neigh], i);
			}
		}
	}
	g.compute();

	// Find the edge on the cycle with the largest betweenness
	size_t shortcutFrom = 0;
	size_t shortcutTo = 0;
	double shortcutBetweenness = 0;
	for(size_t i = 0; i < cycle.size(); i++)
	{
		size_t from = cycle[i];
		size_t to = cycle[(i + 1) % cycle.size()];
		size_t forwIndex = g.neighborIndex(mapIn[from], mapIn[to]);
		size_t revIndex = g.neighborIndex(mapIn[to], mapIn[from]);
		double d = g.edgeBetweennessByNeighbor(mapIn[from], forwIndex) + g.edgeBetweennessByNeighbor(mapIn[to], revIndex);
		if(i == 0 || d > shortcutBetweenness)
		{
			shortcutBetweenness = d;
			shortcutFrom = from;
			shortcutTo = to;
		}
	}

	// Cut the shortcut
	bool cutForward = false;
	for(size_t j = 0; j < m_k; j++)
	{
		if(m_pNeighborhoods[shortcutFrom * m_k + j] == shortcutTo)
		{
			m_pNeighborhoods[shortcutFrom * m_k + j] = INVALID_INDEX;
			cutForward = true;
			m_cuts++;
			break;
		}
	}
	bool cutReverse = false;
	for(size_t j = 0; j < m_k; j++)
	{
		if(m_pNeighborhoods[shortcutTo * m_k + j] == shortcutFrom)
		{
			m_pNeighborhoods[shortcutTo * m_k + j] = INVALID_INDEX;
			cutReverse = true;
			m_cuts++;
			break;
		}
	}
	if(!cutForward && !cutReverse)
		ThrowError("Failed to find the offending edge");
}

#ifndef NO_TEST_CODE
// static
void GShortcutPruner::test()
{
	// Make a fully-connected grid
	size_t w = 6;
	size_t h = 6;
	size_t n = w * h;
	size_t k = 4;
	size_t* pNeighbors = new size_t[n * k];
	ArrayHolder<size_t> hNeighbors(pNeighbors);
	size_t i = 0;
	size_t* pHood = pNeighbors;
	for(size_t y = 0; y < h; y++)
	{
		for(size_t x = 0; x < w; x++)
		{
			size_t j = 0;
			pHood[j++] = (x > 0 ? i - 1 : INVALID_INDEX);
			pHood[j++] = (x < w - 1 ? i + 1 : INVALID_INDEX);
			pHood[j++] = (y > 0 ? i - w : INVALID_INDEX);
			pHood[j++] = (y < h - 1 ? i + w : INVALID_INDEX);
			pHood += k;
			i++;
		}
	}

	// Add 3 shortcuts
	pNeighbors[(0 * w + 0) * k + 0] = n - 1; // connect (0,0) to (w-1, h-1)
	pNeighbors[(0 * w + (w - 1)) * k + 1] = n - 1; // connect (w-1,0) to (w-1,h-1)
	pNeighbors[((h - 1) * w + (w - 1)) * k + 0] = w - 1; // connect (w-1,h-1) to (w-1,0)

	// Cut the shortcuts
	GShortcutPruner pruner(pNeighbors, n, k);
	pruner.setCycleThreshold(h);
	pruner.setSubGraphRange(3);
	size_t cuts = pruner.prune();
	if(pNeighbors[(0 * w + 0) * k + 0] != INVALID_INDEX)
		ThrowError("missed a shortcut");
	if(pNeighbors[(0 * w + (w - 1)) * k + 1] != INVALID_INDEX)
		ThrowError("missed a shortcut");
	if(pNeighbors[((h - 1) * w + (w - 1)) * k + 0] != INVALID_INDEX)
		ThrowError("missed a shortcut");
	if(cuts != 3)
		ThrowError("wrong number of cuts");
}
#endif // NO_TEST_CODE











class GCycleCutAtomicCycleDetector : public GAtomicCycleFinder
{
protected:
	GCycleCut* m_pThis;
	size_t m_thresh;
	bool m_restore;
	bool m_gotOne;

public:
	GCycleCutAtomicCycleDetector(size_t nodes, GCycleCut* pThis, size_t thresh, bool restore) : GAtomicCycleFinder(nodes), m_pThis(pThis), m_thresh(thresh), m_restore(restore), m_gotOne(false)
	{
	}

	virtual ~GCycleCutAtomicCycleDetector()
	{
	}

	bool gotOne() { return m_gotOne; }

	virtual bool onDetectAtomicCycle(vector<size_t>& cycle)
	{
		if(cycle.size() >= (size_t)m_thresh)
		{
			if(m_restore)
				m_gotOne = true;
			else
				m_pThis->onDetectBigAtomicCycle(cycle);
			return false;
		}
		else
			return true;
	}
};

GCycleCut::GCycleCut(size_t* pNeighborhoods, GMatrix* pPoints, size_t k)
: m_pNeighborhoods(pNeighborhoods), m_pPoints(pPoints), m_k(k), m_cycleThresh(10), m_cutCount(0)
{
	// Compute the mean neighbor distance
	size_t* pNeigh = m_pNeighborhoods;
	size_t colCount = m_pPoints->cols();
	size_t count = 0;
	double sum = 0;
	for(size_t i = 0; i < m_pPoints->rows(); i++)
	{
		for(size_t j = 0; j < k; j++)
		{
			if(*pNeigh < m_pPoints->rows())
			{
				sum += sqrt(GVec::squaredDistance(m_pPoints->row(i), m_pPoints->row(*pNeigh), colCount));
				count++;
			}
			pNeigh++;
		}
	}
	m_aveDist = sum / count;

	// Compute the capacities
	pNeigh = m_pNeighborhoods;
	for(size_t i = 0; i < m_pPoints->rows(); i++)
	{
		for(size_t j = 0; j < k; j++)
		{
			if(*pNeigh < m_pPoints->rows())
			{

				double cap = 1.0 / (m_aveDist + sqrt(GVec::squaredDistance(m_pPoints->row(i), m_pPoints->row(*pNeigh), colCount)));
				m_capacities[make_pair(i, *pNeigh)] = cap;
				m_capacities[make_pair(*pNeigh, i)] = cap;
/*
				m_capacities[make_pair(i, *pNeigh)] = 1.0;
				m_capacities[make_pair(*pNeigh, i)] = 1.0;
*/
			}
			pNeigh++;
		}
	}
}

GCycleCut::~GCycleCut()
{
}

bool GCycleCut::doAnyBigAtomicCyclesExist()
{
	// Make the graph
	GCycleCutAtomicCycleDetector g(m_pPoints->rows(), this, m_cycleThresh, true);
	size_t* pHood = m_pNeighborhoods;
	for(size_t i = 0; i < m_pPoints->rows(); i++)
	{
		for(size_t j = 0; j < m_k; j++)
		{
			if(pHood[j] < m_pPoints->rows())
				g.addEdgeIfNotDupe(i, pHood[j]);
		}
		pHood += m_k;
	}

	// Find a large atomic cycle (calls onDetectBigAtomicCycle when found)
	g.compute();
	return g.gotOne();
}

size_t GCycleCut::cut()
{
	m_cuts.clear();

	// Cut the graph
	while(true)
	{
		// Make the graph
		GCycleCutAtomicCycleDetector g(m_pPoints->rows(), this, m_cycleThresh, false);
		size_t* pHood = m_pNeighborhoods;
		for(size_t i = 0; i < m_pPoints->rows(); i++)
		{
			for(size_t j = 0; j < m_k; j++)
			{
				if(pHood[j] < m_pPoints->rows())
					g.addEdgeIfNotDupe(i, pHood[j]);
			}
			pHood += m_k;
		}

		// Find a large atomic cycle (calls onDetectBigAtomicCycle when found)
		size_t oldCuts = m_cutCount;
		g.compute();
		if(m_cutCount == oldCuts)
			break;
	}

	// Restore superfluous cuts
	for(vector<size_t>::iterator it = m_cuts.begin(); it != m_cuts.end(); )
	{
		size_t point = *it;
		it++;
		GAssert(it != m_cuts.end()); // broken cuts list
		size_t neigh = *it;
		it++;
		GAssert(it != m_cuts.end()); // broken cuts list
		size_t other = *it;
		it++;

		// Restore the edge if it doesn't create a big atomic cycle
		m_pNeighborhoods[point * m_k + neigh] = other;
		if(!doAnyBigAtomicCyclesExist())
			m_cutCount--;
		else
			m_pNeighborhoods[point * m_k + neigh] = INVALID_INDEX;
	}
//cerr << "cuts: " << m_cutCount << "\n";
	return m_cutCount;
}

void GCycleCut::onDetectBigAtomicCycle(vector<size_t>& cycle)
{
	// Find the bottleneck
	double bottleneck = 1e308;
	for(size_t i = 0; i < cycle.size(); i++)
	{
		size_t from = cycle[i];
		size_t to = cycle[(i + 1) % cycle.size()];
		pair<size_t, size_t> p = make_pair(from, to);
		double d = m_capacities[p];
		if(i == 0 || d < bottleneck)
			bottleneck = d;
	}
	GAssert(bottleneck > 0); // all capacities should be greater than zero

	// Reduce every edge in the cycle by the bottleneck's capacity
	for(size_t i = 0; i < cycle.size(); i++)
	{
		size_t from = cycle[i];
		size_t to = cycle[(i + 1) % cycle.size()];
		pair<size_t, size_t> p1 = make_pair(from, to);
		pair<size_t, size_t> p2 = make_pair(to, from);
		double d = m_capacities[p1];
		if(d - bottleneck > 1e-12)
		{
			// Reduce the capacity
			m_capacities[p1] = d - bottleneck;
			m_capacities[p2] = d - bottleneck;
		}
		else
		{
			// Remove the edge
			m_capacities.erase(p1);
			m_capacities.erase(p2);
			size_t forw = INVALID_INDEX;
			size_t* pHood = m_pNeighborhoods + from * m_k;
			for(size_t j = 0; j < m_k; j++)
			{
				if(pHood[j] == to)
				{
					forw = j;
					break;
				}
			}
			size_t rev = INVALID_INDEX;
			pHood = m_pNeighborhoods + to * m_k;
			for(size_t j = 0; j < m_k; j++)
			{
				if(pHood[j] == from)
				{
					rev = j;
					break;
				}
			}
			GAssert(rev != INVALID_INDEX || forw != INVALID_INDEX); // couldn't find the edge
			if(forw != INVALID_INDEX)
			{
				m_pNeighborhoods[from * m_k + forw] = INVALID_INDEX;
				m_cuts.push_back(from);
				m_cuts.push_back(forw);
				m_cuts.push_back(to);
				m_cutCount++;
			}
			if(rev != INVALID_INDEX)
			{
				m_pNeighborhoods[to * m_k + rev] = INVALID_INDEX;
				m_cuts.push_back(to);
				m_cuts.push_back(rev);
				m_cuts.push_back(from);
				m_cutCount++;
			}
		}
	}
}

#ifndef NO_TEST_CODE
// static
void GCycleCut::test()
{
	// Make a fully-connected grid
	size_t w = 6;
	size_t h = 6;
	size_t n = w * h;
	size_t k = 4;
	size_t* pNeighbors = new size_t[n * k];
	ArrayHolder<size_t> hNeighbors(pNeighbors);
	size_t i = 0;
	size_t* pHood = pNeighbors;
	for(size_t y = 0; y < h; y++)
	{
		for(size_t x = 0; x < w; x++)
		{
			size_t j = 0;
			pHood[j++] = (x > 0 ? i - 1 : INVALID_INDEX);
			pHood[j++] = (x < w - 1 ? i + 1 : INVALID_INDEX);
			pHood[j++] = (y > 0 ? i - w : INVALID_INDEX);
			pHood[j++] = (y < h - 1 ? i + w : INVALID_INDEX);
			pHood += k;
			i++;
		}
	}

	// Add 3 shortcuts
	pNeighbors[(0 * w + 0) * k + 0] = n - 1; // connect (0,0) to (w-1, h-1)
	pNeighbors[(0 * w + (w - 1)) * k + 1] = n - 1; // connect (w-1,0) to (w-1,h-1)
	pNeighbors[((h - 1) * w + (w - 1)) * k + 0] = w - 1; // connect (w-1,h-1) to (w-1,0)

	// Make some random data
	GMatrix data(0, 5);
	GRand prng(0);
	for(size_t i = 0; i < n; i++)
	{
		double* pRow = data.newRow();
		prng.spherical(pRow, 5);
	}

	// Cut the shortcuts
	GCycleCut pruner(pNeighbors, &data, k);
	pruner.setCycleThreshold(h);
	size_t cuts = pruner.cut();
	if(pNeighbors[(0 * w + 0) * k + 0] != INVALID_INDEX)
		ThrowError("missed a shortcut");
	if(pNeighbors[(0 * w + (w - 1)) * k + 1] != INVALID_INDEX)
		ThrowError("missed a shortcut");
	if(pNeighbors[((h - 1) * w + (w - 1)) * k + 0] != INVALID_INDEX)
		ThrowError("missed a shortcut");
	if(cuts != 3)
		ThrowError("wrong number of cuts");
}
#endif // NO_TEST_CODE










GSaffron::GSaffron(GMatrix* pData, size_t medianCands, size_t neighbors, size_t tangentDims, double sqCorrCap, GRand* pRand)
: GNeighborFinder(pData, neighbors), m_rows(pData->rows())
{
	double radius = GKdTree::medianDistanceToNeighbor(*pData, medianCands);
	double squaredRadius = radius * radius;
	size_t maxCandidates = medianCands * 3 / 2;

	// Make a table of all the neighbors to each point within the radius
	size_t dims = pData->relation()->size();
	GKdTree neighborFinder(pData, maxCandidates, NULL, true);
	size_t* pCandIndexes = new size_t[maxCandidates * pData->rows()];
	ArrayHolder<size_t> hCandIndexes(pCandIndexes);
	double* pCandDists = new double[maxCandidates * pData->rows()];
	ArrayHolder<double> hCandDists(pCandDists);
	size_t* pHoodIndexes = pCandIndexes;
	double* pHoodDists = pCandDists;
	for(size_t i = 0; i < pData->rows(); i++)
	{
		neighborFinder.neighbors(pHoodIndexes, pHoodDists, i);
		neighborFinder.sortNeighbors(pHoodIndexes, pHoodDists);
		for(size_t j = maxCandidates - 1; j < maxCandidates; j--)
		{
			if(pHoodIndexes[j] < pData->rows() && pHoodDists[j] < squaredRadius)
				break;
			pHoodIndexes[j] = INVALID_INDEX;
		}
		pHoodIndexes += maxCandidates;
		pHoodDists += maxCandidates;
	}

	// Initialize the weights
	double* pWeights = new double[maxCandidates * pData->rows()];
	ArrayHolder<double> hWeights(pWeights);
	GVec::setAll(pWeights, 1.0 / maxCandidates, maxCandidates * pData->rows());

	// Refine the weights
	GMatrixArray tanSpaces(dims);
	tanSpaces.newSets(pData->rows(), tangentDims);
	double* pBuf = new double[dims + maxCandidates];
	ArrayHolder<double> hBuf(pBuf);
	double* pSquaredCorr = pBuf + dims;
	double prevGoodness = 0.0;
	size_t iter;
	for(iter = 0; true; iter++)
	{
		// Compute the tangeant hyperplane at each point
		double* pHoodWeights = pWeights;
		GMatrix neighborhood(0, dims);
		for(size_t i = 0; i < pData->rows(); i++)
		{
			// Make the neighborhood
			neighborhood.flush();
			for(size_t j = 0; j < maxCandidates; j++)
			{
				size_t neigh = pCandIndexes[maxCandidates * i + j];
				if(neigh < pData->rows())
					neighborhood.copyRow(pData->row(neigh));
			}

			// Compute the tangeant hyperplane
			GMatrix* pTanSpace = tanSpaces.sets()[i];
			for(size_t j = 0; j < tangentDims; j++)
			{
				double* pBasis = pTanSpace->row(j);
				neighborhood.weightedPrincipalComponent(pBasis, dims, pData->row(i), pHoodWeights, pRand);
				neighborhood.removeComponent(pData->row(i), pBasis, dims);
			}
			pHoodWeights += maxCandidates;
		}

		// Refine the weights
		pHoodWeights = pWeights;
		size_t* pHoodIndexes = pCandIndexes;
		double* pHoodDists = pCandDists;
		double goodness = 0.0;
		for(size_t i = 0; i < pData->rows(); i++)
		{
			// Compute the squaredDistance with each neighbor
			double* pMe = pData->row(i);
			GMatrix* pMyTan = tanSpaces.sets()[i];
			for(size_t j = 0; j < maxCandidates; j++)
			{
				size_t neighIndex = pHoodIndexes[j];
				if(neighIndex >= pData->rows())
				{
					pSquaredCorr[j] = 0.0;
					continue;
				}
				double* pNeigh = pData->row(neighIndex);
				GMatrix* pNeighTan = tanSpaces.sets()[neighIndex];
				pSquaredCorr[j] = measureAlignment(pMe, pMyTan, pNeigh, pNeighTan, sqCorrCap, squaredRadius, pRand);

				// Compute squared correlation between the two neighbors
				double sqdist = GVec::squaredDistance(pMe, pNeigh, dims);
				double alignment = 1.0;
				if(sqdist > 0)
				{
					pNeighTan->project(pBuf, pMe, pNeigh);
					alignment = (1.0 - (GVec::squaredDistance(pBuf, pMe, dims) / sqdist));
					pMyTan->project(pBuf, pNeigh, pMe);
					alignment *= (1.0 - GVec::squaredDistance(pBuf, pNeigh, dims) / sqdist);
				}
				double cosDihedral = pMyTan->dihedralCorrelation(pNeighTan, pRand);
				pSquaredCorr[j] = alignment * cosDihedral * cosDihedral;
			}

			// Adjust the weights
			GVec::smallestToFront(pSquaredCorr, maxCandidates - neighbors, maxCandidates, pHoodWeights, pHoodIndexes, pHoodDists);
			for(size_t j = 0; j < maxCandidates - neighbors; j++)
				pHoodWeights[j] = 0.9 * pHoodWeights[j];
			GVec::sumToOne(pHoodWeights, maxCandidates);
			goodness += GVec::dotProduct(pHoodWeights, pSquaredCorr, maxCandidates);

			// Advance
			pHoodWeights += maxCandidates;
			pHoodIndexes += maxCandidates;
			pHoodDists += maxCandidates;
		}
		if(prevGoodness > 0.0 && (goodness / prevGoodness - 1.0) < 0.0001)
			break;
		prevGoodness = goodness;
		//cout << "	goodness=" << goodness << "\n";
	}
	//cout << "	iters=" << iter << "\n";

	// Store the results
	m_pNeighborhoods = new size_t[m_neighborCount * pData->rows()];
	m_pDistances = new double[m_neighborCount * pData->rows()];
	size_t* pOutIndexes = m_pNeighborhoods;
	double* pOutDists = m_pDistances;
	pHoodDists = pCandDists;
	pHoodIndexes = pCandIndexes;
	for(size_t i = 0; i < pData->rows(); i++)
	{
		for(size_t j = 0; j < m_neighborCount; j++)
		{
			pOutIndexes[j] = pHoodIndexes[maxCandidates - 1 - j];
			pOutDists[j] = pHoodDists[maxCandidates - 1 - j];
		}

		// Advance
		pOutIndexes += m_neighborCount;
		pOutDists += m_neighborCount;
		pHoodDists += maxCandidates;
		pHoodIndexes += maxCandidates;
	}
}

// virtual
GSaffron::~GSaffron()
{
	delete[] m_pNeighborhoods;
	delete[] m_pDistances;
}

// static
double GSaffron::measureAlignment(double* pA, GMatrix* pATan, double* pB, GMatrix* pBTan, double cap, double squaredRadius, GRand* pRand)
{
	size_t dims = pATan->cols();
	double sqdist = GVec::squaredDistance(pA, pB, dims);
	double mono1;
	double mono2;
	if(sqdist > 0)
	{
		GTEMPBUF(double, pBuf, dims);
		pBTan->project(pBuf, pA, pB); // project pA - pB onto pBTan
		mono1 = std::min(cap, GVec::squaredDistance(pBuf, pB, dims) / sqdist);
		pATan->project(pBuf, pB, pA); // project pB - pA onto pATan
		mono2 = std::min(cap, GVec::squaredDistance(pBuf, pA, dims) / sqdist);
	}
	else
	{
		mono1 = cap;
		mono2 = cap;
	}
	double di = pATan->dihedralCorrelation(pBTan, pRand);
	di = std::min(cap, di * di);
	double distancePenalty = 1e-6 * sqdist / squaredRadius; // use distance to break ties
	return mono1 * mono2 * di - distancePenalty;
}

// virtual
void GSaffron::neighbors(size_t* pOutNeighbors, size_t index)
{
	if(index >= m_rows)
		ThrowError("out of range");
	memcpy(pOutNeighbors, m_pNeighborhoods + index * m_neighborCount, sizeof(size_t) * m_neighborCount);
}

// virtual
void GSaffron::neighbors(size_t* pOutNeighbors, double* pOutDistances, size_t index)
{
	neighbors(pOutNeighbors, index);
	GVec::copy(pOutDistances, m_pDistances + index * m_neighborCount, m_neighborCount);
}

double GSaffron::meanNeighborCount(double* pDeviation)
{
	size_t sum = 0;
	size_t sumOfSq = 0;
	size_t* pPos = m_pNeighborhoods;
	for(size_t i = 0; i < m_rows; i++)
	{
		size_t n = 0;
		for(size_t i = 0; i < m_neighborCount; i++)
		{
			if(*pPos < m_rows)
				n++;
			pPos++;
		}
		sum += n;
		sumOfSq += (n * n);
	}
	double mean = (double)sum / m_rows;
	if(pDeviation)
		*pDeviation = ((sumOfSq / m_rows) - (mean * mean)) * m_rows / (m_rows - 1);
	return mean;
}








GTemporalNeighborFinder::GTemporalNeighborFinder(GMatrix* pObservations, GMatrix* pActions, bool ownActionsData, size_t neighborCount, GRand* pRand, size_t maxDims)
: GNeighborFinder(preprocessObservations(pObservations, maxDims, pRand), neighborCount),
m_pPreprocessed(m_pPreprocessed), // don't panic, this is intentional. m_pPreprocessed is initialized in the previous line, and we do this so that its value will not be stomped over.
m_pActions(pActions),
m_ownActionsData(ownActionsData),
m_pRand(pRand)
{
	if(m_pData->rows() != pActions->rows())
		ThrowError("Expected the same number of observations as control vectors");
	if(pActions->cols() != 1)
		ThrowError("Sorry, only one action dim is currently supported");
	int actionValues = (int)m_pActions->relation()->valueCount(0);
	if(actionValues < 2)
		ThrowError("Sorry, only nominal actions are currently supported");

	// Train the consequence maps
	size_t obsDims = m_pData->cols();
	for(int j = 0; j < actionValues; j++)
	{
		GMatrix before(0, obsDims);
		GMatrix delta(0, obsDims);
		for(size_t i = 0; i < m_pData->rows() - 1; i++)
		{
			if((int)pActions->row(i)[0] == (int)j)
			{
				GVec::copy(before.newRow(), m_pData->row(i), obsDims);
				double* pDelta = delta.newRow();
				GVec::copy(pDelta, m_pData->row(i + 1), obsDims);
				GVec::subtract(pDelta, m_pData->row(i), obsDims);
			}
		}
		GAssert(before.rows() > 20); // not much data
		GKNN* pMap = new GKNN(*pRand);
		pMap->setAutoFilter(false);
		pMap->setFeatureFilter(new GPCA(12, pRand));
		m_consequenceMaps.push_back(pMap);
		pMap->train(before, delta);
	}
}

// virtual
GTemporalNeighborFinder::~GTemporalNeighborFinder()
{
	if(m_ownActionsData)
		delete(m_pActions);
	for(vector<GSupervisedLearner*>::iterator it = m_consequenceMaps.begin(); it != m_consequenceMaps.end(); it++)
		delete(*it);
	delete(m_pPreprocessed);
}

GMatrix* GTemporalNeighborFinder::preprocessObservations(GMatrix* pObs, size_t maxDims, GRand* pRand)
{
	if(pObs->cols() > maxDims)
	{
		GPCA pca(maxDims, pRand);
		pca.train(*pObs);
		m_pPreprocessed = pca.transformBatch(*pObs);
		return m_pPreprocessed;
	}
	else
	{
		m_pPreprocessed = NULL;
		return pObs;
	}
}

bool GTemporalNeighborFinder::findPath(size_t from, size_t to, double* path, double maxDist)
{
	// Find the path
	int actionValues = (int)m_pActions->relation()->valueCount(0);
	double* pStart = m_pData->row(from);
	double* pGoal = m_pData->row(to);
	size_t dims = m_pData->cols();
	double origSquaredDist = GVec::squaredDistance(pStart, pGoal, dims);
	GTEMPBUF(double, pObs, dims + dims + dims);
	double* pDelta = pObs + dims;
	double* pRemaining = pDelta + dims;
	GVec::copy(pObs, pStart, dims);
	GBitTable usedActions(actionValues);
	GVec::setAll(path, 0.0, actionValues);
	while(true)
	{
		GVec::copy(pRemaining, pGoal, dims);
		GVec::subtract(pRemaining, pObs, dims);
		if(GVec::squaredMagnitude(pRemaining, dims) < 1e-9)
			break; // We have arrived at the destination
		double biggestCorr = 1e-6;
		int bestAction = -1;
		double stepSize = 0.0;
		int lastPredicted = -1;
		for(int i = 0; i < actionValues; i++)
		{
			if(usedActions.bit(i))
				continue;
			m_consequenceMaps[i]->predict(pObs, pDelta);
			lastPredicted = i;
			double d = GVec::correlation(pDelta, pRemaining, dims);
			if(d <= 0)
				usedActions.set(i);
			else if(d > biggestCorr)
			{
				biggestCorr = d;
				bestAction = i;
				stepSize = std::min(1.0, GVec::dotProduct(pDelta, pRemaining, dims) / GVec::squaredMagnitude(pDelta, dims));
			}
		}
		if(bestAction < 0)
			break; // There are no good actions, so we're done
		if(stepSize < 1.0)
			usedActions.set(bestAction); // let's not do microscopic zig-zagging

		// Advance the current observation
		if(bestAction != lastPredicted)
			m_consequenceMaps[bestAction]->predict(pObs, pDelta);
		GVec::addScaled(pObs, stepSize, pDelta, dims);
		path[bestAction] += stepSize;
		if(GVec::squaredMagnitude(path, actionValues) > maxDist * maxDist)
			return false;
	}
	if(GVec::squaredMagnitude(pRemaining, dims) >= 0.2 * 0.2 * origSquaredDist)
		return false; // Too imprecise. Throw this one out.
	return true;
}

// virtual
void GTemporalNeighborFinder::neighbors(size_t* pOutNeighbors, size_t index)
{
	GTEMPBUF(double, dissims, m_neighborCount);
	neighbors(pOutNeighbors, dissims, index);
}

// virtual
void GTemporalNeighborFinder::neighbors(size_t* pOutNeighbors, double* pOutDistances, size_t index)
{
	int valueCount = (int)m_pActions->relation()->valueCount(0);
	if(m_pActions->cols() > 1 || valueCount == 0)
		ThrowError("continuous and multi-dim actions not supported yet");
	size_t actionValues = m_pActions->relation()->valueCount(0);
	size_t pos = 0;
	GTEMPBUF(double, path, actionValues);
	for(size_t i = 0; pos < m_neighborCount && i < m_pData->rows(); i++)
	{
		if(index == i)
			continue;
		if(!findPath(index, i, path, 2.0 //distCap
			))
		{
			if(index + 1 == i)
			{
				pOutNeighbors[pos] = i;
				pOutDistances[pos] = 1.0;
			}
			else if(index == i + 1)
			{
				pOutNeighbors[pos] = i;
				pOutDistances[pos] = 1.0;
			}
			else
				continue;
		}
		pOutNeighbors[pos] = i;
		pOutDistances[pos] = GVec::squaredMagnitude(path, actionValues);
		//GAssert(ABS(pOutDistances[pos]) < 0.001 || ABS(pOutDistances[pos] - 1.0) < 0.001 || ABS(pOutDistances[pos] - 1.4142) < 0.001 || ABS(pOutDistances[pos] - 2.0) < 0.001); // Noisy result. Does the transition function have noise? If so, then this is expected, so comment me out.
		pos++;
	}

	// Fill the remaining slots with nothing
	while(pos < m_neighborCount)
	{
		pOutNeighbors[pos] = INVALID_INDEX;
		pOutDistances[pos] = 0.0;
		pos++;
	}
}








GSequenceNeighborFinder::GSequenceNeighborFinder(GMatrix* pData, int neighborCount)
: GNeighborFinder(pData, neighborCount)
{
}

// virtual
GSequenceNeighborFinder::~GSequenceNeighborFinder()
{
}

// virtual
void GSequenceNeighborFinder::neighbors(size_t* pOutNeighbors, size_t index)
{
	return neighbors(pOutNeighbors, NULL, index);
}

// virtual
void GSequenceNeighborFinder::neighbors(size_t* pOutNeighbors, double* pOutDistances, size_t index)
{
	size_t prevPos = -1;
	size_t pos = 0;
	size_t i = 1;
	while(true)
	{
		if(pos == prevPos)
		{
			while(pos < m_neighborCount)
				pOutNeighbors[pos++] = INVALID_INDEX;
			break;
		}
		prevPos = pos;
		if(index - i < m_pData->rows())
		{
			pOutNeighbors[pos] = index - i;
			if(++pos >= m_neighborCount)
				break;
		}
		if(index + i < m_pData->rows())
		{
			pOutNeighbors[pos] = index + i;
			if(++pos >= m_neighborCount)
				break;
		}
		i++;
	}
	if(pOutDistances)
	{
		for(size_t i = 0; i < m_neighborCount; i++)
			pOutDistances[i] = (double)((i + 2) / 2);
	}
}



} // namespace GClasses


