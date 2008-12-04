//
// SpectrumListFactory.cpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
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


#include "SpectrumListFactory.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_Filter.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_NativeCentroider.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_PrecursorRecalculator.hpp"
#include "pwiz/data/msdata/SpectrumInfo.hpp"
#include <iostream>


namespace pwiz {
namespace analysis {


using namespace pwiz::msdata;
using namespace pwiz::util;
using namespace std;


namespace {


//
// each SpectrumListWrapper has a filterCreator_* function, 
// and an entry in the jump table below
//


typedef SpectrumListPtr (*FilterCreator)(const MSData& msd, const string& arg);


SpectrumListPtr filterCreator_indexSubset(const MSData& msd, const string& arg)
{
    IntegerSet indexSet;
    indexSet.parse(arg);

    return SpectrumListPtr(new 
        SpectrumList_Filter(msd.run.spectrumListPtr, 
            SpectrumList_FilterPredicate_IndexSet(indexSet)));
}


SpectrumListPtr filterCreator_scanSubset(const MSData& msd, const string& arg)
{
    IntegerSet scanNumberSet;
    scanNumberSet.parse(arg);

    return SpectrumListPtr(new 
        SpectrumList_Filter(msd.run.spectrumListPtr, 
                            SpectrumList_FilterPredicate_ScanNumberSet(scanNumberSet)));
}


SpectrumListPtr filterCreator_nativeCentroid(const MSData& msd, const string& arg)
{
    IntegerSet msLevelsToCentroid;
    msLevelsToCentroid.parse(arg);

    return SpectrumListPtr(new 
        SpectrumList_NativeCentroider(msd.run.spectrumListPtr, msLevelsToCentroid));
}


struct StripIonTrapSurveyScans : public SpectrumList_Filter::Predicate
{
    virtual boost::logic::tribool accept(const SpectrumIdentity& spectrumIdentity) const
    {
        return boost::logic::indeterminate; // need full Spectrum
    }

    virtual bool accept(const Spectrum& spectrum) const
    {
        SpectrumInfo info(spectrum);
        return !(info.msLevel==1 && cvIsA(info.massAnalyzerType, MS_ion_trap));
    }
};


SpectrumListPtr filterCreator_stripIT(const MSData& msd, const string& arg)
{
    return SpectrumListPtr(new 
        SpectrumList_Filter(msd.run.spectrumListPtr, StripIonTrapSurveyScans()));
}


SpectrumListPtr filterCreator_precursorRecalculation(const MSData& msd, const string& arg)
{
    return SpectrumListPtr(new SpectrumList_PrecursorRecalculator(msd));
}


struct JumpTableEntry
{
    const char* command;
    const char* usage;
    FilterCreator creator;
};


JumpTableEntry jumpTable_[] =
{
    {"indexSubset", "[indexRangeBegin,indexRangeEnd] ...", filterCreator_indexSubset},
    {"scanSubset", "[scanRangeBegin,scanRangeEnd] ...", filterCreator_scanSubset},
    {"nativeCentroid", "[msLevelsBegin,msLevelsEnd] ...", filterCreator_nativeCentroid},
    {"stripIT", " (strip ion trap ms1 scans)", filterCreator_stripIT},
    {"precursorRecalculation", " (based on ms1 data)", filterCreator_precursorRecalculation},
};


size_t jumpTableSize_ = sizeof(jumpTable_)/sizeof(JumpTableEntry);


JumpTableEntry* jumpTableEnd_ = jumpTable_ + jumpTableSize_;


struct HasCommand
{
    HasCommand(const string& command) : command_(command) {}
    bool operator()(const JumpTableEntry& entry) {return command_ == entry.command;}
    string command_;
};


} // namespace


void SpectrumListFactory::wrap(MSData& msd, const string& wrapper)
{
    // split wrapper string into command + arg

    istringstream iss(wrapper);
    string command;
    iss >> command;
    string arg = wrapper.substr(command.size());

    // switch on command, instantiate the filter

    JumpTableEntry* entry = find_if(jumpTable_, jumpTableEnd_, HasCommand(command));

    if (entry == jumpTableEnd_)
    {
        cerr << "[SpectrumListFactory] Ignoring wrapper: " << wrapper << endl;
        return;
    }

    SpectrumListPtr filter = entry->creator(msd, arg);

    if (!filter.get())
    {
        cerr << "command: " << command << endl;
        cerr << "arg: " << arg << endl;
        throw runtime_error("[SpectrumListFactory::wrap()] Error creating filter.");
    }

    // replace existing SpectrumList with the new one

    msd.run.spectrumListPtr = filter;
}


void SpectrumListFactory::wrap(msdata::MSData& msd, const vector<string>& wrappers)
{
    for (vector<string>::const_iterator it=wrappers.begin(); it!=wrappers.end(); ++it)
        wrap(msd, *it);
}


string SpectrumListFactory::usage()
{
    ostringstream oss;

    oss << "filter options:\n";

    for (JumpTableEntry* it=jumpTable_; it!=jumpTableEnd_; ++it)
        oss << it->command << " " << it->usage << endl;

    oss << endl;

    return oss.str();
}


} // namespace analysis 
} // namespace pwiz


