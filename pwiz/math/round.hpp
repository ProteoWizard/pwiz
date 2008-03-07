//
// round.hpp 
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics 
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _ROUND_HPP_
#define _ROUND_HPP_


#include <cmath>


#ifdef _MSC_VER // msvc hack
inline double round(double d) {return floor(d + 0.5);}
#endif // _MSC_VER


#endif // _ROUND_HPP_

