//
// $Id$
//
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics
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


#include "Random.hpp"
#include "mt19937ar.h" // Mersenne Twister
#include <cmath>
#include <ctime>


namespace pwiz {
namespace math {


using namespace std;


void Random::initialize()
{
    init_genrand((unsigned long)time(0));
}


double Random::real(double a, double b)
{
    return a + (b-a)*genrand_real2();
}


int Random::integer(int a, int b)
{
    return (int)real(a,b);
}


double Random::gaussian(double sd)
{
   double x1 = genrand_real1();
   double x2 = genrand_real1();
   double q1 = sd*sqrt(-2*log(x1));
   double q2 = 2*M_PI*x2;
   return q1*cos(q2);
}


} // namespace math
} // namespace pwiz

