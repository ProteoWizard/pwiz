/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __GFOURIER_H__
#define __GFOURIER_H__

#include "GError.h"

namespace GClasses {

class GImage;


struct ComplexNumber
{
	double real;
	double imag;

	void multiply(struct ComplexNumber* pOther)
	{
		double t = real * pOther->real - imag * pOther->imag;
		imag = real * pOther->imag + imag * pOther->real;
		real = t;
	}

	/// Returns the squared magnitude of this value
	double squaredMagnitude()
	{
		return real * real + imag * imag;
	}

	/// Performs polar interpolation. If w is close to zero, most
	/// weight will be given to a. If w is close to 1, most weight will be given to b.
	void interpolate(ComplexNumber& a, double w, ComplexNumber& b);
};


/// Fourier transform
class GFourier
{
public:
	/// This will do a Fast Forier Transform. arraySize must be a power of 2. If bForward is
	/// false, it will perform the reverse transform
	static void fft(size_t arraySize, struct ComplexNumber* pComplexNumberArray, bool bForward);

	/// 2D Fast Forier Transform.  nArrayWidth must be a power of 2. nArrayHeight must be a power of 2. If bForward
	/// is false, it will perform the reverse transform.
	static void fft2d(size_t arrayWidth, size_t arrayHeight, struct ComplexNumber* p2DComplexNumberArray, bool bForward);

	/// pArrayWidth returns the width of the array and pOneThirdHeight returns one third the height of the array
	/// (in other words, the height used by each of the three RGB channels)
	static struct ComplexNumber* imageToFftArray(GImage* pImage, int* pArrayWidth, int* pOneThirdHeight);

	/// nArrayWidth is the width of the array. nOneThirdHeight is one third the height of the array. pImage is assumed
	/// to already be allocated and set to the size of the image embedded in the array. normalize specifies whether or
	/// not to ajust the pixel values to use their full range.
	static void fftArrayToImage(struct ComplexNumber* pArray, int nArrayWidth, int nOneThirdHeight, GImage* pImage, bool normalize);

#ifndef NO_TEST_CODE
	/// Performs unit tests for this class. Throws an exception if there is a failure.
	static void test();
#endif // NO_TEST_CODE
};


} // namespace GClasses

#endif // __GFOURIER_H__
