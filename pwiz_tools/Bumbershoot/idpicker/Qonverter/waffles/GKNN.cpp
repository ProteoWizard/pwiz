/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#include "GKNN.h"
#include <math.h>
#include "GError.h"
#include <stdlib.h>
#include "GDom.h"
#include "GDistribution.h"
#include "GRand.h"
#include "GHeap.h"
#include "GNeighborFinder.h"
#include "GVec.h"
#include "GHillClimber.h"
#include "GCluster.h"
#include "GBitTable.h"
#include "GDistance.h"
#include "GSparseMatrix.h"
#include <map>

using std::multimap;
using std::map;
using std::pair;


namespace GClasses {

class GKnnScaleFactorCritic : public GTargetFunction
{
protected:
	size_t m_labelDims;
	GKNN* m_pLearner;
	double* m_pAccuracy;

public:
	GKnnScaleFactorCritic(GKNN* pLearner, size_t featureDims, size_t labelDims)
	: GTargetFunction(featureDims), m_labelDims(labelDims)
	{
		m_pLearner = pLearner;
		m_pAccuracy = new double[labelDims];
	}

	virtual ~GKnnScaleFactorCritic()
	{
		delete[] m_pAccuracy;
	}

	virtual void initVector(double* pVector)
	{
		GRowDistanceScaled* pMetric = m_pLearner->metric();
		GVec::copy(pVector, pMetric->scaleFactors(), relation()->size());
	}

	virtual bool isStable() { return false; }
	virtual bool isConstrained() { return false; }

protected:
	virtual double computeError(const double* pVector)
	{
		// todo: this method is WAAAY too inefficient
		GMatrix* pFeatures = m_pLearner->features();
		GMatrix* pLabels = m_pLearner->labels();
		GKNN temp(m_pLearner->rand());
		temp.setNeighborCount(m_pLearner->neighborCount());
		temp.beginIncrementalLearning(pFeatures->relation(), pLabels->relation());
		GVec::copy(temp.metric()->scaleFactors(), pVector, relation()->size());
		return temp.heuristicValidate(*pFeatures, *pLabels);
	}
};


GKNN::GKNN(GRand& rand)
: GIncrementalLearner(rand)
{
	m_eInterpolationMethod = Linear;
	m_pLearner = NULL;
	m_bOwnLearner = false;
	m_nNeighbors = 1;
	m_pFeatures = NULL;
	m_pSparseFeatures = NULL;
	m_pLabels = NULL;
	m_pNeighborFinder = NULL;
	m_pNeighborFinder2 = NULL;
	m_pEvalNeighbors = new size_t[m_nNeighbors + 1];
	m_pEvalDistances = new double[m_nNeighbors + 1];
	m_optimizeScaleFactors = false;
	m_pDistanceMetric = NULL;
	m_pSparseMetric = NULL;
	m_ownMetric = false;
	m_pValueCounts = NULL;
	m_pCritic = NULL;
	m_pScaleFactorOptimizer = NULL;
	m_dElbowRoom = UNKNOWN_REAL_VALUE;
}

GKNN::GKNN(GDomNode* pNode, GLearnerLoader& ll)
: GIncrementalLearner(pNode, ll)
{
	m_pNeighborFinder = NULL;
	m_pNeighborFinder2 = NULL;
	m_pCritic = NULL;
	m_pScaleFactorOptimizer = NULL;
	m_pLearner = NULL;
	m_pValueCounts = NULL;
	m_bOwnLearner = false;
	m_nNeighbors = (size_t)pNode->field("neighbors")->asInt();
	m_eInterpolationMethod = (InterpolationMethod)pNode->field("interpMethod")->asInt();
	m_optimizeScaleFactors = pNode->field("optimize")->asBool();
	m_dElbowRoom = pNode->field("elbowRoom")->asDouble();
	GMatrix* pFeatures = NULL;
	GSparseMatrix* pSparseFeatures = NULL;
	GDomNode* pFeaturesNode = pNode->fieldIfExists("features");
	if(pFeaturesNode)
		pFeatures = new GMatrix(pFeaturesNode);
	else
		pSparseFeatures = new GSparseMatrix(pNode->field("sparseFeatures"));
	GMatrix* pLabels = new GMatrix(pNode->field("labels"));
	GDomNode* pMetricNode = pNode->fieldIfExists("metric");
	m_pDistanceMetric = NULL;
	m_pSparseMetric = NULL;
	if(pMetricNode)
		m_pDistanceMetric = new GRowDistanceScaled(pNode->field("metric"));
	else
		m_pSparseMetric = GSparseSimilarity::deserialize(pNode->field("sparseMetric"));
	m_ownMetric = true;
	m_pFeatures = NULL;
	m_pSparseFeatures = NULL;
	m_pLabels = NULL;
	m_pEvalNeighbors = new size_t[m_nNeighbors + 1];
	m_pEvalDistances = new double[m_nNeighbors + 1];
	if(pFeatures)
		beginIncrementalLearningInner(pFeatures->relation(), pLabels->relation());
	else
	{
		sp_relation pRel = new GUniformRelation(pSparseFeatures->cols(), 0);
		beginIncrementalLearningInner(pRel, pLabels->relation());
	}
	delete(m_pFeatures);
	delete(m_pSparseFeatures);
	delete(m_pLabels);
	m_pFeatures = pFeatures;
	m_pSparseFeatures = pSparseFeatures;
	m_pLabels = pLabels;
}

GKNN::~GKNN()
{
	delete(m_pNeighborFinder);
	delete(m_pFeatures);
	delete(m_pSparseFeatures);
	delete(m_pLabels);
	delete[] m_pEvalNeighbors;
	delete[] m_pEvalDistances;
	delete[] m_pValueCounts;
	delete(m_pScaleFactorOptimizer);
	delete(m_pCritic);
	if(m_ownMetric)
	{
		delete(m_pDistanceMetric);
		delete(m_pSparseMetric);
	}
}

// virtual
GDomNode* GKNN::serialize(GDom* pDoc)
{
	GDomNode* pNode = baseDomNode(pDoc, "GKNN");
	pNode->addField(pDoc, "neighbors", pDoc->newInt(m_nNeighbors));
	if(m_eInterpolationMethod == Learner)
		ThrowError("Sorry, serialize is not supported for the \"Learner\" interpolation method");
	pNode->addField(pDoc, "interpMethod", pDoc->newInt(m_eInterpolationMethod));
	pNode->addField(pDoc, "optimize", pDoc->newBool(m_optimizeScaleFactors));
	pNode->addField(pDoc, "elbowRoom", pDoc->newDouble(m_dElbowRoom));
	if(m_pFeatures)
		pNode->addField(pDoc, "features", m_pFeatures->serialize(pDoc));
	else
		pNode->addField(pDoc, "sparseFeatures", m_pSparseFeatures->serialize(pDoc));
	pNode->addField(pDoc, "labels", m_pLabels->serialize(pDoc));
	if(m_pDistanceMetric)
		pNode->addField(pDoc, "metric", m_pDistanceMetric->serialize(pDoc));
	else
		pNode->addField(pDoc, "sparseMetric", m_pSparseMetric->serialize(pDoc));
	return pNode;
}

void GKNN::autoTune(GMatrix& features, GMatrix& labels)
{
	// Find the best value for k
	size_t cap = size_t(floor(sqrt(double(features.rows()))));
	size_t bestK = 1;
	double bestErr = 1e308;
	for(size_t i = 1; i < cap; i *= 3)
	{
		setNeighborCount(i);
		double d = heuristicValidate(features, labels);
		if(d < bestErr)
		{
			bestErr = d;
			bestK = i;
		}
		else if(i >= 27)
			break;
	}

	// Set the best values
	m_nNeighbors = bestK;
}

void GKNN::setNeighborCount(size_t k)
{
	delete[] m_pEvalNeighbors;
	delete[] m_pEvalDistances;
	m_nNeighbors = k;
	m_pEvalNeighbors = new size_t[m_nNeighbors + 1];
	m_pEvalDistances = new double[m_nNeighbors + 1];
}

void GKNN::setInterpolationMethod(InterpolationMethod eMethod)
{
	if(eMethod == Learner)
		ThrowError("You should call SetInterpolationLearner instead");
	m_eInterpolationMethod = eMethod;
}

void GKNN::setInterpolationLearner(GSupervisedLearner* pLearner, bool bOwnLearner)
{
	if(m_bOwnLearner)
		delete(m_pLearner);
	m_pLearner = pLearner;
	m_eInterpolationMethod = Learner;
	m_bOwnLearner = bOwnLearner;
}

size_t GKNN::addVector(const double* pFeatures, const double* pLabels)
{
	// Store the features
	size_t index;
	if(m_pNeighborFinder)
		index = m_pNeighborFinder->addCopy(pFeatures);
	else
	{
		index = m_pFeatures->rows();
		GVec::copy(m_pFeatures->newRow(), pFeatures, m_pFeatures->cols());
	}

	// Store the labels
	GVec::copy(m_pLabels->newRow(), pLabels, m_pLabels->cols());
	return index;
}

void GKNN::setOptimizeScaleFactors(bool b)
{
	m_optimizeScaleFactors = b;
}

void GKNN::setMetric(GRowDistanceScaled* pMetric, bool own)
{
	if(m_ownMetric)
	{
		delete(m_pDistanceMetric);
		delete(m_pSparseMetric);
	}
	m_pDistanceMetric = pMetric;
	m_pSparseMetric = NULL;
	m_ownMetric = own;
}

void GKNN::setMetric(GSparseSimilarity* pMetric, bool own)
{
	if(m_ownMetric)
	{
		delete(m_pDistanceMetric);
		delete(m_pSparseMetric);
	}
	m_pDistanceMetric = NULL;
	m_pSparseMetric = pMetric;
	m_ownMetric = own;
}

// virtual
void GKNN::beginIncrementalLearningInner(sp_relation& pFeatureRel, sp_relation& pLabelRel)
{
	clear();
	if(!m_pDistanceMetric && !m_pSparseMetric)
		setMetric(new GRowDistanceScaled(), true);
	if(m_pDistanceMetric)
	{
		m_pFeatures = new GMatrix(pFeatureRel);
		m_pDistanceMetric->init(pFeatureRel);

		// Scale factor optimization
		if(m_optimizeScaleFactors)
		{
			m_pCritic = new GKnnScaleFactorCritic(this, pFeatureRel->size(), pLabelRel->size());
			m_pScaleFactorOptimizer = new GMomentumGreedySearch(m_pCritic);
		}
	}
	else if(m_pSparseMetric)
	{
		if(!pFeatureRel->areContinuous(0, pFeatureRel->size()))
			ThrowError("Sorry, nominal features cannot be used in conjunction with sparse metrics");
		m_pSparseFeatures = new GSparseMatrix(0, pFeatureRel->size(), UNKNOWN_REAL_VALUE);
	}
	else
		ThrowError("Some sort of distance or similarity metric is required");

	m_pLabels = new GMatrix(pLabelRel);

	// Allocate a buffer for counting values
	size_t maxOutputValueCount = 0;
	for(size_t n = 0; n < pLabelRel->size(); n++)
		maxOutputValueCount = std::max(maxOutputValueCount, pLabelRel->valueCount(n));
	m_pValueCounts = new double[maxOutputValueCount];
}

// virtual
void GKNN::trainIncrementalInner(const double* pIn, const double* pOut)
{
	// Make a copy of the vector
	GAssert(m_pDistanceMetric);
	size_t index = addVector(pIn, pOut);

	// Delete the closest neighbor if the (k+1)th neighbor is closer than the specified threshold
	if(m_dElbowRoom != UNKNOWN_REAL_VALUE)
	{
		if(!m_pNeighborFinder2)
		{
			m_pNeighborFinder2 = new GKdTree(m_pFeatures, m_nNeighbors + 1, m_pDistanceMetric, false);
			return;
		}
		m_pNeighborFinder2->neighbors(m_pEvalNeighbors, m_pEvalDistances, index);
		m_pNeighborFinder2->sortNeighbors(m_pEvalNeighbors, m_pEvalDistances);
		if(m_pEvalNeighbors[m_nNeighbors] >= 0 && m_pEvalDistances[m_nNeighbors] < m_dElbowRoom)
		{
			double* pClosest = m_pNeighborFinder->releaseVector(m_pEvalNeighbors[0]);
			delete[] pClosest;
		}
	}

	// Learn how to scale the attributes
	if(m_pScaleFactorOptimizer && m_pFeatures->rows() > 50)
	{
		m_pScaleFactorOptimizer->iterate();
		GVec::copy(m_pDistanceMetric->scaleFactors(), m_pScaleFactorOptimizer->currentVector(), m_pFeatures->cols());
	}
}

void GKNN::trainInner(GMatrix& features, GMatrix& labels)
{
	if(m_pSparseMetric)
		ThrowError("This method is not compatible with sparse similarity metrics. You should either use trainSparse instead, or use a dense dissimilarity metric.");
	beginIncrementalLearningInner(features.relation(), labels.relation());
	m_pFeatures->reserve(features.rows());
	m_pLabels->reserve(features.rows());
	for(size_t i = 0; i < features.rows(); i++)
		addVector(features[i], labels[i]);

	// Give each attribute an equal chance by scaling out the deviation
	double* pScaleFactors = m_pDistanceMetric->scaleFactors();
	for(size_t i = 0; i < features.cols(); i++)
	{
		if(m_pFeatures->relation()->valueCount(i) == 0)
		{
			double m = m_pFeatures->mean(i);
			double d = sqrt(m_pFeatures->variance(i, m));
			if(d >= 1e-8)
				pScaleFactors[i] = 1.0 / (2.0 * d);
			else
				pScaleFactors[i] = 1.0;
		}
		else
			pScaleFactors[i] = 1.0;
	}

	// Learn to scale the attributes
	if(m_pScaleFactorOptimizer)
	{
		if(!m_pNeighborFinder)
		{
			m_pNeighborFinder = new GKdTree(m_pFeatures, m_nNeighbors, m_pDistanceMetric, false);
		}
		for(size_t j = 0; j < 5; j++)
		{
			for(size_t i = 0; i < 20; i++)
				m_pScaleFactorOptimizer->iterate();
			m_pNeighborFinder->reoptimize();
		}
		GVec::copy(pScaleFactors, m_pScaleFactorOptimizer->currentVector(), features.cols());
	}
}

// virtual
void GKNN::trainSparse(GSparseMatrix& features, GMatrix& labels)
{
	if(features.rows() != labels.rows())
		ThrowError("Expected the features and labels to have the same number of rows");
	if(m_pDistanceMetric)
		ThrowError("This method is not compatible with dense dissimilarity metrics. You should either use the train method instead, or use a sparse similarity metric.");
	if(!m_pSparseMetric)
		setMetric(new GCosineSimilarity(), true);
	sp_relation pFeatureRel = new GUniformRelation(features.cols(), 0);
	beginIncrementalLearning(pFeatureRel, labels.relation());

	// Copy the training data
	m_pSparseFeatures->newRows(features.rows());
	m_pSparseFeatures->copyFrom(&features);
	m_pLabels->copy(&labels);
}

void GKNN::findNeighbors(const double* pVector)
{
	if(m_pDistanceMetric)
	{
		if(!m_pNeighborFinder)
		{
			//m_pNeighborFinder = new GBruteForceNeighborFinder(m_pFeatures, m_nNeighbors, m_pDistanceMetric, false);
			m_pNeighborFinder = new GKdTree(m_pFeatures, m_nNeighbors, m_pDistanceMetric, false);
		}
		GAssert(m_pNeighborFinder->neighborCount() == m_nNeighbors);
		m_pNeighborFinder->neighbors(m_pEvalNeighbors, m_pEvalDistances, pVector);
	}
	else
	{
		if(!m_pSparseMetric)
			ThrowError("train, trainSparse, or beginIncrementalLearning must be called before this method");
		multimap<double,size_t> priority_queue;
		for(size_t i = 0; i < m_pSparseFeatures->rows(); i++)
		{
			map<size_t,double>& row = m_pSparseFeatures->row(i);
			double similarity = m_pSparseMetric->similarity(row, pVector);
			priority_queue.insert(pair<double,size_t>(similarity, i));
			if(priority_queue.size() > m_nNeighbors)
				priority_queue.erase(priority_queue.begin());
		}
		size_t pos = 0;
		size_t* pNeigh = m_pEvalNeighbors;
		double* pDist = m_pEvalDistances;
		for(multimap<double,size_t>::iterator it = priority_queue.begin(); it != priority_queue.end(); it++)
		{
			*pNeigh = it->second;
			*pDist = 1.0;
			pos++;
			pNeigh++;
			pDist++;
		}
		while(pos < m_nNeighbors)
		{
			*pNeigh = INVALID_INDEX;
			*pDist = UNKNOWN_REAL_VALUE;
			pos++;
			pNeigh++;
			pDist++;
		}
	}
}

void GKNN::interpolateMean(const double* pIn, GPrediction* pOut, double* pOut2)
{
	for(size_t i = 0; i < m_pLabels->cols(); i++)
	{
		if(m_pLabels->relation()->valueCount(i) == 0)
		{
			// Continuous label
			double dSum = 0;
			double dSumOfSquares = 0;
			size_t count = 0;
			for(size_t j = 0; j < m_nNeighbors; j++)
			{
				size_t k = m_pEvalNeighbors[j];
				if(k < m_pLabels->rows())
				{
					double* pNeighbor = m_pLabels->row(k);
					dSum += pNeighbor[i];
					dSumOfSquares += (pNeighbor[i] * pNeighbor[i]);
					count++;
				}
			}
			if(pOut)
			{
				if(count > 0)
				{
					double mean = dSum / count;
					pOut[i].makeNormal()->setMeanAndVariance(mean, dSumOfSquares / count - (mean * mean));
				}
				else
					pOut[i].makeNormal()->setMeanAndVariance(0, 1);
			}
			if(pOut2)
			{
				if(count > 0)
					pOut2[i] = dSum / count;
				else
					pOut2[i] = 0;
			}
		}
		else
		{
			// Nominal label
			size_t nValueCount = m_pLabels->relation()->valueCount(i);
			GVec::setAll(m_pValueCounts, 0.0, nValueCount);
			for(size_t j = 0; j < m_nNeighbors; j++)
			{
				size_t k = m_pEvalNeighbors[j];
				if(k < m_pLabels->rows())
				{
					double* pNeighbor = m_pLabels->row(k);
					int val = (int)pNeighbor[i];
					if(val < 0 || val >= (int)nValueCount)
						ThrowError("GKNN doesn't support unknown label values");
					m_pValueCounts[val]++;
				}
			}
			if(pOut)
				pOut[i].makeCategorical()->setValues(nValueCount, m_pValueCounts);
			if(pOut2)
				pOut2[i] = (double)GVec::indexOfMax(m_pValueCounts, nValueCount, &m_rand);
		}
	}
}

void GKNN::interpolateLinear(const double* pIn, GPrediction* pOut, double* pOut2)
{
	for(size_t i = 0; i < m_pLabels->cols(); i++)
	{
		if(m_pLabels->relation()->valueCount(i) == 0)
		{
			// Continuous label
			double dSum = 0;
			double dSumOfSquares = 0;
			double dTot = 0;
			for(size_t j = 0; j < m_nNeighbors; j++)
			{
				size_t k = m_pEvalNeighbors[j];
				if(k < m_pLabels->rows())
				{
					double* pNeighbor = m_pLabels->row(k);
					if(pNeighbor[i] == UNKNOWN_REAL_VALUE)
						ThrowError("GKNN doesn't support unknown label values");
					double d = 1.0 / std::max(sqrt(m_pEvalDistances[j]), 1e-9); // the weight
					dTot += d;
					d *= pNeighbor[i]; // weighted sum
					dSum += d;
					d *= pNeighbor[i]; // weighted sum of squares
					dSumOfSquares += d;
				}
			}
			if(pOut)
			{
				if(dTot > 0)
				{
					double d = dSum / dTot;
					pOut[i].makeNormal()->setMeanAndVariance(d, dSumOfSquares / dTot - (d * d));
				}
				else
					pOut[i].makeNormal()->setMeanAndVariance(0, 1);
			}
			if(pOut2)
			{
				if(dTot > 0)
					pOut2[i] = dSum / dTot;
				else
					pOut2[i] = 0;
			}
		}
		else
		{
			// Nominal label
			int nValueCount = (int)m_pLabels->relation()->valueCount(i);
			GVec::setAll(m_pValueCounts, 0.0, nValueCount);
			double dSumWeight = 0;
			for(size_t j = 0; j < m_nNeighbors; j++)
			{
				size_t k = m_pEvalNeighbors[j];
				if(k < m_pLabels->rows())
				{
					double* pNeighbor = m_pLabels->row(k);
					double d = 1.0 / std::max(m_pEvalDistances[j], 1e-9); // to be truly "linear", we should use sqrt(d) instead of d, but this is faster to compute and arguably better for nominal values anyway
					int val = (int)pNeighbor[i];
					if(val < 0 || val >= nValueCount)
						ThrowError("GKNN doesn't support unknown label values");
					m_pValueCounts[val] += d;
					dSumWeight += d;
				}
			}
			if(pOut)
				pOut[i].makeCategorical()->setValues(nValueCount, m_pValueCounts);
			if(pOut2)
				pOut2[i] = (double)GVec::indexOfMax(m_pValueCounts, nValueCount, &m_rand);
		}
	}
}

void GKNN::interpolateLearner(const double* pIn, GPrediction* pOut, double* pOut2)
{
	GAssert(m_pLearner); // no learner is set
	GHeap heap(1000);
	GMatrix dataFeatures(m_pFeatures->relation(), &heap);
	GReleaseDataHolder hDataFeatures(&dataFeatures);
	dataFeatures.reserve(m_nNeighbors);
	GMatrix dataLabels(m_pLabels->relation(), &heap);
	GReleaseDataHolder hDataLabels(&dataLabels);
	dataLabels.reserve(m_nNeighbors);
	for(size_t i = 0; i < m_nNeighbors; i++)
	{
		size_t nNeighbor = m_pEvalNeighbors[i];
		if(nNeighbor < m_pFeatures->rows())
		{
			dataFeatures.takeRow(m_pFeatures->row(nNeighbor));
			dataLabels.takeRow(m_pLabels->row(nNeighbor));
		}
	}
	m_pLearner->train(dataFeatures, dataLabels);
	if(pOut)
		m_pLearner->predictDistribution(pIn, pOut);
	if(pOut2)
		m_pLearner->predict(pIn, pOut2);
}

// virtual
void GKNN::predictDistributionInner(const double* pIn, GPrediction* pOut)
{
	findNeighbors(pIn);
	switch(m_eInterpolationMethod)
	{
		case Linear: interpolateLinear(pIn, pOut, NULL); break;
		case Mean: interpolateMean(pIn, pOut, NULL); break;
		case Learner: interpolateLearner(pIn, pOut, NULL); break;
		default:
			GAssert(false); // unexpected enumeration
			break;
	}
}

// virtual
void GKNN::predictInner(const double* pIn, double* pOut)
{
	findNeighbors(pIn);
	switch(m_eInterpolationMethod)
	{
		case Linear: interpolateLinear(pIn, NULL, pOut); break;
		case Mean: interpolateMean(pIn, NULL, pOut); break;
		case Learner: interpolateLearner(pIn, NULL, pOut); break;
		default:
			GAssert(false); // unexpected enumeration
			break;
	}
}

// virtual
void GKNN::clear()
{
	delete(m_pNeighborFinder); m_pNeighborFinder = NULL;
	delete(m_pFeatures); m_pFeatures = NULL;
	delete(m_pSparseFeatures); m_pSparseFeatures = NULL;
	delete(m_pLabels); m_pLabels = NULL;
	delete(m_pScaleFactorOptimizer); m_pScaleFactorOptimizer = NULL;
	delete(m_pCritic); m_pCritic = NULL;
	delete[] m_pValueCounts; m_pValueCounts = NULL;
}

#ifndef NO_TEST_CODE
//static
void GKNN::test()
{
	GRand prng(0);
	GKNN knn(prng);
	knn.setNeighborCount(3);
	knn.basicTest(0.72, 0.75);
}
#endif

// ---------------------------------------------------------------------------------------

GNeighborTransducer::GNeighborTransducer(GRand& rand)
: GTransducer(rand), m_friendCount(12)
{
}

void GNeighborTransducer::autoTune(GMatrix& features, GMatrix& labels)
{
	// Find the best value for k
	size_t cap = size_t(floor(sqrt(double(features.rows()))));
	size_t bestK = 1;
	double bestErr = 1e308;
	for(size_t i = 1; i < cap; i *= 3)
	{
		m_friendCount = i;
		double d = heuristicValidate(features, labels);
		if(d < bestErr)
		{
			bestErr = d;
			bestK = i;
		}
		else if(i >= 27)
			break;
	}

	// Set the best values
	m_friendCount = bestK;
}

// virtual
GMatrix* GNeighborTransducer::transduceInner(GMatrix& features1, GMatrix& labels1, GMatrix& features2)
{
	// Make a dataset containing all rows
	GMatrix featuresAll(features1.relation());
	featuresAll.reserve(features1.rows() + features2.rows());
	GReleaseDataHolder hFeaturesAll(&featuresAll);
	for(size_t i = 0; i < features2.rows(); i++)
		featuresAll.takeRow(features2[i]);
	for(size_t i = 0; i < features1.rows(); i++)
		featuresAll.takeRow(features1[i]);
	GMatrix* pOut = new GMatrix(labels1.relation());
	pOut->newRows(features2.rows());
	Holder<GMatrix> hOut(pOut);

	// Find friends
	GNeighborFinder* pNF = new GNeighborFinderCacheWrapper(new GKdTree(&featuresAll, m_friendCount, NULL, true), true);
	Holder<GNeighborFinder> hNF(pNF);
	GTEMPBUF(size_t, neighbors, m_friendCount);

	// Transduce
	for(size_t lab = 0; lab < labels1.cols(); lab++)
	{
		size_t labelValues = labels1.relation()->valueCount(lab);
		double* tallys = new double[labelValues];
		ArrayHolder<double> hTallys(tallys);

		// Label the unlabeled patterns
		GBitTable labeled(features2.rows());
		GMatrix labelList(features2.rows(), 3); // pattern index, most likely label, confidence
		for(size_t i = 0; i < features2.rows(); i++)
			labelList[i][0] = (double)i;
		while(labelList.rows() > 0)
		{
			// Compute the most likely label and the confidence for each pattern
			for(size_t i = 0; i < labelList.rows(); i++)
			{
				// Find the most common label
				double* pRow = labelList.row(i);
				size_t index = (size_t)pRow[0];
				pNF->neighbors(neighbors, index);
				GVec::setAll(tallys, 0.0, labelValues);
				for(size_t j = 0; j < m_friendCount; j++)
				{
					if(neighbors[j] >= featuresAll.rows())
						continue;
					if(neighbors[j] >= features2.rows())
					{
						int label = (int)labels1[neighbors[j] - features2.rows()][lab];
						if(label >= 0 && label < (int)labelValues)
							tallys[label]++;
					}
					else if(labeled.bit(neighbors[j]))
					{
						int label = (int)pOut->row(neighbors[j])[lab];
						if(label >= 0 && label < (int)labelValues)
							tallys[label] += 0.6;
					}
				}
				int label = (int)GVec::indexOfMax(tallys, labelValues, &m_rand);
				double conf = tallys[label];

				// Penalize for dissenting votes
				for(size_t j = 0; j < m_friendCount; j++)
				{
					if(neighbors[j] >= featuresAll.rows())
						continue;
					if(neighbors[j] >= features2.rows())
					{
						int l2 = (int)labels1[neighbors[j] - features2.rows()][lab];
						if(l2 != label)
							conf *= 0.5;
					}
					else if(labeled.bit(neighbors[j]))
					{
						int l2 = (int)pOut->row(neighbors[j])[lab];
						if(l2 != label)
							conf *= 0.8;
					}
				}
				pRow[1] = label;
				pRow[2] = conf;
			}
			labelList.sort(2);
	
			// Assign the labels to the patterns we are most confident about
			size_t maxCount = std::max((size_t)4, features1.rows() / 8);
			size_t count = 0;
			for(size_t i = labelList.rows() - 1; i < labelList.rows(); i--)
			{
				double* pRow = labelList.row(i);
				size_t index = (size_t)pRow[0];
				int label = (int)pRow[1];
				pOut->row(index)[lab] = label;
				labeled.set(index);
				labelList.deleteRow(i);
				if(count >= maxCount)
					break;
				count++;
			}
		}
	}
	return hOut.release();
}









GInstanceTable::GInstanceTable(size_t dims, size_t* pDims, GRand& rand)
: GIncrementalLearner(rand), m_dims(dims)
{
	m_pDims = new size_t[dims];
	memcpy(m_pDims, pDims, sizeof(size_t) * dims);
	m_product = 1;
	m_pScales = new size_t[dims];
	for(size_t i = 0; i < dims; i++)
	{
		m_pScales[i] = m_product;
		m_product *= pDims[i];
		m_pDims[i] = pDims[i];
	}
	m_pTable = NULL;
	clear();
}

// virtual
GInstanceTable::~GInstanceTable()
{
	delete[] m_pDims;
	delete[] m_pScales;
	clear();
}

// virtual
GDomNode* GInstanceTable::serialize(GDom* pDoc)
{
	ThrowError("not implemented yet");
	return NULL;
}

// virtual
void GInstanceTable::trainSparse(GSparseMatrix& features, GMatrix& labels)
{
	ThrowError("Sorry, trainSparse is not implemented yet in GInstanceTable");
}

// virtual
void GInstanceTable::trainInner(GMatrix& features, GMatrix& labels)
{
	beginIncrementalLearningInner(features.relation(), labels.relation());
	for(size_t i = 0; i < features.rows(); i++)
		trainIncrementalInner(features[i], labels[i]);
}

// virtual
void GInstanceTable::predictDistributionInner(const double* pIn, GPrediction* pOut)
{
	ThrowError("Sorry, this model cannot predict a distribution");
}

// virtual
void GInstanceTable::predictInner(const double* pIn, double* pOut)
{
	size_t pos = 0;
	for(size_t i = 0; i < m_dims; i++)
	{
		size_t n = (size_t)floor(pIn[i] + 0.5);
		if(n >= m_pDims[i])
			ThrowError("dim=", to_str(i), ", index=", to_str(pIn[i]), ", out of range. Expected >= 0 and < ", to_str(m_pDims[i]));
		pos += n * m_pScales[i];
	}
	GVec::copy(pOut, m_pTable + pos * m_labelDims, m_labelDims);
}

// virtual
void GInstanceTable::clear()
{
	delete[] m_pTable;
	m_pTable = NULL;
}

// virtual
void GInstanceTable::beginIncrementalLearningInner(sp_relation& pFeatureRel, sp_relation& pLabelRel)
{
	// Allocate the table
	clear();
	size_t total = m_product * pLabelRel->size();
	m_pTable = new double[total];

	// Initialize with small random values
	double* p = m_pTable;
	for(size_t i = 0; i < total; i++)
		*(p++) = m_rand.uniform() * 0.1;

	m_featureDims = pFeatureRel->size();
	m_labelDims = pLabelRel->size();
	m_dims = pFeatureRel->size();
}

// virtual
void GInstanceTable::trainIncrementalInner(const double* pIn, const double* pOut)
{
	size_t pos = 0;
	for(size_t i = 0; i < m_dims; i++)
	{
		size_t n = (size_t)floor(pIn[i] + 0.5);
		if(n >= m_pDims[i])
			ThrowError("dim=", to_str(i), ", index=", to_str(pIn[i]), ", out of range. Expected >= 0 and < ", to_str(m_pDims[i]));
		pos += n * m_pScales[i];
	}
	GVec::copy(m_pTable + pos * m_labelDims, pOut, m_labelDims);
}

}
