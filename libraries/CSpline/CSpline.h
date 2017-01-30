/*
* Copyright (c) 2014 by James Bremner
* All rights reserved.
*
* Use license: Modified from standard BSD license.
*
* Redistribution and use in source and binary forms are permitted
* provided that the above copyright notice and this paragraph are
* duplicated in all such forms and that any documentation, advertising
* materials, Web server pages, and other materials related to such
* distribution and use acknowledge that the software was developed
* by James Bremner. The name "James Bremner" may not be used to
* endorse or promote products derived from this software without
* specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED ``AS IS'' AND WITHOUT ANY EXPRESS OR
* IMPLIED WARRANTIES, INCLUDING, WITHOUT LIMITATION, THE IMPLIED
* WARRANTIES OF MERCHANTIBILITY AND FITNESS FOR A PARTICULAR PURPOSE.
*/

#pragma once

// The following ifdef block is the standard way of creating macros which make exporting 
// from a DLL simpler. All files within this DLL are compiled with the CSPLINE_EXPORTS
// symbol defined on the command line. This symbol should not be defined on any project
// that uses this DLL. This way any other project whose source files include this file see 
// CSPLINE_API functions as being imported from a DLL, whereas this DLL sees symbols
// defined with this macro as being exported.
#ifdef CSPLINE_DYN_LINK
#ifdef CSPLINE_EXPORTS
#define CSPLINE_API __declspec(dllexport)
#else
#define CSPLINE_API __declspec(dllimport)
#endif
#else
#define CSPLINE_API
#endif

#include <vector>

// This class is exported from the CSpline.dll
/**  A 2D cubic spline : draws a set of smooth curves through points */
class CSPLINE_API cSpline
{

public:

	/**  Constructor: Calculates the spline curve coefficients

	@param[in] x  The x points
	@param[in] y  The y points

	*/
	cSpline(
		const std::vector< double >& x,
		const std::vector< double >& y);

	/** Check if input is insane

	@return true if all OK
	*/
	bool IsSane() const
	{
		return static_cast<bool>(!myError);
	}

	/// error numbers
	enum CSPLINE_API e_error
	{
		no_error,
		x_not_ascending,
		no_input,
		not_single_valued,
        not_bijective
	} myError;

	/** Check for error

	@return 0 if all OK, otherwise an error number

	*/
	e_error IsError() const
	{
		return myError;
	}

	/** Get the Y value of the spline curves for a particular X

	@param[in] x

	@return the y value

	*/
	double getY(double x);

private:

	/// The coefficients of the spline curve between two points
	struct CSPLINE_API SplineSet
	{
		double a;   // constant
		double b;   // 1st order coefficient
		double c;   // 2nd order coefficient
		double d;   // 3rd order coefficient
		double x;   // starting x value
	};

	/// The coefficients of the spline curves between all points
#pragma warning( disable : 4251 )
	std::vector< SplineSet > mySplineSet_;

	bool IsInputSane(const std::vector<double>& myX, const std::vector<double>& myY);
};

