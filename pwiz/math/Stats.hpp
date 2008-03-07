//
// Stats.hpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _STATS_HPP_
#define _STATS_HPP_


#include <boost/numeric/ublas/vector.hpp>
#include <boost/numeric/ublas/matrix.hpp>
#include <boost/numeric/ublas/io.hpp>
#include <vector>
#include <memory>


namespace pwiz {
namespace math {


class Stats
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

