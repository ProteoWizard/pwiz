/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __GKNN_H__
#define __GKNN_H__

#include "GLearner.h"

namespace GClasses {

class GNeighborFinderGeneralizing;
class GRand;
class GKnnScaleFactorCritic;
class GOptimizer;
class GRowDistanceScaled;
class GSparseSimilarity;


/// The k-Nearest Neighbor learning algorithm
class GKNN : public GIncrementalLearner
{
public:
	enum InterpolationMethod
	{
		Linear,
		Mean,
		Learner,
	};

protected:
	// Settings
	GMatrix* m_pFeatures;
	GSparseMatrix* m_pSparseFeatures;
	GMatrix* m_pLabels;
	size_t m_nNeighbors;
	InterpolationMethod m_eInterpolationMethod;
	GSupervisedLearner* m_pLearner;
	bool m_bOwnLearner;
	double m_dElbowRoom;

	// Scale Factor Optimization
	bool m_optimizeScaleFactors;
	GRowDistanceScaled* m_pDistanceMetric;
	GSparseSimilarity* m_pSparseMetric;
	bool m_ownMetric;
	GKnnScaleFactorCritic* m_pCritic;
	GOptimizer* m_pScaleFactorOptimizer;

	// Working Buffers
	size_t* m_pEvalNeighbors;
	double* m_pEvalDistances;
	double* m_pValueCounts;

	// Neighbor Finding
	GNeighborFinderGeneralizing* m_pNeighborFinder; // used for evaluation
	GNeighborFinderGeneralizing* m_pNeighborFinder2; // used for incremental training

public:
	/// nNeighbors specifies the number of neighbors to evaluate in order to make a prediction.
	GKNN(GRand& rand);

	/// Load from a DOM.
	GKNN(GDomNode* pNode, GLearnerLoader& ll);

	virtual ~GKNN();

	/// Returns the number of neighbors
	size_t neighborCount() { return m_nNeighbors; }

	/// Specify the number of neighbors to use. (The default is 1.)
	void setNeighborCount(size_t k);

#ifndef NO_TEST_CODE
	/// Performs unit tests for this class. Throws an exception if there is a failure.
	static void test();
#endif

	/// Marshal this object into a DOM, which can then be converted to a variety of serial formats.
	virtual GDomNode* serialize(GDom* pDoc);

	/// See the comment for GIncrementalLearner::trainSparse
	virtual void trainSparse(GSparseMatrix& features, GMatrix& labels);

	/// Discard any training (but not any settings) so it can be trained again
	virtual void clear();

	/// Sets the distance metric to use for finding neighbors. If own is true, then
	/// this object will delete pMetric when it is done with it.
	void setMetric(GRowDistanceScaled* pMetric, bool own);

	/// Sets the sparse similarity metric to use for finding neighbors. If own is true, then
	/// this object will delete pMetric when it is done with it.
	void setMetric(GSparseSimilarity* pMetric, bool own);

	/// Sets the technique for interpolation. (If you want to use the "Learner" method,
	/// you should call SetInterpolationLearner instead of this method.)
	void setInterpolationMethod(InterpolationMethod eMethod);

	/// Sets the interpolation method to "Learner" and sets the learner to use. If
	/// bTakeOwnership is true, it will delete the learner when this object is deleted.
	void setInterpolationLearner(GSupervisedLearner* pLearner, bool bTakeOwnership);

	/// Adds a copy of pVector to the internal set.
	size_t addVector(const double* pIn, const double* pOut);

	/// Sets the value for elbow room. (This value is only used with incremental training.)
	void setElbowRoom(double d) { m_dElbowRoom = d * d; }

	/// Returns the dissimilarity metric
	GRowDistanceScaled* metric() { return m_pDistanceMetric; }

	/// If you set this to true, it will use a hill-climber to optimize the
	/// attribute scaling factors. If you set it to false (the default), it won't.
	void setOptimizeScaleFactors(bool b);

	/// Returns the internal feature set
	GMatrix* features() { return m_pFeatures; }

	/// Returns the internal set of sparse features
	GSparseMatrix* sparseFeatures() { return m_pSparseFeatures; }

	/// Returns the internal label set
	GMatrix* labels() { return m_pLabels; }

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

	/// See the comment for GIncrementalLearner::beginIncrementalLearningInner
	virtual void beginIncrementalLearningInner(sp_relation& pFeatureRel, sp_relation& pLabelRel);

	/// Adds a vector to the internal set. Also, if the (k+1)th nearest
	/// neighbor of that vector is less than "elbow room" from it, then
	/// the closest neighbor is deleted from the internal set. (You might
	/// be wondering why the decision to delete the closest neighbor is
	/// determined by the distance of the (k+1)th neigbor. This enables a
	/// clump of k points to form in the most frequently sampled locations.
	/// Also, If you make this decision based on a closer neighbor, then big
	/// holes may form in the model if points are sampled in a poor order.)
	/// Call SetElbowRoom to specify the elbow room distance.
	virtual void trainIncrementalInner(const double* pIn, const double* pOut);

	/// Finds the nearest neighbors of pVector
	void findNeighbors(const double* pVector);

	/// Interpolate with each neighbor having equal vote
	void interpolateMean(const double* pIn, GPrediction* pOut, double* pOut2);

	/// Interpolate with each neighbor having a linear vote. (Actually it's linear with
	/// respect to the squared distance instead of the distance, because this is faster
	/// to compute.)
	void interpolateLinear(const double* pIn, GPrediction* pOut, double* pOut2);

	/// Interpolates with the provided supervised learning algorithm
	void interpolateLearner(const double* pIn, GPrediction* pOut, double* pOut2);

	/// See the comment for GTransducer::canImplicitlyHandleMissingFeatures
	virtual bool canImplicitlyHandleMissingFeatures() { return false; }
};


/// An instance-based transduction algorithm
class GNeighborTransducer : public GTransducer
{
protected:
	size_t m_friendCount;

public:
	/// General-purpose constructor
	GNeighborTransducer(GRand& rand);

	/// Returns the number of neighbors.
	size_t neighbors() { return m_friendCount; }

	/// Specify the number of neighbors to use with each point.
	void setNeighbors(size_t k) { m_friendCount = k; }

	/// Uses cross-validation to find a set of parameters that works well with
	/// the provided data.
	void autoTune(GMatrix& features, GMatrix& labels);

protected:
	/// See the comment for GTransducer::transduce
	virtual GMatrix* transduceInner(GMatrix& features1, GMatrix& labels1, GMatrix& features2);

	/// See the comment for GTransducer::canImplicitlyHandleNominalFeatures
	virtual bool canImplicitlyHandleNominalFeatures() { return false; }

	/// See the comment for GTransducer::canImplicitlyHandleContinuousLabels
	virtual bool canImplicitlyHandleContinuousLabels() { return false; }
};


/// This represents a grid of values. It might be useful as a Q-table with Q-learning.
class GInstanceTable : public GIncrementalLearner
{
protected:
	size_t m_dims;
	size_t* m_pDims;
	size_t* m_pScales;
	double* m_pTable;
	size_t m_product;

public:
	/// dims specifies the number of feature dimensions.
	/// pDims specifies the number of discrete zero-based values for each feature dim.
	GInstanceTable(size_t dims, size_t* pDims, GRand& rand);
	virtual ~GInstanceTable();

	/// Serialize this table
	virtual GDomNode* serialize(GDom* pDoc);

	/// See the comment for GIncrementalLearner::trainSparse
	virtual void trainSparse(GSparseMatrix& features, GMatrix& labels);

	/// Clears the internal model
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

	/// See the comment for GIncrementalLearner::beginIncrementalLearningInner
	virtual void beginIncrementalLearningInner(sp_relation& pFeatureRel, sp_relation& pLabelRel);

	/// See the comment for GIncrementalLearner::trainIncrementalInner
	virtual void trainIncrementalInner(const double* pIn, const double* pOut);
};

} // namespace GClasses

#endif // __GKNN_H__
