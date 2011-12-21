/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __G3D_H__
#define __G3D_H__

#include <math.h>
#include "GError.h"
#include <vector>

namespace GClasses {

typedef double G3DReal;
class G3DMatrix;
class GDomNode;
class GDom;
class GRand;
class GImage;
class GBBAugmented;


/// Represents a 3D vector
class G3DVector
{
public:
	G3DReal m_vals[3];

	G3DVector()
	{
	}

	/// Copies the values from pThat
	G3DVector(const G3DVector* pThat)
	{
		m_vals[0] = pThat->m_vals[0];
		m_vals[1] = pThat->m_vals[1];
		m_vals[2] = pThat->m_vals[2];
	}

	/// Initializes the values to <x,y,z>
	G3DVector(G3DReal x, G3DReal y, G3DReal z)
	{
		m_vals[0] = x;
		m_vals[1] = y;
		m_vals[2] = z;
	}

	/// Returns the vector as an array of reals
	G3DReal* vals() { return m_vals; }

	/// Marshal this object into a DOM, which can then be converted to a variety of serial formats.
	GDomNode* serialize(GDom* pDoc);

	/// Load this object from a DOM.
	void deserialize(GDomNode* pNode);

	/// Returns true iff all three elements are equal.
	bool isEqual(const G3DVector& that) const
	{
		return m_vals[0] == that.m_vals[0] && m_vals[1] == that.m_vals[1] && m_vals[2] == that.m_vals[2];
	}

	/// Makes a copy of pThat
	void copy(const G3DVector* pThat)
	{
		m_vals[0] = pThat->m_vals[0];
		m_vals[1] = pThat->m_vals[1];
		m_vals[2] = pThat->m_vals[2];
	}

	/// Sets the values of this vector
	void set(G3DReal x, G3DReal y, G3DReal z)
	{
		m_vals[0] = x;
		m_vals[1] = y;
		m_vals[2] = z;
	}

	/// Normalizes this vector
	inline void normalize()
	{
		G3DReal mag = (G3DReal)sqrt(squaredMag());
		if(mag == 0)
			ThrowError("Can't normalize a vector with zero magnitude");
		m_vals[0] /= mag;
		m_vals[1] /= mag;
		m_vals[2] /= mag;
	}

	/// Picks a random vector from a spherical distribution
	void makeRandom(GRand* pRand);

	/// returns the squared distance between this and pThat
	inline G3DReal squaredDist(const G3DVector* pThat) const
	{
		return (pThat->m_vals[0] - m_vals[0]) * (pThat->m_vals[0] - m_vals[0]) +
			(pThat->m_vals[1] - m_vals[1]) * (pThat->m_vals[1] - m_vals[1]) +
			(pThat->m_vals[2] - m_vals[2]) * (pThat->m_vals[2] - m_vals[2]);
	}

	/// returns the squared magnitude of this vector
	inline double squaredMag() const
	{
		return (m_vals[0] * m_vals[0] + m_vals[1] * m_vals[1] + m_vals[2] * m_vals[2]);
	}

	/// adds pThat to this
	inline void add(const G3DVector* pThat)
	{
		m_vals[0] += pThat->m_vals[0];
		m_vals[1] += pThat->m_vals[1];
		m_vals[2] += pThat->m_vals[2];
	}

	/// adds mag * pThat to this
	inline void add(G3DReal mag, const G3DVector* pThat)
	{
		m_vals[0] += mag * pThat->m_vals[0];
		m_vals[1] += mag * pThat->m_vals[1];
		m_vals[2] += mag * pThat->m_vals[2];
	}

	inline void add(G3DReal dx, G3DReal dy, G3DReal dz)
	{
		m_vals[0] += dx;
		m_vals[1] += dy;
		m_vals[2] += dz;
	}

	/// subtracts pThat from this
	inline void subtract(const G3DVector* pThat)
	{
		m_vals[0] -= pThat->m_vals[0];
		m_vals[1] -= pThat->m_vals[1];
		m_vals[2] -= pThat->m_vals[2];
	}

	/// multiplies this by mag
	inline void multiply(G3DReal mag)
	{
		m_vals[0] *= mag;
		m_vals[1] *= mag;
		m_vals[2] *= mag;
	}

	/// this = pMatrix * pVector
	void multiply(G3DMatrix* pMatrix, G3DVector* pVector);

	/// returns the dot product of this and that
	inline G3DReal dotProduct(const G3DVector* pThat)
	{
		return m_vals[0] * pThat->m_vals[0] +
			m_vals[1] * pThat->m_vals[1] +
			m_vals[2] * pThat->m_vals[2];
	}

	/// this = pA x pB
	inline void crossProduct(G3DVector* pA, G3DVector* pB)
	{
		m_vals[0] = pA->m_vals[1] * pB->m_vals[2] - pA->m_vals[2] * pB->m_vals[1];
		m_vals[1] = pA->m_vals[2] * pB->m_vals[0] - pA->m_vals[0] * pB->m_vals[2];
		m_vals[2] = pA->m_vals[0] * pB->m_vals[1] - pA->m_vals[1] * pB->m_vals[0];
	}

	/// Sets this to the normal vector of the triangle specified by three points
	inline void triangleNormal(const G3DVector* pPoint1, const G3DVector* pPoint2, const G3DVector* pPoint3)
	{
		G3DVector a(pPoint2);
		a.subtract(pPoint1);
		G3DVector b(pPoint3);
		b.subtract(pPoint1);
		crossProduct(&a, &b);
		normalize();
	}

	/// Computes the plane equation (ax + by + cz + d = 0) of the
	/// specified triangle. this = <a, b, c> and *pOutD = d.
	void planeEquation(const G3DVector* pPoint1, const G3DVector* pPoint2, const G3DVector* pPoint3, G3DReal* pOutD)
	{
		triangleNormal(pPoint1, pPoint2, pPoint3);
		*pOutD = -(this->dotProduct(pPoint1));
	}

	/// Sets this to the reflection of pRay on a surface with
	/// normal vector pNormal.
	void reflectionVector(G3DVector* pRay, G3DVector* pNormal);

	/// *pYaw and *pPitch are in radians
	void yawAndPitch(G3DReal* pYaw, G3DReal* pPitch) const;

	/// dYaw and dPitch are in radians
	inline void fromYawAndPitch(G3DReal dYaw, G3DReal dPitch)
	{
		m_vals[1] = (G3DReal)sin(dPitch);
		double dTmp = (G3DReal)cos(dPitch);
		m_vals[0] = (G3DReal)(dTmp * sin(dYaw));
		m_vals[2] = (G3DReal)(dTmp * cos(dYaw));
	}
};



/// Represents a 3x3 matrix
class G3DMatrix
{
public:
	G3DVector m_rows[3];

	/// serializes this matrix
	GDomNode* serialize(GDom* pDoc);

	/// deserializes this matrix
	void deserialize(GDomNode* pNode);

	/// sets this to the identity matrix
	inline void setToIdentity()
	{
		m_rows[0].set(1, 0, 0);
		m_rows[1].set(0, 1, 0);
		m_rows[2].set(0, 0, 1);
	}

	/// copies pThat
	inline void copy(G3DMatrix* pThat)
	{
		m_rows[0].copy(&pThat->m_rows[0]);
		m_rows[1].copy(&pThat->m_rows[1]);
		m_rows[2].copy(&pThat->m_rows[2]);
	}

	/// multiplies this by d
	inline void multiply(double d)
	{
		m_rows[0].multiply(d);
		m_rows[1].multiply(d);
		m_rows[2].multiply(d);
	}

	/// pVecOut = this x pVecIn
	inline void multiply(const G3DVector* pVecIn, G3DVector* pVecOut)
	{
		pVecOut->m_vals[0] = m_rows[0].dotProduct(pVecIn);
		pVecOut->m_vals[1] = m_rows[1].dotProduct(pVecIn);
		pVecOut->m_vals[2] = m_rows[2].dotProduct(pVecIn);
	}

	/// Computes the dot-product of pVec with the specified column of this matrix
	inline double dotColumn(const G3DVector* pVec, int column) const
	{
		return pVec->m_vals[0] * m_rows[0].m_vals[column] +
				pVec->m_vals[1] * m_rows[1].m_vals[column] +
				pVec->m_vals[2] * m_rows[2].m_vals[column];
	}

	/// this = pA x pB
	inline void multiply(const G3DMatrix* pA, const G3DMatrix* pB)
	{
		m_rows[0].m_vals[0] = pB->dotColumn(&pA->m_rows[0], 0);
		m_rows[0].m_vals[1] = pB->dotColumn(&pA->m_rows[0], 1);
		m_rows[0].m_vals[2] = pB->dotColumn(&pA->m_rows[0], 2);
		m_rows[1].m_vals[0] = pB->dotColumn(&pA->m_rows[1], 0);
		m_rows[1].m_vals[1] = pB->dotColumn(&pA->m_rows[1], 1);
		m_rows[1].m_vals[2] = pB->dotColumn(&pA->m_rows[1], 2);
		m_rows[2].m_vals[0] = pB->dotColumn(&pA->m_rows[2], 0);
		m_rows[2].m_vals[1] = pB->dotColumn(&pA->m_rows[2], 1);
		m_rows[2].m_vals[2] = pB->dotColumn(&pA->m_rows[2], 2);
	}

	/// Creates a matrix comprised of a random set of orthonormal basis vectors
	void makeRandom(GRand* pRand);

	/// Creates a matrix for rotating about the specified axis
	void makeAxisRotationMatrix(int axis, double radians);
};




/// This camera assumes the canvas is specified in cartesian
/// coordinates. The 3D space is based on a right-handed coordinate
/// system. (So if x goes to the right and y goes up, then z comes
/// out of the screen toward you.)
class GCamera
{
protected:
	G3DVector m_lookFromPoint;
	G3DVector m_viewSideVector; // to the right
	G3DVector m_viewUpVector;
	G3DVector m_lookDirection;
	G3DReal m_halfViewHeight; // tan(viewAngle / 2)
	int m_nWidth, m_nHeight;

public:
	/// width and height specify the size of the image that this camera will produce
	GCamera(int width, int height)
	: m_lookFromPoint(0, 0, 10),
	m_viewSideVector(1, 0, 0),
	m_viewUpVector(0, 1, 0),
	m_lookDirection(0, 0, -1),
	m_halfViewHeight(1.0),
	m_nWidth(width),
	m_nHeight(height)
	{
	}

	/// deserializing constructor
	GCamera(GDomNode* pNode);

	virtual ~GCamera()
	{
	}

	/// serializes this object
	virtual GDomNode* serialize(GDom* pDoc);

	/// Specifies the size of the 2-D image this camera will produce
	void setImageSize(int width, int height) { m_nWidth = width; m_nHeight = height; }

	/// Returns the width of the 2-D image this camera will produce
	inline int imageWidth() { return m_nWidth; }

	/// Returns the height of the 2-D image this camera will produce
	inline int imageHeight() { return m_nHeight; }

	/// Returns a reference to the location of this camera. (You can set the
	/// values in the vector this returns to move the camera.)
	G3DVector* lookFromPoint() { return &m_lookFromPoint; }

	/// Specifies the direction that the camera faces, and the roll in radians.
	/// (If rollRads is zero, then the horizon of the XZ-plane would appear as a horizontal
	/// line with positive Y above and negative Y below.)
	void setDirection(G3DVector* pDirection, G3DReal rollRads);

	/// If pUpVector is not orthogonal to pDirection, it will be changed
	/// to the nearest vector that is orthogonal to pDirection.
	void setDirection(G3DVector* pDirection, G3DVector* pUpVector);

	/// Returns the direction in which this camera is facing
	const G3DVector* lookDirection() { return &m_lookDirection; }

	/// Returns the up vector with respect to this camera
	const G3DVector* viewUpVector() { return &m_viewUpVector; }

	/// Returns the right side vector with respect to this camera
	const G3DVector* viewSideVector() { return &m_viewSideVector; }

	/// This is the vertical view angle in radians
	void setViewAngle(G3DReal val);

	/// Returns tan(viewAngle / 2)
	G3DReal halfViewHeight() { return m_halfViewHeight; }

	/// Projects the 3D point onto the canvas. The x and y position
	/// in the output vector specify the location where the point
	/// projects onto the camera's canvas (positive x goes to the
	/// right with 0 at the left side, positive y goes up with 0 at
	/// the bottom). The z position specifies the distance from the camera.
	/// If the z position is <= 0, then x and y are set to 0.
	void project(const G3DVector* pPoint, G3DVector* pOut);

	/// Computes the direction that a ray must travel from lookFromPoint()
	/// for the specified pixel coordinates (relative to the bottom-left corner
	/// of the view image). Note that the ray is not normalized.
	void computeRayDirection(int x, int y, G3DVector* pOutRay);
};

/// This is a billboard (a 2-D image in a 3-D world) for use with GBillboardWorld.
/// You can set m_repeatX and/or m_repeatY to make the image repeat across the billboard.
class GBillboard
{
public:
	/// The image associated with this billboard
	GImage* m_pImage;

	/// The number of times the image repeats horizontally on the billboard
	int m_repeatX;

	/// The number of times the image repeats vertically on the billboard
	int m_repeatY;

	/// The origin corner of the billboard (where the bottom-left corner of the image is displayed)
	G3DVector m_origin;

	/// The horizontal vector of the billboard. The magnitude of this vector specifies
	/// the width of the billboard.
	G3DVector m_x;

	/// The vertical vector of the billboard. The magnitude of this vector specifies
	/// the height of the billboard.
	G3DVector m_y;

	/// Does not take ownership of pImage.
	GBillboard(GImage* pImage);

	/// Ensures that m_y is orthogonal to m_x, and adjusts the magnitude of m_y to restore
	/// the aspect ratio of the billboard image
	void adjustHeightToRestoreAspect();
};

/// This class represents a world of billboards, and provides a rendering engine.
class GBillboardWorld
{
protected:
	std::vector<GBillboard*> m_billboards;

public:
	GBillboardWorld();
	~GBillboardWorld();

	/// Takes ownership of pBB
	void addBillboard(GBillboard* pBB);

	/// Draws all the billboards onto pImage from the perspective of the camera, and
	/// sets the values in pDepthMap to indicate the distance to each pixel.
	/// (pDepthMap should be a buffer with the same number of elements as pixels in pImage.)
	/// The pixels in pDepthMap correspond with the image left-to-right, bottom-to-top.
	void draw(GImage* pImage, double* pDepthMap, GCamera& camera);

protected:
	void drawSection(GImage* pImage, double* pDepthMap, GCamera& camera, GBBAugmented& bb, G3DVector* pBotLeft, G3DVector* pBotRight, G3DVector* pTopRight, G3DVector* pTopLeft);
};

/*
/// This is for making a 3D scene ala QTVR
class GBoxScene
{
protected:
	GImage m_image;
	float* m_pDepthMap;

public:
	GBoxScene(int nWidth)
	{
		m_image.setSize(nWidth, 6 * nWidth);
		m_pDepthMap = new float[nWidth * 6 * nWidth];
	}

	~GBoxScene()
	{
		delete[] m_pDepthMap;
	}

	GImage* GetImage() { return &m_image; }
	float* GetDepthMap() { return m_pDepthMap; }

	void RayToPoint(float* pnOutX, float* pnOutY, G3DVector* pRay)
	{
		int nFrame, u, v;
		G3DReal xx = pRay->m_vals[0] * pRay->m_vals[0];
		G3DReal yy = pRay->m_vals[1] * pRay->m_vals[1];
		G3DReal zz = pRay->m_vals[2] * pRay->m_vals[2];
		if(xx >= yy && xx >= zz)
		{
			nFrame = 0;
			u = 2;
			v = 1;
		}
		else if(yy >= zz)
		{
			u = 2;
			nFrame = 1;
			v = 0;
		}
		else
		{
			u = 0;
			v = 1;
			nFrame = 2;
		}
		int halfWidth = m_image.width() / 2;
		float t = (float)halfWidth / (float)pRay->m_vals[nFrame];
		*pnOutX = t * (float)pRay->m_vals[u] + halfWidth;
		*pnOutY = t * (float)pRay->m_vals[v] + halfWidth;
		if(pRay->m_vals[nFrame] < 0)
			nFrame += 3;
		(*pnOutY) += m_image.width() * nFrame;
	}

	/// The ray this returns is NOT normalized
	void PointToRay(G3DVector* pRay, int x, int y)
	{
		int nFrame = y / m_image.width();
		y = y % m_image.width();
		if(nFrame >= 3)
		{
			nFrame -= 3;
			pRay->m_vals[nFrame] = -1;
			x = m_image.width() - x;
			y = m_image.width() - y;
		}
		else
			pRay->m_vals[nFrame] = 1;
		int u = (nFrame == 2 ? 0 : 2);
		int v = (nFrame == 1 ? 0 : 1);
		pRay->m_vals[u] = (float)(x * 2) / m_image.width() - 1;
		pRay->m_vals[v] = (float)(y * 2) / m_image.width() - 1;
	}
};
*/

} // namespace GClasses

#endif // __G3D_H__
