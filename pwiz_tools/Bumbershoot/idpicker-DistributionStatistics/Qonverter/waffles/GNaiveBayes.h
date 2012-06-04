/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __GNAIVEBAYES_H__
#define __GNAIVEBAYES_H__

#include "GLearner.h"

namespace GClasses {

class GXMLTag;
struct GNaiveBayesOutputAttr;

/// A naive Bayes classifier
class GNaiveBayes : public GIncrementalLearner
{
protected:
	sp_relation m_pFeatureRel;
	sp_relation m_pLabelRel;
	size_t m_nSampleCount;
	GNaiveBayesOutputAttr** m_pOutputs;
	double m_equivalentSampleSize;

public:
	GNaiveBayes(GRand& rand);

	/// Load from a DOM.
	GNaiveBayes(GDomNode* pNode, GLearnerLoader& ll);

	virtual ~GNaiveBayes();

#ifndef NO_TEST_CODE
	/// Performs unit tests for this class. Throws an exception if there is a failure.
	static void test();
#endif

	/// Marshal this object into a DOM, which can then be converted to a variety of serial formats.
	virtual GDomNode* serialize(GDom* pDoc);

	/// See the comment for GIncrementalLearner::trainSparse
	/// This method assumes that the values in pData are all binary values (0 or 1).
	virtual void trainSparse(GSparseMatrix& features, GMatrix& labels);

	/// To ensure that unsampled values don't dominate the joint
	/// distribution by multiplying by a zero, each value is given
	/// at least as much representation as specified here. (The default
	/// is 0.5, which is as if there were half of a sample for each value.)
	void setEquivalentSampleSize(double d) { m_equivalentSampleSize = d; }

	/// Returns the equivalent sample size. (The number of samples of each
	/// possible value that is added by default to prevent zeros.)
	double equivalentSampleSize() { return m_equivalentSampleSize; }

	/// See the comment for GSupervisedLearner::clear
	virtual void clear();

	/// Uses cross-validation to find a set of parameters that works well with
	/// the provided data.
	void autoTune(GMatrix& features, GMatrix& labels);

protected:
	/// See the comment for GSupervisedLearner::trainInner
	virtual void trainInner(GMatrix& features, GMatrix& labels);

	/// See the comment for GSupervisedLearner::predictInner
	virtual void predictInner(const double* pIn, double* pOut);

	/// See the comment for GSupervisedLearner::predictDistributionInner
	virtual void predictDistributionInner(const double* pIn, GPrediction* pOut);

	/// See the comment for GTransducer::canImplicitlyHandleContinuousFeatures
	virtual bool canImplicitlyHandleContinuousFeatures() { return false; }

	/// See the comment for GTransducer::canImplicitlyHandleContinuousLabels
	virtual bool canImplicitlyHandleContinuousLabels() { return false; }

	/// See the comment for GIncrementalLearner::beginIncrementalLearningInner
	virtual void beginIncrementalLearningInner(sp_relation& pFeatureRel, sp_relation& pLabelRel);

	/// Adds a single training sample to the collection
	virtual void trainIncrementalInner(const double* pIn, const double* pOut);
};

} // namespace GClasses

#endif // __GNAIVEBAYES_H__
