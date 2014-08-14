//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
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
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::cv;
using namespace pwiz::msdata;
using namespace pwiz::util;


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

    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData = false) const {return inner_->spectrum(index, getBinaryData);}
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
        s.id = "scan=" + lexical_cast<string>(i);
    }

    // check MyWrapper 

    shared_ptr<MyWrapper> wrapper(new MyWrapper(simple)); 

    wrapper->verifySize(10);
    unit_assert(wrapper->size() == 10);
    for (size_t i=0; i<spectrumCount; i++)
    {
        string id = "scan=" + lexical_cast<string>(i);

        unit_assert(wrapper->find(id) == i);
        IndexList indexList = wrapper->findNameValue("scan", lexical_cast<string>(i));
        unit_assert(indexList.size()==1 && indexList[0]==i);

        const SpectrumIdentity& identity = wrapper->spectrumIdentity(i);
        unit_assert(identity.id == id);

        SpectrumPtr s = wrapper->spectrum(i);
        unit_assert(s->id == id);
    }

    // check FilterWrapper

    shared_ptr<FilterWrapper> filterWrapper(new FilterWrapper(simple)); 

    unit_assert(filterWrapper->size() == 5);

    for (size_t i=0; i<filterWrapper->size(); i++)
    {
        string id = "scan=" + lexical_cast<string>(i*2);
        string scanNumber = lexical_cast<string>(i*2);

        unit_assert(filterWrapper->find(id) == i);
        IndexList indexList = filterWrapper->findNameValue("scan", scanNumber);
        unit_assert(indexList.size()==1 && indexList[0]==i);

        const SpectrumIdentity& identity = filterWrapper->spectrumIdentity(i);
        unit_assert(identity.id == id);

        SpectrumPtr s = filterWrapper->spectrum(i);
        unit_assert(s->id == id);
    }
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        test();
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


