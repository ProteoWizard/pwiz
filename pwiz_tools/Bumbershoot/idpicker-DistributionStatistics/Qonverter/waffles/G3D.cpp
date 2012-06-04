/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#include "G3D.h"
#include "GRand.h"
#include "GDom.h"
#include "GMath.h"
#include "GImage.h"
#include "GVec.h"
#include <cmath>

using std::vector;

namespace GClasses {

GDomNode* G3DVector::serialize(GDom* pDoc)
{
	GDomNode* pNode = pDoc->newObj();
	pNode->addField(pDoc, "x", pDoc->newDouble(m_vals[0]));
	pNode->addField(pDoc, "y", pDoc->newDouble(m_vals[1]));
	pNode->addField(pDoc, "z", pDoc->newDouble(m_vals[2]));
	return pNode;
}

void G3DVector::deserialize(GDomNode* pNode)
{
	m_vals[0] = pNode->field("x")->asDouble();
	m_vals[1] = pNode->field("y")->asDouble();
	m_vals[2] = pNode->field("z")->asDouble();
}

void G3DVector::reflectionVector(G3DVector* pRay, G3DVector* pNormal)
{
	copy(pNormal);
	multiply(pNormal->dotProduct(pRay) * -2);
	add(pRay);
}

void G3DVector::makeRandom(GRand* pRand)
{
	m_vals[0] = pRand->normal();
	m_vals[1] = pRand->normal();
	m_vals[2] = pRand->normal();
	normalize();
}

void G3DVector::multiply(G3DMatrix* pMatrix, G3DVector* pVector)
{
	m_vals[0] = pMatrix->m_rows[0].dotProduct(pVector);
	m_vals[1] = pMatrix->m_rows[1].dotProduct(pVector);
	m_vals[2] = pMatrix->m_rows[2].dotProduct(pVector);
}

void G3DVector::yawAndPitch(G3DReal* pYaw, G3DReal* pPitch) const
{
	*pPitch = (G3DReal)atan2(m_vals[1], sqrt(m_vals[0] * m_vals[0] + m_vals[2] * m_vals[2]));
	*pYaw = (G3DReal)atan2(m_vals[0], m_vals[2]);
	if((*pYaw) < (-M_PI / 2))
		(*pYaw) += (G3DReal)(M_PI * 2);
}

// -----------------------------------------

GDomNode* G3DMatrix::serialize(GDom* pDoc)
{
	GDomNode* pNode = pDoc->newList();
	for(int r = 0; r < 3; r++)
		for(int c = 0; c < 3; c++)
			pNode->addItem(pDoc, pDoc->newDouble(m_rows[r].m_vals[c]));
	return pNode;
}

void G3DMatrix::deserialize(GDomNode* pNode)
{
	GDomListIterator it(pNode);
	for(int r = 0; r < 3; r++)
	{
		for(int c = 0; c < 3; c++)
		{
			m_rows[r].m_vals[c] = it.current()->asDouble();
			it.advance();
		}
	}
}

void G3DMatrix::makeRandom(GRand* pRand)
{
	m_rows[0].makeRandom(pRand);
	m_rows[1].makeRandom(pRand);
	m_rows[2].crossProduct(&m_rows[0], &m_rows[1]);
	m_rows[2].normalize();
	m_rows[1].crossProduct(&m_rows[0], &m_rows[2]);
	m_rows[1].normalize();
}

void G3DMatrix::makeAxisRotationMatrix(int axis, double radians)
{
	double c = cos(radians);
	double s = sin(radians);
	if(axis == 0)
	{
		m_rows[0].set(1, 0, 0);
		m_rows[1].set(0, c, -s);
		m_rows[2].set(0, s, c);
	}
	else if(axis == 1)
	{
		m_rows[0].set(c, 0, s);
		m_rows[1].set(0, 1, 0);
		m_rows[2].set(-s, 0, c);
	}
	else if(axis == 2)
	{
		m_rows[0].set(c, -s, 0);
		m_rows[1].set(s, c, 0);
		m_rows[2].set(0, 0, 1);
	}
}

// -----------------------------------------

GCamera::GCamera(GDomNode* pNode)
{
	m_lookFromPoint.deserialize(pNode->field("from"));
	m_lookDirection.deserialize(pNode->field("dir"));
	m_viewUpVector.deserialize(pNode->field("up"));
	setDirection(&m_lookDirection, &m_viewUpVector);
	m_halfViewHeight = pNode->field("hvh")->asDouble();
	m_nWidth = (int)pNode->field("width")->asInt();
	m_nHeight = (int)pNode->field("height")->asInt();
}

// virtual
GDomNode* GCamera::serialize(GDom* pDoc)
{
	GDomNode* pNode = pDoc->newObj();
	pNode->addField(pDoc, "from", m_lookFromPoint.serialize(pDoc));
	pNode->addField(pDoc, "dir", m_lookDirection.serialize(pDoc));
	pNode->addField(pDoc, "up", m_viewUpVector.serialize(pDoc));
	pNode->addField(pDoc, "hvh", pDoc->newDouble(m_halfViewHeight));
	pNode->addField(pDoc, "width", pDoc->newInt(m_nWidth));
	pNode->addField(pDoc, "height", pDoc->newInt(m_nHeight));
	return pNode;
}

void GCamera::setDirection(G3DVector* pDirection, G3DReal rollRads)
{
	m_lookDirection = *pDirection;
	m_lookDirection.normalize();

	// Compute the up and side vector as if rollRads is zero
	m_viewUpVector.set(0, 1, 0);
	m_viewSideVector.crossProduct(pDirection, &m_viewUpVector);
	if(m_viewSideVector.squaredMag() == 0)
		m_viewSideVector.m_vals[0] = 1.0;
	m_viewSideVector.normalize();
	m_viewUpVector.crossProduct(&m_viewSideVector, &m_lookDirection);
	m_viewUpVector.normalize();

	if(rollRads != 0)
	{
		// Compute the up vector with roll
		m_viewUpVector.multiply(cos(rollRads));
		m_viewSideVector.multiply(sin(rollRads));
		m_viewUpVector.subtract(&m_viewSideVector);

		// Compute the side vector with roll
		m_viewSideVector.crossProduct(&m_lookDirection, &m_viewUpVector);
	}
}

void GCamera::setDirection(G3DVector* pDirection, G3DVector* pUpVector)
{
	m_lookDirection = *pDirection;
	m_viewUpVector = *pUpVector;

	// Compute the side vector
	m_viewSideVector.crossProduct(&m_lookDirection, &m_viewUpVector);

	// Recompute the up vector. This shouldn't have any effect, but we recompute it
	// just in case the one passed in wasn't really orthogonal to pDirection.
	m_viewUpVector.crossProduct(&m_viewSideVector, &m_lookDirection);

	// Normalize
	m_lookDirection.normalize();
	m_viewUpVector.normalize();
	m_viewSideVector.normalize();
}

void GCamera::setViewAngle(G3DReal val)
{
	m_halfViewHeight = tan(val / 2);
}

void GCamera::computeRayDirection(int x, int y, G3DVector* pOutRay)
{
	x -= m_nWidth / 2;
	y -= m_nHeight / 2;
	G3DReal hgt = m_halfViewHeight * 2;
	G3DReal wid = hgt * m_nWidth / m_nHeight;
	G3DReal u = x * wid / (G3DReal)m_nWidth;
	G3DReal v = y * hgt / (G3DReal)m_nHeight;
	pOutRay->copy(&m_lookDirection);
	pOutRay->add(u, &m_viewSideVector);
	pOutRay->add(v, &m_viewUpVector);
}

void GCamera::project(const G3DVector* pPoint, G3DVector* pOut)
{
	G3DVector t(pPoint);
	t.subtract(&m_lookFromPoint);
	G3DReal forw = t.dotProduct(&m_lookDirection);
	if(forw <= 0.0)
	{
		pOut->set(0.0, 0.0, forw);
		return;
	}
	G3DReal hgt = m_halfViewHeight * 2;
	G3DReal wid = hgt * m_nWidth / m_nHeight;
	G3DReal u = t.dotProduct(&m_viewSideVector) * (G3DReal)m_nWidth / (forw * wid);
	G3DReal v = t.dotProduct(&m_viewUpVector) * (G3DReal)m_nHeight / (forw * hgt);

	// Ensure that the x and y values are sufficiently small that they
	// can be cast to 32-bit ints, but don't mess up the ratios between
	// them. (This is necessary because it is very common to use ints
	// for image coordinates.)
	if(std::abs(u) > 1e9)
	{
		G3DReal d = 1e9 / std::abs(u);
		u *= d;
		v *= d;
	}
	if(std::abs(v) > 1e9)
	{
		G3DReal d = 1e9 / std::abs(v);
		u *= d;
		v *= d;
	}

	pOut->set(m_nWidth / 2 + u, m_nHeight / 2 + v, forw);
}











GBillboard::GBillboard(GImage* pImage)
: m_pImage(pImage), m_repeatX(1), m_repeatY(1)
{
}

void GBillboard::adjustHeightToRestoreAspect()
{
	// Subtract out any component of y that correlates with x
	m_y.add(-m_y.dotProduct(&m_x) / m_x.squaredMag(), &m_x);

	// Scale y to restore the aspect ratio of the image
	m_y.multiply(sqrt(m_x.squaredMag()) * m_pImage->height() * m_repeatY / (m_pImage->width() * m_repeatX * sqrt(m_y.squaredMag())));
}

class GBBAugmented
{
public:
	GBillboard& m_bb;
	G3DVector m_b, m_c, m_d, m_face;
	G3DVector* m_pCorners[4];
	double m_squaredMagX;
	double m_squaredMagY;
	int m_pixelsX;
	int m_pixelsY;

	GBBAugmented(GBillboard& bb)
	: m_bb(bb), m_b(&bb.m_origin), m_d(&bb.m_origin), m_squaredMagX(bb.m_x.squaredMag()), m_squaredMagY(bb.m_y.squaredMag())
	{
		m_b.add(&bb.m_x);
		m_c.copy(&m_b);
		m_c.add(&bb.m_y);
		m_d.add(&bb.m_y);
		m_pCorners[0] = &bb.m_origin;
		m_pCorners[1] = &m_b;
		m_pCorners[2] = &m_c;
		m_pCorners[3] = &m_d;
		m_face.crossProduct(&bb.m_x, &bb.m_y);

		// If this fails because m_face has zero-magnitude, then you neglected
		// to set m_x and m_y to reasonable values. (For rectangular billboards,
		// they should be orthogonal.)
		m_face.normalize();
		m_pixelsX = bb.m_pImage->width() * bb.m_repeatX;
		m_pixelsY = bb.m_pImage->height() * bb.m_repeatY;
	}

	unsigned int getPixel(GCamera& camera, int x, int y, double* pZ)
	{
		// Compute the ray vector
		G3DVector ray;
		camera.computeRayDirection(x, y, &ray);

		// Compute the intersection with the billboard plane
		double denom = m_face.dotProduct(&ray);
		if(std::abs(denom) < 1e-12)
		{
			*pZ = -1e200;
			return 0; // ray runs parallel to the plane
		}
		G3DVector t(m_pCorners[0]);
		t.subtract(camera.lookFromPoint());
		double d = m_face.dotProduct(&t) / denom;
		*pZ = d;
		if(d < 0)
			return 0; // the intersection is behind the camera
		ray.multiply(d);
		ray.add(camera.lookFromPoint());
		ray.subtract(m_pCorners[0]);

		// Compute the image coordinates
		int xx = (int)floor(ray.dotProduct(&m_bb.m_x) * m_pixelsX / m_squaredMagX + 0.5);
		int yy = (int)floor(ray.dotProduct(&m_bb.m_y) * m_pixelsY / m_squaredMagY + 0.5);

		// Get the pixel
		if(xx < 0 || yy < 0 || xx >= m_pixelsX || yy >= m_pixelsY)
			return 0;
		return m_bb.m_pImage->pixel(xx % m_bb.m_pImage->width(), m_bb.m_pImage->height() - 1 - (yy % m_bb.m_pImage->height()));
	}
};



GBillboardWorld::GBillboardWorld()
{
}

GBillboardWorld::~GBillboardWorld()
{
}

void GBillboardWorld::addBillboard(GBillboard* pBB)
{
	m_billboards.push_back(pBB);
}

void GBillboardWorld::drawSection(GImage* pImage, double* pDepthMap, GCamera& camera, GBBAugmented& bb, G3DVector* pBotLeft, G3DVector* pBotRight, G3DVector* pTopRight, G3DVector* pTopLeft)
{
	double a, b, z;
	int yBegin = std::max(0, (int)floor(std::max(pBotLeft->m_vals[1], pBotRight->m_vals[1])));
	int yEnd = std::min((int)pImage->height(), (int)floor(std::min(pTopLeft->m_vals[1], pTopRight->m_vals[1])) + (pTopLeft == pTopRight ? 1 : 0));
	for(int y = yBegin; y < yEnd; y++)
	{
		double denom = pTopLeft->m_vals[1] - pBotLeft->m_vals[1];
		if(std::abs(denom) > 1e-6)
			a = ((double)y - pBotLeft->m_vals[1]) / denom * (pTopLeft->m_vals[0] - pBotLeft->m_vals[0]) + pBotLeft->m_vals[0];
		else
			a = pTopLeft->m_vals[0];
		denom = pTopRight->m_vals[1] - pBotRight->m_vals[1];
		if(std::abs(denom) > 1e-6)
			b = ((double)y - pBotRight->m_vals[1]) / denom * (pTopRight->m_vals[0] - pBotRight->m_vals[0]) + pBotRight->m_vals[0];
		else
			b = pTopRight->m_vals[0];
		if(b < a)
			std::swap(a, b);
		int xBegin = std::max(0, (int)floor(a));
		int xEnd = std::min((int)pImage->width() - 1, (int)ceil(b));
		double* pDM = pDepthMap + pImage->width() * y + xBegin;
		unsigned int* pPix = xBegin <= xEnd ? pImage->pixelRef(xBegin, pImage->height() - 1 - y) : NULL;
		for(int x = xBegin; x <= xEnd; x++)
		{
			unsigned int col = bb.getPixel(camera, x, y, &z);
			if(z > 0.0 && z < *pDM && gAlpha(col) > 0)
			{
				*pDM = z;
				*pPix = col;
			}
//			*pPix = 0xffffffff;
			pDM++;
			pPix++;
		}
	}
}

// This code can actually handle any number of vertices that form convex polygon
// on a 2-D surface. For a billboard-engine, though, all we need are rectangles.
#define VERTEX_COUNT 4

void GBillboardWorld::draw(GImage* pImage, double* pDepthMap, GCamera& camera)
{
	G3DVector coords[VERTEX_COUNT + 1];
	int coordMap[VERTEX_COUNT + 1];
	GVec::setAll(pDepthMap, 1e200, pImage->width() * pImage->height());
	for(vector<GBillboard*>::iterator it = m_billboards.begin(); it != m_billboards.end(); it++)
	{
		// Project each of the corners onto the view
		GBBAugmented bb(**it);
		int gap = -1;
		int coordCount = 0;
		for(int i = 0; i < VERTEX_COUNT; i++)
		{
			camera.project(bb.m_pCorners[i], &coords[coordCount]);
			if(coords[coordCount].m_vals[2] > 0.0)
				coordMap[coordCount++] = i;
			else if(gap < 0)
			{
				gap = coordCount;
				coordCount = gap + 2;
			}
		}

		// Fudge the gap due to vertices that are behind the camera
		if(gap >= 0)
		{
			if(coordCount == 0)
				continue; // Nothing to draw

			// Fudge the vertex that follows the chain of valid vertices
			G3DVector tmp;
			G3DVector tmp2;
			int indexValid = coordMap[(gap + coordCount - 1) % coordCount]; // The corner just before the gap
			int indexFudge = (indexValid + 1) % VERTEX_COUNT; // The first corner that was behind the camera
			tmp.copy(bb.m_pCorners[indexValid]);
			tmp.subtract(bb.m_pCorners[indexFudge]);
			tmp2.copy(camera.lookFromPoint());
			tmp2.subtract(bb.m_pCorners[indexFudge]);
			double d = (1e-6 + tmp2.dotProduct(camera.lookDirection())) / tmp.dotProduct(camera.lookDirection());
			tmp.multiply(d);
			tmp.add(bb.m_pCorners[indexFudge]);
			camera.project(&tmp, &coords[gap]);

			// Fudge the vertex that precedes the chain of valid vertices
			indexValid = coordMap[(gap + 2) % coordCount]; // The corner just after the gap
			indexFudge = (indexValid + VERTEX_COUNT - 1) % VERTEX_COUNT; // The last corner that was behind the camera
			tmp.copy(bb.m_pCorners[indexValid]);
			tmp.subtract(bb.m_pCorners[indexFudge]);
			tmp2.copy(camera.lookFromPoint());
			tmp2.subtract(bb.m_pCorners[indexFudge]);
			d = (1e-6 + tmp2.dotProduct(camera.lookDirection())) / tmp.dotProduct(camera.lookDirection());
			tmp.multiply(d);
			tmp.add(bb.m_pCorners[indexFudge]);
			camera.project(&tmp, &coords[gap + 1]);
		}

		// Find the starting point (which is the lowest coordinate in the view)
		int a = 0;
		for(int i = 1; i < coordCount; i++)
		{
			if(coords[i].m_vals[1] < coords[a].m_vals[1])
				a = i;
		}
		int b = a;

		// Find the left and right order in which the corners will be visted (bottom to top)
		while(true)
		{
			int c = (b + 1) % coordCount;
			if(a == c)
				break;
			int d = (a + coordCount - 1) % coordCount;
			drawSection(pImage, pDepthMap, camera, bb, &coords[a], &coords[b], &coords[c], &coords[d]);
			if(coords[d].m_vals[1] < coords[c].m_vals[1])
				a = d;
			else
				b = c;
		}
	}
}

} // namespace GClasses
