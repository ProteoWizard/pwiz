//
// erf.cpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
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


#include "erf.hpp"
#include <iostream>
#include <limits>
#include <cmath>


namespace pwiz {
namespace math {


using namespace std;


// pulled from IT++ Library - dk


const double pi = 3.14159265358979323846;
const double eps = std::numeric_limits<double>::epsilon();


  /*
   * Abramowitz and Stegun: Eq. (7.1.14) gives this continued fraction
   * for erfc(z)
   *
   * erfc(z) = sqrt(pi).exp(-z^2).  1   1/2   1   3/2   2   5/2  
   *                               ---  ---  ---  ---  ---  --- ...
   *                               z +  z +  z +  z +  z +  z +
   *
   * This is evaluated using Lentz's method, as described in the
   * narative of Numerical Recipes in C.
   *
   * The continued fraction is true providing real(z) > 0. In practice
   * we like real(z) to be significantly greater than 0, say greater
   * than 0.5.
   */
  std::complex<double> cerfc_continued_fraction(const std::complex<double>& z)
  {
    const double tiny = std::numeric_limits<double>::min();

    // first calculate z+ 1/2   1 
    //                    ---  --- ...
    //                    z +  z + 
    std::complex<double> f(z);
    std::complex<double> C(f);
    std::complex<double> D(0.0);
    std::complex<double> delta;
    double a;

    a = 0.0;
    do {
      a += 0.5;
      D = z + a * D;
      C = z + a / C;
      if ((D.real() == 0.0) && (D.imag() == 0.0))
        D = tiny;
      D = 1.0 / D;
      delta = C * D;
      f = f * delta;
    } while (abs(1.0 - delta) > eps);

    // Do the first term of the continued fraction
    f = 1.0 / f;

    // and do the final scaling
	f = f * exp(-z * z) / sqrt(pi);

    return f;
  }

  std::complex<double> cerf_continued_fraction(const std::complex<double>& z)
  {
    if (z.real() > 0)
      return 1.0 - cerfc_continued_fraction(z);
    else
      return -1.0 + cerfc_continued_fraction(-z);
  }

  /*
   * Abramawitz and Stegun: Eq. (7.1.5) gives a series for erf(z) good
   * for all z, but converges faster for smallish abs(z), say abs(z) < 2.
   */
  std::complex<double> cerf_series(const std::complex<double>& z)
  {
    const double tiny = std::numeric_limits<double>::min();
    std::complex<double> sum(0.0);
    std::complex<double> term(z);
    std::complex<double> z2(z*z);

    for (int n = 0; (n < 3) || (abs(term) > abs(sum) * tiny); n++) {
      sum += term / static_cast<double>(2 * n + 1);
      term *= -z2 / static_cast<double>(n + 1);
    }

    return sum * 2.0 / sqrt(pi);
  }

  /*
   * Numerical Recipes quotes a formula due to Rybicki for evaluating
   * Dawson's Integral:
   *
   * exp(-x^2) integral exp(t^2).dt = 1/sqrt(pi) lim  sum  exp(-(z-n.h)^2) / n
   *            0 to x                           h->0 n odd
   *
   * This can be adapted to erf(z).
   */
  std::complex<double> cerf_rybicki(const std::complex<double>& z)
  {
    double h = 0.2; // numerical experiment suggests this is small enough

    // choose an even n0, and then shift z->z-n0.h and n->n-h. 
    // n0 is chosen so that real((z-n0.h)^2) is as small as possible. 
    int n0 = 2 * static_cast<int>(z.imag() / (2 * h) + 0.5);

    std::complex<double> z0(0.0, n0 * h);
    std::complex<double> zp(z - z0);
    std::complex<double> sum(0.0, 0.0);

    // limits of sum chosen so that the end sums of the sum are
    // fairly small. In this case exp(-(35.h)^2)=5e-22 
    for (int np = -35; np <= 35; np += 2) {
      std::complex<double> t(zp.real(), zp.imag() - np * h);
      std::complex<double> b(exp(t * t) / static_cast<double>(np + n0));
      sum += b; 
    }

    sum *= 2.0 * exp(-z * z) / pi;

    return std::complex<double>(-sum.imag(), sum.real());
  }

  /*
   * This function calculates a well known error function erf(z) for
   * complex z. Three methods are implemented. Which one is used
   * depends on z. 
	 */
  std::complex<double> erf(const std::complex<double>& z)
  {
    // Use the method appropriate to size of z - 
    // there probably ought to be an extra option for NaN z, or infinite z
    if (abs(z) < 2.0)
      return cerf_series(z);
    else {
      if (std::abs(z.real()) < 0.5)
        return cerf_rybicki(z);
      else
        return cerf_continued_fraction(z);
    }
  }

// end pulled from IT++ Library


// Darren's series experimentation

/*
const double precision_ = numeric_limits<double>::epsilon(); 

complex<double> erf_series(complex<double> z)
{
    // erf(z) = (2/sqrt(pi)) * sum[ (-1)^n * z^(2n+1) / n!(2n+1) ]

    complex<double> sum = 0;
    complex<double> term = z;

    const int maxTermCount = 100;  
    for (int n=0; n<maxTermCount; n++)
    {
        sum += term/double(2*n+1);
        term = -term * z*z/double(n+1);
        
        if (abs(term)<precision_*(2*n+1))
        {
            //cout << "terms: " << n << endl;
            break;
        }    

        if (n+1 == maxTermCount)
            cout << "[erf.cpp::erf_series()]  Warning: Failed to converge at z=" << z << endl;
    }

    return sum * 2. / sqrt(M_PI);
}
*/

complex<double> erf_series2(const complex<double>& z)
{
    // From "Handbook of Mathematical Functions" p297, 7.1.6
    // (seems to converge better than the first series)
    // erf(z) = (2/sqrt(pi)) * exp(-z^2) * sum[2^n * z^(2n+1) / (1*3*5*...*(2n+1))]

    complex<double> sum = 0;
    complex<double> term = z;

    const int maxTermCount = 10000; 
    for (int n=0; n<maxTermCount; n++)
    {
        sum += term;
        term = term*2.*z*z/double(2*n+3); 
        if (abs(term)<1e-12)
        {
            //cout << "terms: " << n << endl;
            break;
        }    
        
        if (n+1 == maxTermCount)
            cout << "[erf.cpp::erf_series2()]  Warning: Failed to converge at z=" << z << endl;
    }

    return sum * exp(-z*z) * 2. / sqrt(pi);
}


} // namespace math
} // namespace pwiz

