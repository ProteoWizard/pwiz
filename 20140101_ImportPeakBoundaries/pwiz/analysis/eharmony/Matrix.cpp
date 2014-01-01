//
// $Id$
//
//
// Original author: Kate Hoff <katherine.hoff@proteowizard.org>
//
// Copyright 2009 Center for Applied Molecular Medicine
//   University of Southern California, Los Angeles, CA
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

///
/// Matrix.cpp
///

#include "Matrix.hpp"
#include <iostream>

using namespace pwiz;
using namespace eharmony;

Matrix::Matrix(const int& r, const int& c)
{
  _rows = vector<vector<double> >(r, vector<double>(c));
  _columns = vector<vector<double> >(c, vector<double>(r));

  for(int row_index = 0; row_index != r; ++row_index)
      {
          for(int column_index = 0; column_index != c; ++column_index) 
	      {		 
		  _data.insert(make_pair(0,make_pair(row_index,column_index)));
		  
	      }
      }
}

void Matrix::insert(const double& value, const int& rowCoordinate, const int& columnCoordinate)
{
    double oldValue = access(rowCoordinate, columnCoordinate);
    pair<multimap<double, pair<int,int> >::iterator, multimap<double, pair<int,int> >::iterator> candidates = _data.equal_range(oldValue);
    pair<int, int> coords = make_pair(rowCoordinate, columnCoordinate);
    
    multimap<double, pair<int,int> >::iterator it = candidates.first;
    for(; it != candidates.second; ++it ) if (it->second == coords) _data.erase(it);

    _rows.at(rowCoordinate).at(columnCoordinate) = value;
    _columns.at(columnCoordinate).at(rowCoordinate) = value;
    
    _data.insert(make_pair(value, make_pair(rowCoordinate, columnCoordinate)));

}

double Matrix::access(const int& rowCoordinate, const int& columnCoordinate)
{
    return _rows.at(rowCoordinate).at(columnCoordinate);

}

ostream& Matrix::write(ostream& os) 
{
    vector<vector<double> >::const_iterator it = _rows.begin();
    for( ; it != _rows.end(); ++it)
        {
            vector<double>::const_iterator jt = it->begin();
            for( ; jt!= it->end(); ++jt)
                {
                    os << *jt << "    ";

                }

            os << endl;
            
        }

    return os;
}
