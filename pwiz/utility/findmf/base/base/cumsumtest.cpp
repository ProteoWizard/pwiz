//
// $Id$
//
//
// Original author: Witold Wolski <wewolski@gmail.com>
//
// Copyright : ETH Zurich
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



#include "pwiz/utility/findmf/base/base/cumsum.hpp"
#include "pwiz/utility/misc/unit.hpp"

#include <vector>
#include <algorithm>

#include "pwiz/utility/findmf/base/base/base.hpp"


namespace {

 using namespace pwiz::util;
  void testCumSum()
  {
    unsigned int resC[]={ 1 , 3 , 6 ,10 ,15 ,21 ,28 ,36 ,45 ,55};

    std::vector<unsigned int> res;
    ralab::base::base::seq(static_cast<unsigned int>(1),static_cast<unsigned int>(10),res);
    std::vector<unsigned int> res2;
    ralab::base::cumsum(res.begin(),res.end() , res2);
    std::equal(res2.begin(),res2.end(),resC);
    //test the in place version.
    ralab::base::cumsum(res.begin(),res.end());
    unit_assert(std::equal(res.begin(),res.end(),resC));
  }

 void testCumProd()
  {
    unsigned int resC[]={1,2,6,24,120,720,5040,40320,362880,3628800};

    std::vector<unsigned int> res;
    ralab::base::base::seq(static_cast<unsigned int>(1),static_cast<unsigned int>(10),res);
    std::vector<unsigned int> res2;
    ralab::base::cumprod(res,res2);
   unit_assert(std::equal(res2.begin(),res2.end(),resC));
  }

  void testCumMin()
  {
    unsigned int data[] ={ 3 ,2 ,1 ,2 ,1 ,0 ,4 ,3 ,2};
    unsigned int resC[]={3 , 2 , 1 , 1 , 1 , 0 , 0 , 0 , 0};

    std::vector<unsigned int> res;
    res.assign(data, data + 9);
    std::vector<unsigned int> res2;
    ralab::base::cummin(res,res2);
    unit_assert(std::equal(res2.begin(),res2.end(),resC));
  }

  void testCumMax()
  {
    unsigned int data[] ={ 3 ,2 ,1 ,2 ,1 ,0 ,4 ,3 ,2};
    unsigned int resC[]={ 3, 3, 3, 3, 3, 3, 4, 4, 4};
    std::vector<unsigned int> res;
    res.assign(data, data + 9);
    std::vector<unsigned int> res2;
    ralab::base::cummax(res,res2);
    unit_assert(std::equal(res2.begin(),res2.end(),resC));
  }
}//end namespace UNITTEST

int main(int argc, char **argv) {
  testCumSum();
testCumProd();
testCumMin();
testCumMax();
}





