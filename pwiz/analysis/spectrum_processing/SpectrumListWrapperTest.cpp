//
// SpectrumListWrapperTest.cpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
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


#include "SpectrumListWrapper.hpp"
#include "utility/misc/unit.hpp"
#include "boost/lexical_cast.hpp"
#include "boost/lexical_cast.hpp"
#include <iostream>


using namespace pwiz::analysis;
using namespace pwiz::msdata;
using namespace pwiz::util;
using namespace std;
using boost::lexical_cast;
using boost::shared_ptr;


class MyWrapper : public SpectrumListWrapper
{
    public:

    MyWrapper(const SpectrumListPtr& inner)
    :   SpectrumListWrapper(inner)
    {}

    void verifySize(size_t size)
    {
        // verify that we can see inner_ 
        unit_assert(size == inner_->size());
    }
};


class FilterWrapper : public SpectrumListWrapper
{
    // a simple filter that returns only even indices 

    public:

    FilterWrapper(const SpectrumListPtr& inner)
    :   SpectrumListWrapper(inner)
    {}

    virtual size_t size() const {return inner_->size()/2;}
    virtual const SpectrumIdentity& spectrumIdentity(size_t index) const {return inner_->spectrumIdentity(index*2);} 
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData = false) const {return inner_->spectrum(index*2, getBinaryData);}
};


void test()
{
    SpectrumListSimplePtr simple(new SpectrumListSimple);

    const size_t spectrumCount = 10;
    for (size_t i=0; i<spectrumCount; i++)
    {
        simple->spectra.push_back(SpectrumPtr(new Spectrum));
        Spectrum& s = *simple->spectra.back();
        s.index = i;
        s.id = "S" + lexical_cast<string>(i);
        s.nativeID = lexical_cast<string>(i);
    }

    // check MyWrapper 

    shared_ptr<MyWrapper> wrapper(new MyWrapper(simple)); 

    wrapper->verifySize(10);
    unit_assert(wrapper->size() == 10);
    for (size_t i=0; i<spectrumCount; i++)
    {
        string id = "S" + lexical_cast<string>(i);
        string nativeID = lexical_cast<string>(i);

        unit_assert(wrapper->find(id) == i);
        unit_assert(wrapper->findNative(nativeID) == i);

        const SpectrumIdentity& identity = wrapper->spectrumIdentity(i);
        unit_assert(identity.id == id);
        unit_assert(identity.nativeID == nativeID);

        SpectrumPtr s = wrapper->spectrum(i);
        unit_assert(s->id == id);
        unit_assert(s->nativeID == nativeID);
    }

    // check FilterWrapper

    shared_ptr<FilterWrapper> filterWrapper(new FilterWrapper(simple)); 

    unit_assert(filterWrapper->size() == 5);

    for (size_t i=0; i<filterWrapper->size(); i++)
    {
        string id = "S" + lexical_cast<string>(i*2);
        string nativeID = lexical_cast<string>(i*2);

        unit_assert(filterWrapper->find(id) == i);
        unit_assert(filterWrapper->findNative(nativeID) == i);

        const SpectrumIdentity& identity = filterWrapper->spectrumIdentity(i);
        unit_assert(identity.id == id);
        unit_assert(identity.nativeID == nativeID);

        SpectrumPtr s = filterWrapper->spectrum(i);
        unit_assert(s->id == id);
        unit_assert(s->nativeID == nativeID);
    }
}


int main()
{
    try
    {
        test();
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
        return 1;
    }
    catch (...)
    {
        cerr << "Caught unknown exception.\n";
        return 1;
    }
}


