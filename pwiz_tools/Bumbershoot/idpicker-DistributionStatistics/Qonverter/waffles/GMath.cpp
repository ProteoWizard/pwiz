/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#include "GMath.h"
#include "string.h"
#include <stdlib.h>
#include "GError.h"
#include "GVec.h"
#include <cmath>

using namespace GClasses;

/*static*/ double GMath::gamma(double x)
{
#ifdef WINDOWS
	int i, k, m;
	double ga, gr, z;
	double r = 0;

	static double g[] =
	{
        1.0,
        0.5772156649015329,
       -0.6558780715202538,
       -0.420026350340952e-1,
        0.1665386113822915,
       -0.421977345555443e-1,
       -0.9621971527877e-2,
        0.7218943246663e-2,
       -0.11651675918591e-2,
       -0.2152416741149e-3,
        0.1280502823882e-3,
       -0.201348547807e-4,
       -0.12504934821e-5,
        0.1133027232e-5,
       -0.2056338417e-6,
        0.6116095e-8,
        0.50020075e-8,
       -0.11812746e-8,
        0.1043427e-9,
        0.77823e-11,
       -0.36968e-11,
        0.51e-12,
       -0.206e-13,
       -0.54e-14,
        0.14e-14
	};

	if(x > 171.0)
		return 1e308; // This value is an overflow flag.
	if(x == (int)x)
	{
		if(x > 0.0)
		{
			ga = 1.0; // use factorial
			for (i = 2; i < x; i++)
				ga *= i;
		}
		else
			ga = 1e308;
	}
	else
	{
		if(fabs(x) > 1.0)
		{
			z = fabs(x);
			m = (int)z;
			r = 1.0;
			for (k = 1; k <= m; k++)
				r *= (z - k);
			z -= m;
		}
		else
			z = x;
		gr = g[24];
		for (k = 23; k >= 0; k--)
			gr = gr * z + g[k];
		ga = 1.0 / (gr*z);
		if(fabs(x) > 1.0)
		{
			ga *= r;
			if (x < 0.0)
				ga = -M_PI / (x * ga * sin(M_PI * x));
		}
	}
	return ga;
#else
	return tgamma(x);
#endif
}

// static
double GMath::logGamma(double x)
{
#ifdef WINDOWS
	double x0, x2, xp, gl, gl0;
	int n = 0;
	int k;
	static double a[] =
	{
		8.333333333333333e-02, // 1/12
		-2.777777777777778e-03, // -1/360
		7.936507936507937e-04, // 1/1260
		-5.952380952380952e-04, // -1/1680
		8.417508417508418e-04, // 1/1188
		-1.917526917526918e-03, // -691/360360
		6.410256410256410e-03, // 1/156
		-2.955065359477124e-02,
		1.796443723688307e-01,
		-1.39243221690590
	};

	x0 = x;
	if (x <= 0.0)
		return 1e308;
	else if ((x == 1.0) || (x == 2.0))
		return 0.0;
	else if (x <= 7.0)
	{
		n = (int)(7 - x);
		x0 = x + n;
	}
	x2 = 1.0 / (x0 * x0);
	xp = 2.0 * M_PI;
	gl0 = a[9];
	for (k = 8; k >= 0; k--)
		gl0 = gl0 * x2 + a[k];
	gl = gl0 / x0 + 0.5 * log(xp) + (x0 - 0.5) * log(x0) - x0;
	if (x <= 7.0)
	{
		for (k = 1; k <= n; k++)
		{
			gl -= log(x0 - 1.0);
			x0 -= 1.0;
		}
	}
	return gl;
#else
	return lgamma(x);
#endif
}

double GMath::logFactorial(int x)
{
	static const double logfact[29] =
	{
		0,
		0.6931471805599452862267639829951804,
		1.791759469228054957312679107417352,
		3.178053830347945751810811998439021,
		4.78749174278204581156614949577488,
		6.579251212010101212968038453254849,
		8.525161361065414666882134042680264,
		10.60460290274525085862933337921277,
		12.80182748008146909057813900290057,
		15.10441257307551587985017249593511,
		17.50230784587388654927053721621633,
		19.98721449566188468338623351883143,
		22.55216385312342453062228742055595,
		25.19122118273867982907177065499127,
		27.89927138384089033706914051435888,
		30.67186010608067192606540629640222,
		33.5050734501368907558571663685143,
		36.39544520803305260869819903746247,
		39.33988418719949464730234467424452,
		42.33561646075348505746660521253943,
		45.38013889847690762735510361380875,
		48.47118135183522724673821358010173,
		51.60667556776437692178660654462874,
		54.78472939811231867679452989250422,
		58.0036052229805179081267851870507,
		61.26170176100200137625506613403559,
		64.55753862700633760596247157081962,
		67.88974313718154007801786065101624,
		71.25703896716801466482138494029641,
	};

	if(x < 1)
		ThrowError("out of range");
	if(x < 30)
		return logfact[x - 1];
	return logGamma(x + 1);
}




// This implements Newton's method for determining a
// polynomial f(t) that goes through all the control points
// pFuncValues at pTValues.  (You could then convert to a
// Bezier curve to get a Bezier curve that goes through those
// points.)  The polynomial coefficients are put in pFuncValues
// in the form c0 + c1*t + c2*t*t + c3*t*t*t + ...
/*static*/ void GMath::newtonPolynomial(const double* pTValues, double* pFuncValues, int nPoints)
{
	// Calculate the coefficients to Newton's blending functions
	double* pNC = (double*)alloca(nPoints * sizeof(double));
	memcpy(pNC, pFuncValues, nPoints * sizeof(double));
	int n, i;
	for(n = 1; n < nPoints; n++)
	{
		for(i = nPoints - n - 1; i >= 0; i--)
		{
			pNC[n + i] -= pNC[n + i - 1];
			pNC[n + i] /= (pTValues[n + i] - pTValues[i]);
		}
	}

	// Accumulate into polynomial coefficients
	double* pBlending = (double*)alloca(nPoints * sizeof(double));
	for(n = 1; n < nPoints; n++)
	{
		pBlending[n] = 0;
		pFuncValues[n] = 0;
	}
	pBlending[0] = 1;
	pFuncValues[0] = pNC[0];
	for(n = 1; n < nPoints; n++)
	{
		for(i = n; i > 0; i--)
			pBlending[i] -= pTValues[n - 1] * pBlending[i - 1];
		for(i = 0; i <= n; i++)
			pFuncValues[n - i] += pNC[n] * pBlending[i];
	}
}

// static
double GMath::integrate(MathFunc pFunc, double dStart, double dEnd, int nSteps, void* pThis)
{
	GAssert(nSteps >= 1); // must have at least one step
	double dWidth = (dEnd - dStart);
	double sum = 0;
	double d = pFunc(pThis, dStart);
	if(d >= -1e100 && d < 1e100)
		sum += d / 2;
	else
	{
		d = pFunc(pThis, (double)dWidth / nSteps + dStart);
		if(d >= -1e100 && d < 1e100)
			sum += d / 2;
		else
			ThrowError("This function can't be integrated due to extreme values");
	}
	int i;
	for(i = 1; i < nSteps; i++)
	{
		d = pFunc(pThis, (double)i * dWidth / nSteps + dStart);
		if(d >= -1e100 && d < 1e100)
			sum += d;
		else
		{
			d = pFunc(pThis, (double)(i - 1) * dWidth / nSteps + dStart);
			if(d >= -1e100 && d < 1e100)
				sum += d / 2;
			else
				ThrowError("This function can't be integrated due to extreme values");
			d = pFunc(pThis, (double)(i + 1) * dWidth / nSteps + dStart);
			if(d >= -1e100 && d < 1e100)
				sum += d / 2;
			else
				ThrowError("This function can't be integrated due to extreme values");
		}
	}
	d = pFunc(pThis, dEnd);
	if(d >= -1e100 && d < 1e100)
		sum += d / 2;
	else
	{
		d = pFunc(pThis, (double)(nSteps - 1) * dWidth / nSteps + dStart);
		if(d >= -1e100 && d < 1e100)
			sum += d / 2;
		else
			ThrowError("This function can't be integrated due to extreme values");
	}
	return sum * dWidth / nSteps;
}

double GMath_IncompleteBetaFunc(void* pThis, double x)
{
	double* params = (double*)pThis;
	if(x <= 0 || x >= 1)
		return 0;
	return pow(x, params[0] - 1) * pow(1.0 - x, params[1] - 1);
}

// static
double GMath::incompleteBeta(double x, double a, double b, int steps)
{
	double params[2];
	params[0] = a;
	params[1] = b;
	return integrate(GMath_IncompleteBetaFunc, 0, x, steps, params);
}

// static
double GMath::tTestAlphaValue(size_t v, double t)
{
	double dv = (double)v;

	double alpha = GMath::incompleteBeta(dv / (dv + t * t), dv / 2, 0.5, 200000);
	alpha /= GMath::incompleteBeta(1, dv / 2, 0.5, 200000);
/*
	// An alternate way to compute the same thing
	double alpha = GMath::Integrate(GMatrix_PairedTTestHelper, -t, t, 200000, &dv);
	alpha /= (sqrt(dv) * GMath::IncompleteBeta(1, 0.5, dv / 2, 200000));
	alpha = 1.0 - alpha;
*/
	return alpha;
}

// static
double GMath::wilcoxonPValue(int n, double t)
{
	GAssert(n >= 12); // n is too small for this approximation to be any good
	double z = (t - n * (n + 1) / 4) / sqrt((double)(n * (n + 1) * (n + n + 1)) / 24);
	double alpha;
#ifdef WINDOWS
	alpha = 0;
	ThrowError("Sorry, GMath::ComputeWilcoxonPValue is not implemented for Windows yet");
#else
	alpha = 1.0 - erf(-z * M_SQRT1_2);
#endif
	return alpha;
}

// static
void GMath::pascalsTriangle(size_t* pOutRow, size_t nRow)
{
	GAssert(nRow >= 0);
	for(size_t i = 0; i <= nRow; i++)
	{
		pOutRow[i] = 1;
		for(size_t j = i - 1; j > 0 && j < i; j--)
			pOutRow[j] = pOutRow[j - 1] + pOutRow[j];
	}
}

// static
double GMath::approximateErf(double x)
{
	double a = x * x * 8.0 / (3.0 * M_PI) * (M_PI - 3.0) / (4.0 - M_PI);
	return sqrt(1.0 - exp(-x * x * (4.0 / M_PI + a) / (1.0 + a)));
}

// static
double GMath::approximateInverseErf(double x)
{
	double a = 8.0 / (3.0 * M_PI) * (M_PI - 3.0) / (4.0 - M_PI);
	double b = log(1.0 - x * x);
	double c = 2.0 / (M_PI * a);
	double d = c + b / 2.0;
	return sqrt(sqrt(d * d - b / a) - c - b / 2);
}

// static
double GMath::productLog(double x)
{
	if(x < -1.0 / M_E)
		ThrowError("undefined");

	// Compute a good initial estimate
	double w;
	if (x <= 500.0)
	{
		double logxplus1 = log(x + 1.0);
		w = 0.665 * (1 + 0.0195 * logxplus1) * logxplus1 + 0.04;
	}
	else
		w = log(x - 4.0) - (1.0 - 1.0 / log(x)) * log(log(x));

	// Iteratively compute a more precise estimate
	if(w > -1e300 && w < 1e300)
	{
		double prec = 1e-12;
		double expW, wTimesExpW, wPlusOneTimesExpW;
		while(true)
		{
			expW = exp(w);
			wTimesExpW = w * expW;
			wPlusOneTimesExpW = wTimesExpW + expW;
			if(prec >= std::abs((x - wTimesExpW) / wPlusOneTimesExpW))
				break;
			w -= (wTimesExpW - x) / (wPlusOneTimesExpW - (w + 2) * (wTimesExpW - x) / (2.0 * w + 2));
		}
		return w;
	}
	else
		return w;
}

// Evaluates the integral of (ax^2+bx+c)/(dx^2+ex+f) at x=1, assuming an integration constant of zero
double integralOfRatioOfQuadraticPolynomials(double a, double b, double c, double d, double e, double f)
{
	double u = e * e;
	double v = d * d;
	double w = b * d;
	double x = d * f;
	double t = sqrt(4.0 * x - u);
	double y = a * d +
		(2.0 * c * v - w * e + a * (u - 2.0 * x)) * atan((2.0 * d + e) / t) / t +
		0.5 * (w - a * e) * log(f + d + e);
	return y / v;
}

#ifndef NO_TEST_CODE
double ComputeCosine(void* pThis, double d)
{
	return cos(d);
}

// static
void GMath::test()
{
	// Test Integrate
	double dTarget = sin(M_PI / 2) - sin(-M_PI / 2);
	double dComputed = GMath::integrate(ComputeCosine, -M_PI / 2, M_PI / 2, 500, NULL);
	if(std::abs(dTarget - dComputed) > .00001)
		throw "wrong answer";

	// Test Newton's Polynomial
	double t0 = 23;
	double t1 = 17;
	double t2 = 37;
	double t3 = 83;
	double x0 = 11;
	double x1 = 53;
	double x2 = 83;
	double x3 = 7;
	double t[4];
	double x[4];
	t[0] = t0;
	t[1] = t1;
	t[2] = t2;
	t[3] = t3;
	x[0] = x0;
	x[1] = x1;
	x[2] = x2;
	x[3] = x3;
	GMath::newtonPolynomial(t, x, 4);
	if(std::abs(x[0] + x[1] * t0 + x[2] * t0 * t0 + x[3] * t0 * t0 * t0 - x0) > .0000001)
		throw "wrong answer";
	if(std::abs(x[0] + x[1] * t1 + x[2] * t1 * t1 + x[3] * t1 * t1 * t1 - x1) > .0000001)
		throw "wrong answer";
	if(std::abs(x[0] + x[1] * t2 + x[2] * t2 * t2 + x[3] * t2 * t2 * t2 - x2) > .0000001)
		throw "wrong answer";
	if(std::abs(x[0] + x[1] * t3 + x[2] * t3 * t3 + x[3] * t3 * t3 * t3 - x3) > .0000001)
		throw "wrong answer";
}
#endif // !NO_TEST_CODE
