//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics 
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


#ifndef _MATCHEDFILTER_HPP_
#define _MATCHEDFILTER_HPP_


#include "pwiz/utility/math/round.hpp"
#include <iostream>
#include <vector>
#include <iterator>
#include <algorithm>
#include <complex>
#include <limits>


namespace pwiz {
namespace math {
namespace MatchedFilter {


template <typename X, typename Y>
struct ProductSpace
{
    typedef X abscissa_type;
    typedef Y ordinate_type;
};


typedef ProductSpace< double, double > DxD;
typedef ProductSpace< double, std::complex<double> > DxCD;


template <typename space_type>
struct SampledData
{
    typedef typename space_type::abscissa_type abscissa_type;
    typedef typename space_type::ordinate_type ordinate_type;
    typedef std::pair<abscissa_type,abscissa_type> domain_type;
    typedef std::vector<ordinate_type> samples_type;

    domain_type domain; 
    samples_type samples;

    abscissa_type domainWidth() const
    {
        return domain.second - domain.first;
    }

    abscissa_type dx() const 
    {   
        return samples.empty() ? 0 : domainWidth()/(samples.size()-1);
    }

    abscissa_type x(typename samples_type::size_type index) const
    {
        return domain.first + domainWidth() * index / (samples.size()-1);
    }

    typename samples_type::size_type sampleIndex(const abscissa_type& x) const
    {
        typename samples_type::size_type sampleCount = samples.size();
        int result = (int)round((sampleCount-1)*(x-domain.first)/domainWidth());
        if (result < 0) result = 0;
        if (result > (int)(sampleCount-1)) result = sampleCount-1;
        return (typename samples_type::size_type)(result);
    }

    const ordinate_type& sample(abscissa_type x) const
    {
        return samples[sampleIndex(x)];
    }
};


template <typename space_type> 
std::ostream& operator<<(std::ostream& os, const SampledData<space_type>& data)
{
    os << "[" << data.domain.first << "," << data.domain.second << "] " 
       << "(" << data.samples.size() << " samples)\n";

    typename SampledData<space_type>::samples_type::const_iterator it=data.samples.begin();
    for (unsigned int index=0; index!=data.samples.size(); ++index, ++it)
        os << data.x(index) << "\t" << *it << std::endl;

    return os;
}


template <typename Kernel>
struct KernelTraitsBase
{
    // When using a kernel function of type Kernel, 
    // KernelTraitsBase<Kernel> must define space_type, 
    // which in turn must define abscissa_type and ordinate_type, e.g:
    //   typedef ProductSpace<X,Y> space_type;

    // As a shortcut, the following default typedef allows a client to 
    // define space_type in the definition of Kernel:
    typedef typename Kernel::space_type space_type;
};


// partial specialization of KernelTraitsBase for function pointers
template <typename X, typename Y>
struct KernelTraitsBase<Y(*)(X)>
{
    typedef ProductSpace<X,Y> space_type;
};


namespace {

template <typename Kernel>
struct KernelConcept 
{
	void check()
	{
	    y = k(x);
	}

	typename KernelTraitsBase<Kernel>::space_type::abscissa_type x;
	typename KernelTraitsBase<Kernel>::space_type::ordinate_type y;
	Kernel k;
};

}

template <typename Kernel>
void checkKernelConcept()
{
    // force compile of KernelConcept::check()
    void (KernelConcept<Kernel>::*dummy)() = &KernelConcept<Kernel>::check; 
    (void)dummy;
}


template <typename Y>
struct Correlation
{
    Y dot;
    double e2;
    double tan2angle;

    Correlation(Y _dot = 0, double _e2 = 0, double _tan2angle = 0)
    :   dot(_dot), e2(_e2), tan2angle(_tan2angle)
    {}

    double angle() const {return atan(sqrt(tan2angle))*180/M_PI;}
};


template<typename Y>
std::ostream& operator<<(std::ostream& os, const Correlation<Y>& c)
{
    os << "<" << c.dot << ", " << c.e2 << ", " << c.angle() << ">";
    return os;
}


template <typename Kernel>
struct KernelTraits
{
    typedef typename KernelTraitsBase<Kernel>::space_type space_type;
    typedef typename space_type::abscissa_type abscissa_type;
    typedef typename space_type::ordinate_type ordinate_type;

    typedef SampledData<space_type> sampled_data_type;
    typedef typename sampled_data_type::samples_type samples_type;
    typedef samples_type filter_type;
    typedef Correlation<ordinate_type> correlation_type;
    typedef ProductSpace<abscissa_type, correlation_type> correlation_space_type;
    typedef SampledData<correlation_space_type> correlation_data_type;

    // verify Kernel concept at compile time
    template <void(*T)()> struct Dummy;
    typedef Dummy< &checkKernelConcept<Kernel> > dummy;
};


namespace details {


template <typename Kernel>
typename KernelTraits<Kernel>::filter_type
createFilter(const Kernel& kernel, 
             int sampleRadius,
             typename KernelTraits<Kernel>::abscissa_type dx,
             typename KernelTraits<Kernel>::abscissa_type shift)
{
    checkKernelConcept<Kernel>();

    typename KernelTraits<Kernel>::filter_type filter;
    for (int i=-sampleRadius; i<=sampleRadius; i++)
        filter.push_back(kernel(i*dx - shift));
    return filter;
}


// mimic complex<> functions
inline double norm(double d) {return d*d;}
inline double conj(double d) {return d;}

 
template <typename Filter>
void normalizeFilter(Filter& filter)
{
    double normalization = 0;
    for (typename Filter::const_iterator it=filter.begin(); it!=filter.end(); ++it)
        normalization += norm(*it);
    normalization = sqrt(normalization);

    for (typename Filter::iterator it=filter.begin(); it!=filter.end(); ++it)
        *it /= normalization;
}


template <typename Kernel>
std::vector<typename KernelTraits<Kernel>::filter_type>
createFilters(const Kernel& kernel, 
              int sampleRadius,
              int subsampleFactor,
              typename KernelTraits<Kernel>::abscissa_type dx)
{
    checkKernelConcept<Kernel>();

    typedef typename KernelTraits<Kernel>::filter_type filter_type;
    std::vector<filter_type> filters;

    for (int i=0; i<subsampleFactor; i++)
        filters.push_back(createFilter(kernel, sampleRadius, dx, dx*i/subsampleFactor));

    for_each(filters.begin(), filters.end(), normalizeFilter<filter_type>);

    return filters;
}


template<typename Kernel>
void 
computeCorrelation(typename KernelTraits<Kernel>::samples_type::const_iterator samples,
                   typename KernelTraits<Kernel>::samples_type::const_iterator samplesEnd,
                   typename KernelTraits<Kernel>::samples_type::const_iterator filter,
                   typename KernelTraits<Kernel>::correlation_type& result)
{
    checkKernelConcept<Kernel>();
    result.dot = 0; 
    double normData = 0;

    for (; samples!=samplesEnd; ++samples, ++filter)
    {
        result.dot += (*samples) * conj(*filter); 
        normData += norm(*samples);
    }

    double normDot = norm(result.dot);
    result.e2 = (std::max)(normData - normDot, 0.);
    result.tan2angle = normDot>0 ? result.e2/normDot : std::numeric_limits<double>::infinity();
}


} // namespace details


template<typename Kernel>
typename KernelTraits<Kernel>::correlation_data_type
computeCorrelationData(const typename KernelTraits<Kernel>::sampled_data_type& data, 
                       const Kernel& kernel, 
                       int sampleRadius,
                       int subsampleFactor)
{
    checkKernelConcept<Kernel>();

    typedef typename KernelTraits<Kernel>::correlation_data_type result_type;
    result_type result;

    result.domain = data.domain;
    if (data.samples.empty()) return result;
    result.samples.resize((data.samples.size()-1) * subsampleFactor + 1); 

    typedef typename KernelTraits<Kernel>::filter_type filter_type; 
    std::vector<filter_type> filters = details::createFilters(kernel, 
                                                              sampleRadius, 
                                                              subsampleFactor, 
                                                              data.dx());

    typedef typename KernelTraits<Kernel>::samples_type samples_type;

    unsigned int sampleIndex = sampleRadius;
    for (typename samples_type::const_iterator itData = data.samples.begin() + sampleRadius; 
         itData + sampleRadius != data.samples.end(); ++itData, ++sampleIndex)
    for (unsigned int filterIndex=0; filterIndex<filters.size(); ++filterIndex)
    {
        unsigned int index = sampleIndex * filters.size() + filterIndex;

        if (index >= result.samples.size()) // only when sampleRadius==0, filterIndex>0
            break;

        details::computeCorrelation<Kernel>(itData-sampleRadius, itData+sampleRadius+1,
                                            filters[filterIndex].begin(),
                                            result.samples[index]);
    }

    return result; 
}


} // namespace MatchedFilter
} // namespace math 
} // namespace pwiz


#endif // _MATCHEDFILTER_HPP_

