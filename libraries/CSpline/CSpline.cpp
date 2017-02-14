// CSpline.cpp : Defines the exported functions for the DLL application.
//
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

The code in the constructor, which calculates the coefficients of the
spline curve, is by stackoverflow user cpp http://stackoverflow.com/users/2606524/cpp
as provided in the stackoverflow answer http://stackoverflow.com/a/19216702/16582
and was released under the cc-wiki licence with attirbution ( this paragraph ) required.
*/

#include "CSpline.h"

#include <algorithm>
#include <cmath>

	using namespace std;

	typedef std::vector< double > vd_t;

	cSpline::cSpline(const vd_t &x, const vd_t &y)
	{
		if (!IsInputSane(x, y))
			return;

		int n = static_cast<int>(x.size() - 1);
		vd_t a;
		a.insert(a.begin(), y.begin(), y.end());
		vd_t b(n);
		vd_t d(n);
		vd_t h;

		for (int i = 0; i < n; ++i)
			h.push_back(x[i + 1] - x[i]);

		vd_t alpha;
		for (int i = 1; i < n; ++i)
			alpha.push_back(3 * (a[i + 1] - a[i]) / h[i] - 3 * (a[i] - a[i - 1]) / h[i - 1]);

		vd_t c(n + 1);
		vd_t l(n + 1);
		vd_t mu(n + 1);
		vd_t z(n + 1);
		l[0] = 1;
		mu[0] = 0;
		z[0] = 0;

		for (int i = 1; i < n; ++i)
		{
			l[i] = 2 * (x[i + 1] - x[i - 1]) - h[i - 1] * mu[i - 1];
			mu[i] = h[i] / l[i];
			z[i] = (alpha[i - 1] - h[i - 1] * z[i - 1]) / l[i];
		}

		l[n] = 1;
		z[n] = 0;
		c[n] = 0;

		for (int j = n - 1; j >= 0; --j)
		{
			c[j] = z[j] - mu[j] * c[j + 1];
			b[j] = (a[j + 1] - a[j]) / h[j] - h[j] * (c[j + 1] + 2 * c[j]) / 3;
			d[j] = (c[j + 1] - c[j]) / 3 / h[j];
		}

		mySplineSet_.resize(n);
		for (int i = 0; i < n; ++i)
		{
			mySplineSet_[i].a = a[i];
			mySplineSet_[i].b = b[i];
			mySplineSet_[i].c = c[i];
			mySplineSet_[i].d = d[i];
			mySplineSet_[i].x = x[i];
		}
		return;
	}

	double cSpline::getY(double x)
	{
		int j;
		for (j = 0; j < (int)mySplineSet_.size(); j++)
		{
			if (mySplineSet_[j].x > x)
			{
				if (j == 0)
					j++;
				break;
			}
		}
		j--;

		double dx = x - mySplineSet_[j].x;
		double y = mySplineSet_[j].a + mySplineSet_[j].b * dx + mySplineSet_[j].c * dx* dx +
			mySplineSet_[j].d * dx* dx * dx;

		return y;

	}

	bool cSpline::IsInputSane(const std::vector<double>& myX, const std::vector<double>& myY)
	{
		if (!myX.size() || !myY.size())
		{
			myError = no_input;
			return false;
		}

        if (myX.size() != myY.size())
        {
            myError = not_bijective;
            return false;
        }

		if (!std::is_sorted(myX.begin(), myX.end()))
		{
			myError = x_not_ascending;
			return false;
		}

		bool first = true;
		double xold;
		for (double x : myX)
		{
			if (first)
			{
				xold = x;
                first = false;
				continue;
			}
			if (fabs(x - xold) < 1.0e-5)
			{
				myError = not_single_valued;
				return false;
			}
			xold = x;
		}

		myError = no_error;
		return true;
	}
