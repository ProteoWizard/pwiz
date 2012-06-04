/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#include "GStabSearch.h"
#include <math.h>
#include <stdio.h>
#include "GVec.h"
#include "GRand.h"

using std::vector;

namespace GClasses {

GBruteForceSearch::GBruteForceSearch(GTargetFunction* pCritic)
: GOptimizer(pCritic)
{
	if(!pCritic->relation()->areContinuous(0, pCritic->relation()->size()))
		ThrowError("Discrete attributes are not supported");
	if(pCritic->isConstrained())
		ThrowError("Sorry, this optimizer doesn't support constrained problems");
	vector<size_t> ranges;
	ranges.resize(pCritic->relation()->size());
	for(size_t i = 0; i < (size_t)pCritic->relation()->size(); i++)
		ranges[i] = 0x4000001;
	m_pCvi = new GCoordVectorIterator(ranges);
	m_pCandidate = new double[2 * pCritic->relation()->size()];
	m_pBestVector = m_pCandidate + pCritic->relation()->size();
}

// virtual
GBruteForceSearch::~GBruteForceSearch()
{
	delete(m_pCvi);
	delete[] std::min(m_pCandidate, m_pBestVector);
}

// virtual
double GBruteForceSearch::iterate()
{
	size_t* pCur = m_pCvi->current();
	double* pCand = m_pCandidate;
	for(size_t i = 0; i < (size_t)m_pCritic->relation()->size(); i++)
		*(pCand++) = (double)*(pCur++) / 0x4000001;
	double err = m_pCritic->computeError(m_pCandidate);
	if(err < m_bestError)
	{
		m_bestError = err;
		std::swap(m_pCandidate, m_pBestVector);
	}
	m_pCvi->advanceSampling();
	return m_bestError;
}

// virtual
double* GBruteForceSearch::currentVector()
{
	return m_pBestVector;
}









GRandomSearch::GRandomSearch(GTargetFunction* pCritic, GRand* pRand)
: GOptimizer(pCritic), m_pRand(pRand)
{
	if(!pCritic->relation()->areContinuous(0, pCritic->relation()->size()))
		ThrowError("Discrete attributes are not supported");
	if(pCritic->isConstrained())
		ThrowError("Sorry, this optimizer doesn't support constrained problems");
	m_pCandidate = new double[2 * pCritic->relation()->size()];
	m_pBestVector = m_pCandidate + pCritic->relation()->size();
}

// virtual
GRandomSearch::~GRandomSearch()
{
	delete[] std::min(m_pCandidate, m_pBestVector);
}

// virtual
double GRandomSearch::iterate()
{
	m_pRand->cubical(m_pCandidate, m_pCritic->relation()->size());
	double err = m_pCritic->computeError(m_pCandidate);
	if(err < m_bestError)
	{
		m_bestError = err;
		std::swap(m_pCandidate, m_pBestVector);
	}
	return m_bestError;
}

// virtual
double* GRandomSearch::currentVector()
{
	return m_pBestVector;
}











GProbeSearch::GProbeSearch(GTargetFunction* pCritic)
: GOptimizer(pCritic), m_rand(0)
{
	if(!pCritic->relation()->areContinuous(0, pCritic->relation()->size()))
		ThrowError("Discrete attributes are not supported");
	if(pCritic->isConstrained())
		ThrowError("Sorry, this optimizer doesn't support constrained problems");
	m_nDimensions = pCritic->relation()->size();
	m_nStabDepth = m_nDimensions * 30;
	m_pMins = new double[m_nDimensions * 4];
	m_pMaxs = m_pMins + m_nDimensions;
	m_pVector = m_pMaxs + m_nDimensions;
	m_pBestYet = m_pVector + m_nDimensions;
	m_samples = 64;
	reset();
}

/*virtual*/ GProbeSearch::~GProbeSearch()
{
	delete[] m_pMins;
}

void GProbeSearch::reset()
{
	m_nMask[0] = 0;
	m_nMask[1] = 0;
	m_nMask[2] = 0;
	m_nMask[3] = 0;
	resetStab();
	m_nMask[0] = 0; // undo the increment that ResetStab() does
	m_nStabs = 0;
	m_bestError = 1e308;
}

void GProbeSearch::resetStab()
{
	m_nCurrentDim = 0;
	m_nDepth = 0;

	// Start at the global scope
	GVec::setAll(m_pMins, 0.0, m_nDimensions);
	GVec::setAll(m_pMaxs, 1.0, m_nDimensions);

	// Increment the mask
	size_t i = 0;
	while(++(m_nMask[i]) == 0)
		i++;
	m_nStabs++;
}

double GProbeSearch::sample(bool greater)
{
	double bestLocal = 1e300;
	m_rand.setSeed(0);
	for(size_t i = 0; i < m_samples; i++)
	{
		m_rand.cubical(m_pVector, m_nDimensions);
		for(size_t j = 0; j < m_nDimensions; j++)
		{
			m_pVector[j] *= (m_pMaxs[j] - m_pMins[j]);
			m_pVector[j] += m_pMins[j];
		}
		m_pVector[m_nCurrentDim] -= m_pMins[m_nCurrentDim];
		m_pVector[m_nCurrentDim] *= 0.5;
		m_pVector[m_nCurrentDim] += m_pMins[m_nCurrentDim];
		if(greater)
			m_pVector[m_nCurrentDim] += 0.5 * (m_pMaxs[m_nCurrentDim] - m_pMins[m_nCurrentDim]);
		double err = m_pCritic->computeError(m_pVector);
		bestLocal = std::min(bestLocal, err);
		if(err < m_bestError)
		{
			m_bestError = err;
			GVec::copy(m_pBestYet, m_pVector, m_nDimensions);
		}
	}
	return bestLocal;
}

/*virtual*/ double GProbeSearch::iterate()
{
	// Test the center of both halves
	double dError1 = sample(false);
	double dError2 = sample(true);

	// Zoom in on half of the search space
	if(m_nMask[std::min(m_nDepth, (size_t)127) / 32] & ((size_t)1 << (std::min(m_nDepth, (size_t)127) % 32))) // if the mask bit is non-zero
	{
		// Pick the worse half
		if(dError1 < dError2)
			m_pMins[m_nCurrentDim] = 0.5 * (m_pMins[m_nCurrentDim] + m_pMaxs[m_nCurrentDim]);
		else
			m_pMaxs[m_nCurrentDim] = 0.5 * (m_pMins[m_nCurrentDim] + m_pMaxs[m_nCurrentDim]);
	}
	else
	{
		// Pick the better half
		if(dError1 < dError2)
			m_pMaxs[m_nCurrentDim] = 0.5 * (m_pMins[m_nCurrentDim] + m_pMaxs[m_nCurrentDim]);
		else
			m_pMins[m_nCurrentDim] = 0.5 * (m_pMins[m_nCurrentDim] + m_pMaxs[m_nCurrentDim]);
	}

	// Advance
	if(++m_nCurrentDim >= m_nDimensions)
		m_nCurrentDim = 0;
	if(++m_nDepth > m_nStabDepth)
		resetStab();
	return m_bestError;
}


#ifndef NO_TEST_CODE
class GProbeSearchTestCritic : public GTargetFunction
{
public:
	double m_target[3];

	GProbeSearchTestCritic() : GTargetFunction(3)
	{
		m_target[0] = 0.7314;
		m_target[1] = 0.1833;
		m_target[2] = 0.3831;
	}

	virtual ~GProbeSearchTestCritic()
	{
	}

	virtual bool isStable() { return true; }
	virtual bool isConstrained() { return false; }

protected:
	virtual void initVector(double* pVector)
	{
	}

	virtual double computeError(const double* pVector)
	{
		if(pVector[0] < 0.5)
			return 0.001;
		else if(pVector[1] >= 0.5)
			return 0.002;
		else
			return GVec::squaredDistance(pVector, m_target, 3);
	}
};

// static
void GProbeSearch::test()
{
	GProbeSearchTestCritic critic;
	GProbeSearch search(&critic);
	size_t stabdepth = 30;
	search.setStabDepth(stabdepth);
	size_t i;
	for(i = 0; i < stabdepth * 3 * 4; i++) // 3 = number of dims, 4 = number of stabs that should find it
		search.iterate();
	double err = GVec::squaredDistance(search.currentVector(), critic.m_target, 3);
	if(err >= 1e-3)
		ThrowError("failed");
}
#endif // !NO_TEST_CODE

} // namespace GClasses

