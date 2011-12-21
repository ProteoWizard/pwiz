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
#include "GNeuralNet.h"
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





GEnsemble::GEnsemble(GRand& rand)
: GSupervisedLearner(rand), m_nAccumulatorDims(0), m_pAccumulator(NULL)
{
}

GEnsemble::GEnsemble(GDomNode* pNode, GLearnerLoader& ll)
: GSupervisedLearner(pNode, ll)
{
	m_pLabelRel = GRelation::deserialize(pNode->field("labelrel"));
	m_nAccumulatorDims = (size_t)pNode->field("accum")->asInt();
	m_pAccumulator = new double[m_nAccumulatorDims];
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

GEnsemble::~GEnsemble()
{
	for(vector<GWeightedModel*>::iterator it = m_models.begin(); it != m_models.end(); it++)
		delete(*it);
	delete[] m_pAccumulator;
}

// virtual
void GEnsemble::serializeBase(GDom* pDoc, GDomNode* pNode)
{
	pNode->addField(pDoc, "labelrel", m_pLabelRel->serialize(pDoc));
	pNode->addField(pDoc, "accum", pDoc->newInt(m_nAccumulatorDims));
	GDomNode* pModels = pNode->addField(pDoc, "models", pDoc->newList());
	for(size_t i = 0; i < m_models.size(); i++)
		pModels->addItem(pDoc, m_models[i]->serialize(pDoc));
}

void GEnsemble::clearBase()
{
	for(vector<GWeightedModel*>::iterator it = m_models.begin(); it != m_models.end(); it++)
		(*it)->m_pModel->clear();
	m_pLabelRel.reset();
	delete[] m_pAccumulator;
	m_pAccumulator = NULL;
	m_nAccumulatorDims = 0;
}

// virtual
void GEnsemble::trainInner(GMatrix& features, GMatrix& labels)
{
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

	trainInnerInner(features, labels);
}

void GEnsemble::normalizeWeights()
{
	double sum = 0.0;
	for(vector<GWeightedModel*>::iterator it = m_models.begin(); it != m_models.end(); it++)
		sum += (*it)->m_weight;
	double f = 1.0 / sum;
	for(vector<GWeightedModel*>::iterator it = m_models.begin(); it != m_models.end(); it++)
		(*it)->m_weight *= f;
}

void GEnsemble::castVote(double weight, const double* pOut)
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

void GEnsemble::tally(GPrediction* pOut)
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

void GEnsemble::tally(double* pOut)
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
void GEnsemble::predictInner(const double* pIn, double* pOut)
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
void GEnsemble::predictDistributionInner(const double* pIn, GPrediction* pOut)
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







GBag::GBag(GRand& rand)
: GEnsemble(rand), m_pCB(NULL), m_pThis(NULL), m_trainSize(1.0)
{
}

GBag::GBag(GDomNode* pNode, GLearnerLoader& ll)
: GEnsemble(pNode, ll), m_pCB(NULL), m_pThis(NULL)
{
	m_trainSize = pNode->field("ts")->asDouble();
}

GBag::~GBag()
{
}

// virtual
GDomNode* GBag::serialize(GDom* pDoc)
{
	GDomNode* pNode = baseDomNode(pDoc, "GBag");
	serializeBase(pDoc, pNode);
	pNode->addField(pDoc, "ts", pDoc->newDouble(m_trainSize));
	return pNode;
}

void GBag::clear()
{
	clearBase();
}

void GBag::flush()
{
	clear();
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
void GBag::trainInnerInner(GMatrix& features, GMatrix& labels)
{
	// Train all the models
	size_t nLearnerCount = m_models.size();
	size_t nDrawSize = size_t(m_trainSize * features.rows());
	GMatrix drawnFeatures(features.relation(), features.heap());
	GMatrix drawnLabels(labels.relation(), labels.heap());
	drawnFeatures.reserve(nDrawSize);
	drawnLabels.reserve(nDrawSize);
	{
		for(size_t i = 0; i < nLearnerCount; i++)
		{
			if(m_pCB)
				m_pCB(m_pThis, i, nLearnerCount);

			// Randomly draw some data (with replacement)
			GReleaseDataHolder hDrawnFeatures(&drawnFeatures);
			GReleaseDataHolder hDrawnLabels(&drawnLabels);
			for(size_t j = 0; j < nDrawSize; j++)
			{
				size_t r = (size_t)m_rand.next(features.rows());
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
	determineWeights(features, labels);
	normalizeWeights();
}

// virtual
void GBag::determineWeights(GMatrix& features, GMatrix& labels)
{
	for(vector<GWeightedModel*>::iterator it = m_models.begin(); it != m_models.end(); it++)
		(*it)->m_weight = 1.0;
}

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






// virtual
void GBayesianModelAveraging::determineWeights(GMatrix& features, GMatrix& labels)
{
	GTEMPBUF(double, results, labels.cols());
	double m = -500.0;
	for(vector<GWeightedModel*>::iterator it = m_models.begin(); it != m_models.end(); it++)
	{
		(*it)->m_pModel->accuracy(features, labels, results);
		double d = GVec::sumElements(results, labels.cols()) / labels.cols();
		double logProbHypothGivenData;
		if(d == 0.0)
			logProbHypothGivenData = -500.0;
		else if(d == 1.0)
			logProbHypothGivenData = 0.0;
		else
			logProbHypothGivenData = features.rows() * (d * log(d) + (1.0 - d) * log(1.0 - d));
		m = std::max(m, logProbHypothGivenData);
		(*it)->m_weight = logProbHypothGivenData;
	}
	for(vector<GWeightedModel*>::iterator it = m_models.begin(); it != m_models.end(); it++)
	{
		double logProbHypothGivenData = (*it)->m_weight;
		(*it)->m_weight = exp(logProbHypothGivenData - m);
	}
}

// virtual
GDomNode* GBayesianModelAveraging::serialize(GDom* pDoc)
{
	GDomNode* pNode = baseDomNode(pDoc, "GBayesianModelAveraging");
	serializeBase(pDoc, pNode);
	pNode->addField(pDoc, "ts", pDoc->newDouble(m_trainSize));
	return pNode;
}







GBayesianModelCombination::GBayesianModelCombination(GDomNode* pNode, GLearnerLoader& ll)
: GBag(pNode, ll)
{
	m_samples = (size_t)pNode->field("samps")->asInt();
}

// virtual
void GBayesianModelCombination::determineWeights(GMatrix& features, GMatrix& labels)
{
	double* pWeights = new double[m_models.size()];
	ArrayHolder<double> hWeights(pWeights);
	GVec::setAll(pWeights, 0.0, m_models.size());
	double sumWeight = 0.0;
	double maxLogProb = -500.0;
	GTEMPBUF(double, results, labels.cols());
	for(size_t i = 0; i < m_samples; i++)
	{
		// Set weights randomly from a dirichlet distribution with unifrom probabilities
		for(vector<GWeightedModel*>::iterator it = m_models.begin(); it != m_models.end(); it++)
			(*it)->m_weight = m_rand.exponential();
		normalizeWeights();

		// Evaluate accuracy
		accuracy(features, labels, results);
		double d = GVec::sumElements(results, labels.cols()) / labels.cols();
		double logProbEnsembleGivenData;
		if(d == 0.0)
			logProbEnsembleGivenData = -500.0;
		else if(d == 1.0)
			logProbEnsembleGivenData = 0.0;
		else
			logProbEnsembleGivenData = features.rows() * (d * log(d) + (1.0 - d) * log(1.0 - d));

		// Update the weights
		if(logProbEnsembleGivenData > maxLogProb)
		{
			GVec::multiply(pWeights, exp(maxLogProb - logProbEnsembleGivenData), m_models.size());
			maxLogProb = logProbEnsembleGivenData;
		}
		double w = exp(logProbEnsembleGivenData - maxLogProb);
		GVec::multiply(pWeights, sumWeight / (sumWeight + w), m_models.size());
		double* pW = pWeights;
		for(vector<GWeightedModel*>::iterator it = m_models.begin(); it != m_models.end(); it++)
			*(pW++) += w * (*it)->m_weight;
		sumWeight += w;
	}
	double* pW = pWeights;
	for(vector<GWeightedModel*>::iterator it = m_models.begin(); it != m_models.end(); it++)
		(*it)->m_weight = *(pW++);
}

// virtual
GDomNode* GBayesianModelCombination::serialize(GDom* pDoc)
{
	GDomNode* pNode = baseDomNode(pDoc, "GBayesianModelCombination");
	serializeBase(pDoc, pNode);
	pNode->addField(pDoc, "ts", pDoc->newDouble(m_trainSize));
	pNode->addField(pDoc, "samps", pDoc->newInt(m_samples));
	return pNode;
}






GAdaBoost::GAdaBoost(GSupervisedLearner* pLearner, bool ownLearner, GLearnerLoader* pLoader)
: GEnsemble(pLoader->rand()), m_pLearner(pLearner), m_ownLearner(ownLearner), m_pLoader(pLoader), m_trainSize(1.0), m_ensembleSize(30)
{
}

GAdaBoost::GAdaBoost(GDomNode* pNode, GLearnerLoader& ll)
: GEnsemble(pNode, ll), m_pLearner(NULL), m_ownLearner(false), m_pLoader(NULL)
{
	m_trainSize = pNode->field("ts")->asDouble();
	m_ensembleSize = (size_t)pNode->field("es")->asInt();
}

// virtual
GAdaBoost::~GAdaBoost()
{
	clear();
	if(m_ownLearner)
		delete(m_pLearner);
	delete(m_pLoader);
}

// virtual
GDomNode* GAdaBoost::serialize(GDom* pDoc)
{
	GDomNode* pNode = baseDomNode(pDoc, "GAdaBoost");
	serializeBase(pDoc, pNode);
	pNode->addField(pDoc, "es", pDoc->newInt(m_ensembleSize));
	pNode->addField(pDoc, "ts", pDoc->newDouble(m_trainSize));
	return pNode;
}

// virtual
void GAdaBoost::clear()
{
	for(vector<GWeightedModel*>::iterator it = m_models.begin(); it != m_models.end(); it++)
		delete(*it);
	m_models.clear();
	if(m_pLearner)
		m_pLearner->clear();
}

// virtual
void GAdaBoost::trainInnerInner(GMatrix& features, GMatrix& labels)
{
	clear();

	// Initialize all instances with uniform weights
	double* pDistribution = new double[features.rows()];
	ArrayHolder<double> hDistribution(pDistribution);
	GVec::setAll(pDistribution, 1.0 / features.rows(), features.rows());
	size_t drawRows = size_t(m_trainSize * features.rows());
	size_t* pDrawnIndexes = new size_t[drawRows];
	ArrayHolder<size_t> hDrawnIndexes(pDrawnIndexes);

	// Train the ensemble
	size_t labelDims = labels.cols();
	double penalty = 1.0 / labelDims;
	GTEMPBUF(double, prediction, labelDims);
	for(size_t es = 0; es < m_ensembleSize; es++)
	{
		// Draw a training set from the distribution
		GCategoricalSamplerBatch csb(features.rows(), pDistribution, m_rand);
		csb.draw(drawRows, pDrawnIndexes);
		GMatrix drawnFeatures(features.relation());
		GReleaseDataHolder hDrawnFeatures(&drawnFeatures);
		GMatrix drawnLabels(labels.relation());
		GReleaseDataHolder hDrawnLabels(&drawnLabels);
		size_t* pIndex = pDrawnIndexes;
		for(size_t i = 0; i < drawRows; i++)
		{
			drawnFeatures.takeRow(features[*pIndex]);
			drawnLabels.takeRow(labels[*pIndex]);
			pIndex++;
		}

		// Train an instance of the model and store a clone of it
		m_pLearner->train(drawnFeatures, drawnLabels);
		GDom doc;
		GSupervisedLearner* pClone = m_pLoader->loadSupervisedLearner(m_pLearner->serialize(&doc));

		// Compute model weight
		double err = 0.0;
		for(size_t i = 0; i < features.rows(); i++)
		{
			pClone->predict(features[i], prediction);
			double* pTarget = labels[i];
			double* pPred = prediction;
			for(size_t j = 0; j < labelDims; j++)
			{
				if((int)*(pTarget++) != (int)*(pPred++))
					err += penalty;
			}
		}
		err /= features.rows();
		if(err >= 0.5)
		{
			delete(pClone);
			break;
		}
		double weight = 0.5 * log((1.0 - err) / err);
		m_models.push_back(new GWeightedModel(weight, pClone));

		// Update the distribution to favor mis-classified instances
		double* pDist = pDistribution;
		for(size_t i = 0; i < features.rows(); i++)
		{
			err = 0.0;
			pClone->predict(features[i], prediction);
			double* pTarget = labels[i];
			double* pPred = prediction;
			for(size_t j = 0; j < labelDims; j++)
			{
				if((int)*(pTarget++) != (int)*(pPred++))
					err += penalty;
			}
			err /= labelDims;
			*pDist *= exp(weight * (err * 2.0 - 1.0));
			pDist++;
		}
		GVec::sumToOne(pDistribution, features.rows());
	}
	normalizeWeights();
}

#ifndef NO_TEST_CODE
// static
void GAdaBoost::test()
{
	GRand rand(0);
	GDecisionTree* pLearner = new GDecisionTree(rand);
	pLearner->useRandomDivisions();
	GAdaBoost boost(pLearner, true, new GLearnerLoader(rand));
	boost.basicTest(0.757, 0.757);
}
#endif








GWag::GWag(size_t size, GRand& rand)
: GSupervisedLearner(rand)
{
	m_pNN = new GNeuralNet(rand);
}

GWag::GWag(GDomNode* pNode, GLearnerLoader& ll)
: GSupervisedLearner(pNode, ll)
{
	m_pNN = new GNeuralNet(pNode->field("nn"), ll);
	m_models = (size_t)pNode->field("models")->asInt();
}

// virtual
GWag::~GWag()
{
	delete(m_pNN);
}

// virtual
GDomNode* GWag::serialize(GDom* pDoc)
{
	GDomNode* pNode = baseDomNode(pDoc, "GWag");
	pNode->addField(pDoc, "models", pDoc->newInt(m_models));
	pNode->addField(pDoc, "nn", m_pNN->serialize(pDoc));
	return pNode;
}

// virtual
void GWag::clear()
{
	m_pNN->clear();
}

// virtual
void GWag::trainInner(GMatrix& features, GMatrix& labels)
{
	GNeuralNet* pTemp = NULL;
	Holder<GNeuralNet> hTemp;
	size_t weights = 0;
	double* pWeightBuf = NULL;
	double* pWeightBuf2 = NULL;
	ArrayHolder<double> hWeightBuf;
	for(size_t i = 0; i < m_models; i++)
	{
		m_pNN->train(features, labels);
		if(pTemp)
		{
			// Average m_pNN with pTemp
			m_pNN->align(*pTemp);
			pTemp->weights(pWeightBuf);
			m_pNN->weights(pWeightBuf2);
			GVec::multiply(pWeightBuf, double(i) / (i + 1), weights);
			GVec::addScaled(pWeightBuf, 1.0 / (i + 1), pWeightBuf2, weights);
			pTemp->setWeights(pWeightBuf);
		}
		else
		{
			// Copy the m_pNN
			GDom doc;
			GDomNode* pNode = m_pNN->serialize(&doc);
			GLearnerLoader ll(m_rand);
			pTemp = new GNeuralNet(pNode, ll);
			hTemp.reset(pTemp);
			weights = pTemp->countWeights();
			pWeightBuf = new double[2 * weights];
			hWeightBuf.reset(pWeightBuf);
			pWeightBuf2 = pWeightBuf + weights;
		}
	}
	pTemp->weights(pWeightBuf);
	m_pNN->setWeights(pWeightBuf);
}

// virtual
void GWag::predictInner(const double* pIn, double* pOut)
{
	m_pNN->predict(pIn, pOut);
}

// virtual
void GWag::predictDistributionInner(const double* pIn, GPrediction* pOut)
{
	m_pNN->predictDistribution(pIn, pOut);
}








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
	bucket.basicTest(0.70, 0.77);
}
#endif
