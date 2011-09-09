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
#include "pwiz/analysis/spectrum_processing/SpectrumList_Sorter.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_PeakPicker.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_Smoother.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_PeakFilter.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_ChargeStateCalculator.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_PrecursorRecalculator.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_PrecursorRefine.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_MZWindow.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_MetadataFixer.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_TitleMaker.hpp"
#include "pwiz/analysis/spectrum_processing/PrecursorMassFilter.hpp"
#include "pwiz/analysis/spectrum_processing/ThresholdFilter.hpp"
#include "pwiz/analysis/spectrum_processing/MS2NoiseFilter.hpp"
#include "pwiz/analysis/spectrum_processing/MS2Deisotoper.hpp"
#include "pwiz/utility/misc/Std.hpp"


namespace pwiz {
namespace analysis {


using namespace pwiz::cv;
using namespace pwiz::msdata;
using namespace pwiz::util;


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


SpectrumListPtr filterCreator_sortScanTime(const MSData& msd, const string& arg)
{
    return SpectrumListPtr(new SpectrumList_Sorter(msd.run.spectrumListPtr,
                                                   SpectrumList_SorterPredicate_ScanStartTime()));
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
    bool bRemPrecursor = removePrecursor == "false" || removePrecursor == "0" ? false : true;
    string removeChargeReduced;
    parser >> removeChargeReduced;
    bool bRemChgRed = removeChargeReduced == "false" || removeChargeReduced == "0" ? false : true;
    string removeNeutralLoss;
    parser >> removeNeutralLoss;
    bool bRemNeutralLoss = removeNeutralLoss == "false" || removeNeutralLoss == "0" ? false : true;
	string useBlanketFiltering;
	parser >> useBlanketFiltering;
	bool bUseBlanketFiltering = useBlanketFiltering == "false" || useBlanketFiltering == "0" ? false : true;

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

SpectrumListPtr filterCreator_MS2Denoise(const MSData& msd, const string& arg)
{
    istringstream parser(arg);

    size_t  numPeaksInWindow = 6;
    double  windowSize = 30.;
    bool    relaxLowMass = false;

    string npeaks, wsize, relax;
    parser >> npeaks;
    if (npeaks.empty() == false)
        numPeaksInWindow = lexical_cast<int>(npeaks);
    parser >> wsize;
    if (wsize.empty() == false)
        windowSize = lexical_cast<double>(wsize);
    parser >> relax;
    if (relax.empty() == false)
        relaxLowMass = lexical_cast<bool>(relax);

    SpectrumDataFilterPtr filter = SpectrumDataFilterPtr(new MS2NoiseFilter(MS2NoiseFilter::Config(numPeaksInWindow, windowSize, relaxLowMass)));
    return SpectrumListPtr(new 
            SpectrumList_PeakFilter(msd.run.spectrumListPtr,
                                   filter));
}

SpectrumListPtr filterCreator_MS2Deisotope(const MSData& msd, const string& arg)
{
    istringstream parser(arg);

    bool hires = false;
    string buf;
    parser >> buf;
    if (buf.empty() == false)
    {
        if (buf == "true")
            hires = true;
    }

    MZTolerance mzt(hires? 0.01 : 0.5);
    if (parser.good())
    {
        parser >> mzt;
    }


    SpectrumDataFilterPtr filter = SpectrumDataFilterPtr(new MS2Deisotoper(MS2Deisotoper::Config(mzt, hires)));
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

    virtual boost::logic::tribool accept(const Spectrum& spectrum) const
    {
        if (!spectrum.hasCVParam(MS_ms_level) ||
            spectrum.scanList.scans.empty() ||
            !spectrum.scanList.scans[0].instrumentConfigurationPtr.get())
            return boost::logic::indeterminate;

        CVID massAnalyzerType = spectrum.scanList.scans[0].instrumentConfigurationPtr->componentList.analyzer(0).
                                    cvParamChild(MS_mass_analyzer_type).cvid;
        if (massAnalyzerType == CVID_Unknown) return boost::logic::indeterminate;
        return !(spectrum.cvParam(MS_ms_level).value == "1" && cvIsA(massAnalyzerType, MS_ion_trap));
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


SpectrumListPtr filterCreator_precursorRefine(const MSData& msd, const string& arg)
{
    return SpectrumListPtr(new SpectrumList_PrecursorRefine(msd));
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

SpectrumListPtr filterCreator_mzPrecursors(const MSData& msd, const string& arg)
{
    char open='\0', comma='\0', close='\0';
	std::set<double> setMz;

    istringstream iss(arg);
    iss >> open;
	while (isdigit(iss.peek()))
	{
		double mz = 0;
		iss >> mz;
		setMz.insert(mz);
		if (iss.peek() == ',')
			iss >> comma;
	}
	iss >> close;

    if (open!='[' || close!=']')
        return SpectrumListPtr();

    return SpectrumListPtr(new
		SpectrumList_Filter(msd.run.spectrumListPtr,
		                    SpectrumList_FilterPredicate_PrecursorMzSet(setMz)));
}

SpectrumListPtr filterCreator_msLevel(const MSData& msd, const string& arg)
{
    IntegerSet msLevelSet;
    msLevelSet.parse(arg);

    return SpectrumListPtr(new 
        SpectrumList_Filter(msd.run.spectrumListPtr, 
                            SpectrumList_FilterPredicate_MSLevelSet(msLevelSet)));
}


SpectrumListPtr filterCreator_defaultArrayLength(const MSData& msd, const string& arg)
{
    IntegerSet defaultArrayLengthSet;
    defaultArrayLengthSet.parse(arg);

    return SpectrumListPtr(new 
        SpectrumList_Filter(msd.run.spectrumListPtr, 
                            SpectrumList_FilterPredicate_DefaultArrayLengthSet(defaultArrayLengthSet)));
}


SpectrumListPtr filterCreator_metadataFixer(const MSData& msd, const string& arg)
{
    return SpectrumListPtr(new SpectrumList_MetadataFixer(msd.run.spectrumListPtr));
}


SpectrumListPtr filterCreator_titleMaker(const MSData& msd, const string& arg)
{
    return SpectrumListPtr(new SpectrumList_TitleMaker(msd, bal::trim_copy(arg)));
}


SpectrumListPtr filterCreator_chargeStatePredictor(const MSData& msd, const string& arg)
{
    istringstream parser(arg);

    string overrideExistingCharge, maxMultipleCharge, minMultipleCharge, singleChargeFractionTIC;
    parser >> overrideExistingCharge >> maxMultipleCharge >> minMultipleCharge >> singleChargeFractionTIC;

    return SpectrumListPtr(new
        SpectrumList_ChargeStateCalculator(msd.run.spectrumListPtr,
                                           overrideExistingCharge == "false" || overrideExistingCharge == "0" ? false : true,
                                           lexical_cast<int>(maxMultipleCharge),
                                           lexical_cast<int>(minMultipleCharge),
                                           lexical_cast<double>(singleChargeFractionTIC)));
}


/** 
  *  filter on the basis of ms2 activation type
  *
  *   handler for --filter Activation option.  Use it to create
  *   output files containing only ETD or CID MSn data where both activation modes have been
  *   interleaved within a given input vendor data file (eg: Thermo's Decision Tree acquisition mode).
  */ 
SpectrumListPtr filterCreator_ActivationType(const MSData& msd, const string& arg)
{
    istringstream parser(arg);
    string activationType;
    parser >> activationType;
    bal::to_upper(activationType);

    // MS_collision_induced_dissociation gets used together with MS_electron_transfer_dissociation
    // for the ETD/Supplemental Activation mode.  Our filter is for separating B/Y from C/Z ions.  
    //
    // Activation Type   ETD "flag"  CID "flag"  Fragment Ion Type
    // ------------------------------------------------------------
    //  ETD                Yes         No              C/Z
    //  CID                No          Yes             B/Y
    //  HCD                No          Yes             B/Y
    //  ETD/SA             Yes         Yes             C/Z
    //
    // Check for presence or absence of ETD flag only.

    set<CVID> cvIDs;

    bool hasNot = false;

    // TODO: replace hand-written code with CVTranslator

    if (activationType == "CID") // HACK: CID means neither of HCD or ETD
    {
        hasNot = true;
        cvIDs.insert(MS_high_energy_collision_induced_dissociation);
        cvIDs.insert(MS_BIRD);
        cvIDs.insert(MS_ECD);
        cvIDs.insert(MS_ETD);
        cvIDs.insert(MS_IRMPD);
        cvIDs.insert(MS_PD);
        cvIDs.insert(MS_PSD);
        cvIDs.insert(MS_PQD);
        cvIDs.insert(MS_SID);
        cvIDs.insert(MS_SORI);
    }
    else if (activationType == "SA")
    {
        cvIDs.insert(MS_ETD);
        cvIDs.insert(MS_CID);
    }
    else if (activationType == "HCD") cvIDs.insert(MS_high_energy_collision_induced_dissociation);
    else if (activationType == "BIRD") cvIDs.insert(MS_BIRD);
    else if (activationType == "ECD") cvIDs.insert(MS_ECD);
    else if (activationType == "ETD") cvIDs.insert(MS_ETD);
    else if (activationType == "IRMPD") cvIDs.insert(MS_IRMPD);
    else if (activationType == "PD") cvIDs.insert(MS_PD);
    else if (activationType == "PSD") cvIDs.insert(MS_PSD);
    else if (activationType == "PQD") cvIDs.insert(MS_PQD);
    else if (activationType == "SID") cvIDs.insert(MS_SID);
    else if (activationType == "SORI") cvIDs.insert(MS_SORI);
    else
        throw runtime_error("[SpectrumListFactory::filterCreator_ActivationType()] invalid activation type \"" + activationType + "\"");

    return SpectrumListPtr(new SpectrumList_Filter(msd.run.spectrumListPtr, 
                                                   SpectrumList_FilterPredicate_ActivationType(cvIDs, hasNot)));
}


SpectrumListPtr filterCreator_AnalyzerType(const MSData& msd, const string& arg)
{
    istringstream parser(arg);
    string analyzerType;
    parser >> analyzerType;
    bal::to_upper(analyzerType);

    set<CVID> cvIDs;

    // sometimes people use FT and Orbi interchangeably, which is OK because there are no hybrid FT+Orbi instruments
    if (bal::starts_with(analyzerType, "FT") ||
        bal::starts_with(analyzerType, "ORBI"))
    {
        cvIDs.insert(MS_orbitrap);
        cvIDs.insert(MS_FT_ICR);
    }
    else if (bal::starts_with(analyzerType, "IT")) cvIDs.insert(MS_ion_trap);
    else if (bal::starts_with(analyzerType, "QUAD")) cvIDs.insert(MS_quadrupole);
    else if (analyzerType == "TOF") cvIDs.insert(MS_TOF);
    else
        throw runtime_error("[SpectrumListFactory::filterCreator_AnalyzerType()] invalid filter argument.");

    return SpectrumListPtr(new SpectrumList_Filter(msd.run.spectrumListPtr,
                                                   SpectrumList_FilterPredicate_AnalyzerType(cvIDs)));

}

SpectrumListPtr filterCreator_thresholdFilter(const MSData& msd, const string& arg)
{
    istringstream parser(arg);
    string byTypeArg, orientationArg;
    double threshold;
    IntegerSet msLevels(1, INT_MAX);

    parser >> byTypeArg >> threshold >> orientationArg;

    if (parser)
    {
        string msLevelSets;
        getline(parser, msLevelSets);

        if (!msLevelSets.empty())
        {
            msLevels = IntegerSet();
            msLevels.parse(msLevelSets);
        }
    }

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

    SpectrumDataFilterPtr filter(new ThresholdFilter(byType, threshold, orientation, msLevels));
    return SpectrumListPtr(new SpectrumList_PeakFilter(msd.run.spectrumListPtr, filter));
}

SpectrumListPtr filterCreator_polarityFilter(const MSData& msd, const string& arg)
{
    istringstream parser(arg);
    string polarityArg;

    parser >> polarityArg;

    CVID polarity = CVID_Unknown;

    if (parser)
    {
        if (polarityArg == "positive" || polarityArg == "+")
            polarity = MS_positive_scan;
        else if (polarityArg == "negative" || polarityArg == "-")
            polarity = MS_negative_scan;
    }

    if (polarity == CVID_Unknown)
        throw runtime_error("[SpectrumListFactory::filterCreator_polarityFilter()] invalid polarity (expected \"positive\" or \"negative\")");

    return SpectrumListPtr(new SpectrumList_Filter(msd.run.spectrumListPtr, SpectrumList_FilterPredicate_Polarity(polarity)));
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
    {"precursorRefine", " (based on ms1 data)", filterCreator_precursorRefine},
    {"peakPicking", "prefer_vendor:<true|false>  int_set(MS levels)", filterCreator_nativeCentroid},
    {"scanNumber", "int_set", filterCreator_scanNumber},
    {"scanEvent", "int_set", filterCreator_scanEvent},
    {"scanTime", "[scanTimeLow,scanTimeHigh]", filterCreator_scanTime},
    {"sortByScanTime", "(sort by ascending scan start time)", filterCreator_sortScanTime},
    {"stripIT", " (strip ion trap ms1 scans)", filterCreator_stripIT},
    {"metadataFixer", " (add/replace TIC/BPI metadata)", filterCreator_metadataFixer},
    {"titleMaker", " (add/replace spectrum title according to user-specified format string; the following keywords are recognized: <RunId> <Index> <Id> <ScanNumber> <ActivationType> <IsolationMz> <SelectedIonMz> <ChargeState> <PrecursorSpectrumId> <SpectrumType> <MsLevel> <ScanStartTimeInMinutes> <ScanStartTimeInSeconds> <BasePeakMz> <BasePeakIntensity> <TotalIonCurrent>", filterCreator_titleMaker},
    {"threshold", "<count|count-after-ties|absolute|bpi-relative|tic-relative|tic-cutoff> <threshold> <most-intense|least-intense> [int_set(MS levels)]", filterCreator_thresholdFilter},
    {"mzWindow", "[mzLow,mzHigh]", filterCreator_mzWindow},
	{"mzPrecursors", "[mz1,mz2, ... mzn] zero for no precursor m/z", filterCreator_mzPrecursors},
    {"defaultArrayLength", "int_set", filterCreator_defaultArrayLength},

    // MSn Spectrum Processing/Filtering
    {"MS2Denoise", "moving window filter for MS2: num peaks to select in window:int_val(default 6) window width (Da):val (default 30) multicharge fragment relaxation: <true|false> (default true)", filterCreator_MS2Denoise},
    {"MS2Deisotope", "deisotope ms2 spectra using Markey method", filterCreator_MS2Deisotope},
    {"ETDFilter", "removePrecursor:<default:true|false>  removeChargeReduced:<default:true|false>  removeNeutralLoss:<default:true|false>  blanketRemoval:<default:true|false>  MatchingTolerance:(val <PPM|MZ>) (default:3.1 MZ)", filterCreator_ETDFilter},
    {"chargeStatePredictor", "overrideExistingCharge:<default:true|false>  maxMultipleCharge:<int>(3)  minMultipleCharge:<int>(2)  singleChargeFractionTIC:<real>(0.9)", filterCreator_chargeStatePredictor},
    {"activation", "<ETD|CID|SA|HCD|BIRD|ECD|IRMPD|PD|PSD|PQD|SID|SORI> (filter by precursor activation type)", filterCreator_ActivationType},
    {"analyzerType", "<FTMS|ITMS> (deprecated syntax for filtering by mass analyzer type)", filterCreator_AnalyzerType},
    {"analyzer", "<quad|orbi|FT|IT|TOF> (filter by mass analyzer type)", filterCreator_AnalyzerType},
    {"polarity", "<positive|negative|+|-> (filter by scan polarity)", filterCreator_polarityFilter}
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


