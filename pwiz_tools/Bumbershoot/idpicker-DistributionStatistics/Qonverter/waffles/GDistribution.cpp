/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#include "GDistribution.h"
#include "GDom.h"
#include "GVec.h"
#include "GRand.h"
#include "GMath.h"
#include "GMatrix.h"
#include <cmath>

using namespace GClasses;
using std::map;

void GCategoricalDistribution::deserialize(GDomNode* pNode)
{
	GDomListIterator it(pNode);
	m_nValueCount = it.remaining();
	delete[] m_pValues;
	m_pValues = new double[m_nValueCount];
	m_nMode = 0;
	for(size_t i = 0; i < m_nValueCount; i++)
	{
		m_pValues[i] = it.current()->asDouble();
		if(m_pValues[i] > m_pValues[m_nMode])
			m_nMode = i;
		it.advance();
	}
}

GDomNode* GCategoricalDistribution::serialize(GDom* pDoc)
{
	GDomNode* pNode = pDoc->newList();
	for(size_t i = 0; i < m_nValueCount; i++)
		pNode->addItem(pDoc, pDoc->newDouble(m_pValues[i]));
	return pNode;
}

// virtual
bool GCategoricalDistribution::isSupported(double x) const
{
	size_t nIndex = (size_t)floor(x + 0.5);
	return (nIndex >= 0 && nIndex < m_nValueCount);
}

// virtual
double GCategoricalDistribution::logLikelihood(double x)
{
	return log(likelihood(x));
}

void GCategoricalDistribution::normalize()
{
	m_nMode = 0;
	if(m_pValues[0] < 0)
		m_pValues[0] = 0;
	double sum = m_pValues[0];
	for(size_t i = 1; i < m_nValueCount; i++)
	{
		if(m_pValues[i] < 0)
		{
			//GAssert(m_pValues[i] >= -1e-9); // Expected a non-negative value
			m_pValues[i] = 0;
		}
		else if(m_pValues[i] > m_pValues[m_nMode])
			m_nMode = i;
		sum += m_pValues[i];
	}
	if(sum > 0)
	{
		for(size_t i = 0; i < m_nValueCount; i++)
			m_pValues[i] /= sum;
	}
	else
		setToUniform(m_nValueCount);
}

void GCategoricalDistribution::normalizeFromLogSpace()
{
	// Adjust the average value to about 1, and then convert from log space
	double sum = 0;
	size_t count = 0;
	for(size_t i = 0; i < m_nValueCount; i++)
	{
		if(m_pValues[i] > -1e50)
		{
			sum += m_pValues[i];
			count++;
		}
	}
	double delta = 1.0 - sum / count;
	for(size_t i = 0; i < m_nValueCount; i++)
		m_pValues[i] = exp(m_pValues[i] + delta);
	normalize();
}

void GCategoricalDistribution::setValues(size_t nValueCount, const double* pValues)
{
	values(nValueCount); // Make sure the right amount of space is allocated
	GVec::copy(m_pValues, pValues, nValueCount);
	normalize();
}

void GCategoricalDistribution::setSpike(size_t nValueCount, size_t nValue, size_t nDepth)
{
	GAssert(nDepth > 0); // nDepth should be at least 1
	values(nValueCount); // Make sure the right amount of space is allocated
	if(nValue < 0 || nValue >= nValueCount)
	{
		m_nMode = -1;
		for(size_t i = 0; i < nValueCount; i++)
			m_pValues[i] = 1;
	}
	else
	{
		m_nMode = nValue;
		double d = 1.0 - 1.0 / nDepth;
		for(size_t i = 0; i < nValueCount; i++)
			m_pValues[i] = d;
		m_pValues[nValue] = 1;
	}
	normalize();
}

double GCategoricalDistribution::entropy()
{
	double dEntropy = 0;
	for(size_t i = 0; i < m_nValueCount; i++)
		dEntropy -= (m_pValues[i] * log(m_pValues[i]) * M_LOG2E);
	return dEntropy;
}




GCategoricalSampler::GCategoricalSampler(size_t categories, const double* pDistribution)
{
	double sum = 0.0;
	for(size_t i = 0; i < categories; i++)
	{
		if(*pDistribution <= 0)
		{
			if(*pDistribution < 0)
				ThrowError("Negative probabilities are not allowed");
			continue;
		}
		sum += *pDistribution;
		m_map.insert(std::pair<double,size_t>(sum, i));
	}
	if(std::abs(sum - 1.0) > 1e-8)
		ThrowError("The probabilities should sum to 1");
}

size_t GCategoricalSampler::draw(double d)
{
	map<double,size_t>::iterator it = m_map.upper_bound(d);
	if(it == m_map.end())
		return 0;
	return it->second;
}





GCategoricalSamplerBatch::GCategoricalSamplerBatch(size_t categories, const double* pDistribution, GRand& rand)
: m_categories(categories), m_pDistribution(pDistribution), m_rand(rand)
{
	m_pIndexes = new size_t[m_categories];
	GIndexVec::makeIndexVec(m_pIndexes, m_categories);
}

GCategoricalSamplerBatch::~GCategoricalSamplerBatch()
{
	delete[] m_pIndexes;
}

void GCategoricalSamplerBatch::draw(size_t samples, size_t* pOutBatch)
{
	double probRemaining = 1.0;
	GIndexVec::shuffle(m_pIndexes, m_categories, &m_rand);
	size_t* pOut = pOutBatch;
	size_t n = samples;
	for(size_t i = 0; i < m_categories; i++)
	{
		size_t index = m_pIndexes[i];
		double prob = m_pDistribution[index];
		size_t k = m_rand.binomial_approx(n, prob / probRemaining);
		GAssert(k <= n);
		for(size_t j = 0; j < k; j++)
		{
			*pOut = index;
			pOut++;
		}
		n -= k;
		probRemaining -= prob;
	}
	GAssert(n == 0);
	GAssert(std::abs(probRemaining) < 1e-6);
	GIndexVec::shuffle(pOutBatch, samples, &m_rand);
}

#ifndef NO_TEST_CODE
#define SAMPLES 10000
// static
void GCategoricalSamplerBatch::test()
{
	double probs[3];
	probs[0] = 0.2;
	probs[1] = 0.5;
	probs[2] = 0.3;
	GRand rand(0);
	GCategoricalSamplerBatch csb(3, probs, rand);
	size_t* pResults = new size_t[SAMPLES];
	ArrayHolder<size_t> hResults(pResults);
	csb.draw(SAMPLES, pResults);
	size_t counts[3];
	counts[0] = 0;
	counts[1] = 0;
	counts[2] = 0;
	for(size_t i = 0; i < SAMPLES; i++)
	{
		size_t n = pResults[i];
		if(n > 2)
			ThrowError("out of range");
		counts[n]++;
	}
	if(std::abs(0.2 - double(counts[0]) / SAMPLES) >= 0.02)
		ThrowError("failed");
	if(std::abs(0.5 - double(counts[1]) / SAMPLES) >= 0.02)
		ThrowError("failed");
	if(std::abs(0.3 - double(counts[2]) / SAMPLES) >= 0.02)
		ThrowError("failed");
}
#endif // NO_TEST_CODE




void GNormalDistribution::precompute()
{
	m_height = 1.0 / sqrt(2.0 * M_PI * m_variance);
}

// virtual
double GPoissonDistribution::logLikelihood(double x)
{
	return -m_rate + x * log(m_rate) - GMath::logFactorial((int)floor(x + .5));
}

// virtual
double GPoissonDistribution::likelihood(double x)
{
	int k = (int)floor(x);
	double d = exp(-m_rate) * pow(m_rate, k);
	while(k > 1)
		d /= k--;
	return d;
}

// virtual
double GGammaDistribution::logLikelihood(double x)
{
	return (m_shape - 1) * log(x) - (x / m_scale) - m_shape * log(m_scale) - GMath::logGamma(m_shape);
}

// virtual
double GGammaDistribution::likelihood(double x)
{
	return exp(logLikelihood(x));
}

// virtual
double GInverseGammaDistribution::likelihood(double x)
{
	return exp(logLikelihood(x));
}

// virtual
double GBetaDistribution::logLikelihood(double x)
{
	return (m_alpha - 1) * log(x) + (m_beta - 1) * log(1.0 - x) + GMath::logGamma(m_alpha + m_beta) - GMath::logGamma(m_alpha) - GMath::logGamma(m_beta);
}

// virtual
double GBetaDistribution::likelihood(double x)
{
	return exp(logLikelihood(x));
}

// virtual
double GSoftImpulseDistribution::likelihood(double x)
{
	double t = (1.0 / x - 1.0);
	double u = pow(t, m_steepness - 1.0);
	double v = u * t + 1.0;
	return m_steepness * u / (v * v * x * x);
}

// virtual
double GSoftImpulseDistribution::logLikelihood(double x)
{
	return log(likelihood(x));
}

double GSoftImpulseDistribution::cdf(double x) const
{
	return GMath::softStep(x, m_steepness);
}


GMultivariateNormalDistribution::GMultivariateNormalDistribution(const double* pMean, GMatrix* pCovariance)
: GDistribution()
{
	GAssert(pCovariance->rows() == (size_t)pCovariance->cols()); // pCovariance should be a square matrix
	m_nDims = pCovariance->rows();
	m_pMean = new double[3 * m_nDims];
	m_pVector1 = &m_pMean[m_nDims];
	m_pVector2 = &m_pMean[2 * m_nDims];
	memcpy(m_pMean, pMean, sizeof(double) * m_nDims);
	precompute(pCovariance);
}

GMultivariateNormalDistribution::GMultivariateNormalDistribution(GMatrix* pData, size_t nDims)
{
	m_nDims = nDims;
	m_pMean = new double[3 * m_nDims];
	m_pVector1 = &m_pMean[m_nDims];
	m_pVector2 = &m_pMean[2 * m_nDims];
	for(size_t i = 0; i < nDims; i++)
		m_pMean[i] = pData->mean(i);
	GMatrix* pCov = pData->covarianceMatrix();
	Holder<GMatrix> hCov(pCov);
	precompute(pCov);
}

GMultivariateNormalDistribution::~GMultivariateNormalDistribution()
{
	delete(m_pInverseCovariance);
	delete(m_pCholesky);
	delete[] m_pMean;
}

double GMultivariateNormalDistribution::likelihood(const double* pParams)
{
	for(size_t i = 0; i < m_nDims; i++)
		m_pVector1[i] = pParams[i] - m_pMean[i];
	m_pInverseCovariance->multiply(m_pVector1/*in*/, m_pVector2/*out*/, false);
	return m_dScale * exp(-0.5 * GVec::dotProduct(m_pVector1, m_pVector2, m_nDims));
}

double* GMultivariateNormalDistribution::randomVector(GRand* pRand)
{
	for(size_t i = 0; i < m_nDims; i++)
		m_pVector1[i] = pRand->normal();
	m_pCholesky->multiply(m_pVector1, m_pVector2, false);
	GVec::add(m_pVector2, m_pMean, m_nDims);
	return m_pVector2;
}

void GMultivariateNormalDistribution::precompute(GMatrix* pCovariance)
{
	m_dScale = 1.0 / sqrt(pow(2.0 * M_PI, (double)m_nDims) * pCovariance->determinant());

	//m_pInverseCovariance = pCovariance->clone();
	//m_pInverseCovariance->invert();
	m_pInverseCovariance->pseudoInverse();

	m_pCholesky = pCovariance->cholesky();
}

