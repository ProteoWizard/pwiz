//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
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


#ifndef _STATS_HPP_
#define _STATS_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include <boost/numeric/ublas/vector.hpp>
#include <boost/numeric/ublas/matrix.hpp>
#include <boost/numeric/ublas/io.hpp>
#include <vector>
#include <memory>


namespace pwiz {
namespace math {


class PWIZ_API_DECL Stats
{
    public:

    typedef boost::numeric::ublas::vector<double> vector_type;
    typedef boost::numeric::ublas::matrix<double> matrix_type;
    typedef std::vector<vector_type> data_type;

    Stats(const data_type& data);
    ~Stats();

    vector_type mean() const;
    matrix_type meanOuterProduct() const;
    matrix_type covariance() const;

    private:
    class Impl;
    std::auto_ptr<Impl> impl_;

    // no copying
    Stats(const Stats& stats);
    Stats& operator=(const Stats& stats);
};


} // namespace math
} // namespace pwiz


#endif // _STATS_HPP_

