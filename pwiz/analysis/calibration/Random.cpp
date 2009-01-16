//
// Random.cpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2005 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
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

