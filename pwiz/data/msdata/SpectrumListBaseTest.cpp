//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics
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


#include "SpectrumListBase.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::msdata;
using namespace pwiz::util;


class MyBase : public SpectrumListBase
{
    public:
    virtual size_t size() const {return 0;}
    virtual const SpectrumIdentity& spectrumIdentity(size_t index) const {throw runtime_error("heh");}
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData = false) const {return SpectrumPtr();}

    // make sure we still compile -- error if setDataProcessingPtr() renamed to dataProcessingPtr()
    virtual const boost::shared_ptr<const DataProcessing> dataProcessingPtr() const {return dp_;}
};


void test()
{
    MyBase base;
    DataProcessingPtr dp(new DataProcessing("dp"));
    base.setDataProcessingPtr(dp);
    unit_assert(base.dataProcessingPtr().get() == dp.get());
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


