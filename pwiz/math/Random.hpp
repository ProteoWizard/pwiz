//
// Random.hpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2005 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _RANDOM_HPP_
#define _RANDOM_HPP_


namespace pwiz {
namespace math {


class Random
{
    public:

    static void initialize();               // initialize the randomizer
    static double real(double a, double b); // random double in [a,b)
    static int integer(int a, int b);       // random int in [a,b)
    static double gaussian(double sd);      // random gaussian w/deviation sd
};


} // namespace math
} // namespace pwiz


#endif // _RANDOM_HPP_

