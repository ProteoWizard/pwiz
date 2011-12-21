/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#include "GVec.h"
#include <cstdio>
#include <cstring>
#include "GRand.h"
#include "GError.h"
#include "GMatrix.h"
#include "GBits.h"
#include "GDom.h"
#include "GMath.h"
#include "GImage.h"
#include "GBitTable.h"
#include <cmath>

namespace GClasses {

using std::vector;

// static
bool GVec::doesContainUnknowns(const double* pVector, size_t nSize)
{
	for(size_t n = 0; n < nSize; n++)
	{
		if(*pVector == UNKNOWN_REAL_VALUE)
			return true;
		pVector++;
	}
	return false;
}

// static
void GVec::copy(double* pDest, const double* pSource, size_t nDims)
{
	memcpy(pDest, pSource, sizeof(double) * nDims);
}

// static
double GVec::dotProduct(const double* pA, const double* pB, size_t nSize)
{
	double d = 0;
	while(nSize > 0)
	{
		d += *(pA++) * *(pB++);
		nSize--;
	}
	return d;
}

// static
double GVec::dotProduct(const double* pOrigin, const double* pTarget, const double* pVector, size_t nSize)
{
	double d = 0;
	while(nSize > 0)
	{
		d += (*(pTarget++) - *(pOrigin++)) * (*(pVector++));
		nSize--;
	}
	return d;
}

// static
double GVec::dotProduct(const double* pOriginA, const double* pTargetA, const double* pOriginB, const double* pTargetB, size_t nSize)
{
	double dVal = 0;
	for(size_t n = 0; n < nSize; n++)
	{
		dVal += (*pTargetA - *pOriginA) * (*pTargetB - *pOriginB);
		pTargetA++;
		pOriginA++;
		pTargetB++;
		pOriginB++;
	}
	return dVal;
}

// static
double GVec::dotProductIgnoringUnknowns(const double* pOrigin, const double* pTarget, const double* pVector, size_t nSize)
{
	double dVal = 0;
	for(size_t n = 0; n < nSize; n++)
	{
		GAssert(pOrigin[n] != UNKNOWN_REAL_VALUE && pVector[n] != UNKNOWN_REAL_VALUE); // unknowns in pOrigin or pVector not supported
		if(pTarget[n] != UNKNOWN_REAL_VALUE)
			dVal += (pTarget[n] - pOrigin[n]) * pVector[n];
	}
	return dVal;
}

// static
double GVec::squaredDistance(const double* pA, const double* pB, size_t nDims)
{
	double dist = 0;
	double d;
	for(size_t n = 0; n < nDims; n++)
	{
		d = (*pA) - (*pB);
		dist += (d * d);
		pA++;
		pB++;
	}
	return dist;
}

// static
double GVec::estimateSquaredDistanceWithUnknowns(const double* pA, const double* pB, size_t nDims)
{
	double dist = 0;
	double d;
	size_t nMissing = 0;
	for(size_t n = 0; n < nDims; n++)
	{
		if(pA[n] == UNKNOWN_REAL_VALUE || pB[n] == UNKNOWN_REAL_VALUE)
			nMissing++;
		else
		{
			d = pA[n] - pB[n];
			dist += (d * d);
		}
	}
	if(nMissing >= nDims)
		return 1e50; // we have no info, so let's make a wild guess
	else
		return dist * nDims / (nDims - nMissing);
}

// static
double GVec::squaredMagnitude(const double* pVector, size_t nSize)
{
	double dMag = 0;
	while(nSize > 0)
	{
		dMag += ((*pVector) * (*pVector));
		pVector++;
		nSize--;
	}
	return dMag;
}

// static
double GVec::lNormMagnitude(double norm, const double* pVector, size_t nSize)
{
	double dMag = 0;
	for(size_t i = 0; i < nSize; i++)
		dMag += std::pow(std::abs(pVector[i]), norm);
	return std::pow(dMag, 1.0 / norm);
}

// static
double GVec::lNormDistance(double norm, const double* pA, const double* pB, size_t dims)
{
	double dist = 0;
	for(size_t i = 0; i < dims; i++)
	{
		dist += std::pow(std::abs(*pA - *pB), norm);
		pA++;
		pB++;
	}
	return std::pow(dist, 1.0 / norm);
}

// static
void GVec::lNormNormalize(double norm, double* pVector, size_t nSize)
{
	double dMag = lNormMagnitude(norm, pVector, nSize);
	for(size_t i = 0; i < nSize; i++)
		pVector[i] /= dMag;
}


// static
double GVec::correlation(const double* pA, const double* pB, size_t nDims)
{
	double dDotProd = dotProduct(pA, pB, nDims);
	if(dDotProd == 0)
		return 0;
	return dDotProd / (sqrt(squaredMagnitude(pA, nDims) * squaredMagnitude(pB, nDims)));
}

// static
double GVec::correlation(const double* pOriginA, const double* pTargetA, const double* pB, size_t nDims)
{
	double dDotProd = dotProduct(pOriginA, pTargetA, pB, nDims);
	if(dDotProd == 0)
		return 0;
	return dDotProd / (sqrt(squaredDistance(pOriginA, pTargetA, nDims) * squaredMagnitude(pB, nDims)));
}

// static
double GVec::correlation(const double* pOriginA, const double* pTargetA, const double* pOriginB, const double* pTargetB, size_t nDims)
{
	double dDotProd = dotProduct(pOriginA, pTargetA, pOriginB, pTargetB, nDims);
	if(dDotProd == 0)
		return 0;
	return dDotProd / (sqrt(squaredDistance(pOriginA, pTargetA, nDims) * squaredDistance(pOriginB, pTargetB, nDims)));
}

// static
void GVec::normalize(double* pVector, size_t nSize)
{
	double dMag = squaredMagnitude(pVector, nSize);
	if(dMag <= 0)
		ThrowError("Can't normalize a vector with zero magnitude");
	GVec::multiply(pVector, 1.0  / sqrt(dMag), nSize);
}

// static
void GVec::safeNormalize(double* pVector, size_t nSize, GRand* pRand)
{
	double dMag = squaredMagnitude(pVector, nSize);
	if(dMag <= 0)
		pRand->spherical(pVector, nSize);
	else
		GVec::multiply(pVector, 1.0  / sqrt(dMag), nSize);
}

// static
void GVec::sumToOne(double* pVector, size_t size)
{
	double sum = GVec::sumElements(pVector, size);
	if(sum == 0)
		GVec::setAll(pVector, 1.0 / size, size);
	else
		GVec::multiply(pVector, 1.0 / sum, size);
}

// static
size_t GVec::indexOfMin(const double* pVector, size_t dims, GRand* pRand)
{
	size_t index = 0;
	size_t count = 1;
	for(size_t n = 1; n < dims; n++)
	{
		if(pVector[n] <= pVector[index])
		{
			if(pVector[n] == pVector[index])
			{
				count++;
				if(pRand->next(count) == 0)
					index = n;
			}
			else
			{
				index = n;
				count = 1;
			}
		}
	}
	return index;
}

// static
size_t GVec::indexOfMax(const double* pVector, size_t dims, GRand* pRand)
{
	size_t index = 0;
	size_t count = 1;
	for(size_t n = 1; n < dims; n++)
	{
		if(pVector[n] >= pVector[index])
		{
			if(pVector[n] == pVector[index])
			{
				count++;
				if(pRand->next(count) == 0)
					index = n;
			}
			else
			{
				index = n;
				count = 1;
			}
		}
	}
	return index;
}

// static
size_t GVec::indexOfMaxMagnitude(const double* pVector, size_t dims, GRand* pRand)
{
	size_t index = 0;
	size_t count = 1;
	for(size_t n = 1; n < dims; n++)
	{
		if(std::abs(pVector[n]) >= std::abs(pVector[index]))
		{
			if(std::abs(pVector[n]) == std::abs(pVector[index]))
			{
				count++;
				if(pRand->next(count) == 0)
					index = n;
			}
			else
			{
				index = n;
				count = 1;
			}
		}
	}
	return index;
}

// static
void GVec::add(double* pDest, const double* pSource, size_t nDims)
{
	for(size_t i = 0; i < nDims; i++)
	{
		*pDest += *pSource;
		pDest++;
		pSource++;
	}
}

// static
void GVec::addScaled(double* pDest, double dMag, const double* pSource, size_t nDims)
{
	for(size_t i = 0; i < nDims; i++)
	{
		*pDest += (dMag * (*pSource));
		pDest++;
		pSource++;
	}
}

// static
void GVec::addLog(double* pDest, const double* pSource, size_t nDims)
{
	for(size_t i = 0; i < nDims; i++)
		pDest[i] += log(pSource[i]);
}

// static
void GVec::subtract(double* pDest, const double* pSource, size_t nDims)
{
	for(size_t i = 0; i < nDims; i++)
	{
		*pDest -= *pSource;
		pDest++;
		pSource++;
	}
}

// static
void GVec::multiply(double* pVector, double dScalar, size_t nDims)
{
	for(size_t i = 0; i < nDims; i++)
	{
		*pVector *= dScalar;
		pVector++;
	}
}

//static 
void GVec::pow(double* pVector, double dScalar, size_t nDims)
{
	for(size_t i = 0; i < nDims; i++)
	{
		*pVector = std::pow(*pVector, dScalar);
		pVector++;
	}
}


// static
void GVec::pairwiseMultiply(double* pDest, double* pOther, size_t dims)
{
	while(dims > 0)
	{
		*(pDest++) *= *(pOther++);
		dims--;
	}
}

// static
void GVec::pairwiseDivide(double* pDest, double* pOther, size_t dims)
{
	while(dims > 0)
	{
		*(pDest++) /= *(pOther++);
		dims--;
	}
}

// static
void GVec::setAll(double* pVector, double value, size_t dims)
{
	for(size_t i = 0; i < dims; i++)
	{
		*pVector = value;
		pVector++;
	}
}

void GVec::interpolateIndexes(size_t nIndexes, double* pInIndexes, double* pOutIndexes, float fRatio, size_t nCorrIndexes, double* pCorrIndexes1, double* pCorrIndexes2)
{
	GAssert(nCorrIndexes >= 2); // need at least two correlated indexes (at least the two extremes)
	size_t nCorr = 0;
	double fInvRatio = (float)1 - fRatio;
	double fIndex, fWeight, f0, f1;
	for(size_t i = 0; i < nIndexes; i++)
	{
		fIndex = pInIndexes[i];
		while(nCorr < nCorrIndexes - 2 && fIndex >= pCorrIndexes1[nCorr + 1])
			nCorr++;
		fWeight = (fIndex - pCorrIndexes1[nCorr]) / (pCorrIndexes1[nCorr + 1] - pCorrIndexes1[nCorr]);
		f0 = fInvRatio * pCorrIndexes1[nCorr] + fRatio * pCorrIndexes2[nCorr];
		f1 = fInvRatio * pCorrIndexes1[nCorr + 1] + fRatio * pCorrIndexes2[nCorr + 1];
		pOutIndexes[i] = ((float)1 - fWeight) * f0 + fWeight * f1;
	}
}

void GVec::rotate(double* pVector, size_t nDims, double dAngle, const double* pA, const double* pB)
{
	// Check that the vectors are orthogonal
	GAssert(pVector != pA && pVector != pB); // expected different vectors
	GAssert(std::abs(GVec::dotProduct(pA, pB, nDims)) < 1e-4); // expected orthogonal plane axes

	// Remove old planar component
	double x = GVec::dotProduct(pVector, pA, nDims);
	double y = GVec::dotProduct(pVector, pB, nDims);
	GVec::addScaled(pVector, -x, pA, nDims);
	GVec::addScaled(pVector, -y, pB, nDims);

	// Rotate
	double dRadius = sqrt(x * x + y * y);
	double dTheta = atan2(y, x);
	dTheta += dAngle;
	x = dRadius * cos(dTheta);
	y = dRadius * sin(dTheta);

	// Add new planar component
	GVec::addScaled(pVector, x, pA, nDims);
	GVec::addScaled(pVector, y, pB, nDims);
}

void GVec::addInterpolatedFunction(double* pOut, size_t nOutVals, double* pIn, size_t nInVals)
{
	if(nInVals > nOutVals)
	{
		size_t inPos = 0;
		size_t outPos, n, count;
		double d;
		for(outPos = 0; outPos < nOutVals; outPos++)
		{
			n = outPos * nInVals / nOutVals;
			d = 0;
			count = 0;
			while(inPos <= n)
			{
				d += pIn[inPos++];
				count++;
			}
			pOut[outPos] += d / count;
		}
	}
	else if(nInVals < nOutVals)
	{
		double d;
		size_t n, i, j;
		for(n = 0; n < nOutVals; n++)
		{
			d = (double)n * nInVals / nOutVals;
			i = (int)d;
			j = std::min(i + 1, nInVals - 1);
			d -= i;
			pOut[n] += ((1.0 - d) * pIn[i] + d * pIn[j]);
		}
	}
	else
	{
		for(size_t n = 0; n < nOutVals; n++)
			pOut[n] += pIn[n];
	}
}

// static
GDomNode* GVec::serialize(GDom* pDoc, const double* pVec, size_t dims)
{
	GDomNode* pNode = pDoc->newList();
	for(size_t i = 0; i < dims; i++)
		pNode->addItem(pDoc, pDoc->newDouble(*(pVec++)));
	return pNode;
}

// static
void GVec::deserialize(double* pVec, GDomListIterator& it)
{
	while(it.current())
	{
		*(pVec++) = it.current()->asDouble();
		it.advance();
	}
}

// static
void GVec::print(std::ostream& stream, int precision, double* pVec, size_t dims)
{
	if(dims == 0)
		return;
	stream.precision(precision);
	stream << *pVec;
	pVec++;
	for(size_t i = 1; i < dims; i++)
	{
		stream << ", ";
		stream << *pVec;
		pVec++;
	}
}

void GVec::project(double* pDest, const double* pPoint, const double* pOrigin, const double* pBasis, size_t basisCount, size_t dims)
{
	GVec::copy(pDest, pOrigin, dims);
	for(size_t j = 0; j < basisCount; j++)
	{
		GVec::addScaled(pDest, GVec::dotProduct(pOrigin, pPoint, pBasis, dims), pBasis, dims);
		pBasis += dims;
	}
}

void GVec::subtractComponent(double* pInOut, const double* pBasis, size_t dims)
{
	double component = dotProduct(pInOut, pBasis, dims);
	for(size_t i = 0; i < dims; i++)
	{
		*pInOut -= *pBasis * component;
		pBasis++;
		pInOut++;
	}
}

void GVec::subtractComponent(double* pInOut, const double* pOrigin, const double* pTarget, size_t dims)
{
	double component = dotProduct(pInOut, pOrigin, pTarget, dims) / squaredDistance(pOrigin, pTarget, dims);
	for(size_t i = 0; i < dims; i++)
	{
		*pInOut -= (*pTarget - *pOrigin) * component;
		pTarget++;
		pOrigin++;
		pInOut++;
	}
}

double GVec::sumElements(const double* pVec, size_t dims)
{
	double sum = 0;
	while(dims > 0)
	{
		sum += *pVec;
		pVec++;
		dims--;
	}
	return sum;
}


void GVec_InsertionSort(double* pVec, size_t size, double* pParallel1, size_t* pParallel2, double* pParallel3)
{
	for(size_t i = 1; i < size; i++)
	{
		for(size_t j = i; j > 0; j--)
		{
			if(pVec[j] >= pVec[j - 1])
				break;

			// Swap
			std::swap(pVec[j - 1], pVec[j]);
			if(pParallel1)
				std::swap(pParallel1[j - 1], pParallel1[j]);
			if(pParallel2)
				std::swap(pParallel2[j - 1], pParallel2[j]);
		}
	}
}

// static
void GVec::smallestToFront(double* pVec, size_t k, size_t size, double* pParallel1, size_t* pParallel2, double* pParallel3)
{
	// Use insertion sort if the list is small
	if(size < 7)
	{
		if(k < size)
			GVec_InsertionSort(pVec, size, pParallel1, pParallel2, pParallel3);
		return;
	}
	size_t beg = 0;
	size_t end = size - 1;

	// Pick a pivot (using the median of 3 technique)
	double pivA = pVec[0];
	double pivB = pVec[size / 2];
	double pivC = pVec[size - 1];
	double pivot;
	if(pivA < pivB)
	{
		if(pivB < pivC)
			pivot = pivB;
		else if(pivA < pivC)
			pivot = pivC;
		else
			pivot = pivA;
	}
	else
	{
		if(pivA < pivC)
			pivot = pivA;
		else if(pivB < pivC)
			pivot = pivC;
		else
			pivot = pivB;
	}

	// Do Quick Sort
	while(true)
	{
		while(beg < end && pVec[beg] < pivot)
			beg++;
		while(end > beg && pVec[end] > pivot)
			end--;
		if(beg >= end)
			break;
		std::swap(pVec[beg], pVec[end]);
		if(pParallel1)
			std::swap(pParallel1[beg], pParallel1[end]);
		if(pParallel2)
			std::swap(pParallel2[beg], pParallel2[end]);
		if(pParallel3)
			std::swap(pParallel3[beg], pParallel3[end]);
		beg++;
		end--;
	}

	// Recurse
	if(pVec[beg] < pivot)
		beg++;
	if(k < beg)
		GVec::smallestToFront(pVec, k, beg, pParallel1, pParallel2, pParallel3);
	if(k > beg)
		GVec::smallestToFront(pVec + beg, k - beg, size - beg, pParallel1 ? pParallel1 + beg : NULL, pParallel2 ? pParallel2 + beg : NULL, pParallel3 ? pParallel3 + beg : NULL);
}

// static
double GVec::refinePoint(double* pPoint, double* pNeighbor, size_t dims, double distance, double learningRate, GRand* pRand)
{
	GTEMPBUF(double, buf, dims);
	GVec::copy(buf, pPoint, dims);
	GVec::subtract(buf, pNeighbor, dims);
	double mag = squaredMagnitude(buf, dims);
	GVec::safeNormalize(buf, dims, pRand);
	GVec::multiply(buf, distance, dims);
	GVec::add(buf, pNeighbor, dims);
	GVec::subtract(buf, pPoint, dims);
	GVec::multiply(buf, learningRate, dims);
	GVec::add(pPoint, buf, dims);
	return mag;
}

// static
void GVec::toImage(const double* pVec, GImage* pImage, int width, int height, int channels, double range)
{
	pImage->setSize(width, height);
	unsigned int* pix = pImage->pixels();
	if(channels == 3)
	{
		for(int y = 0; y < height; y++)
		{
			for(int x = 0; x < width; x++)
			{
				int r = ClipChan((int)(*(pVec++) * 256 / range));
				int g = ClipChan((int)(*(pVec++) * 256 / range));
				int b = ClipChan((int)(*(pVec++) * 256 / range));
				*(pix++) = gARGB(0xff, r, g, b);
			}
		}
	}
	else if(channels == 1)
	{
		for(int y = 0; y < height; y++)
		{
			for(int x = 0; x < width; x++)
			{
				int v = ClipChan((int)(*(pVec++) * 256 / range));
				*(pix++) = gARGB(0xff, v, v, v);
			}
		}
	}
	else
		ThrowError("unsupported value for channels");
}

// static
void GVec::fromImage(GImage* pImage, double* pVec, int width, int height, int channels, double range)
{
	unsigned int* pix = pImage->pixels();
	if(channels == 3)
	{
		for(int y = 0; y < height; y++)
		{
			for(int x = 0; x < width; x++)
			{
				*(pVec++) = gRed(*pix) * range / 255;
				*(pVec++) = gGreen(*pix) * range / 255;
				*(pVec++) = gBlue(*pix) * range / 255;
				pix++;
			}
		}
	}
	else if(channels == 1)
	{
		for(int y = 0; y < height; y++)
		{
			for(int x = 0; x < width; x++)
			{
				*(pVec++) = gGray(*pix) * range / MAX_GRAY_VALUE;
				pix++;
			}
		}
	}
	else
		ThrowError("unsupported value for channels");
}

// static
void GVec::capValues(double* pVec, double cap, size_t dims)
{
	while(true)
	{
		*pVec = std::min(*pVec, cap);
		if(--dims == 0)
			return;
		pVec++;
	}
}

// static
void GVec::floorValues(double* pVec, double floor, size_t dims)
{
	while(true)
	{
		*pVec = std::max(*pVec, floor);
		if(--dims == 0)
			return;
		pVec++;
	}
}

#ifndef NO_TEST_CODE
// static
void GVec::test()
{
	GRand prng(0);
	GTEMPBUF(double, v1, 200);
	double* v2 = v1 + 100;
	for(int i = 0; i < 10; i++)
	{
		prng.spherical(v1, 100);
		prng.spherical(v2, 100);
		GVec::subtractComponent(v2, v1, 100);
		GVec::normalize(v2, 100);
		if(std::abs(GVec::correlation(v1, v2, 100)) > 1e-4)
			ThrowError("Failed");
		if(std::abs(GVec::squaredMagnitude(v1, 100) - 1) > 1e-4)
			ThrowError("Failed");
		if(std::abs(GVec::squaredMagnitude(v2, 100) - 1) > 1e-4)
			ThrowError("Failed");
	}
}
#endif // NO_TEST_CODE







// static
void GIndexVec::makeIndexVec(size_t* pVec, size_t size)
{
	for(size_t i = 0; i < size; i++)
	{
		*pVec = i;
		pVec++;
	}
}

// static
void GIndexVec::shuffle(size_t* pVec, size_t size, GRand* pRand)
{
	for(size_t i = size; i > 1; i--)
	{
		size_t r = (size_t)pRand->next(i);
		size_t t = pVec[i - 1];
		pVec[i - 1] = pVec[r];
		pVec[r] = t;
	}
}

// static
void GIndexVec::setAll(size_t* pVec, size_t value, size_t size)
{
	while(size > 0)
	{
		*pVec = value;
		pVec++;
		size--;
	}
}

// static
void GIndexVec::copy(size_t* pDest, const size_t* pSource, size_t nDims)
{
	memcpy(pDest, pSource, sizeof(size_t) * nDims);
}

// static
size_t GIndexVec::maxValue(size_t* pVec, size_t size)
{
	size_t m = *(pVec++);
	size--;
	while(size > 0)
	{
		m = std::max(m, *(pVec++));
		size--;
	}
	return m;
}

// static
size_t GIndexVec::indexOfMax(size_t* pVec, size_t size)
{
	size_t index = 0;
	size_t m = *(pVec++);
	size--;
	size_t i = 1;
	while(size > 0)
	{
		if(*pVec > m)
		{
			m = *pVec;
			index = i;
		}
		pVec++;
		size--;
		i++;
	}
	return index;
}

// static
GDomNode* GIndexVec::serialize(GDom* pDoc, const size_t* pVec, size_t dims)
{
	GDomNode* pNode = pDoc->newList();
	for(size_t i = 0; i < dims; i++)
		pNode->addItem(pDoc, pDoc->newInt(*(pVec++)));
	return pNode;
}

// static
void GIndexVec::deserialize(size_t* pVec, GDomListIterator& it)
{
	while(it.current())
	{
		*(pVec++) = size_t(it.current()->asInt());
		it.advance();
	}
}











GCoordVectorIterator::GCoordVectorIterator(size_t dims, size_t* pRanges)
{
	m_pCoords = NULL;
	reset(dims, pRanges);
}

GCoordVectorIterator::GCoordVectorIterator(vector<size_t>& ranges)
{
	m_pCoords = NULL;
	reset(ranges);
}

GCoordVectorIterator::~GCoordVectorIterator()
{
	delete[] m_pCoords;
}

void GCoordVectorIterator::reset()
{
	memset(m_pCoords, '\0', sizeof(size_t) * m_dims);
	m_sampleShift = (size_t)-1;
}

void GCoordVectorIterator::reset(size_t dims, size_t* pRanges)
{
	m_dims = dims;
	delete[] m_pCoords;
	if(dims > 0)
	{
		m_pCoords = new size_t[2 * dims];
		m_pRanges = m_pCoords + dims;
		if(pRanges)
			memcpy(m_pRanges, pRanges, sizeof(size_t) * dims);
		else
		{
			for(size_t i = 0; i < dims; i++)
				m_pRanges[i] = 1;
		}
	}
	else
	{
		m_pCoords = NULL;
		m_pRanges = NULL;
	}
	reset();
}

void GCoordVectorIterator::reset(vector<size_t>& ranges)
{
	m_dims = ranges.size();
	delete[] m_pCoords;
	if(m_dims > 0)
	{
		m_pCoords = new size_t[2 * m_dims];
		m_pRanges = m_pCoords + m_dims;
		for(size_t i = 0; i < m_dims; i++)
			m_pRanges[i] = ranges[i];
	}
	else
	{
		m_pCoords = NULL;
		m_pRanges = NULL;
	}
	reset();
}

size_t GCoordVectorIterator::coordCount()
{
	size_t n = 1;
	size_t* pR = m_pRanges;
	for(size_t i = 0; i < m_dims; i++)
		n *= (*(pR++));
	return n;
}

bool GCoordVectorIterator::advance()
{
	size_t j;
	for(j = 0; j < m_dims; j++)
	{
		if(++m_pCoords[j] >= m_pRanges[j])
			m_pCoords[j] = 0;
		else
			break;
	}

	// Test if we're done
	if(j >= m_dims)
		return false;
	return true;
}

bool GCoordVectorIterator::advance(size_t steps)
{
	size_t j;
	for(j = 0; j < m_dims; j++)
	{
		size_t t = m_pCoords[j] + steps;
		m_pCoords[j] = t % m_pRanges[j];
		steps = t / m_pRanges[j];
		if(t == 0)
			break;
	}

	// Test if we're done
	if(j >= m_dims)
		return false;
	return true;
}

bool GCoordVectorIterator::advanceSampling()
{
	if(m_sampleShift == (size_t)-1) // if we have not yet computed the step size
	{
		size_t r = m_pRanges[0];
		for(size_t i = 1; i < m_dims; i++)
			r = std::max(r, m_pRanges[i]);
		m_sampleShift = GBits::boundingShift(r);
		m_sampleMask = 0;
	}

	m_pCoords[0] += ((size_t)1 << (m_sampleShift + (m_sampleMask ? 0 : 1)));
	if(m_pCoords[0] >= m_pRanges[0])
	{
		m_pCoords[0] = 0;
		size_t j = 1;
		for( ; j < m_dims; j++)
		{
			m_pCoords[j] += ((size_t)1 << m_sampleShift);
			m_sampleMask ^= ((size_t)1 << j);
			if(m_pCoords[j] < m_pRanges[j])
				break;
			m_pCoords[j] = 0;
			m_sampleMask &= ~((size_t)1 << j);
		}
		if(j >= m_dims)
		{
			if(--m_sampleShift == (size_t)-1) // if we're all done
				return false;
		}
		if(m_sampleMask == 0)
		{
			m_pCoords[0] -= ((size_t)1 << m_sampleShift);
			return advanceSampling();
		}
	}
	return true;
}

size_t* GCoordVectorIterator::current()
{
	return m_pCoords;
}

void GCoordVectorIterator::currentNormalized(double* pCoords)
{
	for(size_t i = 0; i < m_dims; i++)
	{
		*pCoords = ((double)m_pCoords[i] + 0.5) / m_pRanges[i];
		pCoords++;
	}
}

size_t GCoordVectorIterator::currentIndex()
{
	size_t index = 0;
	size_t n = 1;
	for(size_t i = 0; i < m_dims; i++)
	{
		index += n * m_pCoords[i];
		n *= m_pRanges[i];
	}
	return index;
}

void GCoordVectorIterator::setRandom(GRand* pRand)
{
	for(size_t i = 0; i < m_dims; i++)
		m_pCoords[i] = (size_t)pRand->next(m_pRanges[i]);
}

#ifndef NO_TEST_CODE
#define TEST_DIMS 4
// static
void GCoordVectorIterator::test()
{
	size_t r = 11;
	size_t size = 1;
	for(size_t i = 0; i < TEST_DIMS; i++)
		size *= r;
	GBitTable bt(size);
	size_t ranges[TEST_DIMS];
	for(size_t i = 0; i < TEST_DIMS; i++)
		ranges[i] = r;
	GCoordVectorIterator cvi(TEST_DIMS, ranges);
	size_t count = 0;
	while(true)
	{
		size_t index = cvi.currentIndex();
		if(bt.bit(index))
			ThrowError("already got this one");
		bt.set(index);
		count++;
		if(!cvi.advanceSampling())
			break;
	}
	if(count != size)
		ThrowError("didn't get them all");
}
#endif


} // namespace GClasses

