/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __GLINEAR_H__
#define __GLINEAR_H__

#include "GLearner.h"
#include <vector>

namespace GClasses {

class GPCA;

/// A linear regression model. Let f be a feature vector of real values, and let l be a label vector of real values,
/// then this model estimates l=Bf+e, where B is a matrix of real values, and e is a
/// vector of real values. (In the Wikipedia article on linear regression, B is called
/// "beta", and e is called "epsilon". The approach used by this model to compute
/// beta and epsilon, however, is much more efficient than the approach currently
/// described in that article.)
class GLinearRegressor : public GSupervisedLearner
{
protected:
	GMatrix* m_pBeta;
	double* m_pEpsilon;

public:
	GLinearRegressor(GRand& rand);

	/// Load from a text-format
	GLinearRegressor(GDomNode* pNode, GLearnerLoader& ll);

	virtual ~GLinearRegressor();

#ifndef NO_TEST_CODE
	/// Performs unit tests for this class. Throws an exception if there is a failure.
	static void test();
#endif

	/// Saves the model to a text file. (This doesn't save the short-term
	/// memory used for incremental learning, so if you're doing "incremental"
	/// learning, it will wake up with amnesia when you load it again.)
	virtual GDomNode* serialize(GDom* pDoc);

	/// See the comment for GSupervisedLearner::clear
	virtual void clear();

	/// Returns the matrix that represents the linear transformation.
	GMatrix* beta() { return m_pBeta; }

	/// Returns the vector that is added to the results after the linear transformation is applied.
	double* epsilon() { return m_pEpsilon; }

	/// Performs on-line gradient descent to refine the model
	void refine(GMatrix& features, GMatrix& labels, double learningRate, size_t epochs, double learningRateDecayFactor);

	/// This model has no parameters to tune, so this method is a noop.
	void autoTune(GMatrix& features, GMatrix& labels);

protected:
	/// See the comment for GSupervisedLearner::trainInner
	virtual void trainInner(GMatrix& features, GMatrix& labels);

	/// See the comment for GSupervisedLearner::predictInner
	virtual void predictInner(const double* pIn, double* pOut);

	/// See the comment for GSupervisedLearner::predictDistributionInner
	virtual void predictDistributionInner(const double* pIn, GPrediction* pOut);

	/// See the comment for GTransducer::canImplicitlyHandleNominalFeatures
	virtual bool canImplicitlyHandleNominalFeatures() { return false; }

	/// See the comment for GTransducer::canImplicitlyHandleNominalLabels
	virtual bool canImplicitlyHandleNominalLabels() { return false; }
};



class GLinearProgramming
{
public:
	/// Compute x that maximizes c*x, subject to Ax<=b, x>=0.
	/// The size of pB is the number of rows in pA.
	/// The size of pC is the number of columns in pA.
	/// leConstraints specifies the number of <= constraints. (These must come first in order.)
	/// geConstraints specifies the number of >= constraints. (These come next.)
	/// The remaining constraints are assumed to be = constraints.
	/// The answer is put in pOutX, which is the same size as pC.
	/// Returns false if there is no solution, and true if it finds a solution.
	static bool simplexMethod(GMatrix* pA, const double* pB, int leConstraints, int geConstraints, const double* pC, double* pOutX);

#ifndef NO_TEST_CODE
	/// Perform unit tests for this class. Throws an exception if any tests fail. Returns if they all pass.
	static void test();
#endif
};


} // namespace GClasses

#endif // __GLINEAR_H__

