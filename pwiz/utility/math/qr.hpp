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

#ifndef _QR_HPP
#define _QR_HPP

#include <boost/numeric/ublas/matrix.hpp>
#include <boost/numeric/ublas/matrix_proxy.hpp>
#include <boost/numeric/ublas/vector.hpp>
#include <boost/numeric/ublas/io.hpp>

namespace pwiz {
namespace math {


// Constructs a matrix to reflect a vector x onto ||x|| * e1.
//
// \param x vector to reflect
// \param F matrix object to construct reflector with
template<class matrix_type, class vector_type>
void Reflector(const vector_type& x, matrix_type& F)
{
    using namespace boost::numeric::ublas;

    typedef typename matrix_type::value_type value_type;

    unit_vector<value_type> e1(x.size(), 0);

    //v_k = -sgn( x(1) ) * inner_prod(x) * e1 + x;
    double x_2 = norm_2(x);
    boost::numeric::ublas::vector<value_type>
        v_k((x(0) >= 0 ? x_2 : -1 * x_2) * e1 + x);

    //v_k = v_k / norm_2(v_k);
    double norm_vk = norm_2(v_k);
    if (norm_vk != 0)
        v_k /= norm_2(v_k);
    
    // F = A(k:m,k:n) - 2 * outer_prod(v_k, v_k) * A(k:m,k:n)
    identity_matrix<value_type> eye(v_k.size());
    F = matrix_type(v_k.size(), v_k.size());
    
    F = eye - 2. * outer_prod(v_k, v_k);
}

// Returns a matrix to reflect x onto ||x|| * e1.
//
// \param x vector to reflect
// \return Householder reflector for x
template<class matrix_type, class vector_type>
matrix_type Reflector(const vector_type& x)
{
    using namespace boost::numeric::ublas;

    matrix_type F(x.size(), x.size());

    Reflector<matrix_type, vector_type>(x, F);

    return F;
}

template<class matrix_type>
void qr(const matrix_type& A, matrix_type& Q, matrix_type& R)
{
    using namespace boost::numeric::ublas;

    typedef typename matrix_type::size_type size_type;
    typedef typename matrix_type::value_type value_type;

    // TODO resize Q and R to match the needed size.
    int m=A.size1();
    int n=A.size2();

    identity_matrix<value_type> ident(m);
    if (Q.size1() != ident.size1() || Q.size2() != ident.size2())
        Q = matrix_type(m, m);
    Q.assign(ident);

    R.clear();
    R = A;

    for (size_type k=0; k< R.size1() && k<R.size2(); k++)
    {
        slice s1(k, 1, m - k);
        slice s2(k, 0, m - k);
        unit_vector<value_type> e1(m - k, 0);

        // x = A(k:m, k);
        matrix_vector_slice<matrix_type> x(R, s1, s2);
        matrix_type F(x.size(), x.size());
        
        Reflector(x, F);

        matrix_type temp = subrange(R, k, m, k, n);
        //F = prod(F, temp);
        subrange(R, k, m, k, n) = prod(F, temp);

        // <<---------------------------------------------->>
        // forming Q
        identity_matrix<value_type> iqk(A.size1());
        matrix_type Q_k(iqk);
        
        subrange(Q_k, Q_k.size1() - F.size1(), Q_k.size1(),
                 Q_k.size2() - F.size2(), Q_k.size2()) = F;

        Q = prod(Q, Q_k);
    }
}

}
}

#endif // _QR_HPP
