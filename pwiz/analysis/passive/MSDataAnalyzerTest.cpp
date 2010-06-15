//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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


#include "MSDataAnalyzer.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <cstring>


using namespace pwiz::util;
using namespace pwiz::analysis;


ostream* os_ = 0;


struct SimpleAnalyzer : public MSDataAnalyzer
{
    size_t index;
    bool opened;
    int updateCount;
    bool closed;

    SimpleAnalyzer(size_t _index) : index(_index), opened(false), updateCount(0), closed(false) {}

    virtual void open(const DataInfo& dataInfo) 
    {
        // initialize everything, since Analyzers may be reused
        opened = false;
        updateCount = 0; 
        closed=false;

        // do something   
        opened = true; 
    }

    virtual UpdateRequest updateRequested(const DataInfo& dataInfo, 
                                          const SpectrumIdentity& entry) const 
    {
        // only request this->index
        return entry.index == index ? UpdateRequest_NoBinary : UpdateRequest_None;
    }

    virtual void update(const DataInfo& dataInfo, 
                        const Spectrum& spectrum) 
    {
        if (os_) *os_ << "[" << index << "]" << " update: " << spectrum.index << endl;
        updateCount++;
    }

    virtual void close(const DataInfo& dataInfo) {closed = true;}
};


struct SimpleProgressCallback : public MSDataAnalyzerDriver::ProgressCallback
{
    size_t count;

    SimpleProgressCallback() : count(0) {}

    virtual size_t iterationsPerCallback() const {return 5;}

    virtual MSDataAnalyzerDriver::Status progress(size_t index, size_t size)
    {
        if (os_) *os_ << "progress: " << index << "/" << size << endl;
        count++;
        return MSDataAnalyzerDriver::Status_Ok;
    }
};


struct CancelProgressCallback : public MSDataAnalyzerDriver::ProgressCallback
{
    size_t count;

    CancelProgressCallback() : count(0) {}

    virtual size_t iterationsPerCallback() const {return 5;}

    virtual MSDataAnalyzerDriver::Status progress(size_t index, size_t size)
    {
        if (os_) *os_ << "progress: " << index << "/" << size << endl;
        count++;
        return index<5 ? MSDataAnalyzerDriver::Status_Ok : MSDataAnalyzerDriver::Status_Cancel;
    }
};


void test()
{
    if (os_) *os_ << "test()\n"; 

    // set up analyzers

    MSDataAnalyzerContainer analyzers;
    analyzers.push_back(MSDataAnalyzerPtr(new SimpleAnalyzer(23))); // request index 23
    analyzers.push_back(MSDataAnalyzerPtr(new SimpleAnalyzer(17))); // request index 17

    unit_assert(analyzers.size() == 2);
    for (MSDataAnalyzerContainer::const_iterator it=analyzers.begin(); it!=analyzers.end(); ++it)
    {
        const SimpleAnalyzer& anal = dynamic_cast<const SimpleAnalyzer&>(**it);
        unit_assert(!anal.opened);
        unit_assert(anal.updateCount == 0);
        unit_assert(!anal.closed);
    }

    // instantiate MSData object

    MSData dummy;
    SpectrumListSimplePtr sl(new SpectrumListSimple);
    const int spectrumCount = 30;
    for (int i=0; i<spectrumCount; i++) 
    {
        sl->spectra.push_back(SpectrumPtr(new Spectrum));
        sl->spectra.back()->index = i;
    }
    dummy.run.spectrumListPtr = sl; 

    // run driver

    MSDataAnalyzerDriver driver(analyzers);
    SimpleProgressCallback callback;
    MSDataAnalyzerDriver::Status status = driver.analyze(dummy, &callback);

    unit_assert(status == MSDataAnalyzerDriver::Status_Ok);

    for (MSDataAnalyzerContainer::const_iterator it=analyzers.begin(); it!=analyzers.end(); ++it)
    {
        const SimpleAnalyzer& anal = dynamic_cast<const SimpleAnalyzer&>(**it);
        unit_assert(anal.opened);
        unit_assert(anal.updateCount == 1);
        unit_assert(anal.closed);
    }

    unit_assert(callback.count == spectrumCount/callback.iterationsPerCallback() + 1);

    // run driver again with cancel callback

    if (os_) *os_ << "testing cancel callback:\n";

    CancelProgressCallback cancelCallback;
    status = driver.analyze(dummy, &cancelCallback);

    unit_assert(status == MSDataAnalyzerDriver::Status_Cancel);

    if (os_) *os_ << "cancelled!\n";

    for (MSDataAnalyzerContainer::const_iterator it=analyzers.begin(); it!=analyzers.end(); ++it)
    {
        const SimpleAnalyzer& anal = dynamic_cast<const SimpleAnalyzer&>(**it);
        unit_assert(anal.opened);
        unit_assert(anal.updateCount == 0);
        unit_assert(!anal.closed);
    }

    unit_assert(cancelCallback.count == 2); 
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
    }
    catch (...)
    {
        cerr << "Caught unknown exception.\n";
    }
    
    return 1;
}

