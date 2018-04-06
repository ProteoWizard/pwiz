//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers <a.t> vanderbilt.edu>
//
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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

#define PWIZ_SOURCE


#include "WhittakerSmoother.hpp"
#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <boost/numeric/ublas/matrix.hpp>
#include <boost/numeric/ublas/matrix_sparse.hpp>
#include "pwiz/utility/math/MatrixInverse.hpp"
#include <iostream>


namespace pwiz {
namespace analysis {


PWIZ_API_DECL
WhittakerSmoother::WhittakerSmoother(double lambdaCoefficient)
    : lambda(lambdaCoefficient)
{
    if (lambdaCoefficient < 2.0)
        throw std::runtime_error("[WhittakerSmoother::ctor()] Invalid value for lamda coefficient; valid range is [2, infinity)");
}

PWIZ_API_DECL
void WhittakerSmoother::smooth_copy(vector<double>& x, vector<double>& y)
{
    smooth(x, y, x, y);
}

PWIZ_API_DECL
void WhittakerSmoother::smooth(const std::vector<double>& x, 
                               const std::vector<double>& y,
                               std::vector<double>& xSmoothed,
                               std::vector<double>& ySmoothed)
{
    using namespace boost::numeric::ublas;
    using std::cerr;
    using std::endl;

    typedef compressed_matrix<double> Matrix;
    typedef compressed_matrix<double>::iterator1 Iterator1;
    typedef compressed_matrix<double>::iterator2 Iterator2;

    size_t m = y.size();
    identity_matrix<double> E(m);
    Matrix D(m-1, m, (m-1)*2);

    for (size_t row = 0; row < m-1; ++row)
        for (size_t col = 0; col < m; ++col)
            if (row == col)
                D(row,col) = -1;
            else if (row == col - 1)
                D(row,col) = 1;

    Matrix transD, t1, temp;
    transD.resize(m, m-1, false);
    t1.resize(m, m, false);
    temp.resize(m, m, false);
    noalias(transD) = trans(D);
    noalias(t1) = prod(transD, D);
    noalias(temp) = E + (lambda * t1);
    boost::numeric::ublas::vector<double> yv(y.size());
    std::copy(y.begin(), y.end(), yv.begin());
    bool singular;
    Matrix temp2 = gjinverse<double>(temp, singular);
    boost::numeric::ublas::vector<double> z = prod(temp2, yv);
    ySmoothed.resize(z.size());
    std::copy(z.begin(), z.end(), ySmoothed.begin());

    /*for (Iterator1 i1 = D.begin1(); i1 != D.end1(); ++i1)
    {
        for (Iterator2 i2 = i1.begin(); i2 != i1.end(); ++i2)
            cerr << "(" << i2.index1() << "," << i2.index2()
                 << ":" << *i2 << ")  ";
        cerr << endl;
    }*/
}


} // namespace analysis
} // namespace msdata
