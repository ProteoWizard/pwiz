/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#include "GSelfOrganizingMap.h"
#include "GVec.h"
#include <stdlib.h>
#include "GRand.h"
#include "GMath.h"
#include "GImage.h"
#include <ctime>

namespace GClasses {

class GSOMIterator
{
protected:
	double* m_pCoords;
	int m_nDimensions;
	int m_nWidth;
	int m_nIndex;

public:
	GSOMIterator(double* pCoords, int nDimensions, int nWidth)
	{
		m_pCoords = pCoords;
		m_nDimensions = nDimensions;
		m_nWidth = nWidth;
		GVec::setAll(m_pCoords, 0.0, nDimensions);
		m_nIndex = 0;
	}

	~GSOMIterator()
	{
	}

	int GetIndex()
	{
		return m_nIndex;
	}

	double* GetCoords()
	{
		return m_pCoords;
	}

	bool Advance()
	{
		// Move on to the next point in the lattice
		int j;
		for(j = 0; j < m_nDimensions; j++)
		{
			if(++m_pCoords[j] < m_nWidth)
				break;
			else
				m_pCoords[j] = 0;
		}
		m_nIndex++;
		if(j >= m_nDimensions)
			return false;
		else
			return true;
	}
};


GSelfOrganizingMap::GSelfOrganizingMap(int nMapDims, int nMapWidth, GRand* pRand)
: GTransform()
{
	m_pRand = pRand;
	m_nMapDims = nMapDims;
	m_nMapWidth = nMapWidth;
	m_dLearningRate = 0.12;
	m_dFocusFactor = 0.96;
}

GSelfOrganizingMap::~GSelfOrganizingMap()
{
}

  GMatrix* GSelfOrganizingMap::makeMap(GMatrix* pData, int nInOffset, 
				       bool printProgress)
{
  // Initialize the map to random values drawn from the training set
  size_t nAttrCount = pData->cols();
  size_t nMapSize = (size_t)pow((double)m_nMapWidth, (double)m_nMapDims);
  GMatrix* pMap = new GMatrix(nMapSize, pData->cols());
  double* pVec;
  for(size_t i = 0; i < nMapSize; i++)
    GVec::copy(pMap->row(i), pData->row((size_t)m_pRand->next(pData->rows())) + nInOffset, nAttrCount);
  
  // Do the training cycles
  GTEMPBUF(double, pos, 2 * m_nMapDims);
  double* bmu = &pos[m_nMapDims];
  double dev, d, dClosest;
  double* pMapVec;

  unsigned long presentations = 0; //Count the number of vector
				   //presentations made to the network
  const unsigned secondsPerPrint = 30; //For progress printing
  clock_t nextPrint = clock()+secondsPerPrint*CLOCKS_PER_SEC;
  if(printProgress){ 
    std::cerr << "Printing progress presentation:neighborhoodDev@time" 
	      << std::endl;
  }
  

  for(dev = m_nMapWidth * sqrt((double)m_nMapDims); dev >= 0.3; dev *= m_dFocusFactor)
  {
    for(size_t i = 0; i < pData->rows(); i++)
    {
      if(printProgress && clock() > nextPrint){
	nextPrint = clock()+secondsPerPrint*CLOCKS_PER_SEC;
	time_t now = time(NULL);
	tm* nowptr = std::localtime(&now);
	std::cerr << " " << presentations << ":" << dev << "@" << 
	  std::asctime(nowptr);
      }

      // Find the closest point in the map to the current vector (the
      // BMU)
      dClosest = 1e200;
      pVec = pData->row(i) + nInOffset;
      GVec::setAll(bmu, 0.0, m_nMapDims);
      GSOMIterator iter(pos, m_nMapDims, m_nMapWidth);
      do
      {
	pMapVec = pMap->row(iter.GetIndex());
	d = GVec::squaredDistance(pMapVec, pVec, nAttrCount);
	if(d < dClosest)
	{
	  dClosest = d;
	  GVec::copy(bmu, iter.GetCoords(), m_nMapDims);
	}
      } while(iter.Advance());
	  
      // Adjust every spot on the map near the BMU toward the current
      // vector
      GSOMIterator iter2(pos, m_nMapDims, m_nMapWidth);
      do
      {
	// Compute the influence that the current vector should have
	// on this map point by using a Gaussian with height 1 and
	// standard deviation "dev" centered at the BMU
	d = m_dLearningRate * exp(-0.5 * (GVec::squaredDistance(bmu, iter2.GetCoords(), m_nMapDims) / (dev * dev)));
	pMapVec = pMap->row(iter2.GetIndex());
	GVec::multiply(pMapVec, 1.0 - d, nAttrCount);
	GVec::addScaled(pMapVec, d, pVec, nAttrCount);
      } while(iter2.Advance());
      ++presentations;
    }
  }
  if(printProgress){ 
    std::cerr << std::endl << "Done" << std::endl;
  }

  return pMap;
}

GMatrix* GSelfOrganizingMap::doit(GMatrix& in)
{
	// Make the map
	GMatrix* pMap = makeMap(&in);
	Holder<GMatrix> hMap(pMap);
	
	//Pass it to the more general function
	return doit(&in, pMap);
}



// virtual
GMatrix* GSelfOrganizingMap::doit(GMatrix* pIn, GMatrix* pMap)
{
	GMatrix* pOut = new GMatrix(pIn->rows(), m_nMapDims);
	Holder<GMatrix> hOut(pOut);


	// Map the data to map coordinates
	size_t nAttrCount = pIn->cols();
	GTEMPBUF(double, pos, m_nMapDims);
	double* pMapVec;
	double* pVec;
	double* pOutVec;
	double d;
	double dClosest = 1e200;
	for(size_t i = 0; i < pIn->rows(); i++)
	{
		pVec = pIn->row(i);
		pOutVec = pOut->row(i);
		GVec::setAll(pOutVec, 0.0, m_nMapDims);
		GSOMIterator iter(pos, m_nMapDims, m_nMapWidth);
		do
		{
			pMapVec = pMap->row(iter.GetIndex());
			d = GVec::squaredDistance(pMapVec, pVec, nAttrCount);
			if(d < dClosest)
			{
				dClosest = d;
				GVec::copy(pOutVec, iter.GetCoords(), m_nMapDims);
			}
		} while(iter.Advance());
	}
	return hOut.release();
}

#ifndef NO_TEST_CODE
#include "GRand.h"
#include "GImage.h"
// static
void GSelfOrganizingMap::test()
{
	// Make a dataset of random colors
	GRand prng(0);
	GMatrix dataIn(1000, 3);
	int i;
	double* pVec;
	for(i = 0; i < 1000; i++)
	{
		pVec = dataIn[i];
		pVec[0] = prng.uniform();
		pVec[1] = prng.uniform();
		pVec[2] = prng.uniform();
	}

	// Make the map
	GSelfOrganizingMap som(2, 30, &prng);
	GMatrix* pMapData = som.makeMap(&dataIn);
	Holder<GMatrix> hData(pMapData);

	// Make an image of the map
	GImage image;
	image.setSize(30, 30);
	int x, y;
	for(y = 0; y < 30; y++)
	{
		for(x = 0; x < 30; x++)
		{
			pVec = pMapData->row(30 * y + x);
			image.setPixel(x, y, gARGB(0xff, ClipChan((int)(pVec[0] * 256)), ClipChan((int)(pVec[1] * 256)), ClipChan((int)(pVec[2] * 256))));
		}
	}
	//image.SavePNGFile("som.png");
}
#endif // !NO_TEST_CODE

} // namespace GClasses

