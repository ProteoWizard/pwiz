/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#include "GNaiveInstance.h"
#include "GVec.h"
#include "GDom.h"
#include "GDistribution.h"
#include "GRand.h"
#include "GTransform.h"
#include <map>

using std::multimap;
using std::make_pair;

namespace GClasses {

class GNaiveInstanceAttr
{
protected:
	multimap<double,const double*> m_instances;

public:
	GNaiveInstanceAttr() {}

	GNaiveInstanceAttr(GDomNode* pAttr, size_t labelDims, GHeap* pHeap)
	{
		GDomListIterator it(pAttr);
		size_t count = it.remaining() / (1 + labelDims);
		if(count * (1 + labelDims) != it.remaining())
			ThrowError("invalid list size");
		for(size_t i = 0; i < count; i++)
		{
			double d = it.current()->asDouble();
			it.advance();
			double* pLabel = (double*)pHeap->allocAligned(sizeof(double) * labelDims);
			m_instances.insert(make_pair(d, pLabel));
			for(size_t j = 0; j < labelDims; j++)
			{
				*(pLabel++) = it.current()->asDouble();
				it.advance();
			}
		}
	}

	virtual ~GNaiveInstanceAttr()
	{
	}

	multimap<double,const double*>& instances() { return m_instances; }

	GDomNode* serialize(GDom* pDoc, size_t labelDims)
	{
		GDomNode* pList = pDoc->newList();
		for(multimap<double,const double*>::iterator it = m_instances.begin(); it != m_instances.end(); it++)
		{
			pList->addItem(pDoc, pDoc->newDouble(it->first));
			for(size_t i = 0; i < labelDims; i++)
				pList->addItem(pDoc, pDoc->newDouble(it->second[i]));
		}
		return pList;
	}

	void addInstance(double dInput, const double* pOutputs)
	{
		m_instances.insert(make_pair(dInput, pOutputs));
	}
};

// -----------------------------------------------------------

GNaiveInstance::GNaiveInstance(GRand& rand)
: GIncrementalLearner(rand), m_pHeap(NULL)
{
	m_nNeighbors = 12;
	m_pAttrs = NULL;
	m_internalLabelDims = 0;
	m_internalFeatureDims = 0;
	m_pValueSums = NULL;
}

GNaiveInstance::GNaiveInstance(GDomNode* pNode, GLearnerLoader& ll)
: GIncrementalLearner(pNode, ll), m_pHeap(NULL)
{
	m_pAttrs = NULL;
	m_pValueSums = NULL;
	m_nNeighbors = (size_t)pNode->field("neighbors")->asInt();
	m_internalFeatureDims = (size_t)pNode->field("ifd")->asInt();
	m_internalLabelDims = (size_t)pNode->field("ild")->asInt();
	sp_relation pFeatureRel = new GUniformRelation(m_internalFeatureDims);
	sp_relation pLabelRel = new GUniformRelation(m_internalLabelDims);
	beginIncrementalLearningInner(pFeatureRel, pLabelRel);
	GDomNode* pAttrs = pNode->field("attrs");
	GDomListIterator it(pAttrs);
	if(it.remaining() != m_internalFeatureDims)
		ThrowError("Expected ", to_str(m_internalFeatureDims), " attrs, got ", to_str(it.remaining()), " attrs");
	m_pHeap = new GHeap(1024);
	for(size_t i = 0; i < m_internalFeatureDims; i++)
	{
		delete(m_pAttrs[i]);
		m_pAttrs[i] = new GNaiveInstanceAttr(it.current(), m_internalLabelDims, m_pHeap);
		it.advance();
	}
}

// virtual
GNaiveInstance::~GNaiveInstance()
{
	clear();
}

void GNaiveInstance::clear()
{
	if(m_pAttrs)
	{
		for(size_t i = 0; i < m_internalFeatureDims; i++)
			delete(m_pAttrs[i]);
		delete[] m_pAttrs;
	}
	m_pAttrs = NULL;
	delete[] m_pValueSums;
	m_pValueSums = NULL;
	m_internalLabelDims = 0;
	m_internalFeatureDims = 0;
	delete(m_pHeap);
	m_pHeap = NULL;
}

// virtual
GDomNode* GNaiveInstance::serialize(GDom* pDoc)
{
	GDomNode* pNode = baseDomNode(pDoc, "GNaiveInstance");
	pNode->addField(pDoc, "ifd", pDoc->newInt(m_internalFeatureDims));
	pNode->addField(pDoc, "ild", pDoc->newInt(m_internalLabelDims));
	pNode->addField(pDoc, "neighbors", pDoc->newInt(m_nNeighbors));
	GDomNode* pAttrs = pNode->addField(pDoc, "attrs", pDoc->newList());
	for(size_t i = 0; i < m_internalFeatureDims; i++)
		pAttrs->addItem(pDoc, m_pAttrs[i]->serialize(pDoc, m_internalLabelDims));
	return pNode;
}

void GNaiveInstance::autoTune(GMatrix& features, GMatrix& labels)
{
	// Find the best ess value
	size_t bestK = 0;
	double bestErr = 1e308;
	size_t cap = size_t(floor(sqrt(double(features.rows()))));
	for(size_t i = 2; i < cap; i = size_t(i * 1.5))
	{
		m_nNeighbors = i;
		double d = heuristicValidate(features, labels);
		if(d < bestErr)
		{
			bestErr = d;
			bestK = i;
		}
		else if(i >= 15)
			break;
	}

	// Set the best values
	m_nNeighbors = bestK;
}

// virtual
void GNaiveInstance::beginIncrementalLearningInner(sp_relation& pFeatureRel, sp_relation& pLabelRel)
{
	if(!pFeatureRel->areContinuous(0, pFeatureRel->size()) || !pLabelRel->areContinuous(0, pLabelRel->size()))
		ThrowError("Only continuous attributes are supported.");
	clear();
	m_internalFeatureDims = pFeatureRel->size();
	m_internalLabelDims = pLabelRel->size();
	m_pAttrs = new GNaiveInstanceAttr*[m_internalFeatureDims];
	for(size_t i = 0; i < m_internalFeatureDims; i++)
		m_pAttrs[i] = new GNaiveInstanceAttr();
	m_pValueSums = new double[4 * m_internalLabelDims + m_internalFeatureDims];
	m_pWeightSums = &m_pValueSums[m_internalLabelDims];
	m_pSumBuffer = &m_pValueSums[2 * m_internalLabelDims];
	m_pSumOfSquares = &m_pValueSums[3 * m_internalLabelDims];
}

// virtual
void GNaiveInstance::trainIncrementalInner(const double* pIn, const double* pOut)
{
	if(!m_pHeap)
		m_pHeap = new GHeap(1024);
	double* pOutputs = (double*)m_pHeap->allocAligned(sizeof(double) * m_internalLabelDims);
	GVec::copy(pOutputs, pOut, m_internalLabelDims);
	for(size_t i = 0; i < m_internalFeatureDims; i++)
	{
		if(*pIn != UNKNOWN_REAL_VALUE)
			m_pAttrs[i]->addInstance(*(pIn++), pOutputs);
	}
}

// virtual
void GNaiveInstance::trainInner(GMatrix& features, GMatrix& labels)
{
	beginIncrementalLearningInner(features.relation(), labels.relation());
	for(size_t i = 0; i < features.rows(); i++)
		trainIncrementalInner(features[i], labels[i]);
}

// virtual
void GNaiveInstance::trainSparse(GSparseMatrix& features, GMatrix& labels)
{
	ThrowError("Sorry, trainSparse is not implemented yet in GNaiveInstance");
}

void GNaiveInstance::evalInput(size_t nInputDim, double dInput)
{
	// Init the accumulators
	GVec::setAll(m_pSumBuffer, 0.0, m_internalLabelDims);
	GVec::setAll(m_pSumOfSquares, 0.0, m_internalLabelDims);

	// Find the nodes on either side of dInput
	GNaiveInstanceAttr* pAttr = m_pAttrs[nInputDim];
	multimap<double,const double*>& instances = pAttr->instances();
	multimap<double,const double*>::iterator itLeft = instances.lower_bound(dInput);
	multimap<double,const double*>::iterator itRight = itLeft;
	bool leftValid = true;
	if(itLeft == instances.end())
	{
		if(instances.size() > 0)
			itLeft--;
		else
			leftValid = false;
	}
	else
		itRight++;

	// Compute the mean and variance of the values for the k-nearest neighbors
	size_t nNeighbors = 0;
	bool goRight;
	while(true)
	{
		// Pick the closer of the two nodes
		if(!leftValid)
		{
			if(itRight == instances.end())
				break;
			goRight = true;
		}
		else if(itRight == instances.end())
			goRight = false;
		else if(dInput - itLeft->first < itRight->first - dInput)
			goRight = false;
		else
			goRight = true;

		// Accumulate values
		const double* pOutputVec = goRight ? itRight->second : itLeft->second;
		GVec::add(m_pSumBuffer, pOutputVec, m_internalLabelDims);
		for(size_t j = 0; j < m_internalLabelDims; j++)
			m_pSumOfSquares[j] += (pOutputVec[j] * pOutputVec[j]);

		// See if we're done
		if(++nNeighbors >= m_nNeighbors)
			break;

		// Advance
		if(goRight)
			itRight++;
		else
		{
			if(itLeft == instances.begin())
				leftValid = false;
			else
				itLeft--;
		}
	}
	GVec::multiply(m_pSumBuffer, 1.0 / nNeighbors, m_internalLabelDims);
	GVec::multiply(m_pSumOfSquares, 1.0 / nNeighbors, m_internalLabelDims);

	// Accumulate the predictions across all dimensions
	int dims = 0;
	double weight;
	for(size_t i = 0; i < m_internalLabelDims; i++)
	{
		weight = 1.0 / std::max(m_pSumOfSquares[i] - (m_pSumBuffer[i] * m_pSumBuffer[i]), 1e-5);
		m_pWeightSums[dims] += weight;
		m_pValueSums[dims] += weight * m_pSumBuffer[dims];
		dims++;
	}
}

// virtual
void GNaiveInstance::predictDistributionInner(const double* pIn, GPrediction* pOut)
{
	GVec::setAll(m_pWeightSums, 0.0, m_internalLabelDims);
	GVec::setAll(m_pValueSums, 0.0, m_internalLabelDims);
	for(size_t i = 0; i < m_internalFeatureDims; i++)
		evalInput(i, pIn[i]);
	for(size_t i = 0; i < m_internalLabelDims; i++)
	{
		GNormalDistribution* pNorm = pOut[i].makeNormal();
		pNorm->setMeanAndVariance(m_pValueSums[i] / m_pWeightSums[i], 1.0 / m_pWeightSums[i]);
	}
}

// virtual
void GNaiveInstance::predictInner(const double* pIn, double* pOut)
{
	GVec::setAll(m_pWeightSums, 0.0, m_internalLabelDims);
	GVec::setAll(m_pValueSums, 0.0, m_internalLabelDims);
	for(size_t i = 0; i < m_internalFeatureDims; i++)
		evalInput(i, pIn[i]);
	for(size_t i = 0; i < m_internalLabelDims; i++)
		pOut[i] = m_pValueSums[i] / m_pWeightSums[i];
}

#ifndef NO_TEST_CODE
//static
void GNaiveInstance::test()
{
	GRand prng(0);
	GNaiveInstance ni(prng);
	ni.setNeighbors(8);
	ni.basicTest(0.72, 0.55, 0.02);
}
#endif

} // namespace GClasses
