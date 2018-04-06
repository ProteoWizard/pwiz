///////////////////////////////////////////////////////////////////////////////
// $Id$
//
//  Copyright 2011 Vanderbilt University. Distributed under the Boost
//  Software License, Version 1.0. (See accompanying file
//  LICENSE_1_0.txt or copy at http://www.boost.org/LICENSE_1_0.txt)

#ifndef BOOST_ACCUMULATORS_STATISTICS_PERCENTILE_HPP_
#define BOOST_ACCUMULATORS_STATISTICS_PERCENTILE_HPP_

#include <boost/accumulators/framework/accumulator_base.hpp>
#include <boost/accumulators/framework/extractor.hpp>
#include <boost/accumulators/framework/depends_on.hpp>
#include <boost/accumulators/numeric/functional.hpp>
#include <boost/accumulators/framework/parameters/sample.hpp>
#include <boost/accumulators/statistics_fwd.hpp>

namespace boost { namespace accumulators
{

BOOST_PARAMETER_KEYWORD(tag, percentile_number)

namespace impl
{
    template<typename Sample>
    struct percentile_impl : accumulator_base
    {
        typedef Sample result_type;

        percentile_impl(dont_care) : isSorted(false) {}

        template<typename Args>
        void operator ()(const Args& args) 
        {
            buffer_.push_back(args[sample]);
            isSorted = false;
        }

        template<typename Args>
        result_type result(const Args& args) const
        {
            if (buffer_.empty())
                return result_type();

            if(!isSorted)
            {
                std::sort(buffer_.begin(), buffer_.end());
                isSorted = true;
            }

            size_t percentile_num = args[percentile_number];
            double percentile = percentile_num / 100.0;
            double integer, fraction = modf((buffer_.size()-1)*percentile, &integer);
            size_t index = static_cast<size_t>(integer);
            if (fraction == 0)
                return buffer_[index];
            else
                return static_cast<result_type>(buffer_[index] + fraction * (buffer_[index+1] - buffer_[index]));
        }

    private:
        mutable std::vector<Sample> buffer_;
        mutable bool isSorted;
    };
} // namespace impl

namespace tag
{
    struct percentile : depends_on<>
    {        
        typedef impl::percentile_impl<mpl::_1> impl;
    };
}

namespace extract { extractor<tag::percentile> const percentile = {}; }
using extract::percentile;

}} // namespace boost::accumulators

#endif
