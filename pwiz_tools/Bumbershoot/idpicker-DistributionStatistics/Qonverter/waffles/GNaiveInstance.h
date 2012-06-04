/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __GNAIVEINSTANCE_H__
#define __GNAIVEINSTANCE_H__

#include "GLearner.h"

namespace GClasses {

class GNaiveInstanceAttr;
class GHeap;


/// This is an instance-based learner. Instead of finding the k-nearest
/// neighbors of a feature vector, it finds the k-nearst neighbors in each
/// dimension. That is, it finds n*k neighbors, considering each dimension
/// independently. It then combines the label from all of these neighbors
/// to make a prediction. Finding neighbors in this way makes it more robust to
/// high-dimensional datasets. It tends to perform worse than k-nn in low-dimensional space, and better
/// than k-nn in high-dimensional space. (It may be thought of as a cross
/// between a k-nn instance learner and a Naive Bayes learner. It only
/// supports continuous features and labels (so it is common to wrap it
/// in a Categorize filter which will convert nominal features to a categorical
/// distribution of continuous values).
class GNaiveInstance : public GIncrementalLearner
{
protected:
	size_t m_internalLabelDims, m_internalFeatureDims;
	size_t m_nNeighbors;
	GNaiveInstanceAttr** m_pAttrs;
	double* m_pValueSums;
	double* m_pWeightSums;
	double* m_pSumBuffer;
	double* m_pSumOfSquares;
	GHeap* m_pHeap;

public:
	/// nNeighbors is the number of neighbors (in each dimension)
	/// that will contribute to the output value.
	GNaiveInstance(GRand& rand);

	/// Deserializing constructor
	GNaiveInstance(GDomNode* pNode, GLearnerLoader& ll);
	virtual ~GNaiveInstance();

#ifndef NO_TEST_CODE
	/// Performs unit tests for this class. Throws an exception if there is a failure.
	static void test();
#endif

	/// Marshal this object into a DOM, which can then be converted to a variety of serial formats.
	virtual GDomNode* serialize(GDom* pDoc);

	/// Specify the number of neighbors to use.
	void setNeighbors(size_t k) { m_nNeighbors = k; }

	/// Returns the number of neighbors.
	size_t neighbors() { return m_nNeighbors; }

	/// See the comment for GIncrementalLearner::trainSparse.
	virtual void trainSparse(GSparseMatrix& features, GMatrix& labels);

	/// See the comment for GSupervisedLearner::clear.
	virtual void clear();

	/// Uses cross-validation to find a set of parameters that works well with
	/// the provided data.
	void autoTune(GMatrix& features, GMatrix& labels);

protected:
	void evalInput(size_t nInputDim, double dInput);

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

	/// See the comment for GIncrementalLearner::beginIncrementalLearningInner
	virtual void beginIncrementalLearningInner(sp_relation& pFeatureRel, sp_relation& pLabelRel);

	/// Incrementally train with a single instance
	virtual void trainIncrementalInner(const double* pIn, const double* pOut);
};

} // namespace GClasses

#endif // __GNAIVEINSTANCE_H__

