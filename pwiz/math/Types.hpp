//
// Types.hpp
//
//
// Robert Burke <robert.burke@cshs.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//

#ifndef TYPES_H_
#define TYPES_H_

#include "boost/numeric/ublas/vector.hpp"
#include "boost/numeric/ublas/matrix.hpp"

namespace pwiz {
namespace math {
namespace types {

typedef boost::numeric::ublas::matrix<double> dmatrix;
typedef boost::numeric::ublas::vector<double> dvector;

} // namespace types
} // namespace math
} // namespace pwiz

#endif // TYPES_H_
