/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#include "GKernelTrick.h"
#include "GHillClimber.h"
#include "GDistribution.h"
#include "GMath.h"

using namespace GClasses;


GKernel* makeKernel(size_t dims)
{
	//return new GKernelIdentity(dims);
	//return new GKernelPolynomial(dims, 1, 7);

	GKernel* pK1 = new GKernelPolynomial(dims, 0, 3);
	GKernel* pK2 = new GKernelPolynomial(dims, 1, 7);
	GKernel* pK3 = new GKernelAdd(pK1, pK2);
	GKernel* pK4 = new GKernelNormalize(pK3);

	GKernel* pK5 = new GKernelGaussianRBF(dims, 0.01);
	GKernel* pK6 = new GKernelGaussianRBF(dims, 0.1);
	GKernel* pK7 = new GKernelAdd(pK5, pK6);
	GKernel* pK8 = new GKernelNormalize(pK7);

	GKernel* pK9 = new GKernelGaussianRBF(dims, 1.0);
	GKernel* pK10 = new GKernelGaussianRBF(dims, 10.0);
	GKernel* pK11 = new GKernelMultiply(pK9, pK10);
	GKernel* pK12 = new GKernelNormalize(pK11);

	GKernel* pK13 = new GKernelAdd(pK8, pK12);
	GKernel* pK14 = new GKernelAdd(pK4, pK13);
	return pK14;
}

