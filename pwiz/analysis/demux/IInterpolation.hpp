//
// $Id$
//
//
// Original author: Austin Keller <atkeller .@. uw.edu>
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//

#ifndef _IINTERPOLATION_HPP
#define _IINTERPOLATION_HPP

/// Interface for interpolating between points in a discrete data set.
class IInterpolation
{
public:
    virtual ~IInterpolation() {}

    /// Indicates whether the algorithm can provide an interpolated derivative.
    virtual bool IsDifferentiable() = 0;

    /// Indicates whether the algorithm can provide an interpolated integral.
    virtual bool IsIntegrable() = 0;

    /// Derivative at the point x.
    /// @param x Point at which to integrate.
    /// @return Value of interpolated derivative.
    virtual double Differentiate(double x) = 0;

    /// Definite integral between points a and b over function f
    /// @param[in] a Lower bound of the integration interval [a, b].
    /// @param[in] b Upper bound of the integration interval [a, b].
    /// @return Value of the interpolated integral.
    virtual double Integrate(double a, double b) = 0;

    /// Interpolate at point x.
    // @param[in] x Point x to interpolate at.
    // @return Interpolated value f(x).
    virtual double Interpolate(double x) = 0;
};

#endif // _IINTERPOLATION_HPP