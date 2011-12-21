/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#include "GEnsemble.h"
#include "GVec.h"
#include <stdlib.h>
#include "GDistribution.h"
#include "GDom.h"
#include "GRand.h"

using namespace GClasses;
using std::vector;


GWeightedModel::GWeightedModel(GDomNode* pNode, GLearnerLoader& ll)
{
	m_weight = pNode->field("w")->asDouble();
	m_pModel = ll.loadSupervisedLearner(pNode->field("m"));
}

GWeightedModel::~GWeightedModel()
{
	delete(m_pModel);
}

GDomNode* GWeightedModel::serialize(GDom* pDoc)
{
	GDomNode* pNode = pDoc->newObj();
	pNode->addField(pDoc, "w", pDoc->newDouble(m_weight));
	pNode->addField(pDoc, "m", m_pModel->serialize(pDoc));
	return pNode;
}






GBag::GBag(GRand& rand)
: GSupervisedLearner(rand), m_nAccumulatorDims(0), m_pAccumulator(NULL), m_pCB(NULL), m_pThis(NULL)
{
}

GBag::GBag(GDomNode* pNode, GLearnerLoader& ll)
: GSupervisedLearner(pNode, ll)
{
	m_pLabelRel = GRelation::deserialize(pNode->field("labelrel"));
	m_nAccumulatorDims = (size_t)pNode->field("accum")->asInt();
	m_pAccumulator = new double[m_nAccumulatorDims];
	m_pCB = NULL;
	m_pThis = NULL;
	GDomNode* pModels = pNode->field("models");
	GDomListIterator it(pModels);
	size_t modelCount = it.remaining();
	for(size_t i = 0; i < modelCount; i++)
	{
		GWeightedModel* pWM = new GWeightedModel(it.current(), ll);
		m_models.push_back(pWM);
		it.advance();
	}
}

GBag::~GBag()
{
	for(vector<GWeightedModel*>::iterator it = m_models.begin(); it != m_models.end(); it++)
		delete(*it);
	delete[] m_pAccumulator;
}

// virtual
GDomNode* GBag::serialize(GDom* pDoc)
{
	GDomNode* pNode = baseDomNode(pDoc, "GBag");
	pNode->addField(pDoc, "featuredims", pDoc->newInt(m_featureDims));
	pNode->addField(pDoc, "labelrel", m_pLabelRel->serialize(pDoc));
	pNode->addField(pDoc, "accum", pDoc->newInt(m_nAccumulatorDims));
	GDomNode* pModels = pNode->addField(pDoc, "models", pDoc->newList());
	for(size_t i = 0; i < m_models.size(); i++)
		pModels->addItem(pDoc, m_models[i]->serialize(pDoc));
	return pNode;
}

void GBag::clear()
{
	for(vector<GWeightedModel*>::iterator it = m_models.begin(); it != m_models.end(); it++)
		(*it)->m_pModel->clear();
	m_pLabelRel.reset();
	m_featureDims = 0;
	delete[] m_pAccumulator;
	m_pAccumulator = NULL;
	m_nAccumulatorDims = 0;
}

void GBag::flush()
{
	for(vector<GWeightedModel*>::iterator it = m_models.begin(); it != m_models.end(); it++)
		delete(*it);
	m_models.clear();
}

void GBag::addLearner(GSupervisedLearner* pLearner)
{
	GWeightedModel* pWM = new GWeightedModel(0.0, pLearner); // The weight will be fixed later
	m_models.push_back(pWM);
}

// virtual
void GBag::trainInner(GMatrix& features, GMatrix& labels)
{
	m_featureDims = features.cols();
	m_pLabelRel = labels.relation();

	// Make the accumulator buffer
	size_t labelDims = m_pLabelRel->size();
	m_nAccumulatorDims = 0;
	for(size_t i = 0; i < labelDims; i++)
	{
		size_t nValues = m_pLabelRel->valueCount(i);
		if(nValues > 0)
			m_nAccumulatorDims += nValues;
		else
			m_nAccumulatorDims += 2; // mean and variance
	}
	delete[] m_pAccumulator;
	m_pAccumulator = new double[m_nAccumulatorDims];

	// Train all the models
	size_t nLearnerCount = m_models.size();
	size_t nVectorCount = features.rows();
	GMatrix drawnFeatures(features.relation(), features.heap());
	GMatrix drawnLabels(labels.relation(), labels.heap());
	drawnFeatures.reserve(nVectorCount);
	drawnLabels.reserve(nVectorCount);
	{
		for(size_t i = 0; i < nLearnerCount; i++)
		{
			if(m_pCB)
				m_pCB(m_pThis, i, nLearnerCount);

			// Randomly draw some data (with replacement)
			GReleaseDataHolder hDrawnFeatures(&drawnFeatures);
			GReleaseDataHolder hDrawnLabels(&drawnLabels);
			for(size_t j = 0; j < nVectorCount; j++)
			{
				size_t r = (size_t)m_rand.next(nVectorCount);
				drawnFeatures.takeRow(features[r]);
				drawnLabels.takeRow(labels[r]);
			}

			// Train the learner with the drawn data
			m_models[i]->m_pModel->train(drawnFeatures, drawnLabels);
		}
		if(m_pCB)
			m_pCB(m_pThis, nLearnerCount, nLearnerCount);
	}

	// Determine the weights
	determineWeights();
	normalizeWeights();
}

// virtual
void GBag::determineWeights()
{
	for(vector<GWeightedModel*>::iterator it = m_models.begin(); it != m_models.end(); it++)
		(*it)->m_weight = 1.0;
}

void GBag::normalizeWeights()
{
	double sum = 0.0;
	for(vector<GWeightedModel*>::iterator it = m_models.begin(); it != m_models.end(); it++)
		sum += (*it)->m_weight;
	double f = 1.0 / sum;
	for(vector<GWeightedModel*>::iterator it = m_models.begin(); it != m_models.end(); it++)
		(*it)->m_weight *= f;
}

void GBag::castVote(double weight, const double* pOut)
{
	size_t labelDims = m_pLabelRel->size();
	size_t nDims = 0;
	for(size_t i = 0; i < labelDims; i++)
	{
		size_t nValues = m_pLabelRel->valueCount(i);
		if(nValues > 0)
		{
			int nVal = (int)pOut[i];
			if(nVal >= 0 && nVal < (int)nValues)
				m_pAccumulator[nDims + nVal] += weight;
			nDims += nValues;
		}
		else
		{
			double dVal = pOut[i];
			m_pAccumulator[nDims++] += weight * dVal;
			m_pAccumulator[nDims++] += weight * (dVal * dVal);
		}
	}
	GAssert(nDims == m_nAccumulatorDims); // invalid dim count
}

void GBag::tally(GPrediction* pOut)
{
	size_t labelDims = m_pLabelRel->size();
	size_t nDims = 0;
	double mean;
	for(size_t i = 0; i < labelDims; i++)
	{
		size_t nValues = m_pLabelRel->valueCount(i);
		if(nValues > 0)
		{
			pOut[i].makeCategorical()->setValues(nValues, &m_pAccumulator[nDims]);
			nDims += nValues;
		}
		else
		{
			mean = m_pAccumulator[nDims];
			pOut[i].makeNormal()->setMeanAndVariance(mean, m_pAccumulator[nDims + 1] - (mean * mean));
			nDims += 2;
		}
	}
	GAssert(nDims == m_nAccumulatorDims); // invalid dim count
}

void GBag::tally(double* pOut)
{
	size_t labelDims = m_pLabelRel->size();
	size_t nDims = 0;
	for(size_t i = 0; i < labelDims; i++)
	{
		size_t nValues = m_pLabelRel->valueCount(i);
		if(nValues > 0)
		{
			pOut[i] = (double)GVec::indexOfMax(m_pAccumulator + nDims, nValues, &m_rand);
			nDims += nValues;
		}
		else
		{
			pOut[i] = m_pAccumulator[nDims];
			nDims += 2;
		}
	}
	GAssert(nDims == m_nAccumulatorDims); // invalid dim count
}

// virtual
void GBag::predictInner(const double* pIn, double* pOut)
{
	GVec::setAll(m_pAccumulator, 0.0, m_nAccumulatorDims);
	for(vector<GWeightedModel*>::iterator it = m_models.begin(); it != m_models.end(); it++)
	{
		GWeightedModel* pWM = *it;
		pWM->m_pModel->predict(pIn, pOut);
		castVote(pWM->m_weight, pOut);
	}
	tally(pOut);
}

// virtual
void GBag::predictDistributionInner(const double* pIn, GPrediction* pOut)
{
	GTEMPBUF(double, pTmp, m_pLabelRel->size());
	GVec::setAll(m_pAccumulator, 0.0, m_nAccumulatorDims);
	for(vector<GWeightedModel*>::iterator it = m_models.begin(); it != m_models.end(); it++)
	{
		GWeightedModel* pWM = *it;
		pWM->m_pModel->predict(pIn, pTmp);
		castVote(pWM->m_weight, pTmp);
	}
	tally(pOut);
}
/*
// virtual
double GBag::crossValidate(GMatrix* pData, int nFolds, bool bRegression)
{
	// Split the data into parts
	GTEMPBUF(GMatrix*, pSets, nFolds);
	int nSize = pData->size() / nFolds + nFolds;
	int n, i, j, nLearner;
	for(n = 0; n < nFolds; n++)
		pSets[n] = new GMatrix(nSize);
	int nRowCount = pData->size();
	double* pRow;
	for(n = 0; n < nRowCount; n++)
	{
		pRow = pData->row(n);
		pSets[n % nFolds]->AddVector(pRow);
	}

	// Do the training and testing
	double d;
	double dScore = 0;
	GMatrix trainingSet(pData->size());
	for(n = 0; n < nFolds; n++)
	{
		// Train with all of the sub-sets except one
		{
			GReleaseDataHolder hReleaseData(&trainingSet);
			for(i = 0; i < nFolds; i++)
			{
				if(i == n)
					continue;
				int nCount = pSets[i]->size();
				for(j = 0; j < nCount; j++)
				{
					pRow = pSets[i]->row(j);
					trainingSet.AddVector(pRow);
				}
			}


			initialize the accumulator
			for(nLearner = 0; nLearner < nLearner->size(); nLearner++)
			{
				pLearner = m_models[i];
				pLearner->train(&pTrainer);

				eval each row in pSets[n], and accumulate the results

				// Free the model
				pLearner->Clear();
			}
			pAverageSet = generate a set of average results

			// Measure accuracy
			if(bRegression)
				d = MeasureMeanSquaredError(pAverageSet);
			else
				d = MeasurePredictiveAccuracy(pAverageSet);
			dScore += d;
		}
	}
	dScore /= nFolds;

	// Clean up
	for(n = 0; n < nFolds; n++)
	{
		pSets[n]->releaseAllRows();
		delete(pSets[n]);
	}

	return dScore;
}
*/

#ifndef NO_TEST_CODE
#include "GDecisionTree.h"
// static
void GBag::test()
{
	GRand rand(0);
	GBag bag(rand);
	for(size_t i = 0; i < 64; i++)
	{
		GDecisionTree* pTree = new GDecisionTree(rand);
		pTree->useRandomDivisions();
		bag.addLearner(pTree);
	}
	bag.basicTest(0.76, 0.76, 0.01);
}
#endif

// -------------------------------------------------------------------------

GBucket::GBucket(GRand& rand)
: GSupervisedLearner(rand)
{
	m_nBestLearner = -1;
}

GBucket::GBucket(GDomNode* pNode, GLearnerLoader& ll)
: GSupervisedLearner(pNode, ll)
{
	GDomNode* pModels = pNode->field("models");
	GDomListIterator it(pModels);
	size_t modelCount = it.remaining();
	for(size_t i = 0; i < modelCount; i++)
	{
		m_models.push_back(ll.loadSupervisedLearner(it.current()));
		it.advance();
	}
	m_nBestLearner = (size_t)pNode->field("best")->asInt();
}

GBucket::~GBucket()
{
	for(vector<GSupervisedLearner*>::iterator it = m_models.begin(); it != m_models.end(); it++)
		delete(*it);
}

// virtual
GDomNode* GBucket::serialize(GDom* pDoc)
{
	GDomNode* pNode = baseDomNode(pDoc, "GBucket");
	GDomNode* pModels = pNode->addField(pDoc, "models", pDoc->newList());
	pModels->addItem(pDoc, m_models[m_nBestLearner]->serialize(pDoc));
	pNode->addField(pDoc, "best", pDoc->newInt(0));
	return pNode;
}

void GBucket::clear()
{
	for(vector<GSupervisedLearner*>::iterator it = m_models.begin(); it != m_models.end(); it++)
		(*it)->clear();
}

void GBucket::flush()
{
	for(vector<GSupervisedLearner*>::iterator it = m_models.begin(); it != m_models.end(); it++)
		delete(*it);
	m_models.clear();
}

void GBucket::addLearner(GSupervisedLearner* pLearner)
{
	m_models.push_back(pLearner);
}

// virtual
void GBucket::trainInner(GMatrix& features, GMatrix& labels)
{
	size_t nLearnerCount = m_models.size();
	double dBestError = 1e200;
	GSupervisedLearner* pLearner;
	m_nBestLearner = (size_t)m_rand.next(nLearnerCount);
	double err;
	for(size_t i = 0; i < nLearnerCount; i++)
	{
		pLearner = m_models[i];
		try
		{
			err = pLearner->heuristicValidate(features, labels);
		}
		catch(std::exception& e)
		{
			onError(e);
			continue;
		}
		if(err < dBestError)
		{
			dBestError = err;
			m_nBestLearner = i;
		}
		pLearner->clear();
	}
	pLearner = m_models[m_nBestLearner];
	pLearner->train(features, labels);
}

GSupervisedLearner* GBucket::releaseBestModeler()
{
	if(m_nBestLearner < 0)
		ThrowError("Not trained yet");
	GSupervisedLearner* pModeler = m_models[m_nBestLearner];
	m_models[m_nBestLearner] = m_models[m_models.size() - 1];
	m_models.pop_back();
	m_nBestLearner = -1;
	return pModeler;
}

// virtual
void GBucket::predictInner(const double* pIn, double* pOut)
{
	if(m_nBestLearner < 0)
		ThrowError("not trained yet");
	m_models[m_nBestLearner]->predict(pIn, pOut);
}

// virtual
void GBucket::predictDistributionInner(const double* pIn, GPrediction* pOut)
{
	if(m_nBestLearner < 0)
		ThrowError("not trained yet");
	m_models[m_nBestLearner]->predictDistribution(pIn, pOut);
}

// virtual
void GBucket::onError(std::exception& e)
{
	//cout << e.what() << "\n";
}

#ifndef NO_TEST_CODE
#include "GDecisionTree.h"
// static
void GBucket::test()
{
	GRand rand(0);
	GBucket bucket(rand);
	bucket.addLearner(new GBaselineLearner(rand));
	bucket.addLearner(new GDecisionTree(rand));
	bucket.addLearner(new GMeanMarginsTree(rand));
	bucket.basicTest(0.70, 0.73);
}
#endif
