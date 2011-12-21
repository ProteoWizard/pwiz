/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __SELFORGANIZINGMAP_H__
#define __SELFORGANIZINGMAP_H__

#include "GTransform.h"

namespace GClasses {

class GMatrix;
class GRand;

/// An implementation of a Kohonen map
class GSelfOrganizingMap : public GTransform
{
protected:
	int m_nMapDims;
	int m_nMapWidth;
	double m_dLearningRate;
	double m_dFocusFactor;
	GRand* m_pRand;

public:
	/// nMapDims specifies the number of dimensions for the map.
	/// nMapWidth specifies the size in one dimension of the map.
	/// (so if nMapDims is 3 and nMapWidth is 10, the map will contain 1000 (10^3) nodes.)
	GSelfOrganizingMap(int nMapDims, int nMapWidth, GRand* pRand);
	virtual ~GSelfOrganizingMap();

#ifndef NO_TEST_CODE
	/// Performs unit tests for this class. Throws an exception if there is a failure.
	static void test();
#endif

        ///DO NOT USE, this is here temporarily to make some other
        ///code work and will be removed
        ///
        ///Set the learning rate
        void learningRate(double newRate){ m_dLearningRate = newRate; }

        ///DO NOT USE, this is here temporarily to make some other
        ///code work and will be removed
        ///
        ///Set the focus factor (neighborhood std dev is multiplied by
        ///this every epoch) 
        void focusFactor(double newFactor){ m_dFocusFactor = newFactor; }

	/// Transforms pIn
	virtual GMatrix* doit(GMatrix& in);

	/// Transforms pIn using the given map
        virtual GMatrix* doit(GMatrix* pIn, GMatrix* map);

	/// Makes the map.  If printProgress is true, prints status to
	/// stderr every pass through the dataset.  Note that this
	/// option is experimental and may be removed in the future.
        GMatrix* makeMap(GMatrix* pData, int nInOffset = 0, 
			 bool printProgress = false);
};

} // namespace GClasses

#endif // __SELFORGANIZINGMAP_H__
