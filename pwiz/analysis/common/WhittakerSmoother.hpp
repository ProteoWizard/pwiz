//
// WhittakerSmoother.hpp
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


// Derived from ACS article:
// A Perfect Smoother
// Paul H. C. Eilers
// Anal. Chem., 2003, 75 (14), 3631-3636 • DOI: 10.1021/ac034173t


#ifndef _WHITTAKERSMOOTHER_HPP_ 
#define _WHITTAKERSMOOTHER_HPP_


#include <boost/numeric/ublas/matrix.hpp>
#include <boost/numeric/ublas/matrix_sparse.hpp>
#include "pwiz/utility/math/MatrixInverse.hpp"
#include <iostream>


namespace pwiz {
namespace analysis {


template <typename T>
class WhittakerSmoother
{
    public:
    static std::vector<T> smooth_copy(const std::vector<T>& data, double lambda)
    {
        typename std::vector<T> smoothedData;
        return smooth(data, smoothedData, lambda);
    }

    /// smooths to an existing vector;
    /// higher lambda (real number >0) values increase smoothness
    static std::vector<T>& smooth(const std::vector<T>& data,
                                  std::vector<T>& smoothedData,
                                  double lambda)
    {
        using namespace boost::numeric::ublas;
        using std::cerr;
        using std::endl;

        typedef typename compressed_matrix<T> Matrix;
        typedef typename compressed_matrix<T>::iterator1 Iterator1;
        typedef typename compressed_matrix<T>::iterator2 Iterator2;

        size_t m = data.size();
        identity_matrix<T> E(m);
        Matrix D(m-1, m, (m-1)*2);

        // I need a matrix type that returns lambda*
        for (size_t row = 0; row < m-1; ++row)
            for (size_t col = 0; col < m; ++col)
                if (row == col)
                    D(row,col) = -1;
                else if (row == col - 1)
                    D(row,col) = 1;

        Matrix transD, t1, temp;
        transD.resize(m, m-1);
        t1.resize(m, m);
        temp.resize(m, m);
        noalias(transD) = trans(D);
        noalias(t1) = prod(transD, D);
        noalias(temp) = E + (lambda * t1);
        boost::numeric::ublas::vector<T> y(data.size());
        std::copy(data.begin(), data.end(), y.begin());
        bool singular;
        Matrix temp2 = gjinverse<T>(temp, singular);
        boost::numeric::ublas::vector<T> z = y;//prod(temp2, y);
        smoothedData.resize(z.size());
        std::copy(z.begin(), z.end(), smoothedData.begin());

        /*for (Iterator1 i1 = D.begin1(); i1 != D.end1(); ++i1)
        {
            for (Iterator2 i2 = i1.begin(); i2 != i1.end(); ++i2)
                cerr << "(" << i2.index1() << "," << i2.index2()
                     << ":" << *i2 << ")  ";
            cerr << endl;
        }*/

        return smoothedData;
    }
};

} // namespace analysis
} // namespace pwiz

#endif // _WHITTAKERSMOOTHER_HPP_
