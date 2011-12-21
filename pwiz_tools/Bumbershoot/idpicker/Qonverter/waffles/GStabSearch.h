/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __GSTABSEARCH_H__
#define __GSTABSEARCH_H__

#include "GOptimizer.h"
#include "GRand.h"
#include "GVec.h"

namespace GClasses {

/// This performs a brute force search with uniform sampling over the
/// unit hypercube with increasing granularity. (Your target function should scale
/// the candidate vectors as necessary to cover the desired space.)
class GBruteForceSearch : public GOptimizer
{
protected:
	double* m_pCandidate;
	double* m_pBestVector;
	double m_bestError;
	GCoordVectorIterator* m_pCvi;

public:
	GBruteForceSearch(GTargetFunction* pCritic);
	virtual ~GBruteForceSearch();

	/// Each pass will complete after ((2^n)+1)^d iterations. The distance between
	/// samples at that point will be 1/(2^n). After it completes n=30, it will begin repeating.
	virtual double iterate();

	/// Returns the best vector yet found
	virtual double* currentVector();
};



/// At each iteration, this tries a random vector from the unit
/// hypercube. (Your target function should scale
/// the candidate vectors as necessary to cover the desired space.)
class GRandomSearch : public GOptimizer
{
protected:
	GRand* m_pRand;
	double* m_pCandidate;
	double* m_pBestVector;
	double m_bestError;

public:
	GRandomSearch(GTargetFunction* pCritic, GRand* pRand);
	virtual ~GRandomSearch();

	/// Try another random vector
	virtual double iterate();

	/// Returns the best vector yet found
	virtual double* currentVector();
};


/// This is somewhat of a multi-dimensional version of binary-search.
/// It greedily probes the best choices first, but then starts trying
/// the opposite choices at the higher divisions so that it can also
/// handle non-monotonic target functions.
/// Each iteration performs a binary (divide-and-conquer) search
/// within the unit hypercube. (Your target function should scale
/// the candidate vectors as necessary to cover the desired space.)
/// Because the high-level divisions are typically less correlated
/// with the quality of the final result than the low-level divisions,
/// it searches through the space of possible "probes" by toggling choices in
/// the order from high level to low level. In low-dimensional space, this
/// algorithm tends to quickly find good solutions, especially if the
/// target function is somewhat smooth. In high-dimensional space, the
/// number of iterations to find a good solution seems to grow exponentially.
class GProbeSearch : public GOptimizer
{
protected:
	GRand m_rand;
	size_t m_nDimensions;
	unsigned int m_nMask[4];
	double* m_pMins;
	double* m_pMaxs;
	double* m_pVector;
	double* m_pBestYet;
	double m_bestError;
	size_t m_nStabDepth;
	size_t m_nCurrentDim;
	size_t m_nDepth;
	size_t m_nStabs;
	size_t m_samples;

public:
	GProbeSearch(GTargetFunction* pCritic);
	virtual ~GProbeSearch();

	/// Do a little bit more work toward finding a good vector
	virtual double iterate();

	/// Returns the best vector yet found
	virtual double* currentVector() { return m_pBestYet; }

	/// Specify the number of times to divide the space before
	/// satisfactory accuracy is obtained. Larger values will
	/// result in more computation, but will find more precise
	/// values. For most problems, 20 to 30 should be sufficient.
	void setStabDepth(size_t n) { m_nStabDepth = m_nDimensions * n; }

	/// Returns the total number of completed stabs
	size_t stabCount() { return m_nStabs; }

	/// Specify the number of vectors to use to sample each side of a
	/// binary-split division.
	void setSampleCount(size_t n) { m_samples = n; }

#ifndef NO_TEST_CODE
	/// Performs unit tests for this class. Throws an exception if there is a failure.
	static void test();
#endif // !NO_TEST_CODE
protected:
	void resetStab();
	void reset();
	double sample(bool greater);
};


} // namespace GClasses

#endif // __GSTABSEARCH_H__
