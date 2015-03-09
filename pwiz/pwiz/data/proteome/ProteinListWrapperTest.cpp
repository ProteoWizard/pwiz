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


#include "ProteinListWrapper.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::proteome;
using namespace pwiz::util;


class MyWrapper : public ProteinListWrapper
{
    public:

    MyWrapper(const ProteinListPtr& inner)
    :   ProteinListWrapper(inner)
    {}

    void verifySize(size_t size)
    {
        // verify that we can see inner_ 
        unit_assert(size == inner_->size());
    }
};


class FilterWrapper : public ProteinListWrapper
{
    // a simple filter that returns only even indices 

    public:

    FilterWrapper(const ProteinListPtr& inner)
    :   ProteinListWrapper(inner)
    {}

    virtual size_t size() const {return inner_->size()/2;}
    virtual ProteinPtr protein(size_t index, bool getSequence = true) const {return inner_->protein(index*2, getSequence);}
};


void test()
{
    typedef shared_ptr<ProteinListSimple> ProteinListSimplePtr;
    ProteinListSimplePtr simple(new ProteinListSimple);

    const size_t proteinCount = 10;
    for (size_t i=0; i<proteinCount; i++)
        simple->proteins.push_back(ProteinPtr(new Protein("PWIZ:" + lexical_cast<string>(i), i, "", "")));

    // check MyWrapper 

    shared_ptr<MyWrapper> wrapper(new MyWrapper(simple)); 

    wrapper->verifySize(10);
    unit_assert(wrapper->size() == 10);
    for (size_t i=0; i<proteinCount; i++)
    {
        string id = "PWIZ:" + lexical_cast<string>(i);

        unit_assert(wrapper->find(id) == i);

        ProteinPtr s = wrapper->protein(i);
        unit_assert(s->id == id);
    }

    // check FilterWrapper

    shared_ptr<FilterWrapper> filterWrapper(new FilterWrapper(simple)); 

    unit_assert(filterWrapper->size() == 5);

    for (size_t i=0; i<filterWrapper->size(); i++)
    {
        string id = "PWIZ:" + lexical_cast<string>(i*2);

        unit_assert(filterWrapper->find(id) == i);

        ProteinPtr s = filterWrapper->protein(i);
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
