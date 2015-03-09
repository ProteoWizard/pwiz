//
// $Id$
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

#include "HouseholderQR.hpp"
using namespace pwiz::math;

int main(int argc, char* argv[])
{
  using namespace boost::numeric::ublas;
  using namespace std;
  matrix<double> A (3,3);
  A(0,0) = 1;
  A(0,1) = 1;
  A(0,2) = 0;
  A(1,1) = 1;
  A(1,0) = 0;
  A(1,2) = 0;
  A(2,2) = 1;
  A(2,0) = 1;
  A(2,1) = 0; 
  cout << "A=" << A << endl;

  cout << "QR decomposition using Householder" << endl;
  matrix<double> Q(3,3), R(3,3);
  HouseholderQR (A,Q,R);
  matrix<double> Z = prod(Q,R) - A;
  float f = norm_1 (Z);
  cout << "Q=" << Q <<endl;
  cout << "R=" << R << endl;
  cout << "|Q*R - A|=" << f << endl;

  return 0;
}

