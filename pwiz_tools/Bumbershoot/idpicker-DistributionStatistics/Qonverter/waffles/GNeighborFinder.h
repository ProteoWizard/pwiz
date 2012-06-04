/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __GNEIGHBORFINDER_H__
#define __GNEIGHBORFINDER_H__

#include "GMatrix.h"
#include <vector>
#include <map>

namespace GClasses {

class GMatrix;
class GRelation;
class GRand;
class GKdNode;
class GBitTable;
class GDistanceMetric;
class GSupervisedLearner;


/// Finds the k-nearest neighbors of any vector in a dataset.
class GNeighborFinder
{
protected:
	GMatrix* m_pData;
	size_t m_neighborCount;

public:
	GNeighborFinder(GMatrix* pData, size_t neighborCount)
	: m_pData(pData), m_neighborCount(neighborCount)
	{
	}

	virtual ~GNeighborFinder()
	{
	}

	/// Returns the data passed to the constructor of this object
	GMatrix* data() { return m_pData; }

	/// Returns the number of neighbors to find
	size_t neighborCount() { return m_neighborCount; }

	/// Returns true if this neighbor finder can operate on points that
	/// are not in the dataset passed to the constructor
	virtual bool canGeneralize() { return false; }

	/// Returns true iff the neighbors and distances are pre-computed
	virtual bool isCached() { return false; }

	/// pOutNeighbors should be an array of size neighborCount.
	/// index refers to the point/vector whose neighbors you want to obtain.
	/// The value INVALID_INDEX may be used to fill slots with no point
	/// if necessary.
	virtual void neighbors(size_t* pOutNeighbors, size_t index) = 0;

	/// pOutNeighbors and pOutDistances should both be arrays of size neighborCount.
	/// index refers to the point/vector whose neighbors you want to obtain.
	/// The neighbors are not necessarily sorted, but you can call GNeighborFinder::sortNeighbors
	/// if you want them to be sorted.
	/// If there are not enough points in the data set to fill the
	/// neighbor array, the empty ones will have an index of INVALID_INDEX.
	virtual void neighbors(size_t* pOutNeighbors, double* pOutDistances, size_t index) = 0;

	/// Uses Quick Sort to sort the neighbors from least to most
	/// dissimilar, followed by any slots for with INVALID_INDEX for the index.
	static void sortNeighbors(size_t neighborCount, size_t* pNeighbors, double* pDistances);

	/// Uses Quick Sort to sort the neighbors from least to most
	/// dissimilar, followed by any slots for with INVALID_INDEX for the index.
	void sortNeighbors(size_t* pNeighbors, double* pDistances);
};




/// This wraps a neighbor finding algorithm. It caches the queries for neighbors
/// for the purpose of improving runtime performance.
class GNeighborFinderCacheWrapper : public GNeighborFinder
{
protected:
	GNeighborFinder* m_pNF;
	bool m_own;
	size_t* m_pCache;
	double* m_pDissims;

public:
	/// If own is true, then this will take ownership of pNF
	GNeighborFinderCacheWrapper(GNeighborFinder* pNF, bool own);
	virtual ~GNeighborFinderCacheWrapper();
	virtual void neighbors(size_t* pOutNeighbors, size_t index);
	virtual void neighbors(size_t* pOutNeighbors, double* pOutDistances, size_t index);

	/// See the comment for GNeighborFinder::isCached
	virtual bool isCached() { return true; }

	/// Returns a pointer to the neighbor finder that this wraps
	GNeighborFinder* wrappedNeighborFinder() { return m_pNF; }

	/// Returns the cache of neighbors. (You should probably call fillCache before calling this.)
	size_t* cache() { return m_pCache; }

	/// Returns the table of squared dissimilarities
	double* squaredDistanceTable() { return m_pDissims; }

	/// Ensures that the cache is populated with data for every index in the dataset
	void fillCache();

	/// Uses CycleCut to remove shortcut connections. (Assumes fillCache has already been called.)
	size_t cutShortcuts(size_t cycleLen);

	/// Patches any missing neighbors by randomly selecting another of its neighbors to fill both spots.
	void patchMissingSpots(GRand* pRand);

	/// (Re)computes all neighbor distances using the specified metric.
	void fillDistances(GDistanceMetric* pMetric);

	/// Normalizes all the neighborhoods so that all neighbor distances are approximately 1.
	void normalizeDistances();

	/// Returns true iff the neighbors form a connected graph when each neighbor
	/// is evaluated as a bi-directional edge. (Assumes that fillCache has already been called.)
	bool isConnected();
};



/// Finds the k-nearest neighbors (in a dataset) of an arbitrary vector (which may or may not
/// be in the dataset).
class GNeighborFinderGeneralizing : public GNeighborFinder
{
protected:
	GDistanceMetric* m_pMetric;
	bool m_ownMetric;

public:
	/// Create a neighborfinder for finding the neighborCount
	/// nearest neighbors under the given metric.  If ownMetric is
	/// true, then the neighborFinder takes responsibility for
	/// deleting the metric, otherwise it is the caller's
	/// responsibility.
	GNeighborFinderGeneralizing(GMatrix* pData, size_t neighborCount, GDistanceMetric* pMetric, bool ownMetric);

	virtual ~GNeighborFinderGeneralizing();

	/// Returns true. See the comment for GNeighborFinder::canGeneralize.
	virtual bool canGeneralize() { return true; }

	/// Add a copy of pVector to the data set.
	virtual size_t addCopy(const double* pVector) = 0;

	/// Releases a vector from the dataset. You are responsible to delete[] the vector
	/// that this returns.
	virtual double* releaseVector(size_t nIndex) = 0;

	/// If you make major changes, you can call this to tell it to rebuild
	/// any optimization structures.
	virtual void reoptimize() = 0;

	/// pOutNeighbors and pOutDistances should both be arrays of size neighborCount.
	/// pInputVector is the vector whose neighbors will be found.
	/// The neighbors are not necessarily sorted, but you can call GNeighborFinder::sortNeighbors
	/// if you want them to be sorted.
	/// If there are not enough points in the data set to fill the
	/// neighbor array, the empty ones will have an index of INVALID_INDEX.
	virtual void neighbors(size_t* pOutNeighbors, double* pOutDistances, const double* pInputVector) = 0;

	using GNeighborFinder::neighbors;
};


/// Finds neighbors by measuring the distance to all points. This one should work properly even if
/// the distance metric does not support the triangle inequality.
class GBruteForceNeighborFinder : public GNeighborFinderGeneralizing
{
public:
	GBruteForceNeighborFinder(GMatrix* pData, size_t neighborCount, GDistanceMetric* pMetric, bool ownMetric);
	virtual ~GBruteForceNeighborFinder();

	/// Add a point-vector
	virtual size_t addCopy(const double* pVector);

	/// Returns a point-vector (and removes it from the internal set). You are responsible to delete it.
	virtual double* releaseVector(size_t nIndex);

	/// This is a no-op method in this class.
	virtual void reoptimize();

	/// See the comment for GNeighborFinder::neighbors
	virtual void neighbors(size_t* pOutNeighbors, size_t index);

	/// See the comment for GNeighborFinder::neighbors
	virtual void neighbors(size_t* pOutNeighbors, double* pOutDistances, size_t index);

	/// See the comment for GNeighborFinderGeneralizing::neighbors
	virtual void neighbors(size_t* pOutNeighbors, double* pOutDistances, const double* pInputVector);
};




/// An efficient algorithm for finding neighbors.
class GKdTree : public GNeighborFinderGeneralizing
{
protected:
	size_t m_maxLeafSize;
	size_t m_size;
	GKdNode* m_pRoot;

public:
	GKdTree(GMatrix* pData, size_t neighborCount, GDistanceMetric* pMetric, bool ownMetric);
	virtual ~GKdTree();

#ifndef NO_TEST_CODE
	/// Performs unit tests for this class. Throws an exception if there is a failure.
	static void test();
#endif

	/// Add a new point-vector to the internal set
	virtual size_t addCopy(const double* pVector);

	/// Release a point-vector from the internal set
	virtual double* releaseVector(size_t nIndex);

	/// Rebuilds the tree to improve subsequent performance. This should be called after
	/// a significant number of point-vectors are added to or released from the internal set.
	virtual void reoptimize();

	/// See the comment for GNeighborFinder::neighbors
	virtual void neighbors(size_t* pOutNeighbors, size_t index);

	/// See the comment for GNeighborFinder::neighbors
	virtual void neighbors(size_t* pOutNeighbors, double* pOutDistances, size_t index);

	/// See the comment for GNeighborFinderGeneralizing::neighbors
	virtual void neighbors(size_t* pOutNeighbors, double* pOutDistances, const double* pInputVector);

	/// Specify the max number of point-vectors to store in each leaf node.
	void setMaxLeafSize(size_t n) { m_maxLeafSize = n; }
	
	/// Returns the root node of the kd-tree.
	GKdNode* root() { return m_pRoot; }
	
	/// Build the tree
	GKdNode* buildTree(size_t count, size_t* pIndexes);

	/// Returns true iff the specified point-vector is on the >= side of the specified pivot
	bool isGreaterOrEqual(const double* pPat, size_t attr, double pivot);

	/// Computes the median distance to the n^th closest neighbor of each row in data
	static double medianDistanceToNeighbor(GMatrix& data, size_t n);

protected:
	/// This is the helper method that finds the neighbors
	void findNeighbors(size_t* pOutNeighbors, double* pOutDistances, const double* pInputVector, size_t nExclude);

	/// Computes a good pivot for the specified attribute, and the goodness of splitting on
	/// that attribute. For continuous attributes, the pivot is the (not scaled) mean and the goodness is
	/// the scaled variance. For nominal attributes, the pivot is the most common value and the
	/// goodness is scaled entropy.
	void computePivotAndGoodness(size_t count, size_t* pIndexes, size_t attr, double* pOutPivot, double* pOutGoodness);

	/// Moves all the indexes that refer to rows that have a value less than pivot in
	/// the specified attribute to the beginning of the list, and the rest to the end. Returns
	/// the number of rows with a value less than the pivot. For nominal values, not-equal
	/// values are moved to the beginning, and equal values are moved to the end.
	size_t splitIndexes(size_t count, size_t* pIndexes, size_t attr, double pivot);
};





/// This finds the shortcuts in a table of neighbors and replaces them with INVALID_INDEX.
class GShortcutPruner
{
protected:
	size_t* m_pNeighborhoods;
	size_t m_n;
	size_t m_k;
	size_t m_cycleThresh;
	size_t m_subGraphRange;
	size_t m_cuts;

public:
	/// pNeighborMap is expected to be an array of size n*k, where n is the
	/// number of points, and k is the number of neighbors.
	GShortcutPruner(size_t* pNeighborhoods, size_t n, size_t k);
	~GShortcutPruner();

#ifndef NO_TEST_CODE
	/// Performs unit tests for this class. Throws an exception if there is a failure.
	static void test();
#endif // NO_TEST_CODE

	/// Sets the cycle-length threshold. (The default is 14.)
	void setCycleThreshold(size_t cycleThresh) { m_cycleThresh = cycleThresh; }

	/// Sets the sub graph range. (The default is 6.)
	void setSubGraphRange(size_t range) { m_subGraphRange = range; }

	/// Do the pruning. Returns the number of shortcuts that were removed.
	/// Any atomic cycles in the graph (where neighbors are treated as bi-directional)
	/// with a cycle-length of cycleThresh or bigger indicates the existence of a shortcut
	/// that must be cut. To determine which edge in the cycle is the shortcut, it will
	/// make a subgraph containing all nodes withing "subGraphRange" hops of any vertex
	/// in the cycle, and compute the betweenness centrality of every edge in the sub-graph.
	/// The edge on the cycle with the largest betweenness is determed to be the shortcut,
	/// and is replaced with INVALID_INDEX.
	size_t prune();

	/// Internal method
	void onDetectBigAtomicCycle(std::vector<size_t>& cycle);

protected:
	bool isEveryNodeReachable();
};





/// This finds the shortcuts in a table of neighbors and replaces them with INVALID_INDEX.
class GCycleCut
{
protected:
	size_t* m_pNeighborhoods;
	GMatrix* m_pPoints;
	std::map<std::pair<size_t, size_t>, double> m_capacities;
	std::vector<size_t> m_cuts;
	size_t m_k;
	size_t m_cycleThresh;
	double m_aveDist;
	size_t m_cutCount;

public:
	/// pNeighborMap is expected to be an array of size n*k, where n is the
	/// number pPoints->rows(), and k is the number of neighbors.
	GCycleCut(size_t* pNeighborhoods, GMatrix* pPoints, size_t k);
	~GCycleCut();

#ifndef NO_TEST_CODE
	/// Performs unit tests for this class. Throws an exception if there is a failure.
	static void test();
#endif // NO_TEST_CODE

	/// Sets the cycle-length threshold. (The default is 14.)
	void setCycleThreshold(size_t cycleThresh) { m_cycleThresh = cycleThresh; }

	/// Do the cutting. Returns the number of edges that were removed.
	/// Any atomic cycles in the graph (where neighbors are treated as bi-directional)
	/// with a cycle-length of cycleThresh or bigger will be cut. It will make the
	/// smallest cut that removes all big atomic cycles
	size_t cut();

	/// Internal method
	void onDetectBigAtomicCycle(std::vector<size_t>& cycle);

protected:
	bool doAnyBigAtomicCyclesExist();
};




/// This class implementes the SAFFRON intelligent neighbor-finding algorithm published in
/// Gashler, Michael S. and Martinez, Tony. Tangent space guided intelligent neighbor finding.
/// In Proceedings of the IEEE International Joint Conference on Neural Networks IJCNN’11,
/// pages 2617–2624, IEEE Press, 2011.
/// This class intelligently selects neighbors for each point in a dataset, such that the neighbors
/// define a good neighborhood for manifold learning. A relaxation technique is used to ensure
/// that neighbors lie on a consistent tangent-space while remaining close to the point. This makes
/// manifold learning possible with difficult (somtimes even self-intersecting) manifolds.
class GSaffron : public GNeighborFinder
{
protected:
	size_t* m_pNeighborhoods;
	double* m_pDistances;
	size_t m_rows;
	double m_learningRate;
	int m_windowSize;
	double m_minImprovement;
	bool m_prune;

public:
	/// General-purpose constructor
	GSaffron(GMatrix* pData, size_t medianCands, size_t neighbors, size_t tangentDims, double sqCorrCap, GRand* pRand);
	virtual ~GSaffron();

	/// See the comment for GNeighborFinder::neighbors
	virtual void neighbors(size_t* pOutNeighbors, size_t index);

	/// See the comment for GNeighborFinder::neighbors
	virtual void neighbors(size_t* pOutNeighbors, double* pOutDistances, size_t index);

	/// Returns the mean number of neighbors for each point. If pDeviation is non-NULL,
	/// it is set to the deviation.
	double meanNeighborCount(double* pDeviation);

protected:
	static double measureAlignment(double* pA, GMatrix* pATan, double* pB, GMatrix* pBTan, double cap, double squaredRadius, GRand* pRand);
};



/// A neighbor finder that specializes in dynamical systems. It determines
/// neighbors by searching for the shortest path of actions between observations,
/// and computes the distance as the number of time-steps in that path.
/// This algorithm was published in Gashler, Michael S. and Martinez, Tony. Temporal
/// nonlinear dimensionality reduction. In Proceedings of the International
/// Joint Conference on Neural Networks IJCNN’11, pages 1959–1966, IEEE Press, 2011.
class GTemporalNeighborFinder : public GNeighborFinder
{
protected:
	GMatrix* m_pPreprocessed;
	GMatrix* m_pActions;
	bool m_ownActionsData;
	std::vector<GSupervisedLearner*> m_consequenceMaps;
	size_t m_maxDims;
	GRand* m_pRand;

public:
	/// pObservations is typically a matrix of high-dimensional observations.
	/// pActions is a matrix of corresponding actions (peformed after the corresponding observation was observed).
	/// If ownActionsData is true, then this object will delete pActions when it is deleted.
	/// This neighbor-finder is somewhat slow in high-dimensional space. Consequently, if
	/// the data has more than maxDims dimensions, it will internally use PCA to reduce it to
	/// maxDims dimensions before computing neighbors. The default is 12.
	GTemporalNeighborFinder(GMatrix* pObservations, GMatrix* pActions, bool ownActionsData, size_t neighborCount, GRand* pRand, size_t maxDims = 12);
	virtual ~GTemporalNeighborFinder();

	/// Computes the neighbors of the specified vector
	virtual void neighbors(size_t* pOutNeighbors, size_t index);

	/// Computes the neighbors and distances of the specified vector
	virtual void neighbors(size_t* pOutNeighbors, double* pOutDistances, size_t index);

protected:
	/// Returns false if distCap is exceeded, or if the results
	/// are too imprecise to be reliable. Otherwise, returns true, and path is set
	/// to contain the number of times that each action must be performed to travel
	/// from point "from" to point "to".
	bool findPath(size_t from, size_t to, double* path, double distCap);

	/// This method uses PCA to reduce pObs to maxDims dimensions.
	/// (If pObs is already small enough, it just returns pObs.)
	GMatrix* preprocessObservations(GMatrix* pObs, size_t maxDims, GRand* pRand);
};




/// A simple neighbor-finder that reports the nearest neighbors in the sequence.
/// (That is, the previous and next rows are the closest neighbors.) The distance
/// is sequential distance to the neighbor (not squared).
class GSequenceNeighborFinder : public GNeighborFinder
{
public:
	GSequenceNeighborFinder(GMatrix* pData, int neighborCount);
	virtual ~GSequenceNeighborFinder();
	/// Computes the neighbors of the specified vector
	virtual void neighbors(size_t* pOutNeighbors, size_t index);

	/// Computes the neighbors and distances of the specified vector
	virtual void neighbors(size_t* pOutNeighbors, double* pOutDistances, size_t index);
};


} // namespace GClasses

#endif // __GNEIGHBORFINDER_H__
