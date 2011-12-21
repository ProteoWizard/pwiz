/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __GDISTRIBUTION_H__
#define __GDISTRIBUTION_H__

#include <stddef.h>
#include <math.h>
#include <map>
#include "GError.h"

namespace GClasses {

class GDomNode;
class GDom;
class GRand;
class GMatrix;


class GDistribution
{
};

/// This is the base class for univariate distributions.
class GUnivariateDistribution : public GDistribution
{
public:
	enum Type
	{
		categorical,
		normal,
		uniform,
		poisson,
		gamma,
		inverseGamma,
		beta,
		softImpulse,
	};

	GUnivariateDistribution() {}
	virtual ~GUnivariateDistribution() {}

	/// Returns the type of distribution
	virtual Type type() const = 0;

	/// Returns true iff the space of supported values for this distribution is finite
	virtual bool isDiscrete() const = 0;

	/// Returns true iff the specified value is supported in this distribution
	virtual bool isSupported(double x) const = 0;

	/// Returns the most likely value
	virtual double mode() const = 0;

	/// Returns the likelihood of the specified value
	virtual double likelihood(double x) = 0;

	/// Returns the log-likelihood of the specified value
	virtual double logLikelihood(double x) = 0;
};


/// This is a distribution that specifies a probability for each
/// value in a set of nominal values.
class GCategoricalDistribution : public GUnivariateDistribution
{
protected:
	size_t m_nValueCount;
	size_t m_nMode;
	double* m_pValues;

public:
	GCategoricalDistribution() : GUnivariateDistribution()
	{
		m_nValueCount = 0;
		m_pValues = NULL;
	}

	virtual ~GCategoricalDistribution()
	{
		delete[] m_pValues;
	}

	/// Load values from a text format
	void deserialize(GDomNode* pNode);

	/// Save values to a text format
	GDomNode* serialize(GDom* pDoc);

	/// Returns categorical
	virtual Type type() const
	{
		return categorical;
	}

	/// Returns true
	virtual bool isDiscrete() const
	{
		return true;
	}

	/// Returns true if x is within the range of support. If x is
	/// not an integer, it will round x to the nearest integer and
	/// then return true if that value is supported.
	virtual bool isSupported(double x) const;

	/// Returns the mode
	virtual double mode() const
	{
		return (double)m_nMode;
	}

	/// See the comment for GUnivariateDistribution::logLikelihood
	virtual double logLikelihood(double x);

	/// See the comment for GUnivariateDistribution::likelihood
	virtual double likelihood(double x)
	{
		size_t index = (size_t)x;
		if(index < 0 || index >= m_nValueCount)
			ThrowError("out of range");
		return m_pValues[index];
	}

	/// Returns the number of supported values
	size_t valueCount()
	{
		return m_nValueCount;
	}

	/// Resizes the vector of probabilities if it
	/// does not have nValueCount elements, and returns
	/// that vector
	double* values(size_t nValueCount)
	{
		if(m_nValueCount != nValueCount)
		{
			delete[] m_pValues;
			m_pValues = new double[nValueCount];
			m_nValueCount = nValueCount;
		}
		return m_pValues;
	}

	/// Computes the entropy of the values, normalized to fall between 0 and 1
	double normalizedEntropy()
	{
		double dEntropy = 0;
		for(size_t i = 0; i < m_nValueCount; i++)
		{
			if(m_pValues[i] > 0)
				dEntropy -= m_pValues[i] * log(m_pValues[i]);
		}
		return dEntropy / log((double)m_nValueCount);
	}

	/// Makes the values sum to 1, and finds the mode
	void normalize();

	/// Safely converts from log space, and then normalizes
	void normalizeFromLogSpace();

	/// Sets the specified values, and normalizes
	void setValues(size_t nValueCount, const double* pValues);

	/// Set all uniform probabilities
	void setToUniform(size_t nValues)
	{
		values(nValues);
		for(size_t i = 0; i < m_nValueCount; i++)
			m_pValues[i] = 1.0 / m_nValueCount;
		m_nMode = 0;
	}

	/// (1-d) is the probability of 0, and d is the probability of 1
	void setBoolean(double d)
	{
		double vals[2];
		vals[0] = 1.0 - d;
		vals[1] = d;
		setValues(2, vals);
	}

	/// This is a hack for when you know the mode but
	/// you don't know the other values.
	void setSpike(size_t nValueCount, size_t nValue, size_t nDepth);

	/// Returns the entropy of the values
	double entropy();
};


/// This class is for efficiently drawing random values from
/// a categorical distribution with a large number of categories.
class GCategoricalSampler
{
protected:
	std::map<double,size_t> m_map;

public:
	/// categories specifies the number of categories.
	/// pDistribution should specify a probability value for each
	/// category. They should sum to 1.
	GCategoricalSampler(size_t categories, const double* pDistribution);
	~GCategoricalSampler() {}

	/// d should be a random uniform value from 0 to 1. The corresponding zero-based
	/// category index is returned. This method will take log(categories) time.
	size_t draw(double d);
};


class GCategoricalSamplerBatch
{
protected:
	size_t m_categories;
	const double* m_pDistribution;
	size_t* m_pIndexes;
	GRand& m_rand;

public:
	/// categories specifies the number of categories.
	/// pDistribution should specify a probability value for each
	/// category. They should sum to 1.
	/// pDistribution is expected to remain valid for the duration of this object.
	GCategoricalSamplerBatch(size_t categories, const double* pDistribution, GRand& rand);
	~GCategoricalSamplerBatch();

	/// This will draw a batch of samples from the categorical distribution.
	/// This method is implemented efficiently, such that it will draw them in O(samples + categories) time.
	/// (This is significantly faster than O(samples * log(categories)) time, which is what you get if
	/// you use GCategoricalSampler.)
	void draw(size_t samples, size_t* pOutBatch);

#ifndef NO_TEST_CODE
	static void test();
#endif // NO_TEST_CODE

};


/// This is the Normal (a.k.a. Gaussian) distribution
class GNormalDistribution : public GUnivariateDistribution
{
protected:
	double m_mean, m_variance;
	double m_height;

public:
	GNormalDistribution() : GUnivariateDistribution()
	{
		m_height = 0;
	}

	virtual ~GNormalDistribution()
	{
	}

	virtual Type type() const
	{
		return normal;
	}

	/// Returns false
	virtual bool isDiscrete() const
	{
		return false;
	}

	/// Returns true for all values
	virtual bool isSupported(double x) const
	{
		return true;
	}

	/// Returns the mode (which is also the mean)
	virtual double mode() const
	{
		return mean();
	}

	/// See the comment for GUnivariateDistribution::logLikelihood
	virtual double logLikelihood(double x)
	{
		return -0.5 * (log(m_variance) + (x - m_mean) * (x - m_mean) / m_variance);
	}

	/// See the comment for GUnivariateDistribution::likelihood
	virtual double likelihood(double x)
	{
		if(m_height == 0)
			precompute();
		x -= m_mean;
		return m_height * exp(-0.5 * (x * x) / m_variance);
	}

	/// Sets the mean and variance of this distribution
	void setMeanAndVariance(double mean, double variance)
	{
		GAssert(mean > -1e308 && variance > -1e-5);
		m_mean = mean;
		m_variance = variance;
		m_height = 0;
	}

	/// Returns the probability density (height) of the mode (mode=mean for a normal distribution)
	double modeLikelihood()
	{
		if(m_height == 0)
			precompute();
		return m_height;
	}

	/// Returns the mean
	double mean() const
	{
		return m_mean;
	}

	/// Returns the variance
	double variance() const
	{
		return m_variance;
	}

	/// Multiplies this by another Normal distribution
	void multiply(GNormalDistribution* pOther)
	{
		double b = m_variance / (m_variance + pOther->m_variance);
		double newMean = m_mean * (1.0 - b) + pOther->m_mean * b;
		m_variance = m_variance * pOther->m_variance / (m_variance + pOther->m_variance);
		m_mean = newMean;
		m_height = 0;
	}

protected:
	void precompute();
};


/// This is a continuous uniform distribution.
class GUniformDistribution : public GUnivariateDistribution
{
protected:
	double m_a, m_b;

public:
	GUniformDistribution() : GUnivariateDistribution()
	{
	}

	virtual ~GUniformDistribution()
	{
	}

	virtual Type type() const
	{
		return uniform;
	}

	/// Returns false
	virtual bool isDiscrete() const
	{
		return false;
	}

	/// Returns true iff a <= x < b
	virtual bool isSupported(double x) const
	{
		if(x >= m_a && x < m_b)
			return true;
		else
			return false;
	}

	/// Returns the middle value (which is also the mean and median)
	virtual double mode() const
	{
		return mean();
	}

	/// See the comment for GUnivariateDistribution::logLikelihood
	virtual double logLikelihood(double x)
	{
		return log(likelihood(x));
	}

	/// See the comment for GUnivariateDistribution::likelihood
	virtual double likelihood(double x)
	{
		return 1.0 / (m_a - m_b);
	}

	/// Returns the middle value (which is also the median and one of the modes)
	double mean() const
	{
		return (m_a + m_b) / 2;
	}

	/// Set the parameters of this distribution
	void setParams(double a, double b)
	{
		m_a = a;
		m_b = b;
	}

};


/// The Poisson distribution
class GPoissonDistribution : public GUnivariateDistribution
{
protected:
	double m_rate;

public:
	GPoissonDistribution() : GUnivariateDistribution()
	{
	}

	virtual ~GPoissonDistribution()
	{
	}

	virtual Type type() const
	{
		return poisson;
	}

	/// Returns true
	virtual bool isDiscrete() const
	{
		return true;
	}

	/// Returns true iff x rounds to a non-negative value
	virtual bool isSupported(double x) const
	{
		int n = (int)floor(x + 0.5);
		return (n >= 0);
	}

	/// Returns the mode (which is also the mean)
	virtual double mode() const
	{
		return mean();
	}

	/// See the comment for GUnivariateDistribution::logLikelihood
	virtual double logLikelihood(double x);

	/// See the comment for GUnivariateDistribution::likelihood
	virtual double likelihood(double x);

	/// Sets the parameters of this distribution
	void setParams(double rate)
	{
		m_rate = rate;
	}

	/// Returns the mean
	double mean() const
	{
		return m_rate;
	}

	/// Returns the variance
	double variance() const
	{
		return m_rate;
	}
};


/// The Gamma distribution
class GGammaDistribution : public GUnivariateDistribution
{
protected:
	double m_shape, m_scale;

public:
	GGammaDistribution() : GUnivariateDistribution()
	{
	}

	virtual ~GGammaDistribution()
	{
	}

	virtual Type type() const
	{
		return gamma;
	}

	/// Returns false
	virtual bool isDiscrete() const
	{
		return false;
	}

	/// Returns true iff x is non-negative
	virtual bool isSupported(double x) const
	{
		return (x >= 0);
	}

	/// Returns the mode
	virtual double mode() const
	{
		if(m_shape >= 1)
			return (m_shape - 1) * m_scale;
		else
			return 0;
	}

	/// See the comment for GUnivariateDistribution::logLikelihood
	virtual double logLikelihood(double x);
	
	/// See the comment for GUnivariateDistribution::likelihood
	virtual double likelihood(double x);

	/// Sets the parameters of this distribution
	void setParams(double shape, double scale)
	{
		m_shape = shape;
		m_scale = scale;
	}

	/// Returns the mean
	double mean() const
	{
		return m_shape * m_scale;
	}
};


/// The inverse Gamma distribution
class GInverseGammaDistribution : public GUnivariateDistribution
{
protected:
	double m_shape, m_scale;

public:
	GInverseGammaDistribution() : GUnivariateDistribution()
	{
	}

	virtual ~GInverseGammaDistribution()
	{
	}

	virtual Type type() const
	{
		return inverseGamma;
	}

	/// Returns false
	virtual bool isDiscrete() const
	{
		return false;
	}

	/// Returns true iff x is positive
	virtual bool isSupported(double x) const
	{
		return (x > 0);
	}

	/// Returns the mode
	virtual double mode() const
	{
		return m_scale / (m_shape + 1);
	}

	/// See the comment for GUnivariateDistribution::logLikelihood
	virtual double logLikelihood(double x)
	{
		return -(m_shape+1.0) * log(x) - (m_scale / x);
	}

	/// See the comment for GUnivariateDistribution::likelihood
	virtual double likelihood(double x);

	/// Sets the parameters of this distribution
	void setParams(double shape, double scale)
	{
		m_shape = shape;
		m_scale = scale;
	}

	/// Returns the mean
	double mean() const
	{
		return m_scale / (m_shape - 1);
	}
};


/// The Beta distribution
class GBetaDistribution : public GUnivariateDistribution
{
protected:
	double m_alpha, m_beta;

public:
	GBetaDistribution() : GUnivariateDistribution()
	{
	}

	virtual ~GBetaDistribution()
	{
	}

	virtual Type type() const
	{
		return beta;
	}

	/// Returns false
	virtual bool isDiscrete() const
	{
		return false;
	}

	/// Returns true iff 0 <= x <= 1
	virtual bool isSupported(double x) const
	{
		return (x >= 0 && x <= 1);
	}

	/// Returns the mode
	virtual double mode() const
	{
		if(m_alpha > 1 && m_beta > 1)
			return (m_alpha - 1) / (m_alpha + m_beta - 2);
		else if(m_alpha >= m_beta)
			return 1;
		else
			return 0;
	}

	/// See the comment for GUnivariateDistribution::logLikelihood
	virtual double logLikelihood(double x);
	
	/// See the comment for GUnivariateDistribution::likelihood
	virtual double likelihood(double x);

	/// Sets the parameters of this distribution
	void setParams(double alpha, double beta)
	{
		m_alpha = alpha;
		m_beta = beta;
	}

	/// Returns the mean
	double mean() const
	{
		return m_alpha / (m_alpha + m_beta);
	}
};



class GSoftImpulseDistribution : public GUnivariateDistribution
{
protected:
	double m_steepness;

public:
	GSoftImpulseDistribution() : GUnivariateDistribution()
	{
	}

	virtual ~GSoftImpulseDistribution()
	{
	}

	virtual Type type() const
	{
		return softImpulse;
	}

	/// Returns true
	virtual bool isDiscrete() const
	{
		return false;
	}

	/// Returns true iff 0 <= x <= 1
	virtual bool isSupported(double x) const
	{
		return (x >= 0 && x <= 1);
	}

	/// Returns the mode
	virtual double mode() const
	{
		return 0.5;
	}

	/// See the comment for GUnivariateDistribution::logLikelihood
	virtual double likelihood(double x);
	
	/// See the comment for GUnivariateDistribution::likelihood
	virtual double logLikelihood(double x);

	/// Sets the parameter of this distribution
	void setParams(double steepness)
	{
		m_steepness = steepness;
	}

	/// Returns the mean
	double mean() const
	{
		return 0.5;
	}

	/// Returns the cumulative distribution of this distribution up to x
	double cdf(double x) const;
};


/// A multivariate Normal distribution. It can compute the likelihood of
/// a specified vector, and can also generate random vectors from the distribution.
class GMultivariateNormalDistribution : public GDistribution
{
protected:
	size_t m_nDims;
	double m_dScale;
	double* m_pMean;
	double* m_pVector1;
	double* m_pVector2;
	GMatrix* m_pInverseCovariance;
	GMatrix* m_pCholesky;

public:
	GMultivariateNormalDistribution(const double* pMean, GMatrix* pCovariance);
	GMultivariateNormalDistribution(GMatrix* pData, size_t nDims);
	~GMultivariateNormalDistribution();

	/// Compute the likelihood of the specified vector (which is assumed
	/// to be the same size as the number of columns or rows in the covariance
	/// matrix).
	double likelihood(const double* pParams);

	/// Generates a random vector from this multivariate Normal distribution.
	double* randomVector(GRand* pRand);

protected:
	void precompute(GMatrix* pCovariance);
};

} // namespace GClasses

#endif // __GDISTRIBUTION_H__
