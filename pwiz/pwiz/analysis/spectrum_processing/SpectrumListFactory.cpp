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

#define PWIZ_SOURCE


#include "SpectrumListFactory.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_Filter.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_PeakPicker.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_Smoother.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_PeakFilter.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_ChargeStateCalculator.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_PrecursorRecalculator.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_MZWindow.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_MetadataFixer.hpp"
#include "pwiz/analysis/spectrum_processing/PrecursorMassFilter.hpp"
#include "pwiz/analysis/spectrum_processing/ThresholdFilter.hpp"
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

/**
 *  Handler for --filter "ETDFilter".  There are five optional arguments for this filter:
 *  <true|false> remove unreacted precursor
 *  <true|false> remove charge reduced precursor
 *  <true|false> remove neutral loss species from charge reduced precursor
 *  float_val <MZ|PPM> matching tolerance -- floating point value, followed by units (example: 3.1 MZ)
 */
SpectrumListPtr filterCreator_ETDFilter(const MSData& msd, const string& arg)
{
    istringstream parser(arg);

    string removePrecursor;
    parser >> removePrecursor;
    bool bRemPrecursor = removePrecursor == "false" ? false : true;
    string removeChargeReduced;
    parser >> removeChargeReduced;
    bool bRemChgRed = removeChargeReduced == "false" ? false : true;
    string removeNeutralLoss;
    parser >> removeNeutralLoss;
    bool bRemNeutralLoss = removeNeutralLoss == "false" ? false : true;
	string useBlanketFiltering;
	parser >> useBlanketFiltering;
	bool bUseBlanketFiltering = useBlanketFiltering == "false" ? false : true;

    MZTolerance mzt(3.1);
    if (parser.good())
    {
        parser >> mzt;
    }

    SpectrumDataFilterPtr filter;

    if (bRemNeutralLoss)
    {
        filter = SpectrumDataFilterPtr(new PrecursorMassFilter(PrecursorMassFilter::Config(mzt, bRemPrecursor, bRemChgRed, bUseBlanketFiltering)));
    }
    else
    {
        filter = SpectrumDataFilterPtr(new PrecursorMassFilter(PrecursorMassFilter::Config(mzt, bRemPrecursor, bRemChgRed, bUseBlanketFiltering, 0)));
    }

    return SpectrumListPtr(new 
        SpectrumList_PeakFilter(msd.run.spectrumListPtr,
                                filter));
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


SpectrumListPtr filterCreator_metadataFixer(const MSData& msd, const string& arg)
{
    return SpectrumListPtr(new SpectrumList_MetadataFixer(msd.run.spectrumListPtr));
}


/** 
  *  filter on the basis of ms2 activation type
  *
  *   handler for --filter Activation option.  Use it to create
  *   output files containing only ETD or CID ms2 data where both activation modes have been
  *   interleaved within a given input vendor data file (eg: Thermo's Decision Tree acquisition mode).
  */ 
SpectrumListPtr filterCreator_ActivationType(const MSData& msd, const string& arg)
{
    istringstream parser(arg);
    string sActivationType;
    parser >> sActivationType;

    // MS_collision_induced_dissociation gets used together with MS_electron_transfer_dissociation
    // for the ETD/Supplemental Activation mode.  Our filter is for separating B/Y from C/Z ions.  
    //
    // Activation Type   ETD "flag"  CID "flag"  Fragment Ion Type
    // ------------------------------------------------------------
    //  ETD                Yes         No              C/Z
    //  CID                No          Yes             B/Y
    //  ETD/SA             Yes         Yes             C/Z
    //
    // Check for presence or absense of ETD flag only.

    set<CVID> cvIDs;

    bool hasNot = false;

    if (sActivationType == "CID") // HACK: CID means neither of HCD or ETD
    {
        hasNot = true;
        cvIDs.insert(MS_electron_transfer_dissociation);
        cvIDs.insert(MS_high_energy_collision_induced_dissociation);
    }
    else if (sActivationType == "SA")
    {
        cvIDs.insert(MS_electron_transfer_dissociation);
        cvIDs.insert(MS_collision_induced_dissociation);
    }
    else if (sActivationType == "HCD")
    {
        cvIDs.insert(MS_high_energy_collision_induced_dissociation);
    }
    else if (sActivationType == "ETD")
    {
        cvIDs.insert(MS_electron_transfer_dissociation);
    }

    return SpectrumListPtr(new 
    SpectrumList_Filter(msd.run.spectrumListPtr, 
                        SpectrumList_FilterPredicate_MS2ActivationType(cvIDs, hasNot)));

}


SpectrumListPtr filterCreator_thresholdFilter(const MSData& msd, const string& arg)
{
    istringstream parser(arg);
    string byTypeArg, orientationArg;
    double threshold;

    parser >> byTypeArg >> threshold >> orientationArg;

    ThresholdFilter::ThresholdingBy_Type byType;
    if (byTypeArg == "count")
        byType = ThresholdFilter::ThresholdingBy_Count;
    else if (byTypeArg == "count-after-ties")
        byType = ThresholdFilter::ThresholdingBy_CountAfterTies;
    else if (byTypeArg == "absolute")
        byType = ThresholdFilter::ThresholdingBy_AbsoluteIntensity;
    else if (byTypeArg == "bpi-relative")
        byType = ThresholdFilter::ThresholdingBy_FractionOfBasePeakIntensity;
    else if (byTypeArg == "tic-relative")
        byType = ThresholdFilter::ThresholdingBy_FractionOfTotalIntensity;
    else if (byTypeArg == "tic-cutoff")
        byType = ThresholdFilter::ThresholdingBy_FractionOfTotalIntensityCutoff;
    else
        return SpectrumListPtr();

    ThresholdFilter::ThresholdingOrientation orientation;
    if (orientationArg == "most-intense")
        orientation = ThresholdFilter::Orientation_MostIntense;
    else if (orientationArg == "least-intense")
        orientation = ThresholdFilter::Orientation_LeastIntense;
    else
        return SpectrumListPtr();

    SpectrumDataFilterPtr filter(new ThresholdFilter(byType, threshold, orientation));
    return SpectrumListPtr(new SpectrumList_PeakFilter(msd.run.spectrumListPtr, filter));
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
    {"precursorRecalculation", " (based on ms1 data)", filterCreator_precursorRecalculation},
    {"peakPicking", "prefer_vendor:<true|false>  int_set(MS levels)", filterCreator_nativeCentroid},
    {"scanNumber", "int_set", filterCreator_scanNumber},
    {"scanEvent", "int_set", filterCreator_scanEvent},
    {"scanTime", "[scanTimeLow,scanTimeHigh]", filterCreator_scanTime},
    {"stripIT", " (strip ion trap ms1 scans)", filterCreator_stripIT},
    {"metadataFixer", " (add/replace TIC/BPI metadata)", filterCreator_metadataFixer},
    {"threshold", "<count|count-after-ties|absolute|bpi-relative|tic-relative|tic-cutoff> <threshold> <most-intense|least-intense>", filterCreator_thresholdFilter},
    {"mzWindow", "[mzLow,mzHigh]", filterCreator_mzWindow},

    // MSn Spectrum Processing/Filtering
    {"ETDFilter", "removePrecursor:<default:true|false>  removeChargeReduced:<default:true|false>  removeNeutralLoss:<default:true|false>  blanketRemoval:<default:true|false>  MatchingTolerance:(val <PPM|MZ>) (default:3.1 MZ)", filterCreator_ETDFilter},
    {"activation", "<ETD|CID|SA|HCD> (filter by precursor activation type)", filterCreator_ActivationType},
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


PWIZ_API_DECL
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


PWIZ_API_DECL
void SpectrumListFactory::wrap(msdata::MSData& msd, const vector<string>& wrappers)
{
    for (vector<string>::const_iterator it=wrappers.begin(); it!=wrappers.end(); ++it)
        wrap(msd, *it);
}


PWIZ_API_DECL
string SpectrumListFactory::usage()
{
    ostringstream oss;

    oss << "\nFilter options:\n\n";

    for (JumpTableEntry* it=jumpTable_; it!=jumpTableEnd_; ++it)
        oss << it->command << " " << it->usage << endl;

    oss << endl;

    oss << "\'int_set\' means that a set of integers must be specified, as a list of intervals of the form [a,b] or a[-][b]\n";

    oss << endl;

    return oss.str();
}


} // namespace analysis 
} // namespace pwiz


