/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#include "GLearner.h"
#include <stdlib.h>
#include <string.h>
#include "GError.h"
#include "GVec.h"
#include "GHeap.h"
#include "GDom.h"
#include "GImage.h"
#include "GNeuralNet.h"
#include "GKNN.h"
#include "GDecisionTree.h"
#include "GNaiveInstance.h"
#include "GLinear.h"
#include "GNaiveBayes.h"
#include "GEnsemble.h"
#include "GPolynomial.h"
#include "GTransform.h"
#include "GRand.h"
#include "GPlot.h"
#include "GDistribution.h"
#include "GRecommender.h"
#include <cmath>
#include <iostream>

using std::vector;

namespace GClasses {

GPrediction::~GPrediction()
{
	delete(m_pDistribution);
}

bool GPrediction::isContinuous()
{
	return m_pDistribution->type() == GUnivariateDistribution::normal;
}

// static
void GPrediction::predictionArrayToVector(size_t nOutputCount, GPrediction* pOutputs, double* pVector)
{
	for(size_t i = 0; i < nOutputCount; i++)
		pVector[i] = pOutputs[i].mode();
}

// static
void GPrediction::vectorToPredictionArray(GRelation* pRelation, size_t nOutputCount, double* pVector, GPrediction* pOutputs)
{
	size_t nInputs = pRelation->size() - nOutputCount;
	for(size_t i = 0; i < nOutputCount; i++)
	{
		size_t nValueCount = pRelation->valueCount(nInputs + i);
		if(nValueCount == 0)
			pOutputs[i].makeNormal()->setMeanAndVariance(pVector[i], 1);
		else
			pOutputs[i].makeCategorical()->setSpike(nValueCount, (size_t)pVector[i], 1);
	}
}

double GPrediction::mode()
{
	return m_pDistribution->mode();
}

GCategoricalDistribution* GPrediction::makeCategorical()
{
	if(!m_pDistribution || m_pDistribution->type() != GUnivariateDistribution::categorical)
	{
		delete(m_pDistribution);
		m_pDistribution = new GCategoricalDistribution();
	}
	return (GCategoricalDistribution*)m_pDistribution;
}

GNormalDistribution* GPrediction::makeNormal()
{
	if(!m_pDistribution || m_pDistribution->type() != GUnivariateDistribution::normal)
	{
		delete(m_pDistribution);
		m_pDistribution = new GNormalDistribution();
	}
	return (GNormalDistribution*)m_pDistribution;
}

GCategoricalDistribution* GPrediction::asCategorical()
{
	if(!m_pDistribution || m_pDistribution->type() != GUnivariateDistribution::categorical)
		ThrowError("The current distribution is not a categorical distribution");
	return (GCategoricalDistribution*)m_pDistribution;
}

GNormalDistribution* GPrediction::asNormal()
{
	if(!m_pDistribution || m_pDistribution->type() != GUnivariateDistribution::normal)
		ThrowError("The current distribution is not a normal distribution");
	return (GNormalDistribution*)m_pDistribution;
}

// ---------------------------------------------------------------

GTransducer::GTransducer(GRand& rand)
: m_rand(rand)
{
}

GTransducer::~GTransducer()
{
}

class GTransducerTrainAndTestCleanUpper
{
protected:
	GMatrix* m_pData;
	size_t m_nTestSize;

public:
	GTransducerTrainAndTestCleanUpper(GMatrix* pData, size_t nTestSize)
	: m_pData(pData), m_nTestSize(nTestSize)
	{
	}

	~GTransducerTrainAndTestCleanUpper()
	{
		while(m_pData->rows() > m_nTestSize)
			m_pData->releaseRow(m_pData->rows() - 1);
	}
};

// virtual
GMatrix* GTransducer::transduce(GMatrix& features1, GMatrix& labels1, GMatrix& features2)
{
	if(features1.rows() != labels1.rows())
		ThrowError("Expected features1 and labels1 to have the same number of rows");
	if(features1.cols() != features2.cols())
		ThrowError("Expected both feature matrices to have the same number of cols");

	// Convert the features to a form that this algorithm can handle
	GMatrix& f1 = features1;
	GMatrix& f2 = features2;
	Holder<GMatrix> hF1(NULL);
	Holder<GMatrix> hF2(NULL);
	if(!canImplicitlyHandleNominalFeatures())
	{
		if(!canImplicitlyHandleContinuousFeatures())
			ThrowError("Can't handle nominal or continuous features");

		// Convert nominal features to continuous
 		if(!features1.relation()->areContinuous(0, features1.relation()->size()))
 		{
			GNominalToCat ntc;
			ntc.train(f1);
			GMatrix* pF1 = ntc.transformBatch(f1);
			hF1.reset(pF1);
			f1 = *pF1;
			GMatrix* pF2 = ntc.transformBatch(f2);
			hF2.reset(pF2);
			f2 = *pF2;
		}
	}
	if(!canImplicitlyHandleContinuousFeatures())
	{
		if(!canImplicitlyHandleNominalFeatures())
			ThrowError("Can't handle nominal or continuous features");

		// Convert continuous features to nominal
		if(!features1.relation()->areNominal(0, features1.relation()->size()))
		{
			GDiscretize disc;
			disc.train(f1); // todo: should really use both feature sets here
			GMatrix* pF1 = disc.transformBatch(features1);
			hF1.reset(pF1);
			f1 = *pF1;
			GMatrix* pF2 = disc.transformBatch(features2);
			hF2.reset(pF2);
			f2 = *pF2;
		}
	}
	if(canImplicitlyHandleContinuousFeatures())
	{
		// Normalize feature values to fall within a supported range
		double fmin, fmax;
		if(!supportedFeatureRange(&fmin, &fmax))
		{
			GNormalize norm(fmin, fmax);
			norm.train(f1); // todo: should really use both feature sets here
			GMatrix* pF1 = norm.transformBatch(f1);
			hF1.reset(pF1);
			f1 = *pF1;
			GMatrix* pF2 = norm.transformBatch(f2);
			hF2.reset(pF2);
			f2 = *pF2;
		}
	}

	// Take care of the labels
	if(!canImplicitlyHandleContinuousLabels())
	{
		if(!canImplicitlyHandleNominalLabels())
			ThrowError("This algorithm says it cannot handle nominal or continuous labels");
		if(labels1.relation()->areNominal(0, labels1.relation()->size()))
			return transduceInner(f1, labels1, f2);
		else
		{
			GDiscretize disc;
			disc.train(labels1);
			GMatrix* pL1 = disc.transformBatch(labels1);
			Holder<GMatrix> hL1(pL1);
			GMatrix* pL2 = transduceInner(f1, *pL1, f2);
			Holder<GMatrix> hL2(pL2);
			return disc.untransformBatch(*pL2);
		}
	}
	else
	{
		if(canImplicitlyHandleNominalLabels() || labels1.relation()->areContinuous(0, labels1.relation()->size()))
		{
			double lmin, lmax;
			if(supportedLabelRange(&lmin, &lmax))
				return transduceInner(f1, labels1, f2);
			else
			{
				GNormalize norm(lmin, lmax);
				norm.train(labels1);
				GMatrix* pL1 = norm.transformBatch(labels1);
				Holder<GMatrix> hL1(pL1);
				GMatrix* pL2 = transduceInner(f1, *pL1, f2);
				Holder<GMatrix> hL2(pL2);
				return norm.untransformBatch(*pL2);
			}
		}
		else
		{
			double lmin, lmax;
			if(supportedLabelRange(&lmin, &lmax))
			{
				GNominalToCat ntc;
				ntc.train(labels1);
				GMatrix* pL1 = ntc.transformBatch(labels1);
				Holder<GMatrix> hL1(pL1);
				GMatrix* pL2 = transduceInner(f1, *pL1, f2);
				Holder<GMatrix> hL2(pL2);
				return ntc.untransformBatch(*pL2);
			}
			else
			{
				// todo: both nominalToCat and normalization filters are necessary in this case
				ThrowError("case not yet supported");
				return NULL;
			}
		}
	}
}

// virtual
void GTransducer::trainAndTest(GMatrix& trainFeatures, GMatrix& trainLabels, GMatrix& testFeatures, GMatrix& testLabels, double* pOutResults, std::vector<GMatrix*>* pNominalLabelStats)
{
	// Check assumptions
	if(testFeatures.rows() != testLabels.rows())
		ThrowError("Expected the test features to have the same number of rows as the test labels");
	if(trainFeatures.cols() != testFeatures.cols())
		ThrowError("Expected the training features and test features to have the same number of columns");

	// Transduce
	GMatrix* pPredictedLabels = transduce(trainFeatures, trainLabels, testFeatures);
	Holder<GMatrix> hPredictedLabels(pPredictedLabels);

	// Evaluate the results
	size_t labelDims = trainLabels.cols();
	GVec::setAll(pOutResults, 0.0, labelDims);
	for(size_t i = 0; i < labelDims; i++)
	{
		*pOutResults = testLabels.columnSumSquaredDifference(*pPredictedLabels, i) / testLabels.rows();
		if(testLabels.relation()->valueCount(i) > 0)
			*pOutResults = 1.0 - *pOutResults;
		pOutResults++;
	}
	if(pNominalLabelStats)
	{
		pNominalLabelStats->resize(labelDims);
		for(size_t j = 0; j < labelDims; j++)
		{
			size_t vals = testLabels.relation()->valueCount(j);
			if(vals > 0)
			{
				(*pNominalLabelStats)[j] = new GMatrix(vals, vals);
				(*pNominalLabelStats)[j]->setAll(0.0);
			}
		}
		for(size_t i = 0; i < pPredictedLabels->rows(); i++)
		{
			double* pTarget = testLabels[i];
			double* pPred = pPredictedLabels->row(i);
			for(size_t j = 0; j < labelDims; j++)
			{
				if((*pNominalLabelStats)[j])
				{
					if((int)*pTarget >= 0 && (int)*pPred >= 0)
						((*pNominalLabelStats)[j])->row((int)*pTarget)[(int)*pPred]++;
				}
				pTarget++;
				pPred++;
			}
		}
	}
}

double GTransducer::heuristicValidate(GMatrix& features, GMatrix& labels)
{
	// Check assumptions
	if(features.rows() != labels.rows())
		ThrowError("Expected the features and labels to have the same number of rows");

	// Randomly divide into two datasets
	GMatrix featuresA(features.relation());
	GReleaseDataHolder hFeaturesA(&featuresA);
	featuresA.reserve(features.rows());
	GMatrix featuresB(features.relation());
	GReleaseDataHolder hFeaturesB(&featuresB);
	featuresB.reserve(features.rows());
	GMatrix labelsA(labels.relation());
	GReleaseDataHolder hLabelsA(&labelsA);
	labelsA.reserve(labels.rows());
	GMatrix labelsB(labels.relation());
	GReleaseDataHolder hLabelsB(&labelsB);
	labelsB.reserve(labels.rows());
	for(size_t i = 0; i < features.rows(); i++)
	{
		if(m_rand.next() & 1)
		{
			featuresA.takeRow(features[i]);
			labelsA.takeRow(labels[i]);
		}
		else
		{
			featuresB.takeRow(features[i]);
			labelsB.takeRow(labels[i]);
		}
	}

	// Evaluate
	GTEMPBUF(double, pResults1, 2 * labels.cols());
	double* pResults2 = pResults1 + labels.cols();
	trainAndTest(featuresA, labelsA, featuresB, labelsB, pResults1);
	trainAndTest(featuresB, labelsB, featuresA, labelsA, pResults2);
	double err = 0;
	for(size_t i = 0; i < labels.cols(); i++)
	{
		if(labels.relation()->valueCount(i) == 0)
		{
			err += 0.5 * pResults1[i];
			err += 0.5 * pResults2[i];
		}
		else
		{
			double d = 1.0 - pResults1[i];
			err += 0.5 * d;
			d = 1.0 - pResults2[i];
			err += 0.5 * d;
		}
	}
	return err;
}

GMatrix* GTransducer::crossValidate(GMatrix& features, GMatrix& labels, size_t folds, RepValidateCallback pCB, size_t nRep, void* pThis)
{
	if(features.rows() != labels.rows())
		ThrowError("Expected the features and labels to have the same number of rows");

	// Make a place to store the results
	GMatrix* pResults = new GMatrix(0, labels.cols());
	pResults->reserve(folds);
	Holder<GMatrix> hResults(pResults);

	// Do cross-validation
	GMatrix trainFeatures(features.relation(), features.heap());
	trainFeatures.reserve(features.rows());
	GMatrix testFeatures(features.relation(), features.heap());
	testFeatures.reserve(features.rows() / folds + 1);
	GMatrix trainLabels(labels.relation(), labels.heap());
	trainLabels.reserve(labels.rows());
	GMatrix testLabels(labels.relation(), labels.heap());
	testLabels.reserve(labels.rows() / folds + 1);
	for(size_t i = 0; i < folds; i++)
	{
		// Divide into a training set and a test set
		GReleaseDataHolder hTrainFeatures(&trainFeatures);
		GReleaseDataHolder hTestFeatures(&testFeatures);
		GReleaseDataHolder hTrainLabels(&trainLabels);
		GReleaseDataHolder hTestLabels(&testLabels);
		size_t foldStart = i * features.rows() / folds;
		size_t foldEnd = (i + 1) * features.rows() / folds;
		for(size_t j = 0; j < foldStart; j++)
		{
			trainFeatures.takeRow(features[j]);
			trainLabels.takeRow(labels[j]);
		}
		for(size_t j = foldStart; j < foldEnd; j++)
		{
			testFeatures.takeRow(features[j]);
			testLabels.takeRow(labels[j]);
		}
		for(size_t j = foldEnd; j < features.rows(); j++)
		{
			trainFeatures.takeRow(features[j]);
			trainLabels.takeRow(labels[j]);
		}

		// Evaluate
		double* pFoldResults = pResults->newRow();
		trainAndTest(trainFeatures, trainLabels, testFeatures, testLabels, pFoldResults);
		if(pCB)
			pCB(pThis, nRep, i, labels.cols(), pFoldResults);
	}
	return hResults.release();
}

GMatrix* GTransducer::repValidate(GMatrix& features, GMatrix& labels, size_t reps, size_t folds, RepValidateCallback pCB, void* pThis)
{
	GMatrix* pResults = new GMatrix(0, labels.cols());
	pResults->reserve(reps * folds);
	Holder<GMatrix> hResults(pResults);
	for(size_t i = 0; i < reps; i++)
	{
		features.shuffle(m_rand, &labels);
		GMatrix* pRepResults = crossValidate(features, labels, folds, pCB, i, pThis);
		pResults->mergeVert(pRepResults);
		delete(pRepResults);
	}
	return hResults.release();
}

// ---------------------------------------------------------------

GSupervisedLearner::GSupervisedLearner(GRand& rand)
: GTransducer(rand), m_pFeatureFilter(NULL), m_pLabelFilter(NULL), m_autoFilter(true), m_featureDims((size_t)-1), m_labelDims((size_t)-1), m_pCalibrations(NULL)
{
}

GSupervisedLearner::GSupervisedLearner(GDomNode* pNode, GLearnerLoader& ll)
: GTransducer(ll.rand()), m_pFeatureFilter(NULL), m_pLabelFilter(NULL)
{
	GDomNode* pFeatureFilter = pNode->fieldIfExists("ff");
	if(pFeatureFilter)
		m_pFeatureFilter = ll.loadTwoWayIncrementalTransform(pFeatureFilter);
	GDomNode* pLabelFilter = pNode->fieldIfExists("lf");
	if(pLabelFilter)
		m_pLabelFilter = ll.loadTwoWayIncrementalTransform(pLabelFilter);
	m_featureDims = (size_t)pNode->field("fd")->asInt();
	m_labelDims = (size_t)pNode->field("ld")->asInt();
	m_autoFilter = pNode->field("af")->asBool();
	m_pCalibrations = NULL;
	GDomNode* pCalibs = pNode->fieldIfExists("cal");
	if(pCalibs)
	{
		GDomListIterator it(pCalibs);
		if(it.remaining() != m_labelDims)
			ThrowError("The number of calibrations does not match the number of labels");
		m_pCalibrations = new GNeuralNet*[m_labelDims];
		for(size_t i = 0; i < m_labelDims; i++)
		{
			m_pCalibrations[i] = new GNeuralNet(it.current(), ll);
			it.advance();
		}
	}
}

GSupervisedLearner::~GSupervisedLearner()
{
	if(m_pCalibrations)
	{
		for(size_t i = 0; i < m_labelDims; i++)
			delete(m_pCalibrations[i]);
		delete[] m_pCalibrations;
	}
	delete(m_pFeatureFilter);
	delete(m_pLabelFilter);
}

GDomNode* GSupervisedLearner::baseDomNode(GDom* pDoc, const char* szClassName)
{
	if(m_featureDims == (size_t)-1 || m_labelDims == (size_t)-1)
		ThrowError("The model must be trained before it is serialized.");
	GDomNode* pNode = pDoc->newObj();
	pNode->addField(pDoc, "class", pDoc->newString(szClassName));
	if(m_pFeatureFilter)
		pNode->addField(pDoc, "ff", m_pFeatureFilter->serialize(pDoc));
	if(m_pLabelFilter)
		pNode->addField(pDoc, "lf", m_pLabelFilter->serialize(pDoc));
	pNode->addField(pDoc, "fd", pDoc->newInt(m_featureDims));
	pNode->addField(pDoc, "ld", pDoc->newInt(m_labelDims));
	pNode->addField(pDoc, "af", pDoc->newBool(m_autoFilter));
	if(m_pCalibrations)
	{
		GDomNode* pCal = pNode->addField(pDoc, "cal", pDoc->newList());
		for(size_t i = 0; i < m_labelDims; i++)
			pCal->addItem(pDoc, m_pCalibrations[i]->serialize(pDoc));
	}
	return pNode;
}

void GSupervisedLearner::setFeatureFilter(GTwoWayIncrementalTransform* pFilter)
{
	delete(m_pFeatureFilter);
	m_pFeatureFilter = pFilter;
}

void GSupervisedLearner::setLabelFilter(GTwoWayIncrementalTransform* pFilter)
{
	delete(m_pLabelFilter);
	m_pLabelFilter = pFilter;
}

void GSupervisedLearner::setupFilters(GMatrix& features, GMatrix& labels)
{
	// Discard any existing filters
	setFeatureFilter(NULL);
	setLabelFilter(NULL);

	// Automatically instantiate any necessary filters for the features
	bool hasNominalFeatures = false;
	bool hasContinuousFeatures = false;
	GRelation* pFeatureRel = features.relation().get();
	for(size_t i = 0; i < pFeatureRel->size(); i++)
	{
		if(pFeatureRel->valueCount(i) == 0)
		{
			hasContinuousFeatures = true;
			if(hasNominalFeatures)
				break;
		}
		else
		{
			hasNominalFeatures = true;
			if(hasContinuousFeatures)
				break;
		}
	}
	if(hasNominalFeatures)
	{
		if(!canImplicitlyHandleNominalFeatures())
		{
			if(!canImplicitlyHandleContinuousFeatures())
				ThrowError("This learner says it cannot implicitly handle any type (nominal or continuous) of feature");
			if(m_pFeatureFilter)
				ThrowError("The logic for picking filters has failed");
			setFeatureFilter(new GNominalToCat(16));
		}
	}
	if(!canImplicitlyHandleMissingFeatures())
	{
		if(features.doesHaveAnyMissingValues())
		{
			GImputeMissingVals* pImputer = new GImputeMissingVals(m_rand);
			pImputer->setLabels(&labels);
			if(m_pFeatureFilter)
			{
				GTwoWayIncrementalTransform* pFF = m_pFeatureFilter;
				m_pFeatureFilter = NULL;
				setFeatureFilter(new GTwoWayTransformChainer(pFF, pImputer));
			}
			else
				setFeatureFilter(pImputer);
		}
	}
	if(hasContinuousFeatures)
	{
		if(canImplicitlyHandleContinuousFeatures())
		{
			double supportedMin, supportedMax;
			if(!supportedFeatureRange(&supportedMin, &supportedMax))
			{
				bool normalizationIsNeeded = false;
				for(size_t i = 0; i < pFeatureRel->size(); i++)
				{
					if(pFeatureRel->valueCount(i) != 0)
						continue;
					double m, r;
					features.minAndRange(i, &m, &r);
					if(m < supportedMin || m + r > supportedMax)
					{
						normalizationIsNeeded = true;
						break;
					}
					if(r >= 1e-12 && r * 4 < supportedMax - supportedMin)
					{
						normalizationIsNeeded = true;
						break;
					}
				}
				if(normalizationIsNeeded)
				{
					if(m_pFeatureFilter)
					{
						GTwoWayIncrementalTransform* pFF = m_pFeatureFilter;
						m_pFeatureFilter = NULL;
						setFeatureFilter(new GTwoWayTransformChainer(pFF, new GNormalize(supportedMin, supportedMax))); // (The normalization filter must come last because nominalToCat converts to the range 0-1, which may not be in the range of the model)
					}
					else
						setFeatureFilter(new GNormalize(supportedMin, supportedMax));
				}
			}
		}
		else
		{
			if(!canImplicitlyHandleNominalFeatures())
				ThrowError("This learner says it cannot implicitly handle any type (nominal or continuous) of feature");
			if(m_pFeatureFilter)
				ThrowError("The logic for picking filters has failed");
			setFeatureFilter(new GDiscretize());
		}
	}

	// Automatically instantiate any necessary filters for the labels
	bool hasNominalLabels = false;
	bool hasContinuousLabels = false;
	GRelation* pLabelRel = labels.relation().get();
	for(size_t i = 0; i < pLabelRel->size(); i++)
	{
		if(pLabelRel->valueCount(i) == 0)
		{
			hasContinuousLabels = true;
			if(hasNominalLabels)
				break;
		}
		else
		{
			hasNominalLabels = true;
			if(hasContinuousLabels)
				break;
		}
	}
	if(hasNominalLabels)
	{
		if(!canImplicitlyHandleNominalLabels())
		{
			if(!canImplicitlyHandleContinuousLabels())
				ThrowError("This learner says it cannot implicitly handle any type (nominal or continuous) of label");
			if(m_pLabelFilter)
				ThrowError("The logic for picking filters has failed");
			setLabelFilter(new GNominalToCat(16));
		}
	}
	if(hasContinuousLabels)
	{
		if(canImplicitlyHandleContinuousLabels())
		{
			double supportedMin, supportedMax;
			if(!supportedLabelRange(&supportedMin, &supportedMax))
			{
				bool normalizationIsNeeded = false;
				for(size_t i = 0; i < pLabelRel->size(); i++)
				{
					if(pLabelRel->valueCount(i) != 0)
						continue;
					double m, r;
					labels.minAndRange(i, &m, &r);
					if(m < supportedMin || m + r > supportedMax)
					{
						normalizationIsNeeded = true;
						break;
					}
					if(r >= 1e-12 && r * 4 < supportedMax - supportedMin)
					{
						normalizationIsNeeded = true;
						break;
					}
				}
				if(normalizationIsNeeded)
				{
					if(m_pLabelFilter)
					{
						GTwoWayIncrementalTransform* pLF = m_pLabelFilter;
						m_pLabelFilter = NULL;
						setLabelFilter(new GTwoWayTransformChainer(pLF, new GNormalize(supportedMin, supportedMax))); // (The normalization filter must come last because nominalToCat converts to the range 0-1, which may not be in the range of the model. Also, it is preferable to have the nominalToCat filter come first because it can untransformToDistribution.)
					}
					else
						setLabelFilter(new GNormalize(supportedMin, supportedMax));
				}
			}
		}
		else
		{
			if(!canImplicitlyHandleNominalLabels())
				ThrowError("This learner says it cannot implicitly handle any type (nominal or continuous) of label");
			if(m_pLabelFilter)
				ThrowError("The logic for picking filters has failed");
			setLabelFilter(new GDiscretize());
		}
	}
}

void GSupervisedLearner::train(GMatrix& features, GMatrix& labels)
{
	// Check assumptions
	if(features.rows() != labels.rows())
		ThrowError("Expected features and labels to have the same number of rows");
	if(labels.cols() == 0)
		ThrowError("Expected at least one label dimension");
	m_featureDims = features.cols();
	m_labelDims = labels.cols();

	// Filter the data (if necessary) and train the model
	if(m_autoFilter)
		setupFilters(features, labels);
	if(m_pFeatureFilter)
	{
		m_pFeatureFilter->train(features);
		GMatrix* pFilteredFeatures = m_pFeatureFilter->transformBatch(features);
		Holder<GMatrix> hFilteredFeatures(pFilteredFeatures);
		if(m_pLabelFilter)
		{
			m_pLabelFilter->train(labels);
			GMatrix* pFilteredLabels = m_pLabelFilter->transformBatch(labels);
			Holder<GMatrix> hFilteredLabels(pFilteredLabels);
			trainInner(*pFilteredFeatures, *pFilteredLabels);
		}
		else
			trainInner(*pFilteredFeatures, labels);
	}
	else
	{
		if(m_pLabelFilter)
		{
			m_pLabelFilter->train(labels);
			GMatrix* pFilteredLabels = m_pLabelFilter->transformBatch(labels);
			Holder<GMatrix> hFilteredLabels(pFilteredLabels);
			trainInner(features, *pFilteredLabels);
		}
		else
			trainInner(features, labels);
	}
}

void GSupervisedLearner::predict(const double* pIn, double* pOut)
{
	if(m_pFeatureFilter)
	{
		double* pInnerFeatures = m_pFeatureFilter->innerBuf();
		m_pFeatureFilter->transform(pIn, pInnerFeatures);
		if(m_pLabelFilter)
		{
			double* pInnerLabels = m_pLabelFilter->innerBuf();
			predictInner(pInnerFeatures, pInnerLabels);
			m_pLabelFilter->untransform(pInnerLabels, pOut);
		}
		else
			predictInner(pInnerFeatures, pOut);
	}
	else
	{
		if(m_pLabelFilter)
		{
			double* pInnerLabels = m_pLabelFilter->innerBuf();
			predictInner(pIn, pInnerLabels);
			m_pLabelFilter->untransform(pInnerLabels, pOut);
		}
		else
			predictInner(pIn, pOut);
	}
}

void GSupervisedLearner::calibrate(GMatrix& features, GMatrix& labels)
{
	// Check assumptions
	if(m_labelDims == (size_t)-1)
		ThrowError("The model must be trained before it is calibrated");
	if(features.cols() != m_featureDims || labels.cols() != m_labelDims)
		ThrowError("This data is not compatible with the data used to train this model");
	if(features.rows() != labels.rows())
		ThrowError("Expected features and labels to have the same number of rows");

	// Throw out any existing calibration
	if(m_pCalibrations)
	{
		for(size_t i = 0; i < m_labelDims; i++)
			delete(m_pCalibrations[i]);
		delete[] m_pCalibrations;
	}
	m_pCalibrations = NULL;

	// Calibrate
	vector<GNeuralNet*> calibrations;
	VectorOfPointersHolder<GNeuralNet> hCalibrations(calibrations);
	size_t neighbors = std::max(size_t(4), std::min(size_t(100), (size_t)sqrt(double(features.rows()))));
#ifdef WINDOWS
	GPrediction* out = new GPrediction[m_labelDims];
	ArrayHolder<GPrediction> hOut(out);
#else
	GPrediction out[m_labelDims];
#endif
	for(size_t i = 0; i < m_labelDims; i++)
	{
		// Gather the predicted (before) distribution values
		size_t vals = labels.relation()->valueCount(i);
		GMatrix tmpBefore(features.rows(), std::max(size_t(1), vals));
		if(vals == 0)
		{
			for(size_t j = 0; j < features.rows(); j++)
			{
				predictDistribution(features[j], out);
				tmpBefore[j][0] = out[i].asNormal()->variance();
			}
		}
		else
		{
			for(size_t j = 0; j < features.rows(); j++)
			{
				predictDistribution(features[j], out);
				GVec::copy(tmpBefore[j], out[i].asCategorical()->values(vals), vals);
			}
		}

		// Use a temporary k-NN model to measure the target (after) distribution values
		GKNN knn(m_rand);
		knn.setNeighborCount(neighbors);
		knn.train(tmpBefore, labels);
		GMatrix tmpAfter(features.rows(), std::max(size_t(1), vals));
		if(vals == 0)
		{
			for(size_t j = 0; j < tmpBefore.rows(); j++)
			{
				knn.predictDistribution(tmpBefore[j], out);
				tmpAfter[j][0] = out[0].asNormal()->variance();
			}
		}
		else
		{
			for(size_t j = 0; j < features.rows(); j++)
			{
				knn.predictDistribution(tmpBefore[j], out);
				GVec::copy(tmpAfter[j], out[0].asCategorical()->values(vals), vals);
			}
		}

		// Train a layer of logistic units to map from the before distribution to the after distribution
		GNeuralNet* pNN = new GNeuralNet(m_rand);
		calibrations.push_back(pNN);
		pNN->train(tmpBefore, tmpAfter);
	}

	// Store the resulting calibration functions
	GAssert(calibrations.size() == m_labelDims);
	m_pCalibrations = new GNeuralNet*[m_labelDims];
	for(size_t i = 0; i < m_labelDims; i++)
	{
		m_pCalibrations[i] = calibrations[i];
		calibrations[i] = NULL;
	}
}

void GSupervisedLearner::predictDistribution(const double* pIn, GPrediction* pOut)
{
	if(m_pFeatureFilter)
	{
		double* pInnerFeatures = m_pFeatureFilter->innerBuf();
		m_pFeatureFilter->transform(pIn, pInnerFeatures);
		if(m_pLabelFilter)
		{
			double* pInnerLabels = m_pLabelFilter->innerBuf();
			predictInner(pInnerFeatures, pInnerLabels);
			m_pLabelFilter->untransformToDistribution(pInnerLabels, pOut);
		}
		else
			predictDistributionInner(pInnerFeatures, pOut);
	}
	else
	{
		if(m_pLabelFilter)
		{
			double* pInnerLabels = m_pLabelFilter->innerBuf();
			predictInner(pIn, pInnerLabels);
			m_pLabelFilter->untransformToDistribution(pInnerLabels, pOut);
		}
		else
			predictDistributionInner(pIn, pOut);
	}

	// Adjust the predicted distributions to make them approximate real distributions
	GVecBuf vb;
	if(m_pCalibrations)
	{
		for(size_t i = 0; i < m_labelDims; i++)
		{
			if(pOut[i].isContinuous())
			{
				GNormalDistribution* pNorm = pOut[i].asNormal();
				double varBefore = pNorm->variance();
				double varAfter;
				m_pCalibrations[i]->predict(&varBefore, &varAfter);
				pNorm->setMeanAndVariance(pNorm->mean(), varAfter);
			}
			else
			{
				GCategoricalDistribution* pCat = pOut[i].asCategorical();
				vb.reserve(pCat->valueCount());
				m_pCalibrations[i]->predict(pCat->values(pCat->valueCount()), vb.m_pBuf);
				GVec::copy(pCat->values(pCat->valueCount()), vb.m_pBuf, pCat->valueCount());
			}
		}
	}
}

void GSupervisedLearner::accuracy(GMatrix& features, GMatrix& labels, double* pOutResults, std::vector<GMatrix*>* pNominalLabelStats)
{
	if(features.rows() != labels.rows())
		ThrowError("Expected the features and rows to have the same number of rows");
	size_t labelDims = labels.cols();
	if(pNominalLabelStats)
	{
		pNominalLabelStats->resize(labelDims);
		for(size_t j = 0; j < labelDims; j++)
		{
			size_t vals = labels.relation()->valueCount(j);
			if(vals > 0)
			{
				(*pNominalLabelStats)[j] = new GMatrix(vals, vals);
				(*pNominalLabelStats)[j]->setAll(0.0);
			}
		}
	}
	GTEMPBUF(double, prediction, labelDims);
	GVec::setAll(pOutResults, 0.0, labels.cols());
	for(size_t i = 0; i < features.rows(); i++)
	{
		predict(features[i], prediction);
		double w = 1.0 / (i + 1);
		double* target = labels[i];
		for(size_t j = 0; j < labelDims; j++)
		{
			double d;
			if(labels.relation()->valueCount(j) == 0)
			{
				// Squared error
				d = target[j] - prediction[j];
				d *= d;
			}
			else
			{
				// Predictive accuracy
				if((int)target[j] == (int)prediction[j])
					d = 1.0;
				else
					d = 0.0;
				if(pNominalLabelStats)
				{
					if((int)target[j] >= 0 && (int)prediction[j] >= 0)
						((*pNominalLabelStats)[j])->row((int)target[j])[(int)prediction[j]]++;
				}
			}
			pOutResults[j] *= (1.0 - w);
			pOutResults[j] += w * d;
		}
	}
}

// virtual
GMatrix* GSupervisedLearner::transduceInner(GMatrix& features1, GMatrix& labels1, GMatrix& features2)
{
	// Train
	train(features1, labels1);

	// Predict
	GMatrix* pOut = new GMatrix(labels1.relation());
	pOut->newRows(features2.rows());
	for(size_t i = 0; i < features2.rows(); i++)
		predict(features2.row(i), pOut->row(i));
	return pOut;
}

// virtual
void GSupervisedLearner::trainAndTest(GMatrix& trainFeatures, GMatrix& trainLabels, GMatrix& testFeatures, GMatrix& testLabels, double* pOutResults, std::vector<GMatrix*>* pNominalLabelStats)
{
	train(trainFeatures, trainLabels);
	accuracy(testFeatures, testLabels, pOutResults, pNominalLabelStats);
}

size_t GSupervisedLearner::precisionRecallContinuous(GPrediction* pOutput, double* pFunc, GMatrix& trainFeatures, GMatrix& trainLabels, GMatrix& testFeatures, GMatrix& testLabels, size_t label)
{
	// Predict the variance for each pattern
	train(trainFeatures, trainLabels);
	GMatrix stats(testFeatures.rows(), 2);
	for(size_t i = 0; i < testFeatures.rows(); i++)
	{
		predictDistribution(testFeatures[i], pOutput);
		double* pResultsVec = stats.row(i);
		pResultsVec[0] = testLabels[i][label];
		if(pResultsVec[0] < 0.0 || pResultsVec[0] > 1.0)
			ThrowError("Expected continuous labels to range from 0 to 1");
		GNormalDistribution* pDist = pOutput[label].asNormal();
		pResultsVec[1] = pDist->mean();
	}

	// Make the precision/recall data
	stats.sort(1); // biggest mean last
	stats.reverseRows(); // biggest mean first
	double sumRelevantRetrieved = 0.0;
	for(size_t i = 0; i < stats.rows(); i++)
	{
		double* pVecIn = stats.row(i);
		sumRelevantRetrieved += pVecIn[0];
		pFunc[i] = sumRelevantRetrieved / (i + 1);
	}
	return stats.rows();
}

size_t GSupervisedLearner::precisionRecallNominal(GPrediction* pOutput, double* pFunc, GMatrix& trainFeatures, GMatrix& trainLabels, GMatrix& testFeatures, GMatrix& testLabels, size_t label, int value)
{
	// Predict the likelihood that each pattern is relevant
	train(trainFeatures, trainLabels);
	GMatrix stats(testFeatures.rows(), 2);
	size_t nActualRelevant = 0;
	for(size_t i = 0; i < testFeatures.rows(); i++)
	{
		predictDistribution(testFeatures[i], pOutput);
		double* pStatsVec = stats.row(i);
		pStatsVec[0] = testLabels[i][label];
		if((int)pStatsVec[0] == value)
			nActualRelevant++;
		GCategoricalDistribution* pDist = pOutput[label].asCategorical();
		pStatsVec[1] = pDist->likelihood((double)value); // predicted confidence that it is relevant
	}

	// Make the precision/recall data
	stats.sort(1); // most confident last
	size_t nFoundRelevant = 0;
	size_t nFoundTotal = 0;
	for(size_t i = stats.rows() - 1; i < stats.rows(); i--)
	{
		double* pVecIn = stats.row(i);
		nFoundTotal++;
		if((int)pVecIn[0] == value) // if actually relevant
		{
			nFoundRelevant++;
			if(nFoundTotal <= 1)
				pFunc[nFoundRelevant - 1] = 1.0;
			else
				pFunc[nFoundRelevant - 1] = (double)(nFoundRelevant - 1) / (nFoundTotal - 1);
		}
	}
	GAssert(nFoundRelevant == nActualRelevant);
	return nActualRelevant;
}

void GSupervisedLearner::precisionRecall(double* pOutPrecision, size_t nPrecisionSize, GMatrix& features, GMatrix& labels, size_t label, size_t nReps)
{
	if(features.rows() != labels.rows())
		ThrowError("Expected the features and labels to have the same number of rows");
	size_t nFuncs = std::max((size_t)1, labels.relation()->valueCount(label));
	GVec::setAll(pOutPrecision, 0.0, nFuncs * nPrecisionSize);
	double* pFunc = new double[features.rows()];
	ArrayHolder<double> hFunc(pFunc);
#ifdef WINDOWS
	GPrediction* out = new GPrediction[labels.cols()];
	ArrayHolder<GPrediction> hOut(out);
#else
	GPrediction out[labels.cols()];
#endif
	GMatrix otherFeatures(features.relation(), features.heap());
	GMatrix otherLabels(labels.relation(), labels.heap());
	size_t valueCount = labels.relation()->valueCount(label);
	for(size_t nRep = 0; nRep < nReps; nRep++)
	{
		// Split the data
		GMergeDataHolder hFeatures(features, otherFeatures);
		GMergeDataHolder hLabels(labels, otherLabels);
		features.shuffle(m_rand, &labels);
		size_t otherSize = features.rows() / 2;
		features.splitBySize(&otherFeatures, otherSize);
		labels.splitBySize(&otherLabels, otherSize);

		// Measure precision/recall and merge with the data we've gotten so far
		if(valueCount == 0)
		{
			size_t relevant = precisionRecallContinuous(out, pFunc, features, labels, otherFeatures, otherLabels, label);
			GVec::addInterpolatedFunction(pOutPrecision, nPrecisionSize, pFunc, relevant);
			relevant = precisionRecallContinuous(out, pFunc, otherFeatures, otherLabels, features, labels, label);
			GVec::addInterpolatedFunction(pOutPrecision, nPrecisionSize, pFunc, relevant);
		}
		else
		{
			for(int i = 0; i < (int)valueCount; i++)
			{
				size_t relevant = precisionRecallNominal(out, pFunc, features, labels, otherFeatures, otherLabels, label, i);
				GVec::addInterpolatedFunction(pOutPrecision + nPrecisionSize * i, nPrecisionSize, pFunc, relevant);
				relevant = precisionRecallNominal(out, pFunc, otherFeatures, otherLabels, features, labels, label, i);
				GVec::addInterpolatedFunction(pOutPrecision + nPrecisionSize * i, nPrecisionSize, pFunc, relevant);
			}
		}
	}
	GVec::multiply(pOutPrecision, 1.0 / (2 * nReps), nFuncs * nPrecisionSize);
}

#ifndef NO_TEST_CODE
#define TEST_SIZE 5000
// static
void GSupervisedLearner::test()
{
	// Make a probabilistic training set
	GRand rand(0);
	vector<size_t> vals1;
	vals1.push_back(3);
	vector<size_t> vals2;
	vals2.push_back(2);
	GMatrix f(vals1);
	GMatrix l(vals2);
	f.newRows(TEST_SIZE);
	l.newRows(TEST_SIZE);
	for(size_t i = 0; i < TEST_SIZE; i++)
	{
		size_t n = size_t(rand.next(3));
		if(n == 0)
		{
			if(rand.uniform() < 0.15)
				l[i][0] = 0;
			else
				l[i][0] = 1;
		}
		else if(n == 1)
		{
			if(rand.uniform() < 0.3)
				l[i][0] = 0;
			else
				l[i][0] = 1;
		}
		else
		{
			if(rand.uniform() < 0.85)
				l[i][0] = 0;
			else
				l[i][0] = 1;
		}
		f[i][0] = double(n);
	}

	// Train the model
	GNeuralNet model(rand);
	model.train(f, l);
	GPrediction out;
	double d, prob;

// Uncomment this block if you want to see how it does without calibration (which should be a little worse than with it).
// 	d = 0;
// 	model.predictDistribution(&d, &out);
// 	prob = out.asCategorical()->values(2)[0];
// 	d = 1;
// 	model.predictDistribution(&d, &out);
// 	prob = out.asCategorical()->values(2)[0];
// 	d = 2;
// 	model.predictDistribution(&d, &out);
// 	prob = out.asCategorical()->values(2)[0];

	// Calibrate the model
	model.calibrate(f, l);

	// Check that the predicted distributions are close to the expected distributions
	d = 0;
	model.predictDistribution(&d, &out);
	prob = out.asCategorical()->values(2)[0];
	if(std::abs(prob - 0.15) > .1)
		ThrowError("failed");
	d = 1;
	model.predictDistribution(&d, &out);
	prob = out.asCategorical()->values(2)[0];
	if(std::abs(prob - 0.30) > .1)
		ThrowError("failed");
	d = 2;
	model.predictDistribution(&d, &out);
	prob = out.asCategorical()->values(2)[0];
	if(std::abs(prob - 0.85) > .1)
		ThrowError("failed");
}

void GSupervisedLearner_basicTestEngine(GSupervisedLearner* pLearner, GMatrix& features, GMatrix& labels, GMatrix& testFeatures, GMatrix& testLabels, double minAccuracy, GRand* pRand, double deviation, bool printAccuracy)
{
	// Train the model
	pLearner->train(features, labels);

	// free up some memory, just because we can
	features.flush();
	labels.flush();

	// Test the accuracy
	double resultsBefore;
	pLearner->accuracy(testFeatures, testLabels, &resultsBefore);
	if(printAccuracy){
	  std::cerr << "AccBeforeSerial: " << resultsBefore;
	}
	if(resultsBefore < minAccuracy)
		ThrowError("accuracy has regressed");
	if(resultsBefore >= minAccuracy + 0.035)
		std::cout << "\nThe measured accuracy (" << resultsBefore << ") is much better than expected (" << minAccuracy << "). Please increase the expected accuracy value so that any future regressions will be caught.\n";

	// Roundtrip the model through serialization
	size_t labelDimsBefore = pLearner->labelDims();
	GDom doc;
	doc.setRoot(pLearner->serialize(&doc));
	pLearner->clear(); // free up some memory, just because we can
	GLearnerLoader ll(*pRand);
	GSupervisedLearner* pModel = ll.loadSupervisedLearner(doc.root());
	Holder<GSupervisedLearner> hModel(pModel);
	if(pModel->labelDims() != labelDimsBefore)
		ThrowError("label dims failed to round-trip. Did your deserializing constructor call the base class constructor?");

	// Test the accuracy again
	double resultsAfter;
	pModel->accuracy(testFeatures, testLabels, &resultsAfter);
	if(printAccuracy){
	  std::cerr << "  AccAfterSerial: " << resultsAfter << std::endl;
	}
	if(std::abs(resultsAfter - resultsBefore) > deviation)
		ThrowError("serialization shouldn't influence accuracy this much");
}

void GSupervisedLearner_basicTest1(GSupervisedLearner* pLearner, double minAccuracy, GRand* pRand, double deviation, bool printAccuracy)
{
	GMatrix features(0, 2);
	vector<size_t> vals;
	vals.push_back(3);
	GMatrix labels(vals);
	for(size_t i = 0; i < 2000; i++)
	{
		int c = (int)pRand->next(3);
		double* pF = features.newRow();
		pF[0] = pRand->normal() + (c == 1 ? 2.0 : 0.0);
		pF[1] = pRand->normal() + (c == 2 ? 2.0 : 0.0);
		double* pL = labels.newRow();
		pL[0] = (double)c;
	}
	size_t testSize = features.rows() / 2;
	GMatrix testFeatures(features.relation());
	features.splitBySize(&testFeatures, testSize);
	GMatrix testLabels(labels.relation());
	labels.splitBySize(&testLabels, testSize);
	GSupervisedLearner_basicTestEngine(pLearner, features, labels, testFeatures, testLabels, minAccuracy, pRand, deviation, printAccuracy);
}

void GSupervisedLearner_basicTest2(GSupervisedLearner* pLearner, double minAccuracy, GRand* pRand, double deviation, bool printAccuracy)
{
	if(minAccuracy == -1.0)
		return; // skip this test
	vector<size_t> featureVals;
	featureVals.push_back(3);
	featureVals.push_back(3);
	featureVals.push_back(3);
	GMatrix features(featureVals);
	vector<size_t> labelVals;
	labelVals.push_back(3);
	GMatrix labels(labelVals);
	for(size_t i = 0; i < 1000; i++)
	{
		int c = (int)pRand->next(3);
		double* pF = features.newRow();
		for(size_t j = 0; j < 3; j++)
		{
			if(pRand->next(2) == 0)
				*pF = (double)c;
			else
				*pF = (double)pRand->next(3);
			pF++;
		}
		double* pL = labels.newRow();
		pL[0] = (double)c;
	}
	size_t testSize = features.rows() / 2;
	GMatrix testFeatures(features.relation());
	features.splitBySize(&testFeatures, testSize);
	GMatrix testLabels(labels.relation());
	labels.splitBySize(&testLabels, testSize);
	GSupervisedLearner_basicTestEngine(pLearner, features, labels, testFeatures, testLabels, minAccuracy, pRand, deviation, printAccuracy);
}

void GSupervisedLearner::basicTest(double minAccuracy1, double minAccuracy2, double deviation, bool printAccuracy)
{
	GSupervisedLearner_basicTest1(this, minAccuracy1, &m_rand, deviation, printAccuracy);
	GSupervisedLearner_basicTest2(this, minAccuracy2, &m_rand, deviation, printAccuracy);
}
#endif

// ---------------------------------------------------------------

void GIncrementalLearner::beginIncrementalLearning(sp_relation& pFeatureRel, sp_relation& pLabelRel)
{
	if(m_pFeatureFilter)
		m_featureDims = m_pFeatureFilter->before()->size();
	else
		m_featureDims = pFeatureRel->size();
	if(m_pLabelFilter)
		m_labelDims = m_pLabelFilter->before()->size();
	else
		m_labelDims = pLabelRel->size();
	beginIncrementalLearningInner(pFeatureRel, pLabelRel);
}

void GIncrementalLearner::trainIncremental(const double* pIn, const double* pOut)
{
	const double* pInInner;
	const double* pOutInner;
	if(m_pFeatureFilter)
	{
		pInInner = m_pFeatureFilter->innerBuf();
		m_pFeatureFilter->transform(pIn, m_pFeatureFilter->innerBuf());
	}
	else
		pInInner = pIn;
	if(m_pLabelFilter)
	{
		pOutInner = m_pLabelFilter->innerBuf();
		m_pLabelFilter->transform(pOut, m_pLabelFilter->innerBuf());
	}
	else
		pOutInner = pOut;
	trainIncrementalInner(pInInner, pOutInner);
}

// ---------------------------------------------------------------

// virtual
GIncrementalTransform* GLearnerLoader::loadIncrementalTransform(GDomNode* pNode)
{
	const char* szClass = pNode->field("class")->asString();
	if(szClass[0] == 'G')
	{
		if(szClass[1] < 'P')
		{
			if(strcmp(szClass, "GAttributeSelector") == 0)
				return new GAttributeSelector(pNode, *this);
			else if(strcmp(szClass, "GNoiseGenerator") == 0)
				return new GNoiseGenerator(pNode, *this);
		}
		else
		{
			if(strcmp(szClass, "GPairProduct") == 0)
				return new GPairProduct(pNode, *this);
			else if(strcmp(szClass, "GPCA") == 0)
				return new GPCA(pNode, *this);
		}
	}
	return loadTwoWayIncrementalTransform(pNode);
}

// virtual
GTwoWayIncrementalTransform* GLearnerLoader::loadTwoWayIncrementalTransform(GDomNode* pNode)
{
	const char* szClass = pNode->field("class")->asString();
	if(szClass[0] == 'G')
	{
		if(strcmp(szClass, "GNominalToCat") == 0)
			return new GNominalToCat(pNode, *this);
		else if(strcmp(szClass, "GDiscretize") == 0)
			return new GDiscretize(pNode, *this);
		else if(strcmp(szClass, "GImputeMissingVals") == 0)
			return new GImputeMissingVals(pNode, *this);
		else if(strcmp(szClass, "GNormalize") == 0)
			return new GNormalize(pNode, *this);
		else if(strcmp(szClass, "GTwoWayTransformChainer") == 0)
			return new GTwoWayTransformChainer(pNode, *this);
	}
	if(m_throwIfClassNotFound)
		ThrowError("Unrecognized class: ", szClass);
	return NULL;
}

// virtual
GSupervisedLearner* GLearnerLoader::loadSupervisedLearner(GDomNode* pNode)
{
	const char* szClass = pNode->field("class")->asString();
	if(szClass[0] == 'G')
	{
		if(szClass[1] < 'J')
		{
			if(szClass[1] < 'C')
			{
				if(strcmp(szClass, "GAdaBoost") == 0)
					return new GAdaBoost(pNode, *this);
				else if(strcmp(szClass, "GBag") == 0)
					return new GBag(pNode, *this);
				else if(strcmp(szClass, "GBaselineLearner") == 0)
					return new GBaselineLearner(pNode, *this);
				else if(strcmp(szClass, "GBayesianModelAveraging") == 0)
					return new GBayesianModelAveraging(pNode, *this);
				else if(strcmp(szClass, "GBayesianModelCombination") == 0)
					return new GBayesianModelCombination(pNode, *this);
				else if(strcmp(szClass, "GBucket") == 0)
					return new GBucket(pNode, *this);
			}
			else
			{
				if(strcmp(szClass, "GDecisionTree") == 0)
					return new GDecisionTree(pNode, *this);
				else if(strcmp(szClass, "GIdentityFunction") == 0)
					return new GIdentityFunction(pNode, *this);
			}
		}
		else
		{
			if(szClass[1] < 'N')
			{
				if(strcmp(szClass, "GLinearRegressor") == 0)
					return new GLinearRegressor(pNode, *this);
				else if(strcmp(szClass, "GMeanMarginsTree") == 0)
					return new GMeanMarginsTree(pNode, *this);
			}
			else
			{
				if(strcmp(szClass, "GPolynomial") == 0)
					return new GPolynomial(pNode, *this);
				else if(strcmp(szClass, "GRandomForest") == 0)
					return new GRandomForest(pNode, *this);
			}
		}
	}
	return loadIncrementalLearner(pNode);
}

// virtual
GIncrementalLearner* GLearnerLoader::loadIncrementalLearner(GDomNode* pNode)
{
	const char* szClass = pNode->field("class")->asString();
	if(szClass[0] == 'G')
	{
		if(strcmp(szClass, "GKNN") == 0)
			return new GKNN(pNode, *this);
		else if(strcmp(szClass, "GNaiveBayes") == 0)
			return new GNaiveBayes(pNode, *this);
		else if(strcmp(szClass, "GNaiveInstance") == 0)
			return new GNaiveInstance(pNode, *this);
		else if(strcmp(szClass, "GNeuralNet") == 0)
			return new GNeuralNet(pNode, *this);
	}
	if(m_throwIfClassNotFound)
		ThrowError("Unrecognized class: ", szClass);
	return NULL;
}

// virtual
GCollaborativeFilter* GLearnerLoader::loadCollaborativeFilter(GDomNode* pNode)
{
	const char* szClass = pNode->field("class")->asString();
	if(szClass[0] == 'G')
	{
		if(strcmp(szClass, "GBagOfRecommenders") == 0)
			return new GBagOfRecommenders(pNode, *this);
		else if(strcmp(szClass, "GBaselineRecommender") == 0)
			return new GBaselineRecommender(pNode, *this);
		else if(strcmp(szClass, "GMatrixFactorization") == 0)
			return new GMatrixFactorization(pNode, *this);
		else if(strcmp(szClass, "GNeuralRecommender") == 0)
			return new GNonlinearPCA(pNode, *this);
	}
	if(m_throwIfClassNotFound)
		ThrowError("Unrecognized class: ", szClass);
	return NULL;
}

// ---------------------------------------------------------------

GBaselineLearner::GBaselineLearner(GRand& rand)
: GSupervisedLearner(rand)
{
}

GBaselineLearner::GBaselineLearner(GDomNode* pNode, GLearnerLoader& ll)
: GSupervisedLearner(pNode, ll)
{
	m_prediction.clear();
	GDomNode* pPred = pNode->field("pred");
	GDomListIterator it(pPred);
	m_prediction.reserve(it.remaining());
	for(size_t i = 0; it.current(); i++)
	{
		m_prediction.push_back(it.current()->asDouble());
		it.advance();
	}
}

// virtual
GBaselineLearner::~GBaselineLearner()
{
	clear();
}

// virtual
void GBaselineLearner::clear()
{
	m_prediction.clear();
}

// virtual
GDomNode* GBaselineLearner::serialize(GDom* pDoc)
{
	GDomNode* pNode = baseDomNode(pDoc, "GBaselineLearner");
	if(m_prediction.size() == 0)
		ThrowError("Attempted to serialize a model that has not been trained");
	GDomNode* pPred = pNode->addField(pDoc, "pred", pDoc->newList());
	for(size_t i = 0; i < m_prediction.size(); i++)
		pPred->addItem(pDoc, pDoc->newDouble(m_prediction[i]));
	return pNode;
}

// virtual
void GBaselineLearner::trainInner(GMatrix& features, GMatrix& labels)
{
	clear();
	size_t labelDims = labels.cols();
	m_prediction.reserve(labelDims);
	for(size_t i = 0; i < labelDims; i++)
		m_prediction.push_back(labels.baselineValue(i));
}

// virtual
void GBaselineLearner::predictDistributionInner(const double* pIn, GPrediction* pOut)
{
	ThrowError("Sorry, this learner cannot predict a distribution");
}

// virtual
void GBaselineLearner::predictInner(const double* pIn, double* pOut)
{
	for(vector<double>::iterator it = m_prediction.begin(); it != m_prediction.end(); it++)
		*(pOut++) = *it;
}

void GBaselineLearner::autoTune(GMatrix& features, GMatrix& labels)
{
	// This model has no parameters to tune
}

#ifndef NO_TEST_CODE
// static
void GBaselineLearner::test()
{
	GRand rand(0);
	GBaselineLearner bl(rand);
	bl.basicTest(0.33, 0.33);
}
#endif

// ---------------------------------------------------------------

GIdentityFunction::GIdentityFunction(GRand& rand)
: GSupervisedLearner(rand), m_labelDims(0), m_featureDims(0)
{
}

GIdentityFunction::GIdentityFunction(GDomNode* pNode, GLearnerLoader& ll)
: GSupervisedLearner(pNode, ll)
{
	m_labelDims = (size_t)pNode->field("labels")->asInt();
	m_featureDims = (size_t)pNode->field("features")->asInt();
}

// virtual
GIdentityFunction::~GIdentityFunction()
{
}

// virtual
void GIdentityFunction::clear()
{
	m_labelDims = 0;
	m_featureDims = 0;
}

// virtual
GDomNode* GIdentityFunction::serialize(GDom* pDoc)
{
	GDomNode* pNode = baseDomNode(pDoc, "GIdentityFunction");
	pNode->addField(pDoc, "labels", pDoc->newInt(m_labelDims));
	pNode->addField(pDoc, "features", pDoc->newInt(m_featureDims));
	return pNode;
}

// virtual
void GIdentityFunction::trainInner(GMatrix& features, GMatrix& labels)
{
	m_labelDims = labels.cols();
	m_featureDims = features.cols();
}

// virtual
void GIdentityFunction::predictDistributionInner(const double* pIn, GPrediction* pOut)
{
	ThrowError("Sorry, not implemented yet");
}

// virtual
void GIdentityFunction::predictInner(const double* pIn, double* pOut)
{
	if(m_labelDims <= m_featureDims)
		GVec::copy(pOut, pIn, m_labelDims);
	else
	{
		GVec::copy(pOut, pIn, m_featureDims);
		GVec::setAll(pOut + m_featureDims, 0.0, m_labelDims - m_featureDims);
	}
}

} // namespace GClasses
