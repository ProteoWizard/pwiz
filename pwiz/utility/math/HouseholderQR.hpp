//
// $Id$
//
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
//
// Original author: Robert Burke <robert.burke@gmail.com>
//
// This code taken from the following site:
// http://www.crystalclearsoftware.com/cgi-bin/boost_wiki/wiki.pl?Effective_UBLAS/Matrix_Inversion
//

#ifndef HOUSEHOLDERQR_HPP
#define HOUSEHOLDERQR_HPP

#include <boost/numeric/ublas/matrix.hpp>
#include <boost/numeric/ublas/matrix_proxy.hpp>
#include <boost/numeric/ublas/vector_proxy.hpp>
#include <boost/numeric/ublas/io.hpp>
#include <boost/numeric/ublas/matrix_expression.hpp>

namespace ublas = boost::numeric::ublas;

namespace pwiz {
namespace math {

template<class T>
void TransposeMultiply (const ublas::vector<T>& vector, 
			ublas::matrix<T>& result,
			size_t size)
{
  result.resize (size,size);
  result.clear ();
  for(unsigned int row=0; row< vector.size(); ++row)
    {
      for(unsigned int col=0; col < vector.size(); ++col)
	result(row,col) = vector(col) * vector(row);

    }
}

template<class T>
void HouseholderCornerSubstraction (ublas::matrix<T>& LeftLarge, 
				    const ublas::matrix<T>& RightSmall)
{
  using namespace boost::numeric::ublas;
  using namespace std; 
  if( 
     !( 
       (LeftLarge.size1() >= RightSmall.size1())
       && (LeftLarge.size2() >= RightSmall.size2())
	) 
      )
    {
      cerr << "invalid matrix dimensions" << endl;
      return;
    }  

  size_t row_offset = LeftLarge.size2() - RightSmall.size2();
  size_t col_offset = LeftLarge.size1() - RightSmall.size1();

  for(unsigned int row = 0; row < RightSmall.size2(); ++row )
    for(unsigned int col = 0; col < RightSmall.size1(); ++col )
      LeftLarge(col_offset+col,row_offset+row) -= RightSmall(col,row);
}

template<class T>
void HouseholderQR (const ublas::matrix<T>& M, 
		    ublas::matrix<T>& Q, 
		    ublas::matrix<T>& R)
{
  using namespace boost::numeric::ublas;
  using namespace std;  

  if( 
     !( 
       (M.size1() == M.size2())
	) 
      )
    {
      cerr << "invalid matrix dimensions" << endl;
      return;
    }
  size_t size = M.size1();

  // init Matrices
  matrix<T> H, HTemp;
  HTemp = identity_matrix<T>(size);
  Q = identity_matrix<T>(size);
  R = M;

  // find Householder reflection matrices
  for(unsigned int col = 0; col < size-1; ++col)
    {
      // create X vector
      ublas::vector<T> RRowView = column(R,col);      
      vector_range< ublas::vector<T> > X2 (RRowView, range (col, size));
      ublas::vector<T> X = X2;

      // X -> U~
      if(X(0) >= 0)
	X(0) += norm_2(X);
      else
	X(0) += -1*norm_2(X);      

      HTemp.resize(X.size(),X.size(),true);

      TransposeMultiply(X, HTemp, X.size());

      // HTemp = the 2UUt part of H 
      HTemp *= ( 2 / inner_prod(X,X) );

      // H = I - 2UUt
      H = identity_matrix<T>(size);
      HouseholderCornerSubstraction(H,HTemp);

      // add H to Q and R
      Q = prod(Q,H);
      R = prod(H,R);
    }
}

}
}

#endif // HOUSEHOLDERQR_HPP

