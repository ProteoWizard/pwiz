//
// erf.hpp
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


#ifndef _ERF_HPP_
#define _ERF_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include <complex>


namespace pwiz {
namespace math {


// pulled from IT++ Library
/*!
   * \brief Error function for complex argument
	 * \ingroup errorfunc
	 * \author Adam Piatyszek
   *
   * This function calculates a well known error function \c erf(z)
   * for complex \c z. The implementation is based on unofficial
   * implementation for Octave. Here is a part of the author's note
   * from original sources:
	 *
	 * Put together by John Smith john at arrows dot demon dot co dot uk, 
	 * using ideas by others.
	 *
	 * Calculate \c erf(z) for complex \c z.
	 * Three methods are implemented; which one is used depends on z.
	 *
	 * The code includes some hard coded constants that are intended to
	 * give about 14 decimal places of accuracy. This is appropriate for
	 * 64-bit floating point numbers. 
	 */


PWIZ_API_DECL std::complex<double> erf(const std::complex<double>& z);

// Darren's testing
std::complex<double> erf_series2(const std::complex<double>& z);


} // namespace math
} // namespace pwiz


#endif // _ERF_HPP_

