//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics 
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


#ifndef _LINEARSOLVER_HPP_
#define _LINEARSOLVER_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include <boost/numeric/ublas/vector.hpp>
#include <boost/numeric/ublas/matrix.hpp>
#include <boost/numeric/ublas/lu.hpp>
#include <boost/numeric/ublas/triangular.hpp>
#include <boost/numeric/ublas/vector_proxy.hpp>
#include <boost/numeric/ublas/io.hpp>
#include <stdexcept>

#include <iostream>
#include <iomanip>
#include <math.h>

#include "qr.hpp"

namespace pwiz {
namespace math {


enum PWIZ_API_DECL LinearSolverType {LinearSolverType_LU, LinearSolverType_QR};


template <LinearSolverType solver_type = LinearSolverType_LU>
class LinearSolver;


template<>
class LinearSolver<LinearSolverType_LU>
{
public:

    /// solve system of linear equations Ax = y using boost::ublas;
    /// note: extra copying inefficiencies for ease of client use 
    template<typename matrix_type, typename vector_type>
    vector_type solve(const matrix_type& A, 
                      const vector_type& y)
    {
        namespace ublas = boost::numeric::ublas;

        matrix_type A_factorized = A;
        ublas::permutation_matrix<size_t> pm(y.size());

        int singular = lu_factorize(A_factorized, pm);
        if (singular) throw std::runtime_error("[LinearSolver<LU>::solve()] A is singular.");

        vector_type result(y);
        lu_substitute(A_factorized, pm, result);

        return result;
    }
}; 


template<>
class LinearSolver<LinearSolverType_QR>
{
public:

    /// solve system of linear equations Ax = y using boost::ublas;
    /// note: extra copying inefficiencies for ease of client use 
    template<typename matrix_type, typename vector_type>
    vector_type solve(const matrix_type& A, const vector_type& y)
    {
        typedef typename matrix_type::size_type size_type;
        typedef typename matrix_type::value_type value_type;
        
        namespace ublas = boost::numeric::ublas;

        matrix_type Q(A.size1(), A.size2()), R(A.size1(), A.size2());

        qr (A, Q, R);

        vector_type b = prod(trans(Q), y);

        vector_type result;
        if (R.size1() > R.size2())
        {
            size_type min = (R.size1() < R.size2() ? R.size1() : R.size2());

            result = ublas::solve(subrange(R, 0, min, 0, min),
                                  subrange(b, 0, min),
                                  ublas::upper_tag());
        }
        else
        {
            result = ublas::solve(R, b, ublas::upper_tag());
        }
        return result;
    }
}; 

} // namespace math 
} // namespace pwiz


#endif // _LINEARSOLVER_HPP_ 

