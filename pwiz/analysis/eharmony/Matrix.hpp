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

#ifndef _MATRIX_HPP_
#define _MATRIX_HPP_

#include <iostream>
#include <vector>
#include <map>
#include <algorithm>

using namespace std;

namespace pwiz{
namespace eharmony{

struct KeyLessThan
{
    KeyLessThan(){};
    bool operator()(pair<double, pair<int, int> > a, pair<double, pair<int, int> > b) { return a.first < b.first;}

};

struct Matrix
{
    Matrix(){}
    Matrix(const int& r, const int& c);
    Matrix(const Matrix& m) : _rows(m._rows), _columns(m._columns){}

    // TODO : Add exception for out of range
    void insert(const double& value, const int& rowCoordinate, const int& columnCoordinate);
    double access(const int& rowCoordinate, const int& columnCoordinate);    
    pair<int, int>  getMinValLocation(){ return min_element(_data.begin(), _data.end(), KeyLessThan())->second; } // if more than one location for the minimum element, returns the first one (lexically w.r.t. row/column indices

    vector<vector<double> > _rows;
    vector<vector<double> > _columns;

    multimap<double, pair<int,int> > _data;


    ostream& write(ostream& os);

};

    //ostream& operator<<(ostream& os, const Matrix& m);

} // eharmony
} // pwiz

#endif
