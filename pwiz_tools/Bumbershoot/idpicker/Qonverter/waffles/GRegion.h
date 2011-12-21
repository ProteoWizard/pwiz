/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __GREGION_H__
#define __GREGION_H__

#include <sys/types.h>
#include <vector>

namespace GClasses {

class GImage;
class GHeap;
class GVideo;
class GRect;
struct GRegionEdge;
struct GRegion;

/// The base class for region ajacency graphs. These are useful
/// for breaking down an image into patches of similar color.
class GRegionAjacencyGraph
{
protected:
	std::vector<GRegion*> m_regions;
	std::vector<GRegionEdge*> m_neighbors;
	GHeap* m_pHeap;

public:
	GRegionAjacencyGraph();
	virtual ~GRegionAjacencyGraph();

	/// Creates a new region and returns the region number
	size_t addRegion();

	/// Returns the number of regions so far
	size_t regionCount();

	/// Returns the average pixel color in the specified region
	void averageColor(size_t nRegion, float* pRed, float* pGreen, float* pBlue);

	/// Returns true if the two specified regions are neighbors
	bool areNeighbors(size_t nRegion1, size_t nRegion2);

	/// Makes the two specified regions neighbors (if they aren't
	/// already neighbors)
	void makeNeighbors(size_t nRegion1, size_t nRegion2);

	/// Returns the number of ajacencies
	size_t ajacencyCount();

	/// Returns the two regions that are ajacent
	void ajacency(size_t nEdge, size_t* pRegion1, size_t* pRegion2);
};

/// Implements a region adjacency graph for 2D images, and lets
/// you merge similar regions to create a hierarchical breakdown
/// of the image.
class G2DRegionGraph : public GRegionAjacencyGraph
{
protected:
	GImage* m_pRegionMask;

public:
	G2DRegionGraph(int nWidth, int nHeight);
	virtual ~G2DRegionGraph();

	/// Toboggans the gradient magnitude image of the provided image to produce a
	/// list of watershed regions
	void makeWatershedRegions(const GImage* pImage);

	/// Given a G2DRegionGraph, this merges every region with its closest neighbor to
	/// form a coarser G2DRegionGraph
	void makeCoarserRegions(G2DRegionGraph* pFineRegions);

	/// Gets a pointer to the region mask image
	GImage* regionMask() { return m_pRegionMask; }

	/// Specifies which region the given pixel belongs to. The color
	/// of the pixel is also specified so it can keep track of the
	/// average color of each region
	void setMaskPixel(int x, int y, unsigned int c, size_t nRegion);
};
/*
/// A 3D region adjacency graph
class G3DRegionGraph : public GRegionAjacencyGraph
{
protected:
	GVideo* m_pRegionMask;

public:
	G3DRegionGraph(int nWidth, int nHeight);
	virtual ~G3DRegionGraph();

	/// Toboggans the gradient magnitude image of the provided image to produce a
	/// list of watershed regions
	void makeWatershedRegions(GVideo* pVideo);

	/// Given a G3DRegionGraph, this merges every region with its closest neighbor to
	/// form a coarser G3DRegionGraph
	void makeCoarserRegions(G3DRegionGraph* pFineRegions);

	/// Gets a pointer to the region mask image
	GVideo* regionMask() { return m_pRegionMask; }

	/// Specifies which region the given pixel belongs to. The color
	/// of the pixel is also specified so it can keep track of the
	/// average color of each region. (z is the frame number)
	void setMaskPixel(int x, int y, int z, unsigned int c, int nRegion);
};
*/

/// Iterates the border of a 2D region by running around the border and reporting
/// the coordinates of each interior border pixel and the direction to the
/// edge. It goes in a counter-clockwise direction.
class GRegionBorderIterator
{
protected:
	GImage* m_pImage;
	unsigned int m_nRegion;
	int m_x, m_y, m_endX, m_endY;
	int m_direction;
	bool m_bOddPass;

public:
	/// The point (nSampleX, nSampleY) should be somewhere in the region
	/// The image pImage should be a region mask, such that all points
	/// in the same region have exactly the same pixel value.
	GRegionBorderIterator(GImage* pImage, int nSampleX, int nSampleY);
	~GRegionBorderIterator();

	/// If it returns false, the current values are invalid and it's done.
	/// If it returns true, pX and pY will hold the coordinates
	/// of an interior border pixel. pDirection will be the direction to
	/// the edge. 0=right, 1=up, 2=left, 3=down.
	bool next(int* pX, int* pY, int* pDirection);

protected:
	bool look();
	void leap();
};


/// Iterates over all the pixels in an image that have
/// the same color and are transitively adjacent. In other
/// words, if you were to flood-fill a the specified point,
/// this returns all the pixels that would be changed.
class GRegionAreaIterator
{
protected:
	unsigned int m_nRegion;
	int m_left, m_right, m_top, m_bottom, m_x, m_y;
	GImage* m_pImage;

public:
	/// The point (nSampleX, nSampleY) should be somewhere in the region
	/// The image pImage should be a region mask, such that all points
	/// in the same region have exactly the same pixel value.
	GRegionAreaIterator(GImage* pImage, int nSampleX, int nSampleY);
	~GRegionAreaIterator();

	/// If it returns false, the current values are invalid and it's done.
	/// If it returns true, pX and pY will hold the coordinates
	/// of a pixel in the region
	bool next(int* pX, int* pY);
};



/// This class uses Fourier phase correlation to efficiently find
/// sub-images within a larger image
class GSubImageFinder
{
protected:
	int m_nHaystackWidth, m_nHaystackHeight, m_nHaystackX, m_nHaystackY;
	struct ComplexNumber* m_pHaystackRed;
	struct ComplexNumber* m_pHaystackGreen;
	struct ComplexNumber* m_pHaystackBlue;
	struct ComplexNumber* m_pNeedleRed;
	struct ComplexNumber* m_pNeedleGreen;
	struct ComplexNumber* m_pNeedleBlue;
	struct ComplexNumber* m_pCorRed;
	struct ComplexNumber* m_pCorGreen;
	struct ComplexNumber* m_pCorBlue;

public:
	/// pHaystack is the image that will be searched for
	/// sub-images. Its dimensions do not need to be powers of 2.
	GSubImageFinder(GImage* pHaystack);
	~GSubImageFinder();

#ifndef NO_TEST_CODE
	/// Performs unit tests for this class. Throws an exception if there is a failure.
	static void test();
#endif // NO_TEST_CODE

	/// The width and height of pNeedleRect must be powers of 2 and less than
	/// the dimensions of pHaystack. pNeedleRect specifies the portion of pNeedle
	/// to search for. pHaystackRect allows you to restrict the range. Note that
	/// restricting the range does not really improve performance. Also note that
	/// it's okay for pHaystackRect to range outside of the bounds of pImage.
	void findSubImage(int* pOutX, int* pOutY, GImage* pNeedle, GRect* pNeedleRect, GRect* pHaystackRect);
};

/// This class uses heuristics to find sub-images within a larger image.
/// It is slower, but more stable than GSubImageFinder.
class GSubImageFinder2
{
protected:
	GImage* m_pHaystack;

public:
	/// pHaystack is the image in which to search. It can have any dimensions.
	GSubImageFinder2(GImage* pHaystack);
	~GSubImageFinder2();

#ifndef NO_TEST_CODE
	/// Performs unit tests for this class. Throws an exception if there is a failure.
	static void test();
#endif // NO_TEST_CODE

	/// Finds the best x and y positions of pNeedle within pHaystack. It is assumed that the
	/// needle fits entirely within the haystack. There are no restrictions on image sizes.
	void findSubImage(int* pOutX, int* pOutY, GImage* pNeedle, GRect* pNeedleRect);
};



} // namespace GClasses

#endif // __GREGION_H__
