/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __GVEC_H__
#define __GVEC_H__

#include <stdio.h>
#include <iostream>
#include <vector>

namespace GClasses {

class GRand;
class GDom;
class GDomNode;
class GImage;
class GDomListIterator;

/// Contains some useful functions for operating on vectors
class GVec
{
public:
#ifndef NO_TEST_CODE
	/// Performs unit tests for this class. Throws an exception if there is a failure.
	static void test();
#endif

	/// This returns true if the vector contains any unknown values. Most of the methods in this
	/// class will give bad results if a vector contains unknown values, but for efficiency
	/// reasons, they don't check. So it's your job to check your vectors first.
	static bool doesContainUnknowns(const double* pVector, size_t nSize);

	/// This just wraps memcpy
	static void copy(double* pDest, const double* pSource, size_t nDims);

	/// Computes the dot product of two vectors. Results are undefined if pA or pB
	/// contain unknown values.
	static double dotProduct(const double* pA, const double* pB, size_t nSize);

	/// Computes the dot product of (pTarget - pOrigin) with pVector.
	static double dotProduct(const double* pOrigin, const double* pTarget, const double* pVector, size_t nSize);

	/// Computes the dot product of (pTargetA - pOriginA) with (pTargetB - pOriginB).
	static double dotProduct(const double* pOriginA, const double* pTargetA, const double* pOriginB, const double* pTargetB, size_t nSize);

	/// Computes the dot product of (pTarget - pOrigin) with pVector. Unknown values
	/// in pTarget will simply be ignored. (pOrigin and pVector must not contain any
	/// unknown values.)
	static double dotProductIgnoringUnknowns(const double* pOrigin, const double* pTarget, const double* pVector, size_t nSize);

	/// Computes the squared magnitude of the vector
	static double squaredMagnitude(const double* pVector, size_t nSize);

	/// Computes the magnitude in L-Norm distance (norm=1 is manhattan distance, norm=2 is Euclidean distance, norm=infinity is Chebychev, etc.)
	static double lNormMagnitude(double norm, const double* pVector, size_t nSize);

	/// Normalizes this vector to a magnitude of 1. Throws an exception if the magnitude is zero.
	static void normalize(double* pVector, size_t nSize);

	/// Normalizes this vector to a magnitude of 1. If the magnitude is zero, it returns a random vector.
	static void safeNormalize(double* pVector, size_t nSize, GRand* pRand);

	/// Scale the vector so that the elements sum to 1
	static void sumToOne(double* pVector, size_t size);

	/// Normalizes with L-Norm distance (norm=1 is manhattan distance, norm=2 is Euclidean distance, norm=infinity is Chebychev, etc.)
	static void lNormNormalize(double norm, double* pVector, size_t nSize);

	/// Computes the squared distance between two vectors
	static double squaredDistance(const double* pA, const double* pB, size_t nDims);

	/// Estimates the squared distance between two points that may have some missing values. It assumes
	/// the distance in missing dimensions is approximately the same as the average distance in other
	/// dimensions. If there are no known dimensions that overlap between the two points, it returns
	/// 1e50.
	static double estimateSquaredDistanceWithUnknowns(const double* pA, const double* pB, size_t nDims);

	/// Computes L-Norm distance (norm=1 is manhattan distance, norm=2 is Euclidean distance, norm=infinity is Chebychev, etc.)
	static double lNormDistance(double norm, const double* pA, const double* pB, size_t dims);

	/// Computes the cosine of the angle between two vectors (the origin is the vertex)
	static double correlation(const double* pA, const double* pB, size_t nDims);

	/// Computes the cosine of the angle between two vectors (the origin is the vertex)
	static double correlation(const double* pOriginA, const double* pTargetA, const double* pB, size_t nDims);

	/// Computes the cosine of the angle between two vectors (the origin is the vertex)
	static double correlation(const double* pOriginA, const double* pTargetA, const double* pOriginB, const double* pTargetB, size_t nDims);

	/// Returns the index of the min value in pVector. If multiple elements have
	/// have an equivalent max value, it randomly (uniformly) picks from all the ties.
	static size_t indexOfMin(const double* pVector, size_t dims, GRand* pRand);

	/// Returns the index of the max value in pVector. If multiple elements have
	/// have an equivalent max value, it randomly (uniformly) picks from all the ties.
	static size_t indexOfMax(const double* pVector, size_t dims, GRand* pRand);

	/// Returns the index of the value with the largest magnitude in pVector. If multiple elements have
	/// have an equivalent magnitude, it randomly (uniformly) picks from all the ties.
	static size_t indexOfMaxMagnitude(const double* pVector, size_t dims, GRand* pRand);

	/// Adds pSource to pDest
	static void add(double* pDest, const double* pSource, size_t nDims);

	/// Adds dMag * pSource to pDest
	static void addScaled(double* pDest, double dMag, const double* pSource, size_t nDims);

	/// Adds the log of each element in pSource to pDest
	static void addLog(double* pDest, const double* pSource, size_t nDims);

	/// Subtracts pSource from pDest
	static void subtract(double* pDest, const double* pSource, size_t nDims);

	/// Multiplies pVector by dScalar
	static void multiply(double* pVector, double dScalar, size_t nDims);

	/// Raises each element of pVector to the exponent dScalar
	static void pow(double* pVector, double dScalar, size_t nDims);
	
	/// Multiplies each element in pDest by the corresponding element in pOther
	static void pairwiseMultiply(double* pDest, double* pOther, size_t dims);

	/// Divides each element in pDest by the corresponding element in pOther
	static void pairwiseDivide(double* pDest, double* pOther, size_t dims);

	/// Sets all the elements to the specified value
	static void setAll(double* pVector, double value, size_t dims);

	/// Interpolates (morphs) a set of indexes from one function to another. pInIndexes, pCorrIndexes1,
	/// and pCorrIndexes2 are all expected to be in sorted order. All indexes should be >= 0 and < nDims.
	/// fRatio is the interpolation ratio such that if fRatio is zero, all indexes left unchanged, and
	/// as fRatio approaches one, the indexes are interpolated linearly such that each index in pCorrIndexes1
	/// is interpolated linearly to the corresponding index in pCorrIndexes2. If the two extremes are not
	/// in the list of corresponding indexes, the ends may drift.
	static void interpolateIndexes(size_t nIndexes, double* pInIndexes, double* pOutIndexes, float fRatio, size_t nCorrIndexes, double* pCorrIndexes1, double* pCorrIndexes2);

	/// Rotates pVector by dAngle radians in the plane defined by the orthogonal axes pA and pB
	static void rotate(double* pVector, size_t nDims, double dAngle, const double* pA, const double* pB);

	/// Adds the function pIn to pOut after interpolating pIn to be the same size as pOut.
	static void addInterpolatedFunction(double* pOut, size_t nOutVals, double* pIn, size_t nInVals);

	/// Write the vector to a text format
	static GDomNode* serialize(GDom* pDoc, const double* pVec, size_t dims);

	/// Load the vector from a text format. pVec must be large enough to contain all of the
	/// elements that remain in "it".
	static void deserialize(double* pVec, GDomListIterator& it);

	/// Prints the values in the vector separated by ", ".
	/// precision specifies the number of digits to print
	static void print(std::ostream& stream, int precision, double* pVec, size_t dims);

	/// Projects pPoint onto the hyperplane defined by pOrigin onto the basisCount basis vectors
	/// specified by pBasis. (The basis vectors are assumed to be chained end-to-end in a big vector.)
	static void project(double* pDest, const double* pPoint, const double* pOrigin, const double* pBasis, size_t basisCount, size_t dims);

	/// Subtracts the component of pInOut that projects onto pBasis. (Assumes that pBasis is normalized.)
	/// This might be used, for example, to implement the modified Gram-Schmidt process.
	static void subtractComponent(double* pInOut, const double* pBasis, size_t dims);

	/// Subtracts the component of pInOut that projects onto (pTarget - pOrigin).
	/// This might be used, for example, to implement the modified Gram-Schmidt process.
	static void subtractComponent(double* pInOut, const double* pOrigin, const double* pTarget, size_t dims);

	/// Returns the sum of all the elements
	static double sumElements(const double* pVec, size_t dims);

	/// Moves the smallest k values to the front of the vector, and the biggest (size - k) values
	/// to the end of the vector. (For efficiency, no other guarantees about ordering are made.)
	/// This has an average-case runtime that is linear with respect to size.
	/// pParallel1 and pParallel2 are optional arrays that should be arranged to keep their
	/// indices in sync with pVec.
	static void smallestToFront(double* pVec, size_t k, size_t size, double* pParallel1 = NULL, size_t* pParallel2 = NULL, double* pParallel3 = NULL);

	/// Moves "pPoint" so that it is closer to a distance of "distance" from "pNeighbor". "learningRate"
	/// specifies how much to move it (0=not at all, 1=all the way). Returns the squared distance between
	/// pPoint and pNeighbor.
	static double refinePoint(double* pPoint, double* pNeighbor, size_t dims, double distance, double learningRate, GRand* pRand);

	/// Converts a vector of rasterized pixel values to an image.
	/// channels must be 1 or 3 (for grayscale or rgb)
	/// range specifies the range of channel values. Typical values are 1.0 or 255.0.
	/// Pixels are visited in reading order (left-to-right, top-to-bottom).
	static void toImage(const double* pVec, GImage* pImage, int width, int height, int channels, double range);

	/// Converts an image to a vector of rasterized pixel values.
	/// channels must be 1 or 3 (for grayscale or rgb)
	/// range specifies the range of channel values. Typical values are 1.0 or 255.0.
	/// Pixels are visited in reading order (left-to-right, top-to-bottom).
	/// pVec must be big enough to hold width * height * channels values.
	static void fromImage(GImage* pImage, double* pVec, int width, int height, int channels, double range);

	/// Sets each value, v, to MIN(cap, v)
	static void capValues(double* pVec, double cap, size_t dims);

	/// Sets each value, v, to MAX(floor, v)
	static void floorValues(double* pVec, double floor, size_t dims);
};


/// Holds an array of doubles that can be resized. This class is
/// slightly lighter-weight than the C++ vector class, and it
/// allows access to the buffer in the form of an array of doubles.
/// Basically, it is useful when working with C-style functions
/// that expect parameters in the form of an array of doubles, rather
/// than as a vector of doubles.
class GVecBuf
{
public:
	double* m_pBuf;
	size_t m_size;

	GVecBuf()
	: m_pBuf(NULL), m_size(0)
	{
	}

	~GVecBuf()
	{
		delete[] m_pBuf;
	}

	/// Returns the current size of the buffer
	size_t size()
	{
		return m_size;
	}

	/// Resizes the array, nomatter what, and destroys any existing contents
	void resize(size_t size)
	{
		delete[] m_pBuf;
		m_pBuf = new double[size];
		m_size = size;
	}

	/// Resizes the array if necessary, preserving the contents
	void grow(size_t size)
	{
		if(size > m_size)
		{
			double* pNew = new double[size];
			GVec::copy(pNew, m_pBuf, m_size);
			m_size = size;
			delete[] m_pBuf;
			m_pBuf = pNew;
		}
	}

	/// Ensures that the array is at least the specified size. Resizes
	/// (destroying the contents) if it is not.
	void reserve(size_t size)
	{
		if(size > m_size)
			resize(size);
	}
};


/// Useful functions for operating on vectors of indexes
class GIndexVec
{
public:
	/// Makes a vector of ints where each element contains its index (starting with zero, of course)
	static void makeIndexVec(size_t* pVec, size_t size);

	/// Shuffles the vector of ints
	static void shuffle(size_t* pVec, size_t size, GRand* pRand);

	/// Sets all elements to the specified value
	static void setAll(size_t* pVec, size_t value, size_t size);

	/// This just wraps memcpy
	static void copy(size_t* pDest, const size_t* pSource, size_t nDims);

	/// Returns the max value
	static size_t maxValue(size_t* pVec, size_t size);

	/// Returns the index of the max value. In the event of a tie, the
	/// smallest index of one of the max values is returned.
	static size_t indexOfMax(size_t* pVec, size_t size);

	/// Write the vector to a text format
	static GDomNode* serialize(GDom* pDoc, const size_t* pVec, size_t dims);

	/// Load the vector from a text format. pVec must be large enough to contain all of the
	/// elements that remain in "it".
	static void deserialize(size_t* pVec, GDomListIterator& it);
};


/// An iterator for an n-dimensional coordinate vector. For example, suppose you have
/// a 4-dimensional 2x3x2x1 grid, and you want to iterate through its coordinates:
/// (0000, 0010, 0100, 0110, 0200, 0210, 1000, 1010, 1100, 1110, 1200, 1210). This
/// class will iterate over coordinate vectors in this manner. (For 0-dimensional
/// coordinate vectors, it behaves as though the origin is the only valid coordinate.)
class GCoordVectorIterator
{
protected:
	size_t m_dims;
	size_t* m_pCoords;
	size_t* m_pRanges;
	size_t m_sampleShift;
	size_t m_sampleMask;

public:
	/// Makes an internal copy of pRanges. If pRanges is NULL, then it sets
	/// all the range values to 1.
	GCoordVectorIterator(size_t dims, size_t* pRanges);
	GCoordVectorIterator(std::vector<size_t>& ranges);
	~GCoordVectorIterator();

#ifndef NO_TEST_CODE
	/// Performs unit tests for this class. Throws an exception if any problems are found.
	static void test();
#endif

	/// Sets the coordinate vector to all zeros.
	void reset();

	/// Adjusts the number of dims and ranges, and sets the coordinate vector to all zeros.
	/// If pRanges is NULL, then it sets all the range values to 1.
	void reset(size_t dims, size_t* pRanges);

	/// Adjusts the number of dims and ranges, and sets the coordinate vector to all zeros.
	void reset(std::vector<size_t>& ranges);

	/// Advances to the next coordinate. Returns true if it successfully
	/// advances to another valid coordinate. Returns false if there are
	/// no more valid coordinates.
	bool advance();

	/// Advances by the specified number of steps. Returns false if it
	/// wraps past the end of the coordinate space. Returns true otherwise.
	bool advance(size_t steps);

	/// Advances in a manner that approximates a uniform sampling of the space, but
	/// ultimately visits every coordinate. (Behavior is not defined if dims > 31,
	/// or if the largest range is >= (1 << 31).)
	bool advanceSampling();

	/// Returns the number of dims
	size_t dims() { return m_dims; }

	/// Returns the current coordinate vector.
	size_t* current();

	/// Returns the current ranges.
	size_t* ranges() { return m_pRanges; }

	/// Computes the total number of coordinates
	size_t coordCount();

	/// Returns a coordinate vector that has been normalized so that
	/// each element falls between 0 and 1. (The coordinates are also
	/// offset slightly to sample the space without bias.)
	void currentNormalized(double* pCoords);

	/// Returns the index value of the current coordinate in raster
	/// order. (This is computed, not counted, so it will be accurate
	/// even if you jump to a random coordinate.)
	size_t currentIndex();

	/// Jump to a random coordinate in the valid range.
	void setRandom(GRand* pRand);
};


} // namespace GClasses

#endif // __GVEC_H__

