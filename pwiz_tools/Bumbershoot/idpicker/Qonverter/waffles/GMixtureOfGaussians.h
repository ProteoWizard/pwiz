/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __GMIXTUREOFGAUSSIANS_H__
#define __GMIXTUREOFGAUSSIANS_H__

#include "GDistribution.h"

namespace GClasses {

class GMatrix;


/// This class uses Expectency Maximization to find the mixture of Gaussians that best approximates
/// the data in a specified real attribute of a data set.
class GMixtureOfGaussians
{
protected:
	int m_nKernelCount;
	int m_nAttribute;
	double* m_pArrMeanVarWeight;
	double* m_pCatLikelihoods;
	double* m_pTemp;
	GMatrix* m_pData;
	GNormalDistribution m_dist;
	double m_dMinVariance;

public:
	GMixtureOfGaussians(int nKernelCount, GMatrix* pData, int nAttribute, double minVariance, GRand* pRand);
	virtual ~GMixtureOfGaussians();

#ifndef NO_TEST_CODE
	/// Performs unit tests for this class. Throws an exception if there is a failure.
	static void test();
#endif // !NO_TEST_CODE

	/// This tries to fit the data from several random starting points, and returns the best model it finds
	static GMixtureOfGaussians* stochasticHammer(int nMinKernelCount, int nMaxKernelCount, int nItters, int nTrials, GMatrix* pData, int nAttribute, double minVariance, GRand* pRand);

	/// Returns the log likelihood of the current parameters
	double iterate();

	/// Returns the current parameters of the specified kernel
	void params(int nKernel, double* pMean, double* pVariance, double* pWeight);

protected:
	double evalKernel(double x, int nKernel);
	double likelihoodOfEachCategoryGivenThisFeature(double x);
};

} // namespace GClasses

#endif // __GMIXTUREOFGAUSSIANS_H__
