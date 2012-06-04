/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __GBEZIER_H__
#define __GBEZIER_H__

#include <math.h>
#include "GError.h"
#include "GMath.h"
#include "G3D.h"

namespace GClasses {

struct GBezierPoint;

/// Represents a Bezier curve
class GBezier
{
protected:
	int m_nControlPoints;
	struct GBezierPoint* m_pPoints;

public:
	GBezier(int nControlPoints);
	GBezier(GBezier* pThat);
	~GBezier();

#ifndef NO_TEST_CODE
	/// Performs unit tests for this class. Throws an exception if there is a failure.
	static void test();
#endif

	/// Returns the number of control points in this curve (which is always
	/// one more than the degree of the curve).
	int controlPointCount();

	/// Returns a point on the curve
	void point(double t, G3DVector* pOutPoint);

	/// Returns a control point
	void controlPoint(G3DVector* pOutPoint, double* pOutWeight, int n);

	/// Sets a control point
	void setControlPoint(int n, G3DVector* pPoint, double weight);
	GBezier* copy();

	/// Increases the degree (and number of control points) of the curve by one
	/// without changing the curve.  (Only the control points are changed.)
	void elevateDegree();

	/// Crops the curve.  If bTail is true, only the end of the curve remains.  If
	/// bTail is false, only the beginning of the curve remains.
	void segment(double t, bool bTail);

	/// Returns the tangeant to the curve at t=0
	void derivativeAtZero(G3DVector* pOutPoint);

	/// todo: this method is not reliable--there's a bug in this method somewhere
	double curvatureAtZero();

	/// Example: If you have the three equations:  x=1+2t+3t*t, y=4+5t+6t*t, z=7+8t+9t*t then you would
	///          pass in this array of points: { (1, 4, 7), (2, 5, 8), (3, 6, 9) } to get the equivalent Bezier curve
	static GBezier* fromPolynomial(G3DVector* pCoefficients, int nCoefficients);

	/// This expects you to pass in a pointer to an array of G3DVector of size m_nControlPoints
	void toPolynomial(G3DVector* pOutCoefficients);

protected:
	struct GBezierPoint* deCasteljau(double t, bool bTail);

};





struct GNurbsPoint;

/// NURBS = Non Uniform Rational B-Spline
/// Periodic = closed loop
class GNurbs
{
protected:
	int m_nControlPoints;
	int m_nDegree;
	struct GNurbsPoint* m_pPoints;
	bool m_bPeriodic;

public:
	GNurbs(int nControlPoints, int nDegree, bool periodic);
	GNurbs(GNurbs* pThat);
	~GNurbs();

	/// Returns the number of control points
	int controlPointCount() { return m_nControlPoints; }
	
	/// Returns a control point and the associated weight
	void controlPoint(G3DVector* pOutPoint, double* pOutWeight, int n);
	
	/// Set a control point location and its weight
	void setControlPoint(int n, G3DVector* pPoint, double weight);
	
	double knotInterval(int n);
	void setKnotInterval(int n, double dInterval);
	
	/// Create a Bezier curve that exactly fits a portion of this curve.
	/// todo: what are acceptable values for nInterval?
	GBezier* bezier(int nInterval);

	/// 0 <= dRatio <= 1, (for example, .5 means to insert the knot in the center of the interval)
	void insertKnotPeriodic(int nInterval, double dRatio);

protected:
	void pointCircular(struct GBezierPoint* pOutPoint, int n);
	double knotValue(int n);
	void calculateBezierControlPoint(struct GBezierPoint* pOutPoint, int nInterval, int nControlPoint);
	void newKnotPeriodic(struct GBezierPoint* pA, struct GBezierPoint* pB, int n, int nControlPoint, double dRatio);
};



} // namespace GClasses

#endif // __GBEZIER_H__
