/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __GRAND_H__
#define __GRAND_H__

#include <sys/types.h>
#include <vector>

namespace GClasses {

typedef unsigned long long int uint64;


/// This is a 64-bit pseudo-random number generator.
class GRand
{
protected:
	uint64 m_a;
	uint64 m_b;

public:
	GRand(uint64 seed);
	~GRand();

	/// Sets the seed
	void setSeed(uint64 seed);

	/// Returns an unsigned pseudo-random 64-bit value
	uint64 next()
	{
		m_a = 0x141F2B69ull * (m_a & 0x3ffffffffull) + (m_a >> 32);
		m_b = 0xC2785A6Bull * (m_b & 0x3ffffffffull) + (m_b >> 32);
		return m_a ^ m_b;
	}

	/// Returns a pseudo-random uint from a discrete uniform
	/// distribution in the range 0 to range-1 (inclusive).
	/// (This method guarantees the result will be drawn from
	/// a uniform distribution, whereas doing "next() % range"
	/// does not guarantee a truly uniform distribution.)
	uint64 next(uint64 range);

	/// Returns a random value from a beta distribution
	double beta(double alpha, double beta);

	/// Returns a random value from a binomial distribution
	/// This method draws n samples from a uniform distribution,
	/// so it is very slow for large values of n. binomial_approx
	/// is generally much faster.
	size_t binomial(size_t n, double p);

	/// Returns a random value approximately from a binomial distribution.
	/// This method uses a normal distribution to approximate the binomial
	/// distribution. It is O(1), and is generally quite accurate when
	/// n is large and p is not too close to 0 or 1.
	size_t binomial_approx(size_t n, double p);

	/// Returns a random value from a categorical distribution
	/// with the specified vector of category probabilities.
	size_t categorical(std::vector<double>& probabilities);

	/// Returns a random value from a standard Cauchy distribution
	double cauchy();

	/// Returns a random value from a chi-squared distribution
	double chiSquare(double t);

	/// Returns a random vector from a dirichlet distribution with the
	/// specified parameters and dimensionality. (Both pOutVec and
	/// pParams should be an array of dims values.)
	void dirichlet(double* pOutVec, const double* pParams, int dims);

	/// Returns a random value from a standard exponential distribution.
	/// (To convert it to a random value from an arbitrary exponential
	/// distribution, just divide the value this returns by the
	/// rate (usually lambda), or if you use the scale parameterization,
	/// just multiply the value this returns by the scale (usually beta)).
	double exponential();

	/// Returns a random value from an f-distribution
	double f(double t, double u);

	/// Returns a random value from a gamma distribution with beta=theta=1.
	/// To convert to a value from an arbitrary gamma distribution,
	/// just divide the value this returns by beta (or use alpha=k, and
	/// multiply the value this returns by theta).
	double gamma(double alpha);

	/// Returns a random value from a geometric distribution with support
	/// for {0, 1, 2, ...}.
	int geometric(double p);

	/// Returns a random value from a standard logistic distribution. To
	/// convert to a random value from an arbitrary logistic distribution,
	/// just multiply the value this returns by the scale (or divide by
	/// the steepness), and then add the mean.
	double logistic();

	/// Returns a random value from a log-Normal distribution
	double logNormal(double mean, double dev);

	/// Returns a random value from a standard normal distribution. (To
	/// convert it to a random value from an arbitrary normal distribution,
	/// just multiply the value this returns by the deviation (usually
	/// lowercase-sigma), then add the mean (usually mu).)
	double normal();

	/// Returns a random value from a Poisson distribution
	int poisson(double mu);

	/// Draws uniformly from a unit simplex. (This is a special case of
	/// drawing from a dirichlet distribution with uniform parameters.)
	void simplex(double* pOutVec, size_t dims);

	/// Returns a random value from a soft-impulse distribution with support
	/// from 0 to 1. (The cdf of the soft-impulse distribution is the soft-step
	/// function: (1/(pow(1/x-1,s)+1)). The mean is always at 0.5, where
	/// the probability-density is s.
	double softImpulse(double s);

	/// Returns a random point on the surface of a dims-dimensional unit sphere
	void spherical(double* pOutVec, size_t dims);

	/// Returns a random point within the volume of a dims-dimensional unit sphere
	void spherical_volume(double* pOutVec, size_t dims);

	/// Returns a random point uniformly distributed within a unit cube
	void cubical(double* pOutVec, size_t dims);

	/// Returns a random value from Student's t-distribution
	double student(double t);

	/// Returns a pseudo-random double from 0 (inclusive)
	/// to 1 (exclusive). This uses 52 random bits for the
	/// mantissa, and discards the extra 12 random bits.
	double uniform();

	/// Returns a random value from a Weibull distribution with lambda=1.
	double weibull(double gamma);

	/// Returns the global random number generator.  Useful to
        /// avoid passing GRand* all over the place.  Initializes the
        /// seed based on the time and some other system parameters
        /// the first time it is called.
	static GRand& global();

#ifndef NO_TEST_CODE
	/// Performs unit tests for this class. Throws an exception if there is a failure.
	static void test();
#endif // !NO_TEST_CODE
};

} // namespace GClasses

#endif // __GRAND_H__
