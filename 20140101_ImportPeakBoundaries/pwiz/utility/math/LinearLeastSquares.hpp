//
// $Id$
// 
// Original author: Robert Burke <robert.burke@cshs.org>
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

#ifndef _LINEARLEASTSQUARES_HPP_
#define _LINEARLEASTSQUARES_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include <iostream>
#include "LinearSolver.hpp"
#include "Types.hpp"

namespace pwiz {
namespace math {

enum PWIZ_API_DECL LinearLeastSquaresType {LinearLeastSquaresType_LU, LinearLeastSquaresType_QR};

template <LinearLeastSquaresType lls_type = LinearLeastSquaresType_LU>
class LinearLeastSquares;

template<>
class LinearLeastSquares<LinearLeastSquaresType_LU>
{
public:
    template<typename T>
    boost::numeric::ublas::vector<T> solve(const boost::numeric::ublas::matrix<T>& A,
                           const boost::numeric::ublas::vector<T>& y)
    {
        boost::numeric::ublas::permutation_matrix<std::size_t> m(A.size1());
        boost::numeric::ublas::matrix<T> AtA = prod(trans(A), A);
        boost::numeric::ublas::vector<T> b = y;
        boost::numeric::ublas::vector<T> r;

        // This serves as a sanity check. Note that an exception here
        // probably indicates a data file error.
        if (boost::numeric::ublas::lu_factorize(AtA, m) == 0.)
        {
            r = prod(trans(A), b);

            boost::numeric::ublas::lu_substitute(AtA, m, r);
        }

        return r;
    }
};

template<>
class LinearLeastSquares<LinearLeastSquaresType_QR>
{
public:

    template<typename T>
    boost::numeric::ublas::vector<T> solve(
        const boost::numeric::ublas::matrix<T>& A,
        const boost::numeric::ublas::vector<T>& x)
    {
        LinearSolver<LinearSolverType_QR> solver;

        boost::numeric::ublas::vector<T> y = solver.solve(A, x);

        return y;
    }
};

}
}

#endif // _LINEARLEASTSQUARES_HPP_
