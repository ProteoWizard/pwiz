/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __GPOLYNOMIAL_H__
#define __GPOLYNOMIAL_H__

#include "GLearner.h"
#include <vector>

namespace GClasses {

class GPolynomialSingleLabel;


/// This regresses a multi-dimensional polynomial to fit the data
class GPolynomial : public GSupervisedLearner
{
protected:
	size_t m_controlPoints;
	std::vector<GPolynomialSingleLabel*> m_polys;

public:
	/// It will have the same number of control points in every feature dimension
	GPolynomial(GRand& rand);

	/// Load from a DOM.
	GPolynomial(GDomNode* pNode, GLearnerLoader& ll);

	virtual ~GPolynomial();

	/// Set the number of control points in the Bezier representation of the
	/// polynomial (which is one more than the polynomial order). The default
	/// is 3.
	void setControlPoints(size_t n);

	/// Returns the number of control points.
	size_t controlPoints();

	/// Uses cross-validation to find a set of parameters that works well with
	/// the provided data.
	void autoTune(GMatrix& features, GMatrix& labels);

#ifndef NO_TEST_CODE
	/// Performs unit tests for this class. Throws an exception if there is a failure.
	static void test();
#endif // NO_TEST_CODE

	/// Marshal this object into a DOM, which can then be converted to a variety of serial formats.
	virtual GDomNode* serialize(GDom* pDoc);

	/// See the comment for GSupervisedLearner::clear
	virtual void clear();

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


} // namespace GClasses

#endif // __GPOLYNOMIAL_H__
