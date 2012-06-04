/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#include <string.h>
#include "GRand.h"
#include <math.h>
#include "GError.h"
#include <stdlib.h>
#include "GHistogram.h"
#include "GTime.h"
#include "GMath.h"
#include "GVec.h"
#include "GReverseBits.h"
#include <cmath>
#include <ctime>
#ifdef WINDOWS
#include <process.h>
#else 
#include <unistd.h>
#endif

namespace GClasses {

using std::vector;

COMPILER_ASSERT(sizeof(uint64) == 8);

GRand::GRand(uint64 seed)
{
	setSeed(seed);
}

GRand::~GRand()
{
}

void GRand::setSeed(uint64 seed)
{
	m_b = 0xCA535ACA9535ACB2ull + seed;
	m_a = 0x6CCF6660A66C35E7ull + (seed << 24);
}

uint64 GRand::next(uint64 range)
{
	// Use rejection to find a random value in a range that is a multiple of "range"
	uint64 n = (0xffffffffffffffffull % range) + 1;
	uint64 x;
	do
	{
		x = next();
	} while((x + n) < n);

	// Use modulus to return the final value
	return x % range;
}

double GRand::uniform()
{
	// use 52 random bits for the mantissa (as specified in IEEE 754. See
	// http://en.wikipedia.org/wiki/Double_precision_floating-point_format)
	return (double)(next() & 0xfffffffffffffull) / 4503599627370496.0;
}

double GRand::normal()
{
	double x, y, mag;
	do
	{
		x = uniform() * 2 - 1;
		y = uniform() * 2 - 1;
		mag = x * x + y * y;
	} while(mag >= 1.0 || mag == 0);
	return y * sqrt(-2.0 * log(mag) / mag); // the Box-Muller transform	
}

size_t GRand::categorical(vector<double>& probabilities)
{
	double d = uniform();
	size_t i = 0;
	for(vector<double>::iterator it = probabilities.begin(); it != probabilities.end(); it++)
	{
		d -= *it;
		if(d < 0)
			return i;
		i++;
	}
	GAssert(false); // the probabilities are not normalized
	return probabilities.size() - 1;
}

double GRand::exponential()
{
	return -log(uniform());
}

double GRand::cauchy()
{
	return normal() / normal();
}

int GRand::poisson(double mu)
{
	if(mu <= 0)
		ThrowError("invalid parameter");
	double p = 1.0;
	int n = 0;
	if(mu < 30)
	{
		mu = exp(-mu);
		do {
			p *= uniform();
			n++;
		} while(p >= mu);
		return n - 1;
	}
	else
	{
		double u1, u2, x, y;
		double c = 0.767-3.36 / mu;
		double b = M_PI / sqrt(3.0 * mu);
		double a = b * mu;
		if(c <= 0)
			ThrowError("Error generating Poisson deviate");
		double k = log(c) - mu - log(b);
		double ck1 = 0.0;
		double ck2;
		do {
			ck2=0.;
			do {
				u1 = uniform();
				x = (a - log(0.1e-18 + (1.0 - u1) / u1)) / b;
				if(x > -0.5)
					ck2=1.0;
			} while (ck2<0.5);
			n = (int)(x + 0.5);
			u2 = uniform();
			y = 1 + exp(a - b * x);
			ck1 = a - b * x + log(.1e-18 + u2/(y * y));
#ifdef WINDOWS
			ck2 = k + n * log(.1e-18 + mu) - GMath::logGamma(n + 1.0);
#else
			ck2 = k + n * log(.1e-18 + mu) - lgamma(n + 1.0);
#endif
			if(ck1 <= ck2)
				ck1 = 1.0;
		} while (ck1 < 0.5);
		return n;
	}
}

double GRand::gamma(double alpha)
{
	double x;
	if(alpha <= 0)
		ThrowError("invalid parameter");
	if(alpha == 1)
		return exponential();
	else if(alpha < 1)
	{
		double aa = (alpha + M_E) / M_E;
		double r1, r2;
		do {
			r1 = uniform();
			r2 = uniform();
			if(r1 > 1.0 / aa)
			{
				x = -log(aa * (1.0 - r1) / alpha);
				if(r2 < pow(x, (alpha - 1.0)))
					return x;
			}
			else
			{
				x = pow((aa * r1), (1.0 / alpha));
				if(r2 < exp(-x))
					return x;
			}
		} while(r2 < 2);
	}
	else
	{
		double c1 = alpha-1;
		double c2 = (alpha - 1.0 / (6.0 * alpha)) / c1;
		double c3 = 2.0 / c1;
		double c4 = c3 + 2.0;
		double c5 = 1.0 / sqrt(alpha);
		double r1, r2;
		do {
			do {
				r1 = uniform();
				r2 = uniform();
				if(alpha > 2.5)
					r1 = r2 + c5 * (1.0 - 1.86 * r1);
			} while(r1 <= 0 || r1 >= 1);
			double w = c2 * r2 / r1;
			if((c3 * r1) + w + (1.0 / w) <= c4)
				return c1 * w;
			if((c3 * log(r1)) - log(w) + w < 1)
				return c1 * w;
		} while(r2 < 2);
	}
	ThrowError("Error making random gamma");
	return 0;
}

double GRand::chiSquare(double t)
{
	return gamma(t / 2.0) * 2.0;
}

size_t GRand::binomial(size_t n, double p)
{
	size_t c = 0;
	for(size_t i = 0; i < n; i++)
	{
		if(uniform() < p)
			c++;
	}
	return c;
}

size_t GRand::binomial_approx(size_t n, double p)
{
	double mean = p * n;
	double dev = sqrt(std::max(0.0, mean * (1.0 - p)));
	return std::min(n, size_t(floor(std::max(0.0, normal() * dev + mean + 0.5))));
}

void GRand::simplex(double* pOutVec, size_t dims)
{
	for(size_t i = 0; i < dims; i++)
		*(pOutVec++) = exponential();
	GVec::sumToOne(pOutVec, dims);
}

double GRand::softImpulse(double s)
{
	double y = uniform();
	return 1.0 / (1.0 + pow(1.0 / y - 1.0, 1.0 / s));
}

double GRand::weibull(double gamma)
{
	if(gamma <= 0)
		ThrowError("invalid parameter");
	return pow(exponential(), (1.0 / gamma));
}

void GRand::dirichlet(double* pOutVec, const double* pParams, int dims)
{
	double* pOut = pOutVec;
	const double* pIn = pParams;
	for(int i = 0; i < dims; i++)
		*(pOut++) = gamma(*(pIn++));
	GVec::sumToOne(pOutVec, dims);
}

double GRand::student(double t)
{
	if(t <= 0)
		ThrowError("invalid parameter");
	return normal() / sqrt(chiSquare(t) / t);
}

int GRand::geometric(double p)
{
	if(p < 0 || p > 1)
		ThrowError("invalid parameter");
	return (int)floor(-exponential() / log(1.0 - p));
}

double GRand::f(double t, double u)
{
	if(t <= 0 || u <= 0)
		ThrowError("invalid parameters");
	return chiSquare(t) * u / (t * chiSquare(u));
}

double GRand::logistic()
{
	double y = uniform();
	return log(y) - log(1.0 - y);
}

double GRand::logNormal(double mean, double dev)
{
	return exp(normal() * dev + mean);
}

double GRand::beta(double alpha, double beta)
{
	if(alpha <= 0 || beta <= 0)
		ThrowError("invalid parameters");
	double r = gamma(alpha);
	return r / (r + gamma(beta));
}

void GRand::spherical(double* pOutVec, size_t dims)
{
	double* pEl = pOutVec;
	for(size_t i = 0; i < dims; i++)
		*(pEl++) = normal();
	GVec::safeNormalize(pOutVec, dims, this);
}

void GRand::spherical_volume(double* pOutVec, size_t dims)
{
	spherical(pOutVec, dims);
	GVec::multiply(pOutVec, pow(uniform(), 1.0 / dims), dims);
}

void GRand::cubical(double* pOutVec, size_t dims)
{
	double* pEl = pOutVec;
	for(size_t i = 0; i < dims; i++)
		*(pEl++) = uniform();
}

GRand& GRand::global(){
  static GRand rng(0);
  static bool initialized = false;
  if(!initialized){
    std::time_t t = std::time(NULL);
#ifdef WINDOWS
    int pid = _getpid();
#else
    pid_t pid = getpid();
#endif
    uint64 seed = (~ reverseBits((unsigned)t))+pid;
    rng.setSeed(seed);
  }
  return rng;
}


#ifndef NO_TEST_CODE
#define TEST_BIT_HIST_ITERS 100000
void GRand_testBitHistogram()
{
	GRand prng(0);
	size_t counts[64];
	for(size_t i = 0; i < 64; i++)
		counts[i] = 0;
	for(size_t i = 0; i < TEST_BIT_HIST_ITERS; i++)
	{
		unsigned long long n = prng.next();
		for(size_t j = 0; j < 64; j++)
		{
			if(n & (1ull << j))
				counts[j]++;
		}
	}
	for(size_t i = 0; i < 64; i++)
	{
		double d = (double)counts[i] / TEST_BIT_HIST_ITERS;
		if(std::abs(d - 0.5) > 0.01)
			ThrowError("Poor bit-wise histogram");
	}
}

#define GRANDUINT_TEST_PRELUDE_SIZE 10000
#define GRANDUINT_TEST_PERIOD_SIZE 100000

void GRand_testSpeed()
{
	// Compare speed with rand(). (Be sure to build optimized, or else the results aren't very meaningful.)
	int i;
	double t1,t2,t3;
	t1 = GTime::seconds();
	for(i = 0; i < 100000000; i++)
		rand();
	t2 = GTime::seconds();
	GRand gr(0);
	for(i = 0; i < 100000000; i++)
		gr.next();
	t3 = GTime::seconds();
	double randtime = t2 - t1;
	double grandtime = t3 - t2;
	if(randtime < grandtime)
		ThrowError("rand is faster than GRand");
}


void GRand_testRange()
{
	// Make sure random doubles are within range
	GRand r(0);
	double min = 0.5;
	double max = 0.5;
	for(int n = 0; n < 100000; n++)
	{
		double d = r.uniform();
		min = std::min(min, d);
		max = std::max(max, d);
	}
	if(min < 0.0 || max > 1.0)
		ThrowError("Out of range");
	if(std::abs(min - 0.0) > 0.001)
		ThrowError("poor min");
	if(std::abs(max - 1.0) > 0.001)
		ThrowError("poor max");
}

void GRand_test_determinism()
{
	static unsigned int expected[] = { 1917992778u, 1993289697u, 632158740u, 2429171905u, 22912061u, 2753716493u, 316743267u, 3124664097u, 382509932u, 191925157u, 2298038407u, 246378453u, 1806533664u, 2162141831u, 2260504017u, 3155449906u };
	GRand rand(12345678);
	for(size_t i = 0; i < 16; i++)
	{
		if((unsigned int)rand.next() != expected[i])
			ThrowError("failed");
	}
}

// static
void GRand::test()
{
	GRand_testBitHistogram();

	// Test cycle length
	int n;
	for(n = 0; n < 100; n++)
	{
		GRand r(n);
		for(uint64 j = 0; j < GRANDUINT_TEST_PRELUDE_SIZE; j++)
			r.next();
		uint64 startA = r.m_a;
		uint64 startB = r.m_b;
		r.next();
		for(uint64 j = 0; j < GRANDUINT_TEST_PERIOD_SIZE; j++)
		{
			if(r.m_a == startA || r.m_b == startB)
				ThrowError("Loop too small");
			r.next();
		}
	}

	GRand_testRange();
	GRand_test_determinism();
	//GRand_testSpeed();
	// todo: add a test for correlations
}
#endif // !NO_TEST_CODE


} // namespace GClasses

