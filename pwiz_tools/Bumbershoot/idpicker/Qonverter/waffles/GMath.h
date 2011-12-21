/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __GMATH_H__
#define __GMATH_H__

#include <math.h>
#include <cstring>

#ifdef WINDOWS
#ifndef M_PI
# define M_E            2.7182818284590452354   /* e */
# define M_LOG2E        1.4426950408889634074   /* log_2 e = 1/(log_e 2)*/
# define M_LOG10E       0.43429448190325182765  /* log_10 e = 1/(log_e 10)*/
# define M_LN2          0.69314718055994530942  /* log_e 2 */
# define M_LN10         2.30258509299404568402  /* log_e 10 */
# define M_PI           3.14159265358979323846  /* pi */
# define M_PI_2         1.57079632679489661923  /* pi/2 */
# define M_PI_4         0.78539816339744830962  /* pi/4 */
# define M_1_PI         0.31830988618379067154  /* 1/pi */
# define M_2_PI         0.63661977236758134308  /* 2/pi */
# define M_2_SQRTPI     1.12837916709551257390  /* 2/sqrt(pi) */
# define M_SQRT2        1.41421356237309504880  /* sqrt(2) */
# define M_SQRT1_2      0.70710678118654752440  /* 1/sqrt(2) = sqrt(1/2) */
#endif
#endif


namespace GClasses {


typedef double (*MathFunc)(void* pThis, double x);

/// Provides some useful math functions
class GMath
{
public:
	/// Returns sign(x) * sqrt(ABS(x))
	inline static double signedRoot(double x)
	{

		if(x >= 0)
			return sqrt(x);
		else
			return -sqrt(-x);
	}

	/// The logistic sigmoid function.
	inline static double logistic(double x)
	{
		if(x >= 500.0) // Don't trigger a floating point exception
			return 1.0;
		if(x < -500.0) // Don't trigger a floating point exception
			return 0.0;
		return 1.0 / (exp(-x) + 1.0);
	}

	/// This evaluates the derivative of the sigmoid function
	inline static double logisticDerivative(double x)
	{
		double d = logistic(x);
		return d * (1.0 - d);
	}

	/// This is the inverse of the logistic sigmoid function
	inline static double logisticInverse(double y)
	{
		// return (log(y) - log(1.0 - y));
		return -log((1.0 / y) - 1.0);
	}

	/// Calculates a function that always passes through (0, 0),
	/// (1, 1), and (0.5, 0.5). The slope at (0.5, 0.5) will be
	/// "steepness". If steepness is > 1, then the slope at
	/// (0, 0) and (1, 1) will be 0. If steepness is < 1, the
	/// slope at (0, 0) and (1, 1) will be infinity. If steepness
	/// is exactly 1, the slope will be 1 at those points.
	/// softStep(1/x, 2) = PI*cauchy(x-1).
	inline static double softStep(double x, double steepness)
	{
		return 1.0 / (pow(1.0 / x - 1.0, steepness) + 1.0);
	}

	/// A soft step function with a very high degree of continuity at 0 and 1.
	inline static double interpolatingFunc(double x)
	{
		double a = pow(x, 1.0 / x);
		double b = pow(1.0 - x, 1.0 / (1.0 - x));
		return a / (a + b);
	}

	/// The bend function has a slope of 1 at very negative values of x, and 2 at
	/// very positive values of x, and a smooth transition in between.
	inline static double bend(double x)
	{
		if(x >= 500.0) // Don't trigger a floating point exception
			return x + x;
		if(x < -500.0) // Don't trigger a floating point exception
			return x;
		return x + log(exp(x) + 1.0);
	}

	/// The inverse of the bend function
	inline static double bendInverse(double y)
	{
		if(y >= 1000.0)
			return 0.5 * y;
		if(y < -500.0)
			return y;
		return log(0.5 * (sqrt(4.0 * exp(y) + 1.0) - 1.0));
	}

	/// The derivative of the bend function
	inline static double bendDerivative(double x)
	{
		return logistic(x) + 1.0;
	}

	/// The gamma function
	static double gamma(double x);

	/// returns log(Gamma(x))
	static double logGamma(double x);

	/// returns log(x!)
	static double logFactorial(int x);

	/// The gaussian function
	inline static double gaussian(double x)
	{
		return exp(-0.5 * (x * x));
	}

	/// Returns an approximation for the error function of x
	static double approximateErf(double x);

	/// Returns an approximation for the inverse of the error function
	static double approximateInverseErf(double x);

	/// Computes the y where x = y*exp(y). This is also known as the
	/// Omega function, or the Lambert W function. x must be > -1/e.
	static double productLog(double x);

	/// When l is somewhere between 0 and 1, it will do something in between "add"
	/// and "multiply" with a and b. When l is close to 0, this returns a value
	/// close to a + b. When l is close to 1, this returns a value close to a * b.
	/// When l is exactly 0 or 1, results are undefined, so you special-case those
	/// values to just call add or multiply.
	static double limboAdd(double l, double a, double b)
	{
		//l = sqrt(l) - 1;
		//l = 1.0 - (l * l);
		double t = (1.0 - l) * a + l * log(a) + (1.0 - l) * b + l * log(b);
		double v = -(l - 1.0) * exp(t / l) / l;
		return -l * GMath::productLog(v) / (l - 1.0);
	}

	/// This implements Newton's method for determining a
	/// polynomial f(t) that goes through all the control points
	/// pFuncValues at pTValues.  (You could then convert to a
	/// Bezier curve to get a Bezier curve that goes through those
	/// points.)  The polynomial coefficients are put in pFuncValues
	/// in the form c0 + c1*t + c2*t*t + c3*t*t*t + ...
	static void newtonPolynomial(const double* pTValues, double* pFuncValues, int nPoints);

	/// Integrates the specified function from dStart to dEnd
	static double integrate(MathFunc pFunc, double dStart, double dEnd, int nSteps, void* pThis);

	/// Estimates the Incomplete Beta function by "integrating" with the specified number of steps
	static double incompleteBeta(double x, double a, double b, int steps);

	/// Returns the specified row from Pascal's triangle. pOutRow must be big enough to hold nRow + 1 elements
	/// Row 0           1
	/// Row 1          1 1
	/// Row 2         1 2 1
	/// Row 3        1 3 3 1
	/// Row 4       1 4 6 4 1
	/// etc. such that each value is the sum of its two parents
	static void pascalsTriangle(size_t* pOutRow, size_t nRow);

	/// Returns the value of n choose k
	static double nChooseK(unsigned int n, unsigned int k)
	{
		double d = n--;
		unsigned int i;
		for(i = 2; i <= k; i++)
		{
			d *= n;
			n--;
			d /= i;
		}
		return d;
	}

	/// Computes the p-value from the degrees of freedom, and the
	/// t-value obtained from a T-test.
	static double tTestAlphaValue(size_t v, double t);

	/// This computes the Wilcoxon P-value assuming n is large
	/// enough that the Normal approximation will suffice.
	static double wilcoxonPValue(int n, double t);

#ifndef NO_TEST_CODE
	/// Performs unit tests for this class. Throws an exception if there is a failure.
	static void test();
#endif // !NO_TEST_CODE
};


} // namespace GClasses

#endif // __GMATH_H__
