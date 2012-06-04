/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#include "GDecisionTree.h"
#include "GError.h"
#include <stdlib.h>
#include "GVec.h"
#include "GPolynomial.h"
#include "GHillClimber.h"
#include "GDistribution.h"
#include "GRand.h"
#include "GDom.h"
#include "GTransform.h"
#include "GEnsemble.h"
#include <string>
#include <iostream>

using namespace GClasses;
using std::cout;
using std::string;
using std::ostream;
using std::ostringstream;
using std::vector;

namespace GClasses {

class GDecisionTreeNode
{
public:
	GDecisionTreeNode()
	{
	}

	virtual ~GDecisionTreeNode()
	{
	}

	virtual bool IsLeaf() = 0;
	virtual size_t GetBranchSize() = 0;
	virtual GDecisionTreeNode* DeepCopy(size_t nOutputCount, GDecisionTreeNode* pInterestingNode, GDecisionTreeNode** ppOutInterestingCopy) = 0;
	virtual void print(GRelation* pFeatureRel, GRelation* pLabelRel, ostream& stream, size_t depth, const char* parentValue) = 0;
	virtual void CountValues(size_t nOutput, size_t* pnCounts) = 0;
	virtual double FindSumOutputValue(size_t nOutput) = 0;
	static GDecisionTreeNode* deserialize(GDomNode* pNode);
	virtual GDomNode* serialize(GDom* pDoc, size_t outputCount) = 0;
};

class GDecisionTreeInteriorNode : public GDecisionTreeNode
{
friend class GDecisionTree;
protected:
	size_t m_nAttribute;
	double m_dPivot;
	size_t m_nChildren;
	size_t m_defaultChild;
	GDecisionTreeNode** m_ppChildren;

public:
	GDecisionTreeInteriorNode(size_t nAttribute, double dPivot, size_t children, size_t defaultChild)
	: GDecisionTreeNode(), m_nAttribute(nAttribute), m_dPivot(dPivot), m_nChildren(children), m_defaultChild(defaultChild)
	{
		m_ppChildren = new GDecisionTreeNode*[children];
		memset(m_ppChildren, '\0', sizeof(GDecisionTreeNode*) * children);
	}

	GDecisionTreeInteriorNode(GDomNode* pNode) : GDecisionTreeNode()
	{
		m_nAttribute = (size_t)pNode->field("attr")->asInt();
		m_dPivot = pNode->field("pivot")->asDouble();
		GDomNode* pChildren = pNode->field("children");
		GDomListIterator it(pChildren);
		m_nChildren = it.remaining();
		m_ppChildren = new GDecisionTreeNode*[m_nChildren];
		for(size_t i = 0 ; it.current(); i++)
		{
			m_ppChildren[i] = GDecisionTreeNode::deserialize(it.current());
			it.advance();
		}
		m_defaultChild = (size_t)pNode->field("def")->asInt();
	}

	virtual ~GDecisionTreeInteriorNode()
	{
		if(m_ppChildren)
		{
			for(size_t n = 0; n < m_nChildren; n++)
				delete(m_ppChildren[n]);
			delete[] m_ppChildren;
		}
	}

	virtual GDomNode* serialize(GDom* pDoc, size_t outputCount)
	{
		GDomNode* pNode = pDoc->newObj();
		pNode->addField(pDoc, "attr", pDoc->newInt(m_nAttribute));
		pNode->addField(pDoc, "pivot", pDoc->newDouble(m_dPivot));
		GDomNode* pChildren = pDoc->newList();
		pNode->addField(pDoc, "children", pChildren);
		for(size_t i = 0; i < m_nChildren; i++)
			pChildren->addItem(pDoc, m_ppChildren[i]->serialize(pDoc, outputCount));
		pNode->addField(pDoc, "def", pDoc->newInt(m_defaultChild));
		return pNode;
	}

	virtual bool IsLeaf() { return false; }

	virtual size_t GetBranchSize()
	{
		size_t size = 1;
		size_t i;
		for(i = 0; i < m_nChildren; i++)
			size += m_ppChildren[i]->GetBranchSize();
		return size;
	}

	virtual GDecisionTreeNode* DeepCopy(size_t nOutputCount, GDecisionTreeNode* pInterestingNode, GDecisionTreeNode** ppOutInterestingCopy)
	{
		GDecisionTreeInteriorNode* pNewNode = new GDecisionTreeInteriorNode(m_nAttribute, m_dPivot, m_nChildren, m_defaultChild);
		for(size_t n = 0; n < m_nChildren; n++)
			pNewNode->m_ppChildren[n] = m_ppChildren[n]->DeepCopy(nOutputCount, pInterestingNode, ppOutInterestingCopy);
		if(this == pInterestingNode)
			*ppOutInterestingCopy = pNewNode;
		return pNewNode;
	}

	virtual void print(GRelation* pFeatureRel, GRelation* pLabelRel, ostream& stream, size_t depth, const char* parentValue)
	{
		for(size_t n = 0; n < depth; n++)
			stream << "  ";
		if(parentValue)
			stream << parentValue << " -> ";
		if(pFeatureRel->valueCount(m_nAttribute) == 0)
		{
			ostringstream oss;
			pFeatureRel->printAttrValue(oss, m_nAttribute, m_dPivot);
			string s = oss.str();
			if(pFeatureRel->type() == GRelation::ARFF)
				stream << "Is " << ((GArffRelation*)pFeatureRel)->attrName(m_nAttribute) << " < " << s.c_str() << "?\n";
			else
				stream << "Is attr " << m_nAttribute << " < " << s.c_str() << "?\n";
			if(m_nChildren != 2)
				ThrowError("expected this node to have two child nodes");
			m_ppChildren[0]->print(pFeatureRel, pLabelRel, stream, depth + 1, "Yes");
			m_ppChildren[1]->print(pFeatureRel, pLabelRel, stream, depth + 1, "No");
		}
		else
		{
			if(pFeatureRel->type() == GRelation::ARFF)
				stream << "What is the value of " << ((GArffRelation*)pFeatureRel)->attrName(m_nAttribute) << "?\n";
			else
				stream << "What is the value of attr " << m_nAttribute << "?\n";
			for(size_t n = 0; n < m_nChildren; n++)
			{
				ostringstream oss;
				pFeatureRel->printAttrValue(oss, m_nAttribute, (double)n);
				string s = oss.str();
				m_ppChildren[n]->print(pFeatureRel, pLabelRel, stream, depth + 1, s.c_str());
			}
		}
	}

	// Recursive function that counts the number of times a particular
	// value is found in a particular output in this branch of the tree
	virtual void CountValues(size_t nOutput, size_t* pnCounts)
	{
		for(size_t n = 0; n < m_nChildren; n++)
			m_ppChildren[n]->CountValues(nOutput, pnCounts);
	}

	virtual double FindSumOutputValue(size_t nOutput)
	{
		double dSum = 0;
		for(size_t n = 0; n < m_nChildren; n++)
			dSum += m_ppChildren[n]->FindSumOutputValue(nOutput);
		return dSum;
	}
};

class GDecisionTreeLeafNode : public GDecisionTreeNode
{
public:
	double* m_pOutputValues;
	size_t m_nSampleSize;

public:
	GDecisionTreeLeafNode(double* pOutputValues, size_t nSampleSize) : GDecisionTreeNode()
	{
		m_pOutputValues = pOutputValues;
		m_nSampleSize = nSampleSize;
	}

	GDecisionTreeLeafNode(GDomNode* pNode) : GDecisionTreeNode()
	{
		m_nSampleSize = (size_t)pNode->field("size")->asInt();
		GDomNode* pOut = pNode->field("out");
		GDomListIterator it(pOut);
		size_t count = it.remaining();
		m_pOutputValues = new double[count];
		for(size_t i = 0; it.current(); i++)
		{
			m_pOutputValues[i] = it.current()->asDouble();
			it.advance();
		}
	}

	virtual ~GDecisionTreeLeafNode()
	{
		delete[] m_pOutputValues;
	}

	virtual GDomNode* serialize(GDom* pDoc, size_t outputCount)
	{
		GDomNode* pNode = pDoc->newObj();
		pNode->addField(pDoc, "size", pDoc->newInt(m_nSampleSize));
		GDomNode* pOut = pDoc->newList();
		pNode->addField(pDoc, "out", pOut);
		for(size_t i = 0; i < outputCount; i++)
			pOut->addItem(pDoc, pDoc->newDouble(m_pOutputValues[i]));
		return pNode;
	}

	virtual bool IsLeaf() { return true; }

	virtual size_t GetBranchSize()
	{
		return 1;
	}

	virtual GDecisionTreeNode* DeepCopy(size_t nOutputCount, GDecisionTreeNode* pInterestingNode, GDecisionTreeNode** ppOutInterestingCopy)
	{
		double* pOutputValues = new double[nOutputCount];
		GVec::copy(pOutputValues, m_pOutputValues, nOutputCount);
		GDecisionTreeLeafNode* pNewNode = new GDecisionTreeLeafNode(pOutputValues, m_nSampleSize);
		if(this == pInterestingNode)
			*ppOutInterestingCopy = pNewNode;
		return pNewNode;
	}

	virtual void print(GRelation* pFeatureRel, GRelation* pLabelRel, ostream& stream, size_t depth, const char* parentValue)
	{
		for(size_t n = 0; n < depth; n++)
			stream << "  ";
		if(parentValue)
			stream << parentValue << " -> ";
		for(size_t n = 0; n < pLabelRel->size(); n++)
		{
			if(n > 0)
				stream << ", ";
			ostringstream oss;
			pLabelRel->printAttrValue(oss, n, m_pOutputValues[n]);
			string s = oss.str();
			if(pLabelRel->type() == GRelation::ARFF)
				stream << ((GArffRelation*)pLabelRel)->attrName(n) << "=" << s.c_str();
			else
				stream << s.c_str();
		}
		stream << "\n";
	}

	virtual void CountValues(size_t nOutput, size_t* pnCounts)
	{
		int nVal = (int)m_pOutputValues[nOutput];
		pnCounts[nVal] += m_nSampleSize;
	}

	virtual double FindSumOutputValue(size_t nOutput)
	{
		return m_pOutputValues[nOutput] * m_nSampleSize;
	}
};

}

// static
GDecisionTreeNode* GDecisionTreeNode::deserialize(GDomNode* pNode)
{
	if(pNode->fieldIfExists("children"))
		return new GDecisionTreeInteriorNode(pNode);
	else
		return new GDecisionTreeLeafNode(pNode);
}

// -----------------------------------------------------------------

GDecisionTree::GDecisionTree(GRand& rand)
: GSupervisedLearner(rand), m_leafThresh(1), m_maxLevels(0)
{
	m_pRoot = NULL;
	m_eAlg = GDecisionTree::MINIMIZE_ENTROPY;
}
/*
GDecisionTree::GDecisionTree(GDecisionTree* pThat, GDecisionTreeNode* pInterestingNode, GDecisionTreeNode** ppOutInterestingCopy)
: GSupervisedLearner(), m_leafThresh(1), m_maxLevels(0)
{
	if(!m_pLabelRel.get())
		ThrowError("not trained");
	m_pRoot = pThat->m_pRoot->DeepCopy(pThat->m_labelDims, pInterestingNode, ppOutInterestingCopy);
}
*/
GDecisionTree::GDecisionTree(GDomNode* pNode, GLearnerLoader& ll)
: GSupervisedLearner(pNode, ll), m_leafThresh(1), m_maxLevels(0)
{
	m_pFeatureRel = GRelation::deserialize(pNode->field("frel"));
	m_pLabelRel = GRelation::deserialize(pNode->field("lrel"));
	m_eAlg = (DivisionAlgorithm)pNode->field("alg")->asInt();
	m_pRoot = GDecisionTreeNode::deserialize(pNode->field("root"));
}

// virtual
GDecisionTree::~GDecisionTree()
{
	clear();
}

// virtual
GDomNode* GDecisionTree::serialize(GDom* pDoc)
{
	if(!m_pRoot)
		ThrowError("Attempted to serialize a model that has not been trained");
	GDomNode* pNode = baseDomNode(pDoc, "GDecisionTree");
	pNode->addField(pDoc, "frel", m_pFeatureRel->serialize(pDoc));
	pNode->addField(pDoc, "lrel", m_pLabelRel->serialize(pDoc));
	pNode->addField(pDoc, "alg", pDoc->newInt(m_eAlg));
	pNode->addField(pDoc, "root", m_pRoot->serialize(pDoc, m_pLabelRel->size()));
	return pNode;
}

size_t GDecisionTree::treeSize()
{
	return m_pRoot->GetBranchSize();
}

void GDecisionTree::print(ostream& stream, GArffRelation* pFeatureRel, GArffRelation* pLabelRel)
{
	if(!m_pRoot)
		ThrowError("not trained yet");
	GRelation* pFRel = pFeatureRel;
	GRelation* pLRel = pLabelRel;
	if(!pFRel)
		pFRel = m_pFeatureRel.get();
	if(!pLRel)
		pLRel = m_pLabelRel.get();
	m_pRoot->print(pFRel, pLRel, stream, 0, NULL);
}

// virtual
void GDecisionTree::trainInner(GMatrix& features, GMatrix& labels)
{
	m_pFeatureRel = features.relation();
	m_pLabelRel = labels.relation();
	clear();

	// Make a list of available features
	vector<size_t> attrPool;
	attrPool.reserve(m_pFeatureRel->size());
	for(size_t i = 0; i < m_pFeatureRel->size(); i++)
		attrPool.push_back(i);

	// We must make a copy of the features because buildNode will mess with it
	// by calling RandomlyReplaceMissinGMatrix
	GMatrix tmpFeatures(m_pFeatureRel, features.heap());
	tmpFeatures.copy(&features);
	GMatrix tmpLabels(m_pLabelRel, labels.heap());
	tmpLabels.copy(&labels);

	m_pRoot = buildBranch(tmpFeatures, tmpLabels, attrPool, 0/*depth*/, 4/*tolerance*/);
}

void GDecisionTree::autoTune(GMatrix& features, GMatrix& labels)
{
	// Find the best leaf threshold
	size_t cap = size_t(floor(sqrt(double(features.rows()))));
	size_t bestLeafThresh = 1;
	double bestErr = 1e308;
	for(size_t i = 1; i < cap; i = std::max(i + 1, size_t(i * 1.5)))
	{
		m_leafThresh = i;
		double d = heuristicValidate(features, labels);
		if(d < bestErr)
		{
			bestErr = d;
			bestLeafThresh = i;
		}
		else if(i >= 27)
			break;
	}

	// Set the best values
	m_maxLevels = 0;
	m_leafThresh = bestLeafThresh;
}

double GDecisionTree_measureRealSplitInfo(GMatrix& features, GMatrix& labels, GMatrix& tmpFeatures, GMatrix& tmpLabels, size_t attr, double pivot)
{
	GAssert(tmpFeatures.rows() == 0 && tmpLabels.rows() == 0);
	size_t rowCount = features.rows();
	features.splitByPivot(&tmpFeatures, attr, pivot, &labels, &tmpLabels);
	double d;
	if(features.rows() > 0 && tmpLabels.rows() > 0)
		d = (labels.measureInfo() * labels.rows() + tmpLabels.measureInfo() * tmpLabels.rows()) / rowCount;
	else
		d = 1e308;
	features.mergeVert(&tmpFeatures);
	labels.mergeVert(&tmpLabels);
	return d;
}

double GDecisionTree_pickPivotToReduceInfo(GMatrix& features, GMatrix& labels, GMatrix& tmpFeatures, GMatrix& tmpLabels, double* pPivot, size_t attr, GRand* pRand)
{
	size_t nRows = features.rows();
	double bestPivot = UNKNOWN_REAL_VALUE;
	double bestInfo = 1e100;
	double* pRow1;
	double* pRow2;
	size_t attempts = std::min(features.rows() - 1, (features.rows() * features.cols() > 100000 ? (size_t)1 : (size_t)8));
	for(size_t n = 0; n < attempts; n++)
	{
		pRow1 = features.row((size_t)pRand->next(nRows));
		pRow2 = features.row((size_t)pRand->next(nRows));
		double pivot = 0.5 * (pRow1[attr] + pRow2[attr]);
		double info = GDecisionTree_measureRealSplitInfo(features, labels, tmpFeatures, tmpLabels, attr, pivot);
		if(info + 1e-14 < bestInfo) // the small value makes it deterministic across hardware
		{
			bestInfo = info;
			bestPivot = pivot;
		}
	}
	*pPivot = bestPivot;
	return bestInfo;
}

double GDecisionTree_measureNominalSplitInfo(GMatrix& features, GMatrix& labels, GMatrix& tmpFeatures, GMatrix& tmpLabels, size_t nAttribute)
{
	size_t nRowCount = features.rows() - features.countValue(nAttribute, UNKNOWN_DISCRETE_VALUE);
	int values = (int)features.relation()->valueCount(nAttribute);
	double dInfo = 0;
	for(int n = 0; n < values; n++)
	{
		features.splitByNominalValue(&tmpFeatures, nAttribute, n, &labels, &tmpLabels);
		dInfo += ((double)tmpLabels.rows() / nRowCount) * tmpLabels.measureInfo();
		features.mergeVert(&tmpFeatures);
		labels.mergeVert(&tmpLabels);
	}
	return dInfo;
}

size_t GDecisionTree::pickDivision(GMatrix& features, GMatrix& labels, double* pPivot, vector<size_t>& attrPool, size_t nDepth)
{
	GMatrix tmpFeatures(features.relation(), features.heap());
	tmpFeatures.reserve(features.rows());
	GMatrix tmpLabels(labels.relation(), labels.heap());
	tmpLabels.reserve(features.rows());
	if(m_eAlg == MINIMIZE_ENTROPY)
	{
		// Pick the best attribute to divide on
		GAssert(labels.rows() > 0); // Can't work without data
		double bestInfo = 1e100;
		double pivot = 0.0;
		double bestPivot = 0;
		size_t index = 0;
		size_t bestIndex = attrPool.size();
		for(vector<size_t>::iterator it = attrPool.begin(); it != attrPool.end(); it++)
		{
			double info;
			if(features.relation()->valueCount(*it) == 0)
				info = GDecisionTree_pickPivotToReduceInfo(features, labels, tmpFeatures, tmpLabels, &pivot, *it, &m_rand);
			else
				info = GDecisionTree_measureNominalSplitInfo(features, labels, tmpFeatures, tmpLabels, *it);
			if(info + 1e-14 < bestInfo) // the small value makes it deterministic across hardware
			{
				bestInfo = info;
				bestIndex = index;
				bestPivot = pivot;
			}
			index++;
		}
		*pPivot = bestPivot;
		return bestIndex;
	}
	else if(m_eAlg == RANDOM)
	{
		// Pick the best of m_randomDraws random attributes from the attribute pool
		GAssert(features.rows() > 0); // Can't work without data
		double bestInfo = 1e200;
		double bestPivot = 0;
		size_t bestIndex = attrPool.size();
		size_t patience = std::max((size_t)6, m_randomDraws * 2);
		for(size_t i = 0; i < m_randomDraws && patience > 0; i++)
		{
			size_t index = (size_t)m_rand.next(attrPool.size());
			size_t attr = attrPool[index];
			double pivot = 0.0;
			double info;
			if(features.relation()->valueCount(attr) == 0)
			{
				// Pick a random pivot. (Note that this is not a uniform distribution. This
				// distribution is biased in favor of pivots that will divide the data well.)
				double a = features[(size_t)m_rand.next(features.rows())][attr];
				double b = features[(size_t)m_rand.next(features.rows())][attr];
				pivot = 0.5 * (a + b);
				if(m_randomDraws > 1)
					info = GDecisionTree_measureRealSplitInfo(features, labels, tmpFeatures, tmpLabels, attr, pivot);
				else
					info = 0.0;
			}
			else
			{
				if(m_randomDraws > 1)
					info = GDecisionTree_measureNominalSplitInfo(features, labels, tmpFeatures, tmpLabels, attr);
				else
					info = 0.0;
			}
			if(info + 1e-14 < bestInfo) // the small value makes it deterministic across hardware
			{
				bestInfo = info;
				bestIndex = index;
				bestPivot = pivot;
			}
		}
		if(bestIndex < attrPool.size() && !features.isAttrHomogenous(attrPool[bestIndex]))
		{
			*pPivot = bestPivot;
			return bestIndex;
		}

		// We failed to find a useful attribute with random draws. (This may happen if there is a large
		// ratio of homogenous attributes.) Now, we need to be a little more systematic about finding a good
		// attribute. (This is not specified in the random forest algorithm, but it can make a big difference
		// with some problems.)
		size_t k = (size_t)m_rand.next(attrPool.size());
		for(size_t i = 0; i < attrPool.size(); i++)
		{
			size_t index = (i + k) % attrPool.size();
			size_t attr = attrPool[index];
			if(features.relation()->valueCount(attr) == 0)
			{
				// Find the min
				double m = 1e300;
				for(size_t j = 0; j < features.rows(); j++)
				{
					double d = features[j][attr];
					if(d != UNKNOWN_REAL_VALUE)
						m = std::min(m, d);
				}

				// Randomly pick one of the non-min values
				size_t candidates = 0;
				for(size_t j = 0; j < features.rows(); j++)
				{
					double d = features[j][attr];
					if(d != UNKNOWN_REAL_VALUE && d > m)
					{
						if(m_rand.next(++candidates) == 0)
							*pPivot = d;
					}
				}
				if(candidates == 0)
					continue; // This attribute is worthless
			}
			else
			{
				if(features.isAttrHomogenous(attr))
					continue; // This attribute is worthless
			}
			return index;
		}
	}
	else
		GAssert(false); // unknown division algorithm
	return attrPool.size();
}

double* GDecisionTreeNode_labelVec(GMatrix& labels)
{
	size_t n = labels.cols();
	double* pVec = new double[n];
	for(size_t i = 0; i < n; i++)
		pVec[i] = labels.baselineValue(i);
	return pVec;
}

double* GDecisionTreeNode_labelVec(GMatrix& labels, GMatrixArray& parts)
{
	GMatrix* pB = parts.sets()[parts.largestSet()];
	if(labels.rows() > pB->rows())
		return GDecisionTreeNode_labelVec(labels);
	else
		return GDecisionTreeNode_labelVec(*pB);
}

// This constructs the decision tree in a recursive depth-first manner
GDecisionTreeNode* GDecisionTree::buildBranch(GMatrix& features, GMatrix& labels, vector<size_t>& attrPool, size_t nDepth, size_t tolerance)
{
	GAssert(features.rows() == labels.rows());

	// Make a leaf if we're out of tolerance or the output is
	// homogenous or there are no attributes left or we have
	// reached the maximum number of levels in the tree
	if(tolerance <= 0 || features.rows() <= m_leafThresh 
	   || attrPool.size() == 0 || labels.isHomogenous() 
	   || (nDepth+1 == m_maxLevels)){
	  return new GDecisionTreeLeafNode(GDecisionTreeNode_labelVec(labels), 
					   labels.rows());
	}

	// Pick the division
	double pivot = 0.0;
	size_t bestIndex = pickDivision(features, labels, &pivot, attrPool, nDepth);

	// Make a leaf if there are no good divisions
	if(bestIndex >= attrPool.size()){
	  return new GDecisionTreeLeafNode(GDecisionTreeNode_labelVec(labels), 
					   labels.rows());
	}
	size_t attr = attrPool[bestIndex];

	// Make sure there aren't any missing values in the decision attribute
	features.replaceMissingValuesWithBaseline(attr);

	// Split the data
	GMatrixArray featureParts(m_pFeatureRel);
	GMatrixArray labelParts(m_pLabelRel);
	size_t nonEmptyBranchCount = 0;
	if(m_pFeatureRel->valueCount(attr) == 0)
	{
		// Split on a continuous attribute
		GMatrix* pOtherFeatures = featureParts.newSet(0);
		GMatrix* pOtherLabels = labelParts.newSet(0);
		features.splitByPivot(pOtherFeatures, attr, pivot, &labels, pOtherLabels);
		nonEmptyBranchCount += (features.rows() > 0 ? 1 : 0) + (pOtherFeatures->rows() > 0 ? 1 : 0);
	}
	else
	{
		// Split on a nominal attribute
		int valueCount = (int)features.relation()->valueCount(attr);
		for(int i = 1; i < valueCount; i++)
		{
			GMatrix* pOtherFeatures = featureParts.newSet(0);
			GMatrix* pOtherLabels = labelParts.newSet(0);
			features.splitByNominalValue(pOtherFeatures, attr, i, &labels, pOtherLabels);
			if(pOtherFeatures->rows() > 0)
				nonEmptyBranchCount++;
		}
		if(features.rows() > 0)
			nonEmptyBranchCount++;

		// Remove this attribute from the pool of available attributes
		std::swap(attrPool[bestIndex], attrPool[attrPool.size() - 1]);
		attrPool.erase(attrPool.end() - 1);
	}

	// If we didn't actually separate anything
	if(nonEmptyBranchCount < 2)
	{
		size_t setCount = featureParts.sets().size();
		for(size_t i = 0; i < setCount; i++)
		{
			features.mergeVert(featureParts.sets()[i]);
			labels.mergeVert(labelParts.sets()[i]);
		}
		if(m_eAlg == MINIMIZE_ENTROPY)
			return new GDecisionTreeLeafNode(GDecisionTreeNode_labelVec(labels), labels.rows());
		else
		{
			// Try another division
			GDecisionTreeNode* pNode = buildBranch(features, labels, attrPool, nDepth, tolerance - 1);
			attrPool.push_back(attr);
			return pNode;
		}
	}

	// Make an interior node
	GDecisionTreeInteriorNode* pNode = new GDecisionTreeInteriorNode(attr, pivot, featureParts.sets().size() + 1, 0);
	Holder<GDecisionTreeInteriorNode> hNode(pNode);
	size_t biggest = features.rows();
	if(features.rows() > 0){
		pNode->m_ppChildren[0] = buildBranch(features, labels, attrPool, nDepth + 1, tolerance);
	}else{
		pNode->m_ppChildren[0] = new GDecisionTreeLeafNode(GDecisionTreeNode_labelVec(labels, labelParts), 0);
	}
	for(size_t i = 0; i < featureParts.sets().size(); i++)
	{
		if(featureParts.sets()[i]->rows() > 0)
		{
			pNode->m_ppChildren[i + 1] = buildBranch(*featureParts.sets()[i], *labelParts.sets()[i], attrPool, nDepth + 1, tolerance);
			if(featureParts.sets()[i]->rows() > biggest)
			{
				biggest = featureParts.sets()[i]->rows();
				pNode->m_defaultChild = i + 1;
			}
		}
		else
			pNode->m_ppChildren[i + 1] = new GDecisionTreeLeafNode(GDecisionTreeNode_labelVec(labels, labelParts), 0);
	}
	attrPool.push_back(attr);
	return hNode.release();
}

GDecisionTreeLeafNode* GDecisionTree::findLeaf(const double* pIn, size_t* pDepth)
{
	if(!m_pRoot)
		ThrowError("Not trained yet");
	GDecisionTreeNode* pNode = m_pRoot;
	int nVal;
	size_t nDepth = 1;
	while(!pNode->IsLeaf())
	{
		GDecisionTreeInteriorNode* pInterior = (GDecisionTreeInteriorNode*)pNode;
		if(m_pFeatureRel->valueCount(pInterior->m_nAttribute) == 0)
		{
			if(pIn[pInterior->m_nAttribute] == UNKNOWN_REAL_VALUE)
				pNode = pInterior->m_ppChildren[pInterior->m_defaultChild];
			else if(pIn[pInterior->m_nAttribute] < pInterior->m_dPivot)
				pNode = pInterior->m_ppChildren[0];
			else
				pNode = pInterior->m_ppChildren[1];
		}
		else
		{
			nVal = (int)pIn[pInterior->m_nAttribute];
			if(nVal < 0)
			{
				GAssert(nVal == UNKNOWN_DISCRETE_VALUE); // out of range
				nVal = (int)pInterior->m_defaultChild;
			}
			GAssert((size_t)nVal < m_pFeatureRel->valueCount(pInterior->m_nAttribute)); // value out of range
			pNode = pInterior->m_ppChildren[nVal];
		}
		nDepth++;
	}
	*pDepth = nDepth;
	return (GDecisionTreeLeafNode*)pNode;
}

// virtual
void GDecisionTree::predictInner(const double* pIn, double* pOut)
{
	size_t depth;
	GDecisionTreeLeafNode* pLeaf = findLeaf(pIn, &depth);
	GVec::copy(pOut, pLeaf->m_pOutputValues, m_pLabelRel->size());
}

// virtual
void GDecisionTree::predictDistributionInner(const double* pIn, GPrediction* pOut)
{
	// Copy the output values into the row
	size_t depth;
	GDecisionTreeLeafNode* pLeaf = findLeaf(pIn, &depth);
	size_t n, nValues;
	size_t labelDims = m_pLabelRel->size();
	for(n = 0; n < labelDims; n++)
	{
		nValues = m_pLabelRel->valueCount(n);
		if(nValues == 0)
			pOut[n].makeNormal()->setMeanAndVariance(pLeaf->m_pOutputValues[n], (double)depth);
		else
			pOut[n].makeCategorical()->setSpike(nValues, (int)pLeaf->m_pOutputValues[n], (int)depth);
	}
}

// virtual
void GDecisionTree::clear()
{
	delete(m_pRoot);
	m_pRoot = NULL;
}

#ifndef NO_TEST_CODE
// static
void GDecisionTree::test()
{
	{
		GRand rand(0);
		GDecisionTree tree(rand);
		tree.basicTest(0.71, 0.77);
	}
	{
		GRand rand(0);
		GDecisionTree ml2Tree(rand);
		ml2Tree.setMaxLevels(2);
		ml2Tree.basicTest(0.57, 0.65);
	}
	{
		GRand rand(0);
		GDecisionTree ml1Tree(rand);
		ml1Tree.setMaxLevels(1);
		ml1Tree.basicTest(0.33, 0.33);
	}
}
#endif

// ----------------------------------------------------------------------

namespace GClasses {
class GMeanMarginsTreeNode
{
public:
	GMeanMarginsTreeNode()
	{
	}

	virtual ~GMeanMarginsTreeNode()
	{
	}

	virtual bool IsLeaf() = 0;
	virtual GDomNode* serialize(GDom* pDoc, size_t nInputs, size_t nOutputs) = 0;

	static GMeanMarginsTreeNode* deserialize(GDomNode* pNode);
};


class GMeanMarginsTreeInteriorNode : public GMeanMarginsTreeNode
{
protected:
	double* m_pCenter;
	double* m_pNormal;
	GMeanMarginsTreeNode* m_pLeft;
	GMeanMarginsTreeNode* m_pRight;

public:
	GMeanMarginsTreeInteriorNode(size_t featureDims, const double* pCenter, const double* pNormal)
	: GMeanMarginsTreeNode()
	{
		m_pCenter = new double[featureDims];
		GVec::copy(m_pCenter, pCenter, featureDims);
		m_pNormal = new double[featureDims];
		GVec::copy(m_pNormal, pNormal, featureDims);
		m_pLeft = NULL;
		m_pRight = NULL;
	}

	GMeanMarginsTreeInteriorNode(GDomNode* pNode)
	: GMeanMarginsTreeNode()
	{
		GDomNode* pCenter = pNode->field("center");
		GDomListIterator it(pCenter);
		size_t dims = it.remaining();
		m_pCenter = new double[dims];
		GVec::deserialize(m_pCenter, it);
		GDomListIterator it2(pNode->field("normal"));
		m_pNormal = new double[it2.remaining()];
		GAssert(it2.remaining() == dims);
		GVec::deserialize(m_pNormal, it2);
		m_pLeft = GMeanMarginsTreeNode::deserialize(pNode->field("left"));
		m_pRight = GMeanMarginsTreeNode::deserialize(pNode->field("right"));
	}

	virtual ~GMeanMarginsTreeInteriorNode()
	{
		delete[] m_pCenter;
		delete[] m_pNormal;
		delete(m_pLeft);
		delete(m_pRight);
	}

	virtual GDomNode* serialize(GDom* pDoc, size_t nInputs, size_t nOutputs)
	{
		GDomNode* pNode = pDoc->newObj();
		pNode->addField(pDoc, "center", GVec::serialize(pDoc, m_pCenter, nInputs));
		pNode->addField(pDoc, "normal", GVec::serialize(pDoc, m_pNormal, nInputs));
		pNode->addField(pDoc, "left", m_pLeft->serialize(pDoc, nInputs, nOutputs));
		pNode->addField(pDoc, "right", m_pRight->serialize(pDoc, nInputs, nOutputs));
		return pNode;
	}

	virtual bool IsLeaf()
	{
		return false;
	}

	bool Test(const double* pInputVector, size_t nInputs)
	{
		return GVec::dotProductIgnoringUnknowns(m_pCenter, pInputVector, m_pNormal, nInputs) >= 0;
	}

	void SetLeft(GMeanMarginsTreeNode* pNode)
	{
		m_pLeft = pNode;
	}

	void SetRight(GMeanMarginsTreeNode* pNode)
	{
		m_pRight = pNode;
	}

	GMeanMarginsTreeNode* GetRight()
	{
		return m_pRight;
	}

	GMeanMarginsTreeNode* GetLeft()
	{
		return m_pLeft;
	}
};

class GMeanMarginsTreeLeafNode : public GMeanMarginsTreeNode
{
protected:
	double* m_pOutputs;

public:
	GMeanMarginsTreeLeafNode(size_t nOutputCount, double* pOutputs)
	: GMeanMarginsTreeNode()
	{
		m_pOutputs = new double[nOutputCount];
		GVec::copy(m_pOutputs, pOutputs, nOutputCount);
	}

	GMeanMarginsTreeLeafNode(GDomNode* pNode)
	: GMeanMarginsTreeNode()
	{
		GDomListIterator it(pNode);
		size_t dims = it.remaining();
		m_pOutputs = new double[dims];
		GVec::deserialize(m_pOutputs, it);
	}

	virtual ~GMeanMarginsTreeLeafNode()
	{
		delete[] m_pOutputs;
	}

	virtual GDomNode* serialize(GDom* pDoc, size_t nInputs, size_t nOutputs)
	{
		return GVec::serialize(pDoc, m_pOutputs, nOutputs);
	}

	virtual bool IsLeaf()
	{
		return true;
	}

	double* GetOutputs()
	{
		return m_pOutputs;
	}
};
}

// static
GMeanMarginsTreeNode* GMeanMarginsTreeNode::deserialize(GDomNode* pNode)
{
	if(pNode->type() == GDomNode::type_list)
		return new GMeanMarginsTreeLeafNode(pNode);
	else
		return new GMeanMarginsTreeInteriorNode(pNode);
}

// ---------------------------------------------------------------

GMeanMarginsTree::GMeanMarginsTree(GRand& rand)
: GSupervisedLearner(rand), m_internalFeatureDims(0), m_internalLabelDims(0), m_pRoot(NULL)
{
}

GMeanMarginsTree::GMeanMarginsTree(GDomNode* pNode, GLearnerLoader& ll)
: GSupervisedLearner(pNode, ll)
{
	m_pRoot = GMeanMarginsTreeNode::deserialize(pNode->field("root"));
	m_internalFeatureDims = (size_t)pNode->field("ifd")->asInt();
	m_internalLabelDims = (size_t)pNode->field("ild")->asInt();
}

GMeanMarginsTree::~GMeanMarginsTree()
{
	delete(m_pRoot);
}

// virtual
GDomNode* GMeanMarginsTree::serialize(GDom* pDoc)
{
	GDomNode* pNode = baseDomNode(pDoc, "GMeanMarginsTree");
	pNode->addField(pDoc, "ifd", pDoc->newInt(m_internalFeatureDims));
	pNode->addField(pDoc, "ild", pDoc->newInt(m_internalLabelDims));
	if(!m_pRoot)
		ThrowError("Attempted to serialize a model that has not been trained");
	pNode->addField(pDoc, "root", m_pRoot->serialize(pDoc, m_internalFeatureDims, m_internalLabelDims));
	return pNode;
}

// virtual
void GMeanMarginsTree::trainInner(GMatrix& features, GMatrix& labels)
{
	clear();
	m_internalFeatureDims = features.cols();
	m_internalLabelDims = labels.cols();
	double* pBuf = new double[m_internalLabelDims * 2 + m_internalFeatureDims * 2];
	ArrayHolder<double> hBuf(pBuf);
	size_t* pBuf2 = new size_t[m_internalFeatureDims * 2];
	ArrayHolder<size_t> hBuf2(pBuf2);
	m_pRoot = buildNode(features, labels, pBuf, pBuf2);
}

void GMeanMarginsTree::autoTune(GMatrix& features, GMatrix& labels)
{
	// This model has no parameters to tune
}

GMeanMarginsTreeNode* GMeanMarginsTree::buildNode(GMatrix& features, GMatrix& labels, double* pBuf, size_t* pBuf2)
{
	// Check for a leaf node
	GAssert(features.rows() == labels.rows());
	size_t nCount = features.rows();
	if(nCount < 2)
	{
		GAssert(nCount > 0); // no data left
		return new GMeanMarginsTreeLeafNode(m_internalLabelDims, labels[0]);
	}

	// Compute the centroid and principal component of the labels
	double* pLabelCentroid = pBuf;
	labels.centroid(pLabelCentroid);
	double* pPrincipalComponent = pLabelCentroid + m_internalLabelDims;
	labels.principalComponentIgnoreUnknowns(pPrincipalComponent, m_internalLabelDims, pLabelCentroid, &m_rand);

	// Compute the centroid of each feature cluster in a manner tolerant of unknown values
	double* pFeatureCentroid1 = pPrincipalComponent + m_internalLabelDims;
	double* pFeatureCentroid2 = pFeatureCentroid1 + m_internalFeatureDims;
	GVec::setAll(pFeatureCentroid1, 0.0, m_internalFeatureDims);
	GVec::setAll(pFeatureCentroid2, 0.0, m_internalFeatureDims);
	size_t* pCounts1 = pBuf2;
	size_t* pCounts2 = pCounts1 + m_internalFeatureDims;
	memset(pCounts1, '\0', sizeof(size_t) * m_internalFeatureDims);
	memset(pCounts2, '\0', sizeof(size_t) * m_internalFeatureDims);
	for(size_t i = 0; i < nCount; i++)
	{
		double* pF = features[i];
		if(GVec::dotProductIgnoringUnknowns(pLabelCentroid, labels[i], pPrincipalComponent, m_internalLabelDims) >= 0)
		{
			double* pM = pFeatureCentroid2;
			size_t* pC = pCounts2;
			for(size_t j = 0; j < m_internalFeatureDims; j++)
			{
				if(*pF != UNKNOWN_REAL_VALUE)
				{
					*pM += *pF;
					(*pC)++;
				}
				pF++;
				pM++;
				pC++;
			}
		}
		else
		{
			double* pM = pFeatureCentroid1;
			size_t* pC = pCounts1;
			for(size_t j = 0; j < m_internalFeatureDims; j++)
			{
				if(*pF != UNKNOWN_REAL_VALUE)
				{
					*pM += *pF;
					(*pC)++;
				}
				pF++;
				pM++;
				pC++;
			}
		}
	}
	size_t* pC1 = pCounts1;
	size_t* pC2 = pCounts2;
	double* pF1 = pFeatureCentroid1;
	double* pF2 = pFeatureCentroid2;
	for(size_t j = 0; j < m_internalFeatureDims; j++)
	{
		if(*pC1 == 0 || *pC2 == 0)
			return new GMeanMarginsTreeLeafNode(m_internalLabelDims, pLabelCentroid);
		*(pF1++) /= *(pC1++);
		*(pF2++) /= *(pC2++);
	}

	// Compute the feature center and normal
	GVec::add(pFeatureCentroid1, pFeatureCentroid2, m_internalFeatureDims);
	GVec::multiply(pFeatureCentroid1, 0.5, m_internalFeatureDims);
	GVec::subtract(pFeatureCentroid2, pFeatureCentroid1, m_internalFeatureDims);
	GVec::safeNormalize(pFeatureCentroid2, m_internalFeatureDims, &m_rand);

	// Make the interior node
	GMeanMarginsTreeInteriorNode* pNode = new GMeanMarginsTreeInteriorNode(m_internalFeatureDims, pFeatureCentroid1, pFeatureCentroid2);
	Holder<GMeanMarginsTreeInteriorNode> hNode(pNode);

	// Divide the data
	GMatrix otherFeatures(features.relation(), features.heap());
	GMatrix otherLabels(labels.relation(), labels.heap());
	{
		GMergeDataHolder hFeatures(features, otherFeatures);
		GMergeDataHolder hLabels(labels, otherLabels);
		otherFeatures.reserve(features.rows());
		otherLabels.reserve(labels.rows());
		for(size_t i = features.rows() - 1; i < features.rows(); i--)
		{
			if(pNode->Test(features[i], m_internalFeatureDims))
			{
				otherFeatures.takeRow(features.releaseRow(i));
				otherLabels.takeRow(labels.releaseRow(i));
			}
		}
	
		// If we couldn't separate anything, just return a leaf node
		if(features.rows() == 0 || otherFeatures.rows() == 0)
			return new GMeanMarginsTreeLeafNode(m_internalLabelDims, pLabelCentroid);
	
		// Build the child nodes
		pNode->SetLeft(buildNode(features, labels, pBuf, pBuf2));
		pNode->SetRight(buildNode(otherFeatures, otherLabels, pBuf, pBuf2));
	}
	GAssert(otherFeatures.rows() == 0 && otherLabels.rows() == 0);
	return hNode.release();
}

// virtual
void GMeanMarginsTree::predictDistributionInner(const double* pIn, GPrediction* pOut)
{
	ThrowError("Sorry, this model cannot predict a distribution");
}

// virtual
void GMeanMarginsTree::predictInner(const double* pIn, double* pOut)
{
	GMeanMarginsTreeNode* pNode = m_pRoot;
	size_t nDepth = 1;
	while(!pNode->IsLeaf())
	{
		if(((GMeanMarginsTreeInteriorNode*)pNode)->Test(pIn, m_internalFeatureDims))
			pNode = ((GMeanMarginsTreeInteriorNode*)pNode)->GetRight();
		else
			pNode = ((GMeanMarginsTreeInteriorNode*)pNode)->GetLeft();
		nDepth++;
	}
	GVec::copy(pOut, ((GMeanMarginsTreeLeafNode*)pNode)->GetOutputs(), m_internalLabelDims);
}

// virtual
void GMeanMarginsTree::clear()
{
	delete(m_pRoot);
	m_pRoot = NULL;
	m_internalFeatureDims = 0;
	m_internalLabelDims = 0;
}

#ifndef NO_TEST_CODE
// static
void GMeanMarginsTree::test()
{
	GRand rand(0);
	GMeanMarginsTree mm(rand);
	mm.basicTest(0.70, 0.77);
}
#endif










GRandomForest::GRandomForest(GRand& rand, size_t trees, size_t samples)
: GSupervisedLearner(rand)
{
	m_pEnsemble = new GBag(rand);
	for(size_t i = 0; i < trees; i++)
	{
		GDecisionTree* pTree = new GDecisionTree(rand);
		pTree->useRandomDivisions(samples);
		m_pEnsemble->addLearner(pTree);
	}
}

GRandomForest::GRandomForest(GDomNode* pNode, GLearnerLoader& ll)
: GSupervisedLearner(pNode, ll)
{
	m_pEnsemble = new GBag(pNode->field("bag"), ll);
}

// virtual
GRandomForest::~GRandomForest()
{
	delete(m_pEnsemble);
}

// virtual
GDomNode* GRandomForest::serialize(GDom* pDoc)
{
	GDomNode* pNode = baseDomNode(pDoc, "GRandomForest");
	pNode->addField(pDoc, "bag", m_pEnsemble->serialize(pDoc));
	return pNode;
}

// virtual
void GRandomForest::clear()
{
	m_pEnsemble->clear();
}

// virtual
void GRandomForest::trainInner(GMatrix& features, GMatrix& labels)
{
	m_pEnsemble->train(features, labels);
}

// virtual
void GRandomForest::predictInner(const double* pIn, double* pOut)
{
	m_pEnsemble->predict(pIn, pOut);
}

// virtual
void GRandomForest::predictDistributionInner(const double* pIn, GPrediction* pOut)
{
	m_pEnsemble->predictDistribution(pIn, pOut);
}

#ifndef NO_TEST_CODE
// static
void GRandomForest::test()
{
	GRand rand(0);
	GRandomForest rf(rand, 30);
	rf.basicTest(0.765, 0.78, 0.01);
}
#endif
