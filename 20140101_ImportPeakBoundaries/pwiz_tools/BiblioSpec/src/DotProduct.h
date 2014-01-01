//
// $Id$
//
//
// Original author: Barbara Frewen <frewen@u.washington.edu>
//
// Copyright 2012 University of Washington - Seattle, WA 98195
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

//header file for DotProduct

#ifndef DOTPRODUCT_H
#define DOTPRODUCT_H

#include <vector>
#include <cmath>
#include "Match.h"

#ifdef _MSC_VER
#include <float.h>
#define isnan _isnan
#endif


namespace BiblioSpec {

class DotProduct
{
 private:
  void init();
  static void getAngle(const vector<PEAK_T>& exp, 
                       const vector<PEAK_T>& ref,
                       Match& match);

 public: 
  DotProduct();
  ~DotProduct();
  static void compare(Match& match); 
};

} // namespace

#endif //DOTPRODUCT_H

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
