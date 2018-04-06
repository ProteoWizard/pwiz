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


#define PWIZ_SOURCE

#include "Stats.hpp"
#include "pwiz/utility/misc/Std.hpp"


namespace pwiz {
namespace math {


class Stats::Impl
{
    public:

    Impl(const Stats::data_type& data);

    Stats::vector_type mean() const;
    Stats::matrix_type covariance() const;
    Stats::matrix_type meanOuterProduct() const;

    private:
    unsigned int D_; // dimension of the data
    int N_; // number of data points

    Stats::vector_type sumData_;
    Stats::matrix_type sumOuterProducts_;

    void computeSums(const Stats::data_type& data);
};


Stats::Impl::Impl(const Stats::data_type& data)
:   D_(0),
    N_(data.size())
{
    computeSums(data);
}


Stats::vector_type Stats::Impl::mean() const
{
    return sumData_/N_; 
}


Stats::matrix_type Stats::Impl::meanOuterProduct() const
{
    return sumOuterProducts_/N_;
}


Stats::matrix_type Stats::Impl::covariance() const
{
    Stats::vector_type m = mean(); 
    return meanOuterProduct() - outer_prod(m, m); 
}
    
    
void Stats::Impl::computeSums(const Stats::data_type& data)
{
    if (data.size()>0) D_ = data[0].size();
    sumData_ = Stats::vector_type(D_);
    sumOuterProducts_ = Stats::matrix_type(D_, D_);

    sumData_.clear();
    sumOuterProducts_.clear();

    for (Stats::data_type::const_iterator it=data.begin(); it!=data.end(); ++it)
    {
        if (it->size() != D_)
        {
            ostringstream message;
            message << "[Stats::Impl::computeSums()] " << D_ << "-dimensional data expected: " << *it; 
            throw runtime_error(message.str());
        } 

        sumData_ += *it;
        sumOuterProducts_ += outer_prod(*it, *it);
    }
}


PWIZ_API_DECL Stats::Stats(const Stats::data_type& data) : impl_(new Stats::Impl(data)) {}
PWIZ_API_DECL Stats::~Stats() {} // auto destruction of impl_
PWIZ_API_DECL Stats::vector_type Stats::mean() const {return impl_->mean();}
PWIZ_API_DECL Stats::matrix_type Stats::meanOuterProduct() const {return impl_->meanOuterProduct();}
PWIZ_API_DECL Stats::matrix_type Stats::covariance() const {return impl_->covariance();}


} // namespace math
} // namespace pwiz

