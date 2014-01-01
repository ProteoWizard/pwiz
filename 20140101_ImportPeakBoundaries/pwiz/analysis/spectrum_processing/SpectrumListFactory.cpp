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
#include "pwiz/analysis/spectrum_processing/SpectrumList_ZeroSamplesFilter.hpp"
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
typedef const char *UsageInfo[2];  // usage like <int_set>, and details

SpectrumListPtr filterCreator_index(const MSData& msd, const string& arg)
{
    IntegerSet indexSet;
    indexSet.parse(arg);

    return SpectrumListPtr(new 
        SpectrumList_Filter(msd.run.spectrumListPtr, 
            SpectrumList_FilterPredicate_IndexSet(indexSet)));
}
UsageInfo usage_index = {"<index_value_set>",
    "Selects spectra by index - an index value 0-based numerical order in which the spectrum appears in the input.\n"
    "  <index_value_set> is an int_set of indexes."
};

SpectrumListPtr filterCreator_scanNumber(const MSData& msd, const string& arg)
{
    IntegerSet scanNumberSet;
    scanNumberSet.parse(arg);

    return SpectrumListPtr(new 
        SpectrumList_Filter(msd.run.spectrumListPtr, 
                            SpectrumList_FilterPredicate_ScanNumberSet(scanNumberSet)));
}
UsageInfo usage_scanNumber = {"<scan_numbers>",
    "This filter selects spectra by scan number.  Depending on the input data type, scan number and spectrum index are not always the same thing - scan numbers are not always contiguous, and are usually 1-based.\n"
    "<scan_numbers> is an int_set of scan numbers to be kept."
};

SpectrumListPtr filterCreator_scanEvent(const MSData& msd, const string& arg)
{
    IntegerSet scanEventSet;
    scanEventSet.parse(arg);

    return SpectrumListPtr(new 
        SpectrumList_Filter(msd.run.spectrumListPtr, 
                            SpectrumList_FilterPredicate_ScanEventSet(scanEventSet)));
}
UsageInfo usage_scanEvent = {"<scan_event_set>","This filter selects spectra by scan event.  For example, to include all scan events except scan event 5, use "
            "filter \"scanEvent 1-4 6-\".  A \"scan event\" is a preset scan configuration: a user-defined scan configuration that "
            "specifies the instrumental settings in which a spectrum is acquired. An instrument may cycle through a list of preset "
            "scan configurations to acquire data. This is a more generic term for the Thermo \"scan event\", which is defined in "
            "the Thermo Xcalibur glossary as: \"a mass spectrometer scan that is defined by choosing the necessary scan parameter "
            "settings. Multiple scan events can be defined for each segment of time.\"."};

SpectrumListPtr filterCreator_scanTime(const MSData& msd, const string& arg)
{
    double scanTimeLow = 0;
    double scanTimeHigh = 0;

    istringstream iss(arg);
    char open='\0', comma='\0', close='\0';
    iss >> open >> scanTimeLow >> comma >> scanTimeHigh >> close;

    if (open!='[' || comma!=',' || close!=']')
    {
        cerr << "scanTime filter argument does not have form \"[\"<startTime>,<endTime>\"]\", ignored." << endl;
        return SpectrumListPtr();
    }

    return SpectrumListPtr(new
        SpectrumList_Filter(msd.run.spectrumListPtr, 
                            SpectrumList_FilterPredicate_ScanTimeRange(scanTimeLow, scanTimeHigh)));
}
UsageInfo usage_scanTime = {"<scan_time_range>",
    "This filter selects only spectra within a given time range.\n"
    "  <scan_time_range> is a time range, specified in seconds.  For example, to select only spectra within the "
    "second minute of the run, use \"scanTime [60-119.99]\"."};

SpectrumListPtr filterCreator_sortScanTime(const MSData& msd, const string& arg)
{
    return SpectrumListPtr(new SpectrumList_Sorter(msd.run.spectrumListPtr,
                                                   SpectrumList_SorterPredicate_ScanStartTime()));
}
UsageInfo usage_sortScanTime = {"","This filter reorders spectra, sorting them by ascending scan start time."};

SpectrumListPtr filterCreator_nativeCentroid(const MSData& msd, const string& arg)
{
    istringstream parser(arg);
    string preferVendorPeakPicking;
    parser >> preferVendorPeakPicking;
    bool preferVendor = boost::iequals(preferVendorPeakPicking , "true");

    string msLevelSets;
    getline(parser, msLevelSets);

    IntegerSet msLevelsToCentroid;
    msLevelsToCentroid.parse(msLevelSets);

    if (preferVendor && msd.countFiltersApplied())
    {
        cerr << "[SpectrumList_PeakPicker] Warning: vendor peakPicking requested, but peakPicking is not the first filter.  Since the vendor DLLs can only operate directly on raw data, this filter will likely not have any effect." << endl;
    }

    return SpectrumListPtr(new 
        SpectrumList_PeakPicker(msd.run.spectrumListPtr,
                                PeakDetectorPtr(new LocalMaximumPeakDetector(3)),
                                preferVendor,
                                msLevelsToCentroid));
}
UsageInfo usage_nativeCentroid = {"<prefer_vendor> <ms_levels>","This filter performs centroiding on spectra with the "
    "selected <ms_levels>, expressed as an int_set.  The value for <prefer_vendor> must be \"True\" or \"False\": when "
    "True, vendor (Windows DLL) code is used if available.  IMPORTANT NOTE: since this filter operates on the raw "
    "data through the vendor DLLs, IT MUST BE THE FIRST FILTER IN ANY LIST OF FILTERS when <prefer_vendor> is set to \"True\"."
};

/**
 *  Handler for --filter zeroSamples removeExtra|addMissing[=FlankingZeroCount] [mslevels]
 *
 **/
SpectrumListPtr filterCreator_ZeroSamples(const MSData& msd, const string& arg)
{
    istringstream parser(arg);
    string action;
    parser >> action;
    bool bRemover = ("removeExtra"==action);
    int FlankingZeroCount=-1;
    if (string::npos!=action.rfind('=')) 
    {
        FlankingZeroCount = atoi(action.substr(action.rfind('=')+1).c_str());
        action = action.substr(0,action.rfind('='));
    }
    if (!bRemover && ("addMissing"!=action))
        throw user_error("[SpectrumListFactory::filterCreator_ZeroSamples()] unknown mode \"" + action + "\"");
    string msLevelSets;
    getline(parser, msLevelSets);
    if (""==msLevelSets) msLevelSets="1-"; // default is all msLevels

    IntegerSet msLevelsToFilter;
    msLevelsToFilter.parse(msLevelSets);
    return SpectrumListPtr(new 
        SpectrumList_ZeroSamplesFilter(msd.run.spectrumListPtr,msLevelsToFilter,
        bRemover ? SpectrumList_ZeroSamplesFilter::Mode_RemoveExtraZeros : SpectrumList_ZeroSamplesFilter::Mode_AddMissingZeros,
        FlankingZeroCount));
}
UsageInfo usage_zeroSamples = {"<mode> [<MS_levels>]",
    "This filter deals with zero values in spectra - either removing them, or adding them where they are missing.\n"
    "  <mode> is either removeExtra or addMissing[=<flankingZeroCount>] .\n"
    "  <MS_levels> is optional, when provided (as an int_set) the filter is applied only to spectra with those MS levels.\n"
    "When <mode> is \"removeExtra\", consecutive zero intensity peaks are removed from spectra.  For example, a peak list\n"
    "  \"100.1,1000 100.2,0 100.3,0 100.4,0 100.5,0 100.6,1030\"\n"
    "would become \n"
    "  \"100.1,1000 100.2,0 100.5,0 100.6,1030\"\n"
    "and a peak list \n"
    "  \"100.1,0 100.2,0 100.3,0 100.4,0 100.5,0 100.6,1030 100.7,0 100.8,1020 100.9,0 101.0,0\"\n"
    "would become \n"
    "  \"100.5,0 100.6,1030 100.7,0 100.8,1020 100.9,0\"\n"
    "When <mode> is \"addMissing\", each spectrum's sample rate is automatically determined (the rate can change but only "
    "gradually) and flanking zeros are inserted around non-zero data points.  The optional [=<flankingZeroCount>] value "
    "can be used to limit the number of flanking zeros, otherwise the spectrum is completely populated between nonzero points. "
    "For example, to make sure spectra have at least 5 flanking zeros around runs on nonzero points, use filter \"addMissing=5\"."
};

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
UsageInfo usage_ETDFilter = {"[<removePrecursor> [<removeChargeReduced> [<removeNeutralLoss> [<blanketRemoval> [<matchingTolerance> ]]]]]",
    "Filters ETD MSn spectrum data points, removing unreacted precursors, charge-reduced precursors, and neutral losses.\n"
    "  <removePrecursor> - specify \"true\" to remove unreacted precursor (default is \"false\")\n"
    "  <removeChargeReduced> - specify \"true\" to remove charge reduced precursor (default is \"false\")\n"
    "  <removeNeutralLoss> - specify \"true\" to remove neutral loss species from charge reduced precursor (default is \"false\")\n"
    "  <matchingTolerance> - specify matching tolerance in MZ or PPM (examples: \"3.1 MZ\" (the default) or \"2.2 PPM\")"
};

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
UsageInfo usage_MS2Denoise = {"[<peaks_in_window> [<window_width_Da> [multicharge_fragment_relaxation]]]",
    "Noise peak removal for spectra with precursor ions.\n"
    "   <peaks_in_window> - the number peaks to select in moving window, default is 6.\n"
    "   <window_width_Da> - the width of the window in Da, default is 30.\n"
    "   <multicharge_fragment_relaxation> - if \"true\" (the default), allows more data below multiply charged precursors.\n"
    "The filter first removes any m/z values above the precursor mass minus the mass of glycine.\n"
    "It then removes any m/z values within .5 Da of the unfragmented precursor mass.\n"
    "Finally it retains only the <peaks_in_window> most intense ions within a "
    "sliding window of <window_width_Da>.\n"
    "If <multicharge_fragment_relaxation> is true, allows more peaks at lower mass (i.e. below precursor).\n"
    "If <window_width_Da> is set to 0, the window size defaults to the highest observed mass in the spectrum "
    "(this leaving only <peaks_in_window> ions in the output spectrum).\n"
    "Reference: \"When less can yield more - Computational preprocessing of MS/MS spectra for peptide "
    "identification\", Bernhard Y. Renard, Marc Kirchner, Flavio Monigatti, Alexander R. Ivanov, "
    "Juri Rappsilber, Dominic Winter, Judith A. J. Steen, Fred A. Hamprecht and Hanno Steen  "
    "Proteomics, 9, 4978-4984, 2009.\n"
};

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
UsageInfo usage_MS2Deisotope = { "[<hi_res> [<mz_tolerance>]]",
    "Deisotopes ms2 spectra using Markey method.\n"
    "  <hi_res> sets high resolution mode to \"false\" (the default) or \"true\".\n"
    "  <mz_tolerance> sets the mz tolerance.  It defaults to .01 in high resoltion mode, otherwise it defaults to 0.5."
    };

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
UsageInfo usage_stripIT={"","This filter rejects ion trap data spectra with MS level 1."};


SpectrumListPtr filterCreator_precursorRecalculation(const MSData& msd, const string& arg)
{
    return SpectrumListPtr(new SpectrumList_PrecursorRecalculator(msd));
}
UsageInfo usage_precursorRecalculation = {"","This filter recalculates the precursor m/z and charge for MS2 spectra. "
    "It looks at the prior MS1 scan to better infer the parent mass.  However, it only works on orbitrap and FT data,"
    "although it does not use any 3rd party (vendor DLL) code.  Since the time the code was written, Thermo has since fixed "
    "up its own estimation in response, so it's less critical than it used to be (though can still be useful)."};

SpectrumListPtr filterCreator_precursorRefine(const MSData& msd, const string& arg)
{
    return SpectrumListPtr(new SpectrumList_PrecursorRefine(msd));
}
UsageInfo usage_precursorRefine = {"", "This filter recalculates the precursor m/z and charge for MS2 spectra. "
    "It looks at the prior MS1 scan to better infer the parent mass.  It only works on orbitrap, FT, and TOF data. "
    "It does not use any 3rd party (vendor DLL) code."};

SpectrumListPtr filterCreator_mzWindow(const MSData& msd, const string& arg)
{
    double mzLow = 0;
    double mzHigh = 0;

    istringstream iss(arg);
    char open='\0', comma='\0', close='\0';
    iss >> open >> mzLow >> comma >> mzHigh >> close;

    if (open!='[' || comma!=',' || close!=']')
    {
        cerr << "mzWindow filter expected an mzrange formatted something like \"[123.4,567.8]\"" << endl;
        return SpectrumListPtr();
    }

    return SpectrumListPtr(new SpectrumList_MZWindow(msd.run.spectrumListPtr, mzLow, mzHigh));
}
UsageInfo usage_mzWindow = {"<mzrange>",
    "keeps mz/intensity pairs whose m/z values fall within the specified range.\n"
    "  <mzrange> is formatted as [mzLow,mzHigh].  For example, in msconvert to retain data in the m/z range "
    "100.1 to 307.5, use --filter \"mzWindow [100.1,307.5]\" ."
};

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
    {
        cerr << "mzPrecursors filter expected a list of m/z values formatted something like \"[123.4,567.8,789.0]\"" << endl;
        return SpectrumListPtr();
    }
    return SpectrumListPtr(new
        SpectrumList_Filter(msd.run.spectrumListPtr,
                            SpectrumList_FilterPredicate_PrecursorMzSet(setMz)));
}
UsageInfo usage_mzPrecursors = {"<precursor_mz_list>",
    "Retains spectra with precursor m/z values found in the <precursor_mz_list>.  For example, in msconvert to retain "
    "only spectra with precursor m/z values of 123.4 and 567.8 you would use --filter \"mzPrecursors [123.4,567.8]\".  "
    "Note that this filter will drop MS1 scans unless you include 0.0 in the list of precursor values."
    };

SpectrumListPtr filterCreator_msLevel(const MSData& msd, const string& arg)
{
    IntegerSet msLevelSet;
    msLevelSet.parse(arg);

    return SpectrumListPtr(new 
        SpectrumList_Filter(msd.run.spectrumListPtr, 
                            SpectrumList_FilterPredicate_MSLevelSet(msLevelSet)));
}
UsageInfo usage_msLevel = {"<mslevels>",
    "This filter selects only spectra with the indicated <mslevels>, expressed as an int_set."}; 


SpectrumListPtr filterCreator_mzPresent(const MSData& msd, const string& arg)
{
    istringstream parser(arg);

    MZTolerance mzt;
    parser >> mzt;

    string byTypeArg, orientationArg;
    double threshold;
    IntegerSet msLevels(1, INT_MAX);

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


    char open='\0', comma='\0', close='\0';
    std::set<double> setMz;

    parser >> open;
    while (isdigit(parser.peek()))
    {
        double mz = 0;
        parser >> mz;
        setMz.insert(mz);
        if (parser.peek() == ',')
            parser >> comma;
    }
    parser >> close;

    std::string inex = "include";
    bool inverse = false;
    if (parser.good())
        parser >> inex;
    if (inex != "include" && inex != "exclude")
        throw user_error("[SpectrumListFactory::filterCreator_mzPresent()] invalid parameter (expected \"include\" or \"exclude\")");

    if (inex == "exclude")
        inverse = true;

    if (open!='[' || close!=']')
    {
        cerr << "mzPresent filter expected a list of mz values like \"[100,200,300.4\\" << endl ;
       return SpectrumListPtr();
    }

    return SpectrumListPtr(new
        SpectrumList_Filter(msd.run.spectrumListPtr,
                        SpectrumList_FilterPredicate_MzPresent(mzt, setMz, ThresholdFilter(byType, threshold, orientation, msLevels), inverse)));
}
UsageInfo usage_mzPresent = {"<tolerance> <type> <threshold> <orientation> <mz_list> [<include_or_exclude>]",
    "This filter is similar to the \"threshold\" filter, with a few more options.\n"
    "   <tolerance> is specified as a number and units (PPM or MZ). For example, \"5 PPM\" or \"2.1 MZ\".\n"
    "   <type>, <threshold>, and <orientation> operate as in the \"threshold\" filter (see above).\n"
    "   <mz_list> is a list of mz values of the form [mz1,mz2, ... mzn] (for example, \"[100, 300, 405.6]\"). "
    "Data points within <tolerance> of any of these values will be kept.\n"
    "   <include_or_exclude> is optional and has value \"include\" (the default) or \"exclude\".  If \"exclude\" is "
    "used the filter drops data points that match the various criteria instead of keeping them."
};

SpectrumListPtr filterCreator_chargeState(const MSData& msd, const string& arg)
{
    IntegerSet chargeStateSet;
    chargeStateSet.parse(arg);

    return SpectrumListPtr(new 
        SpectrumList_Filter(msd.run.spectrumListPtr, 
                            SpectrumList_FilterPredicate_ChargeStateSet(chargeStateSet)));
}
UsageInfo usage_chargeState = {"<charge_states>",
    "This filter keeps spectra that match the listed charge state(s), expressed as an int_set.  Both known/single "
    "and possible/multiple charge states are tested.  Use 0 to include spectra with no charge state at all."};

SpectrumListPtr filterCreator_defaultArrayLength(const MSData& msd, const string& arg)
{
    IntegerSet defaultArrayLengthSet;
    defaultArrayLengthSet.parse(arg);

    return SpectrumListPtr(new 
        SpectrumList_Filter(msd.run.spectrumListPtr, 
                            SpectrumList_FilterPredicate_DefaultArrayLengthSet(defaultArrayLengthSet)));
}
UsageInfo usage_defaultArrayLength = { "<peak_count_range>",
    "Keeps only spectra with peak counts within <peak_count_range>, expressed as an int_set. (In mzML the peak list "
    "length is expressed as \"defaultArrayLength\", hence the name.)  For example, to include only spectra with 100 "
    "or more peaks, you would use filter \"defaultArrayLength 100-\" ."
    };


SpectrumListPtr filterCreator_metadataFixer(const MSData& msd, const string& arg)
{
    return SpectrumListPtr(new SpectrumList_MetadataFixer(msd.run.spectrumListPtr));
}
UsageInfo usage_metadataFixer={"","This filter is used to add or replace a spectra's TIC/BPI metadata, usually after "
    "peakPicking where the change from profile to centroided data may make the TIC and BPI values inconsistent with "
    "the revised scan data.  The filter traverses the m/z intensity arrays to find the sum and max. For example, in "
    "msconvert it can be used as: --filter \"peakPicking true 1-\" --filter metadataFixer.  It can also be used without "
    "peak picking for some strange results. Certainly adding up all the samples of profile data to get the TIC is "
    "just wrong, but we do it anyway."};

SpectrumListPtr filterCreator_titleMaker(const MSData& msd, const string& arg)
{
    return SpectrumListPtr(new SpectrumList_TitleMaker(msd, bal::trim_copy(arg)));
}
UsageInfo usage_titleMaker={"<format_string>","This filter adds or replaces spectrum titles according to specified "
    "<format_string>. You can use it, for example, to customize the TITLE line in MGF output in msconvert.  The following "
    "keywords are recognized: \n"
    "   \"<RunId>\" - prints the spectrum's Run id - for example, \"Data.d\" from \"C:/Agilent/Data.d/AcqData/mspeak.bin\"\n"
    "   \"<Index>\" - prints the spectrum's index\n"
    "   \"<Id>\" - prints the spectrum's nativeID\n"
    "   \"<SourcePath>\" - prints the path of the spectrum's source data\n"
    "   \"<ScanNumber>\" - if the nativeID can be represented as a single number, prints that number, else index+1\n"
    "   \"<ActivationType>\" - for the first precursor, prints the spectrum's \"dissociation method\" value\n"
    "   \"<IsolationMz>\" - for the first precursor, prints the the spectrum's \"isolation target m/z\" value\n"
    "   \"<PrecursorSpectrumId>\" - prints the nativeID of the spectrum of the first precursor\n"
    "   \"<SelectedIonMz>\" - prints the m/z value of the first selected ion of the first precursor\n"
    "   \"<ChargeState>\" - prints the charge state for the first selected ion of the first precursor\n"
    "   \"<SpectrumType>\" - prints the spectrum type\n"
    "   \"<ScanStartTimeInSeconds>\" - prints the spectrum's first scan's start time, in seconds\n"
    "   \"<ScanStartTimeInMinutes>\" - prints the spectrum's first scan's start time, in minutes\n"
    "   \"<BasePeakMz>\" - prints the spectrum's base peak m/z\n"
    "   \"<BasePeakIntensity>\" - prints the spectrum's base peak intensity\n"
    "   \"<TotalIonCurrent>\" - prints the spectrum's total ion current\n"    
    "   \"<MsLevel>\" - prints the spectrum's MS level\n"
    "For example, to create a TITLE line in msconvert MGF output with the \"name.first_scan.last_scan.charge\" style (eg. \"mydata.145.145.2\"), use\n"
     "--filter \"titleMaker <RunId>.<ScanNumber>.<ScanNumber>.<ChargeState>\""
};

SpectrumListPtr filterCreator_chargeStatePredictor(const MSData& msd, const string& arg)
{
    istringstream parser(arg);

    string overrideExistingCharge, maxMultipleCharge, minMultipleCharge, singleChargeFractionTIC, makeMS2;
    parser >> overrideExistingCharge >> maxMultipleCharge >> minMultipleCharge >> singleChargeFractionTIC >> makeMS2;

    return SpectrumListPtr(new
        SpectrumList_ChargeStateCalculator(msd.run.spectrumListPtr,
                                           overrideExistingCharge == "false" || overrideExistingCharge == "0" ? false : true,
                                           maxMultipleCharge!=""?lexical_cast<int>(maxMultipleCharge):3,
                                           minMultipleCharge!=""?lexical_cast<int>(minMultipleCharge):2,
                                           singleChargeFractionTIC!=""?lexical_cast<double>(singleChargeFractionTIC):0.9,
                                           makeMS2 == "true" || makeMS2 == "1" ? true : false));
}
UsageInfo usage_chargeStatePredictor = {"[<overrideExistingCharge> [<maxMultipleCharge> [<minMultipleCharge> [<singleChargeFractionTIC> [<algorithmMakeMS2>]]]]]",
    "Predicts MSn spectrum precursors to be singly or multiply charged depending on the ratio of intensity above and below the precursor m/z, or optionally using the \"makeMS2\" algorithm\n"
    "  <overrideExistingCharge> : always override existing charge information (default:\"true\")\n"
    "  <maxMultipleCharge> (default 3) and <minMultipleCharge> (default 2): range of values to add to the spectrum's existing \"MS_possible_charge_state\" values."
    "If these are the same values, the spectrum's MS_possible_charge_state values are removed and replaced with this single value.\n"
    "  <singleChargeFractionTIC> : is a percentage expressed as a value between 0 and 1 (the default is 0.9, or 90 percent). "
    "This is the value used as the previously mentioned ratio of intensity above and below the precursor m/z.\n" 
    "  <algorithmMakeMS2> : default is \"false\", when set to \"true\" the \"makeMS2\" algorithm is used instead of the one described above."
    };

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
        throw user_error("[SpectrumListFactory::filterCreator_ActivationType()] invalid activation type \"" + activationType + "\"");

    return SpectrumListPtr(new SpectrumList_Filter(msd.run.spectrumListPtr, 
                                                   SpectrumList_FilterPredicate_ActivationType(cvIDs, hasNot)));
}
UsageInfo usage_activation = { "<precursor_activation_type>",
    "Keeps only spectra whose precursors have the specifed activation type.  It doesn't affect non-MS spectra, and doesn't "
    "affect MS1 spectra. Use it to create output files containing only ETD or CID MSn data where both activation modes "
    "have been interleaved within a given input vendor data file (eg: Thermo's Decision Tree acquisition mode).\n"
    "   <precursor_activation_type> is any one of: ETD CID SA HCD BIRD ECD IRMPD PD PSD PQD SID or SORI."
    };

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
        throw user_error("[SpectrumListFactory::filterCreator_AnalyzerType()] invalid filter argument.");

    return SpectrumListPtr(new SpectrumList_Filter(msd.run.spectrumListPtr,
                                                   SpectrumList_FilterPredicate_AnalyzerType(cvIDs)));

}
UsageInfo usage_analyzerTypeOld = { "<analyzer>",
    "This is deprecated syntax for filtering by mass analyzer type.\n"
    "  <analyzer> can be \"FTMS\" or \"ITMS\"."
};
UsageInfo usage_analyzerType = { "<analyzer>",
    "This filter keeps only spectra with the indicated mass analyzer type. \n"
    "  <analyzer> is any one of \"quad\" \"orbi\" \"FT\" \"IT\" or \"TOF\".\n"
    "Sometimes people use the terms FT and Orbi interchangeably, which is OK "
    "because there are no hybrid FT+Orbi instruments - so this filter does too.\n"
};

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
    {
        cerr << "unknown ThresholdFilter type " << byTypeArg << endl;
        return SpectrumListPtr();
    }

    ThresholdFilter::ThresholdingOrientation orientation;
    if (orientationArg == "most-intense")
        orientation = ThresholdFilter::Orientation_MostIntense;
    else if (orientationArg == "least-intense")
        orientation = ThresholdFilter::Orientation_LeastIntense;
    else
    {
        cerr << "unknown ThresholdFilter orientation " << orientationArg << endl;
        return SpectrumListPtr();
    }

    SpectrumDataFilterPtr filter(new ThresholdFilter(byType, threshold, orientation, msLevels));
    return SpectrumListPtr(new SpectrumList_PeakFilter(msd.run.spectrumListPtr, filter));
}
UsageInfo usage_thresholdFilter={"<type> <threshold> <orientation> [<mslevels>]",
    "This filter keeps data whose values meet various threshold criteria.\n"
    "   <type> must be one of:\n"
    "      count - keep the n=<threshold> [most|least] intense data points, where n is an integer.  Any data points with the same intensity as the nth [most|least] intense data point are removed.\n"
    "      count-after-ties - like \"count\", except that any data points with the same intensity as the nth [most|least] data point are retained.\n"
    "      absolute - keep data whose absolute intensity is [more|less] than <threshold>\n"
    "      bpi-relative - keep data whose intensity is [more|less] than <threshold> percent of the base peak intensity.  Percentage is expressed as a number between 0 and 1, for example 75 percent is \"0.75\".\n"
    "      tic-relative - keep data whose individual intensities are [more|less] than <threshold> percent of the total ion current for the scan. Again, precentage is expressed as a number between 0 and 1.\n"
    "      tic-cutoff - keep the [most|least] intense data points up to <threshold> percent of the total ion current.  That is, the TIC of the retained points is <threshold> percent (expressed as a number between 0 and 1) of the original TIC.\n"
    "   <orientation> must be one of:\n"
    "      most-intense (keep m/z-intensity pairs above the threshold)\n"
    "      least-intense (keep m/z-intensity pairs below the threshold)\n"
    "   <mslevels> is an optional int_set of MS levels - if provided, only scans with those MS levels will be filtered, and others left untouched."
};

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
        throw user_error("[SpectrumListFactory::filterCreator_polarityFilter()] invalid polarity (expected \"positive\" or \"negative\")");

    return SpectrumListPtr(new SpectrumList_Filter(msd.run.spectrumListPtr, SpectrumList_FilterPredicate_Polarity(polarity)));
}
UsageInfo usage_polarity = { "<polarity>",
    "Keeps only spectra with scan of the selected <polarity>.\n"
    "   <polarity> is any one of \"positive\" \"negative\" \"+\" or \"-\"."
};

struct JumpTableEntry
{
    const char* command;
    UsageInfo &usage; // {const char *usage,const char &details}
    FilterCreator creator;
};


JumpTableEntry jumpTable_[] =
{
    {"index", usage_index, filterCreator_index},
    {"msLevel", usage_msLevel, filterCreator_msLevel},
    {"chargeState", usage_chargeState, filterCreator_chargeState},
    {"precursorRecalculation", usage_precursorRecalculation, filterCreator_precursorRecalculation},
    {"precursorRefine", usage_precursorRefine, filterCreator_precursorRefine},
    {"peakPicking", usage_nativeCentroid, filterCreator_nativeCentroid},
    {"scanNumber", usage_scanNumber, filterCreator_scanNumber},
    {"scanEvent", usage_scanEvent, filterCreator_scanEvent},
    {"scanTime", usage_scanTime, filterCreator_scanTime},
    {"sortByScanTime",usage_sortScanTime, filterCreator_sortScanTime},
    {"stripIT", usage_stripIT, filterCreator_stripIT},
    {"metadataFixer", usage_metadataFixer, filterCreator_metadataFixer},
    {"titleMaker", usage_titleMaker, filterCreator_titleMaker},
    {"threshold", usage_thresholdFilter, filterCreator_thresholdFilter},
    {"mzWindow", usage_mzWindow, filterCreator_mzWindow},
    {"mzPrecursors", usage_mzPrecursors, filterCreator_mzPrecursors},
    {"defaultArrayLength", usage_defaultArrayLength, filterCreator_defaultArrayLength},
    {"zeroSamples", usage_zeroSamples , filterCreator_ZeroSamples},
    {"mzPresent", usage_mzPresent , filterCreator_mzPresent},

    // MSn Spectrum Processing/Filtering
    {"MS2Denoise", usage_MS2Denoise , filterCreator_MS2Denoise},
    {"MS2Deisotope", usage_MS2Deisotope , filterCreator_MS2Deisotope},
    {"ETDFilter", usage_ETDFilter , filterCreator_ETDFilter},
    {"chargeStatePredictor", usage_chargeStatePredictor , filterCreator_chargeStatePredictor},
    {"activation", usage_activation , filterCreator_ActivationType},
    {"analyzer", usage_analyzerType , filterCreator_AnalyzerType},
    {"analyzerType", usage_analyzerTypeOld , filterCreator_AnalyzerType},
    {"polarity", usage_polarity , filterCreator_polarityFilter}
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
        // possibly a quoted commandline copied to a config file, 
        // eg filter=\"index [3,7]\" or filter=\"precursorRecalculation\"
        string quot;
        if (bal::starts_with(command,"\""))
            quot="\"";
        else if (bal::starts_with(command,"'"))
            quot="\'";
        if (quot.size())
        {
            command = command.substr(1);
            if (arg.size())
            {
                if  (bal::ends_with(arg,quot))
                {
                    arg    = arg.substr(0,arg.size()-1);
                }
            }
            else if (bal::ends_with(command,quot)) 
            {
                command    = command.substr(0,command.size()-1);
            }
            entry = find_if(jumpTable_, jumpTableEnd_, HasCommand(command));
        }
    }
    if (entry == jumpTableEnd_)
    {
        cerr << "[SpectrumListFactory] Ignoring wrapper: " << wrapper << endl;
        return;
    }

    SpectrumListPtr filter = entry->creator(msd, arg);
    msd.filterApplied(); // increase the filter count

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
string SpectrumListFactory::usage(bool detailedHelp,const char *morehelp_prompt)
{
    ostringstream oss;
    MSData fakemsd;

    oss << endl;

    oss << "FILTER OPTIONS" << endl;
    if (!detailedHelp)
    {
        if (morehelp_prompt)
            oss << morehelp_prompt << endl;
    }
    else
    {
        oss << endl;
        oss << "Note: Filters are applied sequentially in the order that you list them, and the sequence order\n";
        oss << "can make a large difference in your output.  In particular, the peakPicking filter must be first\n";
        oss << "in line if you wish to use the vendor-supplied centroiding algorithms since these use the vendor\n";
        oss << "DLLs, which only operate on raw untransformed data.\n\n";
        oss << "Many filters take 'int_set' arguments.  An \'int_set\' is a list of intervals of the form [a,b] or a[-][b].\n";
        oss << "For example \'[0,3]\' and \'0-3\' both mean \'the set of integers from 0 to 3 inclusive\'.\n";
        oss << "\'1-\' means \'the set of integers from 1 to the largest allowable number\'.  \n";
        oss << "\'9\' is also an integer set, equivalent to \'[9,9]\'.\n";
        oss << "\'[0,2] 5-7\' is the set \'0 1 2 5 6 7\'. \n";
    }

    for (JumpTableEntry* it=jumpTable_; it!=jumpTableEnd_; ++it)
    {
        if (detailedHelp)
            oss << it->command << " " << it->usage[0] << endl << it->usage[1] << endl << endl ;
        else
            oss << it->command << " " << it->usage[0] << endl ;
    }

    oss << endl;

    // tidy up the word wrap
    std::string str = oss.str();
    const size_t wrap = 70; // wrap at 70 columns
    size_t lastPos = 0;
    for (size_t curPos = wrap ; curPos < str.length(); ) 
    {
        std::string::size_type newlinePos = str.rfind( '\n', curPos );
        if( newlinePos == std::string::npos || (newlinePos <= lastPos))
        {   // no newline within next wrap chars, add one
            std::string::size_type spacePos = str.rfind( ' ', curPos );
            lastPos = curPos;
            if( spacePos == std::string::npos )
            {
                curPos++; // no spaces, go long
            }
            else 
            {
                str[ spacePos ] = '\n';
                curPos = spacePos + wrap + 1;
            }
        } 
        else 
        {
            lastPos = curPos;
            curPos = newlinePos + wrap + 1;
        }
    }

    return str;
}


} // namespace analysis 
} // namespace pwiz


