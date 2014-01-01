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


#include "MatchedFilter.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <complex>
#include <cstring>
#include <typeinfo>

using namespace pwiz::math;
using namespace pwiz::util;


ostream* os_ = 0;


void test_SampledData()
{
    if (os_) *os_ << "test_SampledData()\n";
    using namespace MatchedFilter;

    SampledData<DxD> sd;
    sd.domain = make_pair(0.0, 2.0);
    sd.samples.push_back(10);
    sd.samples.push_back(11);
    sd.samples.push_back(12);
    sd.samples.push_back(13);
    sd.samples.push_back(14);

    if (os_) *os_ << sd << endl; 

    if (os_) *os_ << "domainWidth: " << sd.domainWidth() << endl;
    unit_assert(sd.domainWidth() == 2.0);

    if (os_) *os_ << "dx: " << sd.dx() << endl;
    unit_assert(sd.dx() == .5); 
    
    for (unsigned int i=0; i<sd.samples.size(); i++)
    {
        if (os_) *os_ << "x(" << i << "): " << sd.x(i) << endl;
        unit_assert(sd.x(i) == i * .5);
    }

    unsigned int count = 0;
    for (double x=-.2; x<2.3; x+=.1, count++)
    {
        //if (os_) *os_ << "sampleIndex(" << x << "): " << sd.sampleIndex(x) << endl;
        unit_assert(sd.sampleIndex(x) == count/5);
        //if (os_) *os_ << "sample(" << x << "): " << sd.sample(x) << endl;
        unit_assert(sd.sample(x) == 10 + count/5);        
    }
}


// Kernel types defined as objects must define space_type in the class definition, 
// or in a specialization of KernelTraitsBase<>.
struct OneMinusAbs 
{
    typedef MatchedFilter::DxD space_type;
    double operator()(double d) const {return (d>=-1 && d<=1) ? 1 - abs(d) : 0;} 
};


// KernelTraitsBase<> has default specialization for function pointer types
complex<double> OneMinusAbsComplex(double d)
{
    return (d>=-1 && d<=1) ? 1 - abs(d) : 0;  
}


template <typename Kernel>
void test_createFilter(const Kernel& f)
{
    using namespace MatchedFilter;

    if (os_) *os_ << "test_createFilter() " << typeid(f).name() << endl;

    int sampleRadius = 4;
    double dx = .25;
    double shift = 0;

    typedef typename KernelTraits<Kernel>::filter_type filter_type;
    typedef typename KernelTraits<Kernel>::abscissa_type abscissa_type;
    typedef typename KernelTraits<Kernel>::ordinate_type ordinate_type;

    filter_type filter = details::createFilter(f, sampleRadius, dx, shift);

    if (os_)
    {
        copy(filter.begin(), filter.end(), ostream_iterator<ordinate_type>(*os_, " "));
        *os_ << endl;
    }

    unit_assert((int)filter.size() == sampleRadius*2 + 1);
    for (int i=-sampleRadius; i<=sampleRadius; ++i)
        unit_assert(filter[sampleRadius+i] == f(i*dx - shift));
    
    if (os_) *os_ << endl;
}


template <typename Kernel>
void test_createFilters(const Kernel& f)
{
    using namespace MatchedFilter;

    if (os_) *os_ << "test_createFilters() " << typeid(f).name() << endl;

    int sampleRadius = 2;
    int subsampleFactor = 4;
    double dx = 1;

    typedef typename KernelTraits<Kernel>::filter_type filter_type;
    typedef typename KernelTraits<Kernel>::ordinate_type ordinate_type;
    vector<filter_type> filters = details::createFilters(f, 
                                                         sampleRadius, 
                                                         subsampleFactor, 
                                                         dx);
    
    // verify filter count
    unit_assert((int)filters.size() == subsampleFactor);

    for (typename vector<filter_type>::const_iterator it=filters.begin(); it!=filters.end(); ++it)
    {
        if (os_)
        {
            copy(it->begin(), it->end(), ostream_iterator<ordinate_type>(*os_, " "));
            *os_ << endl;
        }

        // verify filter size
        unit_assert((int)it->size() == sampleRadius*2 + 1);
    
        // verify filter normalization
        double sum = 0;
        for (typename filter_type::const_iterator jt=it->begin(); jt!=it->end(); ++jt)
            sum += norm(complex<double>(*jt));
        unit_assert_equal(sum, 1, 1e-14);
    }

    if (os_) *os_ << endl;
}


template <typename Kernel>
void test_compute(const Kernel& f)
{
    using namespace MatchedFilter;

    if (os_) *os_ << "test_compute() " << typeid(f).name() << endl;

    typename KernelTraits<Kernel>::sampled_data_type data;
    data.domain = make_pair(0, 10);
    data.samples.resize(11);
    data.samples[5] = 1.;

    if (os_) *os_ << "data: " << data << endl;

    int sampleRadius = 2;
    int sampleFactor = 4;

    typedef typename KernelTraits<Kernel>::correlation_data_type CorrelationData;

    CorrelationData correlationData = 
        computeCorrelationData(data, f, sampleRadius, sampleFactor); 

    if (os_) *os_ << "correlationData: " << correlationData << endl;

    unit_assert(correlationData.samples.size() == 41);
    unit_assert(abs(correlationData.samples[20].dot - 1.) < 1e-12);
}


template <typename Kernel>
void test_kernel(const Kernel& kernel)
{
    if (os_) *os_ << "***************************************************************\n";
    if (os_) *os_ << "test_kernel() " << typeid(kernel).name() << endl;
    if (os_) *os_ << "***************************************************************\n";
    if (os_) *os_ << endl;

    test_createFilter(kernel);
    test_createFilters(kernel);
    test_compute(kernel);
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        if (os_) *os_ << "MatchedFilterTest\n";

        test_SampledData();
        test_kernel(OneMinusAbs()); 
        test_kernel(&OneMinusAbsComplex); 

    }
    catch (exception& e)
    {
        TEST_FAILED(e.what())
    }
    catch (...)
    {
        TEST_FAILED("Caught unknown exception.")
    }

    TEST_EPILOG
}

