/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __GDECISIONTREE_H__
#define __GDECISIONTREE_H__

#include "GLearner.h"
#include <vector>

namespace GClasses {

class GDecisionTreeNode;
class GRegressionTreeNode;
class GRand;
class GMeanMarginsTreeNode;
class GDecisionTreeLeafNode;
class GBag;


/// This is an efficient learning algorithm. It divides
/// on the attributes that reduce entropy the most, or alternatively
/// can make random divisions.
class GDecisionTree : public GSupervisedLearner
{
public:
	enum DivisionAlgorithm
	{
		MINIMIZE_ENTROPY,
		RANDOM,
	};

protected:
	sp_relation m_pFeatureRel;
	sp_relation m_pLabelRel;
	GDecisionTreeNode* m_pRoot;
	DivisionAlgorithm m_eAlg;
	size_t m_leafThresh;
	size_t m_randomDraws;
	size_t m_maxLevels;

public:
	GDecisionTree(GRand& rand);
#if 0 //Comment out deep-copy constructor
	/// Makes a deep copy of another decision tree.  Also, if pInterestingNode
	/// is non-NULL, then ppOutInterestingNode will return the node that is
	/// a copy of pInterestingNode
	GDecisionTree(GDecisionTree* pThat, GDecisionTreeNode* pInterestingNode, GDecisionTreeNode** ppOutInterestingCopy);
#endif
	/// Loads from a DOM.
	GDecisionTree(GDomNode* pNode, GLearnerLoader& ll);

	virtual ~GDecisionTree();

#ifndef NO_TEST_CODE
	/// Performs unit tests for this class. Throws an exception if there is a failure.
	static void test();
#endif

	/// Marshal this object into a DOM, which can then be converted to a variety of serial formats.
	virtual GDomNode* serialize(GDom* pDoc);

	/// Specifies for this decision tree to use random divisions (instead of
	/// divisions that reduce entropy). Random divisions make the algorithm
	/// train somewhat faster, and also increase model variance, so it is better
	/// suited for ensembles, but random divisions also make the decision tree
	/// vulnerable to problems with irrelevant attributes.
	void useRandomDivisions(size_t randomDraws = 1)
	{
		m_eAlg = RANDOM;
		m_randomDraws = randomDraws;
	}

	/// Returns the leaf threshold.
	size_t leafThresh() { return m_leafThresh; }

	/// Sets the leaf threshold. When the number of samples is <= this value,
	/// it will no longer try to divide the data, but will create a leaf node.
	/// The default value is 1. For noisy data, a larger value may be advantageous.
	void setLeafThresh(size_t n) { m_leafThresh = n; }

	/// Sets the max levels.  When a path from the root to the
	/// current node contains n nodes (including the root), it
	/// will no longer try to divide the data, but will create a
	/// leaf node.  If set to 0, then there is no maximum.  0 is
	/// the default.
	void setMaxLevels(size_t n) { m_maxLevels = n; }

	/// Frees the model
	virtual void clear();

	/// Returns the number of nodes in this tree
	size_t treeSize();

	/// Prints an ascii representation of the decision tree to the specified stream.
	/// pRelation is an optional relation that can be supplied in order to provide
	/// better meta-data to make the print-out richer.
	void print(std::ostream& stream, GArffRelation* pFeatureRel = NULL, GArffRelation* pLabelRel = NULL);

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

	/// Finds the leaf node that corresponds with the specified feature vector
	GDecisionTreeLeafNode* findLeaf(const double* pIn, size_t* pDepth);

	/// A recursive helper method used to construct the decision tree
	GDecisionTreeNode* buildBranch(GMatrix& features, GMatrix& labels, std::vector<size_t>& attrPool, size_t nDepth, size_t tolerance);

	/// InfoGain is defined as the difference in entropy in the data
	/// before and after dividing it based on the specified attribute. For
	/// continuous attributes it uses the difference between the original
	/// variance and the sum of the variances of the two parts after
	/// dividing at the point the maximizes this value.
	double measureInfoGain(GMatrix* pData, size_t nAttribute, double* pPivot);

	size_t pickDivision(GMatrix& features, GMatrix& labels, double* pPivot, std::vector<size_t>& attrPool, size_t nDepth);
};



/// A GMeanMarginsTree is an oblique decision tree specified in
/// Gashler, Michael S. and Giraud-Carrier, Christophe and Martinez, Tony.
/// Decision Tree Ensemble: Small Heterogeneous Is Better Than Large
/// Homogeneous. In The Seventh International Conference on Machine
/// Learning and Applications, Pages 900 - 905, ICMLA '08. 2008.
/// It divides features as follows:
/// It finds the mean and principle component of the output vectors.
/// It divides all the vectors into two groups, one that has a
/// positive dot-product with the principle component (after subtracting
/// the mean) and one that has a negative dot-product with the
/// principle component (after subtracting the mean). Next it finds the
/// average input vector for each of the two groups. Then it finds
/// the mean and principle component of those two vectors. The dividing
/// criteria for this node is to subtract the mean and then see whether
/// the dot-product with the principle component is positive or negative.
class GMeanMarginsTree : public GSupervisedLearner
{
protected:
	size_t m_internalFeatureDims, m_internalLabelDims;
	GMeanMarginsTreeNode* m_pRoot;

public:
	/// nOutputs specifies the number of output dimensions
	GMeanMarginsTree(GRand& rand);

	/// Load from a DOM.
	GMeanMarginsTree(GDomNode* pNode, GLearnerLoader& ll);

	virtual ~GMeanMarginsTree();

#ifndef NO_TEST_CODE
	static void test();
#endif

	/// Marshal this object into a DOM, which can then be converted to a variety of serial formats.
	virtual GDomNode* serialize(GDom* pDoc);

	/// See the comment for GSupervisedLearner::clear
	virtual void clear();

	/// This model has no parameters to tune, so this method is a noop.
	void autoTune(GMatrix& features, GMatrix& labels);

protected:
	GMeanMarginsTreeNode* buildNode(GMatrix& features, GMatrix& labels, double* pBuf, size_t* pBuf2);

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



class GRandomForest : public GSupervisedLearner
{
protected:
	GBag* m_pEnsemble;

public:
	GRandomForest(GRand& rand, size_t trees, size_t samples = 1);
	GRandomForest(GDomNode* pNode, GLearnerLoader& ll);
	virtual ~GRandomForest();

#ifndef NO_TEST_CODE
	static void test();
#endif

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
};

} // namespace GClasses

#endif // __GDECISIONTREE_H__
