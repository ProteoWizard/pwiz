/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#include "GNaiveBayes.h"
#include "GError.h"
#include <math.h>
#include <stdlib.h>
#include "GVec.h"
#include "GDom.h"
#include "GDistribution.h"
#include "GRand.h"
#include "GTransform.h"
#include "GSparseMatrix.h"
#include <cmath>

namespace GClasses {

struct GNaiveBayesInputAttr
{
	size_t m_nValues;
	size_t* m_pValueCounts;

	GNaiveBayesInputAttr(size_t nValues)
	{
		m_nValues = nValues;
		m_pValueCounts = new size_t[m_nValues];
		memset(m_pValueCounts, '\0', sizeof(size_t) * m_nValues);
	}

	GNaiveBayesInputAttr(GDomNode* pNode)
	{
		GDomListIterator it(pNode);
		m_nValues = it.remaining();
		m_pValueCounts = new size_t[m_nValues];
		for(size_t i = 0; i < m_nValues; i++)
		{
			m_pValueCounts[i] = (size_t)it.current()->asInt();
			it.advance();
		}
	}

	~GNaiveBayesInputAttr()
	{
		delete[] m_pValueCounts;
	}

	GDomNode* serialize(GDom* pDoc)
	{
		GDomNode* pNode = pDoc->newList();
		for(size_t i = 0; i < m_nValues; i++)
			pNode->addItem(pDoc, pDoc->newInt(m_pValueCounts[i]));
		return pNode;
	}

	void AddTrainingSample(const int inputValue)
	{
		if(inputValue >= 0 && (size_t)inputValue < m_nValues)
			m_pValueCounts[inputValue]++;
		else
			GAssert(inputValue == UNKNOWN_DISCRETE_VALUE);
	}

	size_t eval(const int inputValue)
	{
		if(inputValue >= 0 && (size_t)inputValue < m_nValues)
			return m_pValueCounts[inputValue];
		else
			return 0;
	}
};

// --------------------------------------------------------------------

struct GNaiveBayesOutputValue
{
	size_t m_nCount;
	size_t m_featureDims;
	struct GNaiveBayesInputAttr** m_pInputs;

	GNaiveBayesOutputValue(GRelation* pFeatureRel, size_t nInputCount)
	{
		m_nCount = 0;
		m_featureDims = nInputCount;
		m_pInputs = new struct GNaiveBayesInputAttr*[nInputCount];
		for(size_t n = 0; n < m_featureDims; n++)
			m_pInputs[n] = new struct GNaiveBayesInputAttr(pFeatureRel->valueCount(n));
	}

	GNaiveBayesOutputValue(GDomNode* pNode, size_t nInputCount)
	{
		GDomListIterator it(pNode);
		if(it.remaining() != nInputCount + 1)
			ThrowError("Unexpected number of inputs");
		m_nCount = (size_t)it.current()->asInt();
		it.advance();
		m_featureDims = nInputCount;
		m_pInputs = new struct GNaiveBayesInputAttr*[m_featureDims];
		for(size_t n = 0; n < m_featureDims; n++)
		{
			m_pInputs[n] = new struct GNaiveBayesInputAttr(it.current());
			it.advance();
		}
	}

	~GNaiveBayesOutputValue()
	{
		for(size_t n = 0; n < m_featureDims; n++)
			delete(m_pInputs[n]);
		delete[] m_pInputs;
	}

	GDomNode* serialize(GDom* pDoc)
	{
		GDomNode* pNode = pDoc->newList();
		pNode->addItem(pDoc, pDoc->newInt(m_nCount));
		for(size_t i = 0; i < m_featureDims; i++)
			pNode->addItem(pDoc, m_pInputs[i]->serialize(pDoc));
		return pNode;
	}

	void AddTrainingSample(const double* pIn)
	{
		for(size_t n = 0; n < m_featureDims; n++)
			m_pInputs[n]->AddTrainingSample((int)pIn[n]);
		m_nCount++;
	}

	double eval(const double* pInputVector, double equivalentSampleSize)
	{
		// The prior output probability
		double dLogProb = log((double)m_nCount);

		// The probability of inputs given this output
		for(size_t n = 0; n < m_featureDims; n++)
		{
			dLogProb += log(std::max(1e-300,
					(
						(double)m_pInputs[n]->eval((int)pInputVector[n]) + 
						(equivalentSampleSize / m_pInputs[n]->m_nValues)
					) / 
					(equivalentSampleSize + m_nCount)
				));
		}
		return dLogProb;
	}
};

// --------------------------------------------------------------------

struct GNaiveBayesOutputAttr
{
	size_t m_nValueCount;
	struct GNaiveBayesOutputValue** m_pValues;

	GNaiveBayesOutputAttr(GRelation* pFeatureRel, size_t nInputCount, size_t nValueCount)
	{
		m_nValueCount = nValueCount;
		m_pValues = new struct GNaiveBayesOutputValue*[m_nValueCount];
		for(size_t n = 0; n < m_nValueCount; n++)
			m_pValues[n] = new struct GNaiveBayesOutputValue(pFeatureRel, nInputCount);
	}

	GNaiveBayesOutputAttr(GDomNode* pNode, size_t nInputCount, size_t nValueCount)
	{
		GDomListIterator it(pNode);
		if(it.remaining() != nValueCount)
			ThrowError("Unexpected number of values");
		m_nValueCount = nValueCount;
		m_pValues = new struct GNaiveBayesOutputValue*[m_nValueCount];
		for(size_t n = 0; n < m_nValueCount; n++)
		{
			m_pValues[n] = new struct GNaiveBayesOutputValue(it.current(), nInputCount);
			it.advance();
		}
	}

	~GNaiveBayesOutputAttr()
	{
		for(size_t n = 0; n < m_nValueCount; n++)
			delete(m_pValues[n]);
		delete[] m_pValues;
	}

	GDomNode* serialize(GDom* pDoc)
	{
		GDomNode* pNode = pDoc->newList();
		for(size_t i = 0; i < m_nValueCount; i++)
			pNode->addItem(pDoc, m_pValues[i]->serialize(pDoc));
		return pNode;
	}

	void AddTrainingSample(const double* pIn, int out)
	{
		if(out >= 0 && (size_t)out < m_nValueCount)
			m_pValues[out]->AddTrainingSample(pIn);
	}

	void eval(const double* pIn, GPrediction* pOut, double equivalentSampleSize)
	{
		GCategoricalDistribution* pDist = pOut->makeCategorical();
		double* pValues = pDist->values(m_nValueCount);
		for(size_t n = 0; n < m_nValueCount; n++)
			pValues[n] = m_pValues[n]->eval(pIn, equivalentSampleSize);
		pDist->normalizeFromLogSpace();
	}

	double predict(const double* pIn, double equivalentSampleSize, GRand* pRand)
	{
		GTEMPBUF(double, pValues, m_nValueCount);
		for(size_t n = 0; n < m_nValueCount; n++)
			pValues[n] = m_pValues[n]->eval(pIn, equivalentSampleSize);
		return (double)GVec::indexOfMax(pValues, m_nValueCount, pRand);
	}
};

// --------------------------------------------------------------------

GNaiveBayes::GNaiveBayes(GRand& rand)
: GIncrementalLearner(rand)
{
	m_pOutputs = NULL;
	m_equivalentSampleSize = 0.5;
	m_nSampleCount = 0;
}

GNaiveBayes::GNaiveBayes(GDomNode* pNode, GLearnerLoader& ll)
: GIncrementalLearner(pNode, ll)
{
	m_pFeatureRel = GRelation::deserialize(pNode->field("featurerel"));
	m_pLabelRel = GRelation::deserialize(pNode->field("labelrel"));
	m_nSampleCount = (size_t)pNode->field("sampleCount")->asInt();
	m_equivalentSampleSize = pNode->field("ess")->asDouble();
	GDomNode* pOutputs = pNode->field("outputs");
	GDomListIterator it(pOutputs);
	if(it.remaining() != m_pLabelRel->size())
		ThrowError("Wrong number of outputs");
	m_pOutputs = new struct GNaiveBayesOutputAttr*[m_pLabelRel->size()];
	for(size_t i = 0; i < m_pLabelRel->size(); i++)
	{
		m_pOutputs[i] = new struct GNaiveBayesOutputAttr(it.current(), m_pFeatureRel->size(), m_pLabelRel->valueCount(i));
		it.advance();
	}
}

GNaiveBayes::~GNaiveBayes()
{
	clear();
}

// virtual
GDomNode* GNaiveBayes::serialize(GDom* pDoc)
{
	GDomNode* pNode = baseDomNode(pDoc, "GNaiveBayes");
	pNode->addField(pDoc, "featurerel", m_pFeatureRel->serialize(pDoc));
	pNode->addField(pDoc, "labelrel", m_pLabelRel->serialize(pDoc));
	pNode->addField(pDoc, "sampleCount", pDoc->newInt(m_nSampleCount));
	pNode->addField(pDoc, "ess", pDoc->newDouble(m_equivalentSampleSize));
	GDomNode* pOutputs = pNode->addField(pDoc, "outputs", pDoc->newList());
	for(size_t i = 0; i < m_pLabelRel->size(); i++)
		pOutputs->addItem(pDoc, m_pOutputs[i]->serialize(pDoc));
	return pNode;
}

// virtual
void GNaiveBayes::clear()
{
	m_nSampleCount = 0;
	if(m_pOutputs)
	{
		for(size_t n = 0; n < m_pLabelRel->size(); n++)
			delete(m_pOutputs[n]);
		delete[] m_pOutputs;
	}
	m_pOutputs = NULL;
}

// virtual
void GNaiveBayes::beginIncrementalLearningInner(sp_relation& pFeatureRel, sp_relation& pLabelRel)
{
	clear();
	m_pFeatureRel = pFeatureRel;
	m_pLabelRel = pLabelRel;
	m_pOutputs = new struct GNaiveBayesOutputAttr*[m_pLabelRel->size()];
	for(size_t n = 0; n < m_pLabelRel->size(); n++)
		m_pOutputs[n] = new struct GNaiveBayesOutputAttr(m_pFeatureRel.get(), m_pFeatureRel->size(), m_pLabelRel->valueCount(n));
}

// virtual
void GNaiveBayes::trainIncrementalInner(const double* pIn, const double* pOut)
{
	for(size_t n = 0; n < m_pLabelRel->size(); n++)
		m_pOutputs[n]->AddTrainingSample(pIn, (int)pOut[n]);
	m_nSampleCount++;
}

// virtual
void GNaiveBayes::trainInner(GMatrix& features, GMatrix& labels)
{
	beginIncrementalLearningInner(features.relation(), labels.relation());
	for(size_t n = 0; n < features.rows(); n++)
		trainIncrementalInner(features[n], labels[n]);
}

// virtual
void GNaiveBayes::trainSparse(GSparseMatrix& features, GMatrix& labels)
{
	if(features.rows() != labels.rows())
		ThrowError("Expected the features and labels to have the same number of rows");
	size_t featureDims = features.cols();
	sp_relation pFeatureRel = new GUniformRelation(featureDims, 2);
	beginIncrementalLearning(pFeatureRel, labels.relation());
	double* pFullRow = new double[featureDims];
	ArrayHolder<double> hFullRow(pFullRow);
	for(size_t n = 0; n < features.rows(); n++)
	{
		features.fullRow(pFullRow, n);
		double* pEl = pFullRow;
		for(size_t i = 0; i < featureDims; i++)
		{
			if(*pEl < 1e-6)
				*pEl = 0.0;
			else
				*pEl = 1.0;
			pEl++;
		}
		trainIncremental(pFullRow, labels[n]);
	}
}

void GNaiveBayes::predictDistributionInner(const double* pIn, GPrediction* pOut)
{
	if(m_nSampleCount <= 0)
		ThrowError("You must call train before you call eval");
	for(size_t n = 0; n < m_pLabelRel->size(); n++)
		m_pOutputs[n]->eval(pIn, &pOut[n], m_equivalentSampleSize);
}

void GNaiveBayes::predictInner(const double* pIn, double* pOut)
{
	if(m_nSampleCount <= 0)
		ThrowError("You must call train before you call eval");
	for(size_t n = 0; n < m_pLabelRel->size(); n++)
		pOut[n] = m_pOutputs[n]->predict(pIn, m_equivalentSampleSize, &m_rand);
}

void GNaiveBayes::autoTune(GMatrix& features, GMatrix& labels)
{
	// Find the best ess value
	double bestEss = 0.0;
	double bestErr = 1e308;
	for(double i = 0.0; i < 8; i += 0.25)
	{
		m_equivalentSampleSize = i;
		double d = heuristicValidate(features, labels);
		if(d < bestErr)
		{
			bestErr = d;
			bestEss = i;
		}
		else if(i >= 2.0)
			break;
	}

	// Set the best values
	m_equivalentSampleSize = bestEss;
}

#ifndef NO_TEST_CODE
void GNaiveBayes_CheckResults(double yprior, double ycond, double nprior, double ncond, GPrediction* out)
{
	double py = yprior * ycond;
	double pn = nprior * ncond;
	double sum = py + pn;
	py /= sum;
	pn /= sum;
	GCategoricalDistribution* pCat = out->asCategorical();
	double* pVals = pCat->values(2);
	if(std::abs(pVals[0] - py) > 1e-8)
		ThrowError("wrong");
	if(std::abs(pVals[1] - pn) > 1e-8)
		ThrowError("wrong");
}

void GNaiveBayes_testMath(GRand* pRand)
{
	const char* trainFile =
	"@RELATION test\n"
	"@ATTRIBUTE a {t,f}\n"
	"@ATTRIBUTE b {r,g,b}\n"
	"@ATTRIBUTE c {y,n}\n"
	"@DATA\n"
	"t,r,y\n"
	"f,r,n\n"
	"t,g,y\n"
	"f,g,y\n"
	"f,g,n\n"
	"t,r,n\n"
	"t,r,y\n"
	"t,b,y\n"
	"f,r,y\n"
	"f,g,n\n"
	"f,b,y\n"
	"t,r,n\n";
	GMatrix* pTrain = GMatrix::parseArff(trainFile, strlen(trainFile));
	Holder<GMatrix> hTrain(pTrain);
	GMatrix* pFeatures = pTrain->cloneSub(0, 0, pTrain->rows(), 2);
	Holder<GMatrix> hFeatures(pFeatures);
	GMatrix* pLabels = pTrain->cloneSub(0, 2, pTrain->rows(), 1);
	Holder<GMatrix> hLabels(pLabels);
	GNaiveBayes nb(*pRand);
	nb.setEquivalentSampleSize(0.0);
	nb.train(*pFeatures, *pLabels);
	GPrediction out;
	double pat[2];
	pat[0] = 0; pat[1] = 0;
	nb.predictDistribution(pat, &out);
	GNaiveBayes_CheckResults(7.0/12.0, 4.0/7.0*3.0/7.0, 5.0/12.0, 2.0/5.0*3.0/5.0, &out);
	pat[0] = 0; pat[1] = 1;
	nb.predictDistribution(pat, &out);
	GNaiveBayes_CheckResults(7.0/12.0, 4.0/7.0*2.0/7.0, 5.0/12.0, 2.0/5.0*2.0/5.0, &out);
	pat[0] = 0; pat[1] = 2;
	nb.predictDistribution(pat, &out);
	GNaiveBayes_CheckResults(7.0/12.0, 4.0/7.0*2.0/7.0, 5.0/12.0, 2.0/5.0*0.0/5.0, &out);
	pat[0] = 1; pat[1] = 0;
	nb.predictDistribution(pat, &out);
	GNaiveBayes_CheckResults(7.0/12.0, 3.0/7.0*3.0/7.0, 5.0/12.0, 3.0/5.0*3.0/5.0, &out);
	pat[0] = 1; pat[1] = 1;
	nb.predictDistribution(pat, &out);
	GNaiveBayes_CheckResults(7.0/12.0, 3.0/7.0*2.0/7.0, 5.0/12.0, 3.0/5.0*2.0/5.0, &out);
	pat[0] = 1; pat[1] = 2;
	nb.predictDistribution(pat, &out);
	GNaiveBayes_CheckResults(7.0/12.0, 3.0/7.0*2.0/7.0, 5.0/12.0, 3.0/5.0*0.0/5.0, &out);
}

// static
void GNaiveBayes::test()
{
	GRand prng(0);
	GNaiveBayes_testMath(&prng);
	GNaiveBayes nb(prng);
	nb.basicTest(0.77, 0.77);
}
#endif // !NO_TEST_CODE

} // namespace GClasses
