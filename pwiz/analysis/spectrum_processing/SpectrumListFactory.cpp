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


#include "SpectrumListFactory.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_Filter.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_PeakPicker.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_Smoother.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_PeakFilter.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_ChargeStateCalculator.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_PrecursorRecalculator.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_MZWindow.hpp"
#include "pwiz/analysis/spectrum_processing/PrecursorMassFilter.hpp"
#include "pwiz/data/msdata/SpectrumInfo.hpp"
#include <iostream>


namespace pwiz {
namespace analysis {


using namespace pwiz;
using namespace pwiz::msdata;
using namespace pwiz::util;
using namespace std;


namespace {


//
// each SpectrumListWrapper has a filterCreator_* function, 
// and an entry in the jump table below
//


typedef SpectrumListPtr (*FilterCreator)(const MSData& msd, const string& arg);


SpectrumListPtr filterCreator_index(const MSData& msd, const string& arg)
{
    IntegerSet indexSet;
    indexSet.parse(arg);

    return SpectrumListPtr(new 
        SpectrumList_Filter(msd.run.spectrumListPtr, 
            SpectrumList_FilterPredicate_IndexSet(indexSet)));
}


SpectrumListPtr filterCreator_scanNumber(const MSData& msd, const string& arg)
{
    IntegerSet scanNumberSet;
    scanNumberSet.parse(arg);

    return SpectrumListPtr(new 
        SpectrumList_Filter(msd.run.spectrumListPtr, 
                            SpectrumList_FilterPredicate_ScanNumberSet(scanNumberSet)));
}


SpectrumListPtr filterCreator_scanEvent(const MSData& msd, const string& arg)
{
    IntegerSet scanEventSet;
    scanEventSet.parse(arg);

    return SpectrumListPtr(new 
        SpectrumList_Filter(msd.run.spectrumListPtr, 
                            SpectrumList_FilterPredicate_ScanEventSet(scanEventSet)));
}


SpectrumListPtr filterCreator_scanTime(const MSData& msd, const string& arg)
{
    double scanTimeLow = 0;
    double scanTimeHigh = 0;

    istringstream iss(arg);
    char open='\0', comma='\0', close='\0';
    iss >> open >> scanTimeLow >> comma >> scanTimeHigh >> close;

    if (open!='[' || comma!=',' || close!=']')
        return SpectrumListPtr();

    return SpectrumListPtr(new
        SpectrumList_Filter(msd.run.spectrumListPtr, 
                            SpectrumList_FilterPredicate_ScanTimeRange(scanTimeLow, scanTimeHigh)));
}


SpectrumListPtr filterCreator_nativeCentroid(const MSData& msd, const string& arg)
{
    istringstream parser(arg);
    string preferVendorPeakPicking;
    parser >> preferVendorPeakPicking;
    bool preferVendor = preferVendorPeakPicking == "true" ? true : false;

    string msLevelSets;
    getline(parser, msLevelSets);

    IntegerSet msLevelsToCentroid;
    msLevelsToCentroid.parse(msLevelSets);

    return SpectrumListPtr(new 
        SpectrumList_PeakPicker(msd.run.spectrumListPtr,
                                PeakDetectorPtr(new LocalMaximumPeakDetector(3)),
                                preferVendor,
                                msLevelsToCentroid));
}

SpectrumListPtr filterCreator_ETDFilter(const MSData& msd, const string& arg)
{
    istringstream parser(arg);

    string removeMS1;
    parser >> removeMS1;
    bool bRemoveMS1 = removeMS1 == "false" ? false : true;
    string removePrecursor;
    parser >> removePrecursor;
    bool bRemPrecursor = removePrecursor == "false" ? false : true;
    string removeChargeReduced;
    parser >> removeChargeReduced;
    bool bRemChgRed = removeChargeReduced == "false" ? false : true;
    string removeNeutralLoss;
    parser >> removeNeutralLoss;
    bool bRemNeutralLoss = removeNeutralLoss == "false" ? false : true;
    string selectiveRemoval;
    parser >> selectiveRemoval;
    bool bSelectiveRemoval = selectiveRemoval == "false" ? false : true;
	string useBlanketFiltering;
	parser >> useBlanketFiltering;
	bool bUseBlanketFiltering = useBlanketFiltering == "false" ? false : true;

    MZTolerance mzt;
    parser >> mzt;

    PrecursorMassFilter::Config params(mzt, bRemPrecursor, bRemChgRed, bRemNeutralLoss, bSelectiveRemoval, bUseBlanketFiltering);
    PrecursorMassFilter* filter = new PrecursorMassFilter(params);

    if (bRemoveMS1 == false)
    {
        return SpectrumListPtr(new 
            SpectrumList_PeakFilter(msd.run.spectrumListPtr,
                                    filter));
    }
    else
    {
        return SpectrumListPtr(new 
            SpectrumList_PeakFilter( 
            SpectrumListPtr(new SpectrumList_Filter(msd.run.spectrumListPtr, 
                                SpectrumList_FilterPredicate_MSLevelSet(IntegerSet(2)))),
                                    filter));
    }
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


SpectrumListPtr filterCreator_mzWindow(const MSData& msd, const string& arg)
{
    double mzLow = 0;
    double mzHigh = 0;

    istringstream iss(arg);
    char open='\0', comma='\0', close='\0';
    iss >> open >> mzLow >> comma >> mzHigh >> close;

    if (open!='[' || comma!=',' || close!=']')
        return SpectrumListPtr();

    return SpectrumListPtr(new SpectrumList_MZWindow(msd.run.spectrumListPtr, mzLow, mzHigh));
}


SpectrumListPtr filterCreator_msLevel(const MSData& msd, const string& arg)
{
    IntegerSet msLevelSet;
    msLevelSet.parse(arg);

    return SpectrumListPtr(new 
        SpectrumList_Filter(msd.run.spectrumListPtr, 
                            SpectrumList_FilterPredicate_MSLevelSet(msLevelSet)));
}


struct JumpTableEntry
{
    const char* command;
    const char* usage;
    FilterCreator creator;
};


JumpTableEntry jumpTable_[] =
{
    {"index", "int_set", filterCreator_index},
    {"msLevel", "int_set", filterCreator_msLevel},
    {"mzWindow", "[mzLow,mzHigh]", filterCreator_mzWindow},
    {"peakPicking", "prefer_vendor:<true|false>  int_set(MS levels)", filterCreator_nativeCentroid},
    {"ETDFilter", "removeMS2:<true|false> removePrecursor:<true|false> removeChargeReduced:<true|false> removeNeutralLoss:<true|false> mostIntenseInWindow:<true|false> blanketRemoval<true|false> MatchingTolerance:(val <PPM|MZ>)", filterCreator_ETDFilter},
    {"precursorRecalculation", " (based on ms1 data)", filterCreator_precursorRecalculation},
    {"scanNumber", "int_set", filterCreator_scanNumber},
    {"scanEvent", "int_set", filterCreator_scanEvent},
    {"scanTime", "[scanTimeLow,scanTimeHigh]", filterCreator_scanTime},
    {"stripIT", " (strip ion trap ms1 scans)", filterCreator_stripIT},
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

    oss << "\nfilter options:\n\n";

    for (JumpTableEntry* it=jumpTable_; it!=jumpTableEnd_; ++it)
        oss << it->command << " " << it->usage << endl;

    oss << endl;

    oss << "\'int_set\' means that a set of integers must be specified, as a list of intervals of the form [a,b] or a[-][b]\n";

    oss << endl;

    return oss.str();
}


} // namespace analysis 
} // namespace pwiz


