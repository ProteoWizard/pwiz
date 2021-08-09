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
#include "pwiz/analysis/spectrum_processing/SpectrumList_ScanSummer.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_PeakPicker.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_Smoother.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_PeakFilter.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_ChargeStateCalculator.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_ChargeFromIsotope.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_PrecursorRecalculator.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_PrecursorRefine.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_MZWindow.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_MZRefiner.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_LockmassRefiner.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_MetadataFixer.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_TitleMaker.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_Demux.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_DiaUmpire.hpp"
#include "pwiz/analysis/spectrum_processing/PrecursorMassFilter.hpp"
#include "pwiz/analysis/spectrum_processing/ThresholdFilter.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_ZeroSamplesFilter.hpp"
#include "pwiz/analysis/spectrum_processing/MS2NoiseFilter.hpp"
#include "pwiz/analysis/spectrum_processing/MS2Deisotoper.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"

namespace pwiz {
namespace analysis {


using namespace pwiz::cv;
using namespace pwiz::msdata;
using namespace pwiz::util;

namespace {


inline const char* cppTypeToNaturalLanguage(short T) { return "a small integer"; }
inline const char* cppTypeToNaturalLanguage(int T) { return "an integer"; }
inline const char* cppTypeToNaturalLanguage(long T) { return "an integer"; }
inline const char* cppTypeToNaturalLanguage(unsigned short T) { return "a small positive integer"; }
inline const char* cppTypeToNaturalLanguage(unsigned int T) { return "a positive integer"; }
inline const char* cppTypeToNaturalLanguage(unsigned long T) { return "a positive integer"; }
inline const char* cppTypeToNaturalLanguage(float T) { return "a real number"; }
inline const char* cppTypeToNaturalLanguage(double T) { return "a real number"; }
inline const char* cppTypeToNaturalLanguage(bool T) { return "true or false"; }
inline const char* cppTypeToNaturalLanguage(const string& T) { return "some text"; }
inline const char* cppTypeToNaturalLanguage(const MZTolerance& T) { return "a mass tolerance (e.g. '10 ppm', '2.5 Da')"; }
inline const char* cppTypeToNaturalLanguage(const SpectrumList_Filter::Predicate::FilterMode& T) { return "a filter mode ('include' or 'exclude')"; }

struct LocaleBool {
    bool data;
    LocaleBool() : data(true) {}
    LocaleBool(bool data) : data(data) {}
    operator bool() const { return data; }
    friend std::ostream & operator << (std::ostream &out, LocaleBool b) {
        out << std::boolalpha << b.data;
        return out;
    }
    friend std::istream & operator >> (std::istream &in, LocaleBool &b) {
        in >> std::boolalpha >> b.data;
        return in;
    }
};

/// parses a lexical-castable key=value pair from a string of arguments which may also include non-key-value strings;
/// if the key is not in the argument string, defaultValue is returned;
/// if bad_lexical_cast is thrown, it is converted into a sensible error message
template <typename ArgT, int tokensInValue = 1>
ArgT parseKeyValuePair(string& args, const string& tokenName, const ArgT& defaultValue)
{
    try
    {
        size_t keyIndex = args.rfind(tokenName);
        if (keyIndex != string::npos)
        {
            size_t valueIndex = keyIndex + tokenName.length();
            if (valueIndex < args.length())
            {
                string valueStr;
                size_t nextTokenIndex = args.find(" ", valueIndex);
                for (int i = tokensInValue; i >= 0; --i)
                {
                    try
                    {
                        valueStr = args.substr(valueIndex, nextTokenIndex - valueIndex);
                        ArgT value = lexical_cast<ArgT>(valueStr);
                        args.erase(keyIndex, nextTokenIndex - keyIndex);
                        return value;
                    }
                    catch (exception&)
                    {
                        nextTokenIndex = args.find(" ", nextTokenIndex+1);
                        if (i > 0)
                            continue;
                        throw runtime_error("error parsing \"" + valueStr + "\" as value for \"" + tokenName + "\"; expected " + cppTypeToNaturalLanguage(defaultValue));
                    }
                }
            }
        }
        return defaultValue;
    }
    catch (exception& e)
    {
        throw runtime_error(string("[parseKeyValuePair] ") + e.what());
    }
}

/// parses a lexical-castable key=value pair from a string of arguments which may also include non-key-value strings;
/// if the key is not in the argument string or if bad_lexical_cast is thrown, a default-constructed ArgT is returned
template <typename ArgT>
ArgT parseKeyValuePair(string& args, const string& tokenName)
{
    return parseKeyValuePair<ArgT>(args, tokenName, ArgT());
}


//
// each SpectrumListWrapper has a filterCreator_* function, 
// and an entry in the jump table below
//


typedef SpectrumListPtr (*FilterCreator)(const MSData& msd, const string& arg, pwiz::util::IterationListenerRegistry* ilr);
typedef const char *UsageInfo[2];  // usage like <int_set>, and details

SpectrumListPtr filterCreator_index(const MSData& msd, const string& arg, pwiz::util::IterationListenerRegistry* ilr)
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

SpectrumListPtr filterCreator_scanNumber(const MSData& msd, const string& arg, pwiz::util::IterationListenerRegistry* ilr)
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

SpectrumListPtr filterCreator_id(const MSData& msd, const string& arg, pwiz::util::IterationListenerRegistry* ilr)
{
    vector<string> idSet;
    bal::split(idSet, arg, bal::is_any_of(";"));
    for (auto& id : idSet) bal::trim(id);

    return SpectrumListPtr(new
        SpectrumList_Filter(msd.run.spectrumListPtr,
            SpectrumList_FilterPredicate_IdSet(set<string>(idSet.begin(), idSet.end()))));
}
UsageInfo usage_id = { "<id_set>",
"Selects one or more spectra by native IDs separated by semicolon (;).\n"
"  <id_set> is a semicolon-delimited set of ids."
};

SpectrumListPtr filterCreator_scanEvent(const MSData& msd, const string& arg, pwiz::util::IterationListenerRegistry* ilr)
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

SpectrumListPtr filterCreator_scanTime(const MSData& msd, const string& arg, pwiz::util::IterationListenerRegistry* ilr)
{
    double scanTimeLow = 0;
    double scanTimeHigh = 0;
    LocaleBool assumeSorted = true;

    istringstream iss(arg);
    char open='\0', comma='\0', close='\0';
    iss >> open >> scanTimeLow >> comma >> scanTimeHigh >> close;
    
    if (iss.good())
        iss >> assumeSorted;
    cout << assumeSorted << endl;
    if (open!='[' || comma!=',' || close!=']')
    {
        cerr << "scanTime filter argument does not have form \"[\"<startTime>,<endTime>\"]\", ignored." << endl;
        return SpectrumListPtr();
    }

    return SpectrumListPtr(new
        SpectrumList_Filter(msd.run.spectrumListPtr, 
                            SpectrumList_FilterPredicate_ScanTimeRange(scanTimeLow, scanTimeHigh, assumeSorted)));
}
UsageInfo usage_scanTime = {"<scan_time_range>",
    "This filter selects only spectra within a given time range.\n"
    "  <scan_time_range> is a time range, specified in seconds.  For example, to select only spectra within the "
    "second minute of the run, use \"scanTime [60-119.99]\"."};

SpectrumListPtr filterCreator_sortScanTime(const MSData& msd, const string& arg, pwiz::util::IterationListenerRegistry* ilr)
{
    return SpectrumListPtr(new SpectrumList_Sorter(msd.run.spectrumListPtr,
                                                   SpectrumList_SorterPredicate_ScanStartTime()));
}
UsageInfo usage_sortScanTime = {"","This filter reorders spectra, sorting them by ascending scan start time."};

SpectrumListPtr filterCreator_scanSummer(const MSData& msd, const string& carg, pwiz::util::IterationListenerRegistry* ilr)
{
    string arg = carg;
    double precursorTol = parseKeyValuePair<double>(arg, "precursorTol=", 0.05); // m/z
    double scanTimeTol = parseKeyValuePair<double>(arg, "scanTimeTol=", 10); // seconds
    double ionMobilityTol = parseKeyValuePair<double>(arg, "ionMobilityTol=", 0.01); // ms for drift time or vs/cm^2 for TIMS
    bool sumMs1 = parseKeyValuePair<bool>(arg, "sumMs1", false);
    if (bal::icontains(arg, "="))
        throw user_error("[SpectrumList_ScanSummer] unused argument (key=value) in " + arg + "; supported arguments are precursorTol, scanTimeTol, ionMobilityTol, and sumMs1");

    return SpectrumListPtr(new SpectrumList_ScanSummer(msd.run.spectrumListPtr, precursorTol, scanTimeTol, ionMobilityTol, sumMs1, ilr));
}
UsageInfo usage_scanSummer = {"[precursorTol=<precursor tolerance>] [scanTimeTol=<scan time tolerance in seconds>] [ionMobilityTol=<ion mobility tolerance>]",
    "This filter sums MS2 sub-scans whose precursors are within <precursor tolerance> (default: 0.05 m/z)"
    ", <scan time tolerance> (default: 10 s), and for ion mobility data, <ion mobility tolerance> (default 0.01 ms or vs/cm^2). It is intended for some Waters DDA data and Bruker PASEF data, where sub-scans " 
    "should be summed together to increase the SNR."};

SpectrumListPtr filterCreator_nativeCentroid(const MSData& msd, const string& arg, pwiz::util::IterationListenerRegistry* ilr)
{

    // by default assume we are using the low-quality localMax code unless explicitly told otherwise

    // default values for CWT
    bool preferVendor = false;
    bool preferCwt = false; 
    double mzTol = 0.1; // default value, minimum spacing between peaks
    double minSnr = 1.0; // for the cwt algorithm, default value is 1.01
    bool centroid = false; // for the cwt algorithm, whether to centroid fitted peaks
    int fixedPeaksKeep = 0; // this will always be set to zero, except from charge determination algorithm

    string msLevelSets = "1-"; // peak-pick all MS levels by default

    istringstream parser(arg);
    string pickerType;
    parser >> pickerType;
    string nextStr;

    bool backwardsCompatible = pickerType == "true" || pickerType == "false";

    if ( !pickerType.empty() && !backwardsCompatible )
    {

        preferVendor = boost::iequals(pickerType,"vendor");
        preferCwt = boost::iequals(pickerType,"cwt");

        while ( parser >> nextStr )
        {

            if ( string::npos == nextStr.rfind('=') )
                throw user_error("[SpectrumList_PeakPicker] = sign required after keyword argument");

            string keyword = nextStr.substr(0,nextStr.rfind('='));
            string paramVal = nextStr.substr(nextStr.rfind('=')+1);

            if ( boost::iequals(keyword,"snr") )
            {
                minSnr = parseKeyValuePair<double>(nextStr, "snr=", 1.0);
                if ( minSnr < 0 ) { throw user_error("[SpectrumList_PeakPicker] snr must be greater than or equal to zero."); }
            }
            else if ( boost::iequals(keyword,"peakSpace") ) 
            {
                mzTol = parseKeyValuePair<double>(nextStr, "peakSpace=");
                if ( mzTol < 0 ) { throw user_error("[SpectrumList_PeakPicker] peakSpace must be greater than or equal to zero."); }
            }
            else if ( boost::iequals(keyword,"centroid") ) 
            {
                centroid = parseKeyValuePair<bool>(nextStr, "centroid=", false);
            }
            else if ( boost::iequals(keyword,"msLevel") )
            {
                msLevelSets = paramVal;
            }
            else
            {
                throw user_error("[SpectrumList_PeakPicker] Invalid keyword argument.");
            }

        }

        // give the user some feedback
        if ( preferCwt & preferVendor )
        {
            throw user_error("[SpectrumList_PeakPicker] Cannot request both cwt and vendor peak-picking.");
        }
        else if ( preferCwt )
        {
            //TODO: Give user some feedback for the applied parameters
            //cout << "---CantWaiT Parameters---" << endl;
            //cout << "minimum SNR: " << minSnr << endl;
            //cout << "minimum peak spacing: " << mzTol << endl;
            //cout << "msLevels: " << msLevelSets << endl << endl;
        }
        else if ( preferVendor )
        {
            //cout << "Applying vendor peak-picking" << endl;
        }
        else
        {
            //cout << "WARNING: applying simple local maxima peak-picking. Specify cwt or vendor for high-quality processing." << endl;
        }

    }
    else
    {
        preferVendor = lexical_cast<bool>(pickerType);
        parser >> msLevelSets;
    }

    IntegerSet msLevelsToCentroid;
    msLevelsToCentroid.parse(msLevelSets);

    if (preferVendor && msd.countFiltersApplied())
        cerr << "[SpectrumList_PeakPicker] Warning: vendor peakPicking requested, but peakPicking is not the first filter.  Since the vendor DLLs can only operate directly on raw data, this filter will likely not have any effect." << endl;

    if (preferCwt)
    {
        return SpectrumListPtr(new 
            SpectrumList_PeakPicker(msd.run.spectrumListPtr,
                                    PeakDetectorPtr(new CwtPeakDetector(minSnr,fixedPeaksKeep,mzTol,centroid)),
                                    preferVendor,
                                    msLevelsToCentroid));
    }
    else
    {
        return SpectrumListPtr(new 
            SpectrumList_PeakPicker(msd.run.spectrumListPtr,
                                    PeakDetectorPtr(new LocalMaximumPeakDetector(3)),
                                    preferVendor,
                                    msLevelsToCentroid));
    }
}
UsageInfo usage_nativeCentroid = {"[<PickerType> [snr=<minimum signal-to-noise ratio>] [peakSpace=<minimum peak spacing>] [msLevel=<ms_levels>]]","This filter performs centroiding on spectra"
    "with the selected <ms_levels>, expressed as an int_set.  The value for <PickerType> must be \"cwt\" or \"vendor\": when <PickerType> = "
    "\"vendor\", vendor (Windows DLL) code is used if available.  IMPORTANT NOTE: since this filter operates on the raw "
    "data through the vendor DLLs, IT MUST BE THE FIRST FILTER IN ANY LIST OF FILTERS when \"vendor\" is used. "
    "The other option for PickerType is \"cwt\", which uses ProteoWizard's wavelet-based algorithm for performing peak-picking with a "
    "wavelet-space signal-to-noise ratio of <signal-to-noise ratio>.\n" 
    "Defaults:\n "
    "<PickerType> is a low-quality (non-vendor) local maxima algorithm\n "
    "<signal-to-noise ratio> = 1.0\n "
    "<minimum peak spacing> = 0.1\n "
    "<ms_levels> = 1-\n "

};

/**
 *  Handler for --filter zeroSamples removeExtra|addMissing[=FlankingZeroCount] [mslevels]
 *
 **/
SpectrumListPtr filterCreator_ZeroSamples(const MSData& msd, const string& arg, pwiz::util::IterationListenerRegistry* ilr)
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
    getlinePortable(parser, msLevelSets);
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
SpectrumListPtr filterCreator_ETDFilter(const MSData& msd, const string& arg, pwiz::util::IterationListenerRegistry* ilr)
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
    "  <removePrecursor> - if \"true\", remove unreacted precursor (default is \"true\")\n"
    "  <removeChargeReduced> - if \"true\", remove charge reduced precursor (default is \"true\")\n"
    "  <removeNeutralLoss> - if \"true\", remove neutral loss species from charge reduced precursor (default is \"true\")\n"
    "  <blanketRemoval> - if \"true\", remove neutral losses in a charge-scaled 60 Da swath rather than only around known loss species (default is \"true\")\n"
    "  <matchingTolerance> - specify matching tolerance in m/z or ppm (examples: \"3.1 mz\" (the default) or \"2.2 ppm\")"
};

SpectrumListPtr filterCreator_MS2Denoise(const MSData& msd, const string& arg, pwiz::util::IterationListenerRegistry* ilr)
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

SpectrumListPtr filterCreator_MS2Deisotope(const MSData& msd, const string& arg, pwiz::util::IterationListenerRegistry* ilr)
{
    istringstream parser(arg);

    bool hires = false;
    string buf;
    parser >> buf;

    MZTolerance mzt(hires? 0.01 : 0.5);

    bool poisson = false;
    int maxCharge = 3, minCharge = 1;

    if ( !buf.empty() )
    {
        if ( boost::iequals(buf,"Poisson") )
            poisson = true;
        else if ( boost::iequals(buf,"hi_res") )
            hires = true;
        else
            throw user_error("[filterCreator_MS2Deisotope] Invalid keyword entered.");


        int whileLoopCnt = 0;

        while ( parser >> buf )
        {

            bool backwardsCompatible = false;
            if ( hires && whileLoopCnt == 0 )
            {
                bool changeBool = true;
                try { lexical_cast<double>(buf); }
                catch (...) { changeBool = false; }
                if (changeBool) backwardsCompatible = true;
            }

            if (backwardsCompatible)
            {
                mzt = lexical_cast<double>(buf);
                break;
            }

            if ( string::npos == buf.rfind('=') )
                throw user_error("[filterCreator_MS2Deisotope] = sign required after keyword argument");

            string keyword = buf.substr(0,buf.rfind('='));
            string paramVal = buf.substr(buf.rfind('=')+1);

            if ( boost::iequals(keyword,"minCharge") )
            {
                try { lexical_cast<int>(paramVal); }
                catch (...) { throw user_error("[filterCreator_MS2Deisotope] An integer must follow the minCharge argument."); }
                minCharge = lexical_cast<int>(paramVal);
                if ( minCharge < 0 ) { throw user_error("[filterCreator_MS2Deisotope] minCharge must be a positive integer."); }
            }
            else if ( boost::iequals(keyword,"maxCharge") )
            {
                try { lexical_cast<int>(paramVal); }
                catch (...) { throw user_error("[filterCreator_MS2Deisotope] An integer must follow the maxCharge argument."); }
                maxCharge = lexical_cast<int>(paramVal);    
                if ( maxCharge < 0 ) { throw user_error("[filterCreator_MS2Deisotope] maxCharge must be a positive integer."); }
            }
            else if ( boost::iequals(keyword,"mzTol") )
            {
                try { lexical_cast<double>(paramVal); }
                catch (...) { throw user_error("[filterCreator_MS2Deisotope] A numeric value must follow the mzTol keyword."); }
                if ( lexical_cast<double>(paramVal) < 0 ) { throw user_error("[filterCreator_MS2Deisotope] mzTol must be a positive."); }
                mzt = lexical_cast<double>(paramVal);    
            }
            else
            {
                throw user_error("[filterCreator_MS2Deisotope] Invalid keyword entered.");
            }

            whileLoopCnt++;

        }

        // sanity check
        if ( minCharge > maxCharge)
            throw user_error("[filterCreator_MS2Deisotope] minCharge must be less than maxCharge.");

    }

    SpectrumDataFilterPtr filter = SpectrumDataFilterPtr(new MS2Deisotoper(MS2Deisotoper::Config(mzt, hires, poisson, maxCharge, minCharge)));
    return SpectrumListPtr(new 
            SpectrumList_PeakFilter(msd.run.spectrumListPtr,
                                   filter));
}
UsageInfo usage_MS2Deisotope = { "[hi_res [mzTol=<mzTol>]] [Poisson [minCharge=<minCharge>] [maxCharge=<maxCharge>]]",
    "Deisotopes ms2 spectra using the Markey method or a Poisson model.\n"
    "  For the Markey method, hi_res sets high resolution mode to \"false\" (the default) or \"true\".\n"
    "  <mzTol> sets the mz tolerance.  It defaults to .01 in high resoltion mode, otherwise it defaults to 0.5.\n"
    "  Poisson activates a Poisson model based on the relative intensity distribution.\n"
    "  <minCharge> (default: 1) and <maxCharge> (default: 3) define the charge search range within the Poisson deisotoper. (default: 1)"
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

    virtual string describe() const { return "stripping ion trap MS1s"; }
};

SpectrumListPtr filterCreator_stripIT(const MSData& msd, const string& arg, pwiz::util::IterationListenerRegistry* ilr)
{
    return SpectrumListPtr(new 
        SpectrumList_Filter(msd.run.spectrumListPtr, StripIonTrapSurveyScans(), ilr));
}
UsageInfo usage_stripIT={"","This filter rejects ion trap data spectra with MS level 1."};


SpectrumListPtr filterCreator_precursorRecalculation(const MSData& msd, const string& arg, pwiz::util::IterationListenerRegistry* ilr)
{
    return SpectrumListPtr(new SpectrumList_PrecursorRecalculator(msd));
}
UsageInfo usage_precursorRecalculation = {"","This filter recalculates the precursor m/z and charge for MS2 spectra. "
    "It looks at the prior MS1 scan to better infer the parent mass.  However, it only works on orbitrap and FT data,"
    "although it does not use any 3rd party (vendor DLL) code.  Since the time the code was written, Thermo has since fixed "
    "up its own estimation in response, so it's less critical than it used to be (though can still be useful)."};

SpectrumListPtr filterCreator_mzRefine(const MSData& msd, const string& arg, pwiz::util::IterationListenerRegistry* ilr)
{
    // Example string:
    // "mzRefiner input1.pepXML input2.mzid msLevels=1- thresholdScore=specEValue thresholdValue=1e-10 thresholdStep=10 maxSteps=3"

    istringstream parser(arg);
    // expand the filenames by globbing to handle wildcards
    vector<bfs::path> globbedFilenames;
    string thresholdCV = "MS-GF:SpecEValue"; // Remove this default?
    string thresholdSet = "-1e-10";          // Remove this default?
    double thresholdStep = 0.0;
    int maxSteps = 0;
    bool assumeHighRes = false;
    string msLevelSets = "1-";

    string nextStr;
    while (parser >> nextStr)
    {

        if (nextStr.rfind('=') == string::npos)
        {
            // Add to ident file list, and check for existence?
            // Remove quotes that may be used to encapsulate a path with spaces
            if ((bal::starts_with(nextStr, "\'") && bal::ends_with(nextStr, "\'")) || (bal::starts_with(nextStr, "\"") && bal::ends_with(nextStr, "\"")))
            {
                // Remove the first and last characters
                nextStr = nextStr.substr(1, nextStr.length() - 2);
            }
            // expand the filenames by globbing to handle wildcards
            if (expand_pathmask(bfs::path(nextStr), globbedFilenames) == 0)
                cout << "[mzRefiner] no files found matching \"" << nextStr << "\"" << endl;
        }
        else
        {
            string keyword = nextStr.substr(0, nextStr.rfind('='));
            string paramVal = nextStr.substr(nextStr.rfind('=') + 1);
            if (keyword == "msLevels")
            {
                // use an IntegerSet
                msLevelSets = paramVal;
            }
            else if (keyword == "thresholdScore")
            {
                thresholdCV = paramVal;
            }
            else if (keyword == "thresholdValue")
            {
                thresholdSet = paramVal;
            }
            else if (keyword == "thresholdStep")
            {
                thresholdStep = boost::lexical_cast<double>(paramVal);
            }
            else if (keyword == "maxSteps")
            {
                maxSteps = boost::lexical_cast<int>(paramVal);
            }
            else if (keyword == "assumeHighRes")
            {
                assumeHighRes = boost::lexical_cast<bool>(paramVal);
            }
        }
    }
    // expand the filenames by globbing to handle wildcards
    vector<string> files;
    for(const bfs::path& filename : globbedFilenames)
        files.push_back(filename.string());

    IntegerSet msLevelsToRefine;
    msLevelsToRefine.parse(msLevelSets);

    string identFilePath = "";

    vector<string> possibleDataFiles;
    string lastSourceFile;

    for(const SourceFilePtr& sf : msd.fileDescription.sourceFilePtrs)
    {
        lastSourceFile = sf->name;
    }

    lastSourceFile = lastSourceFile.substr(0, lastSourceFile.rfind(".gz")); // remove a ".gz", if there is one
    lastSourceFile = lastSourceFile.substr(0, lastSourceFile.rfind(".")); // remove the extension
    possibleDataFiles.push_back(lastSourceFile);
    possibleDataFiles.push_back(msd.run.id);

    // Search for a file that matches the MSData file name, then search for one matching the dataset if not found.
    // Load identfiles, and look at mzid.DataCollection.Inputs.SpectraData.name?
    for(string &dataFile : possibleDataFiles)
    {
        for(string &file : files)
        {
            if (file.find(dataFile) != string::npos)
            {
                identFilePath = file;
                break;
            }
        }
        if (identFilePath != "")
        {
            break;
        }
    }

    return SpectrumListPtr(new SpectrumList_MZRefiner(msd, identFilePath, thresholdCV, thresholdSet, msLevelsToRefine, thresholdStep, maxSteps, assumeHighRes, ilr));
}
UsageInfo usage_mzRefine = { "input1.pepXML input2.mzid [msLevels=<1->] [thresholdScore=<CV_Score_Name>] [thresholdValue=<floatset>] [thresholdStep=<float>] [maxSteps=<count>]", "This filter recalculates the m/z and charges, adjusting precursors for MS2 spectra and spectra masses for MS1 spectra. "
"It uses an ident file with a threshold field and value to calculate the error and will then choose a shifting mechanism to correct masses throughout the file. "
"It only works on orbitrap, FT, and TOF data. It is designed to work on mzML files created by msconvert from a single dataset (single run), and with an identification file created using that mzML file. "
"It does not use any 3rd party (vendor DLL) code. "
"Recommended Scores and thresholds: MS-GF:SpecEValue,-1e-10 (<1e-10); MyriMatch:MVH,35- (>35); xcorr,3- (>3)" };

SpectrumListPtr filterCreator_lockmassRefiner(const MSData& msd, const string& carg, pwiz::util::IterationListenerRegistry* ilr)
{
    const string mzToken("mz=");
    const string mzNegIonsToken("mzNegIons=");
    const string toleranceToken("tol=");

    string arg = carg;
    double lockmassMz = parseKeyValuePair<double>(arg, mzToken, 0);
    double lockmassMzNegIons = parseKeyValuePair<double>(arg, mzNegIonsToken, 0); // Optional
    double lockmassTolerance = parseKeyValuePair<double>(arg, toleranceToken, 1.0);
    bal::trim(arg);
    if (!arg.empty())
        throw runtime_error("[lockmassRefiner] unhandled text remaining in argument string: \"" + arg + "\"");

    if ((lockmassMz <= 0 && lockmassMzNegIons <= 0) || lockmassTolerance <= 0)
    {
        cerr << "lockmassMz and lockmassTolerance must be positive real numbers" << endl;
        return SpectrumListPtr();
    }
    else if (lockmassMzNegIons <= 0)
        lockmassMzNegIons = lockmassMz;

    return SpectrumListPtr(new SpectrumList_LockmassRefiner(msd.run.spectrumListPtr, lockmassMz, lockmassMzNegIons, lockmassTolerance));
}
UsageInfo usage_lockmassRefiner = { "mz=<real> mzNegIons=<real (mz)> tol=<real (1.0 Daltons)>", "For Waters data, adjusts m/z values according to the specified lockmass m/z and tolerance. Distinct m/z value for negative ions is optional and defaults to the given mz value. For other data, currently does nothing." };

SpectrumListPtr filterCreator_demux(const MSData& msd, const string& carg, pwiz::util::IterationListenerRegistry* ilr)
{
    string arg = carg;

    const SpectrumList_Demux::Params k_defaultDemuxParams;
    SpectrumList_Demux::Params demuxParams;
    demuxParams.massError = parseKeyValuePair<MZTolerance>(arg, "massError=", k_defaultDemuxParams.massError);
    demuxParams.nnlsMaxIter = (int)parseKeyValuePair<unsigned int>(arg, "nnlsMaxIter=", k_defaultDemuxParams.nnlsMaxIter);
    demuxParams.nnlsEps = parseKeyValuePair<double>(arg, "nnlsEps=", k_defaultDemuxParams.nnlsEps);
    demuxParams.applyWeighting = !parseKeyValuePair<LocaleBool>(arg, "noWeighting=", !k_defaultDemuxParams.applyWeighting);
    demuxParams.demuxBlockExtra = parseKeyValuePair<double>(arg, "demuxBlockExtra=", k_defaultDemuxParams.demuxBlockExtra);
    demuxParams.variableFill = parseKeyValuePair<LocaleBool>(arg, "variableFill=", k_defaultDemuxParams.variableFill);
    demuxParams.regularizeSums = !parseKeyValuePair<LocaleBool>(arg, "noSumNormalize=", !k_defaultDemuxParams.regularizeSums);
    string optimization = parseKeyValuePair<string>(arg, "optimization=", "none");
    demuxParams.interpolateRetentionTime = parseKeyValuePair<LocaleBool>(arg, "interpolateRT=", k_defaultDemuxParams.interpolateRetentionTime);
    demuxParams.minimumWindowSize = parseKeyValuePair<double>(arg, "minWindowSize=", k_defaultDemuxParams.minimumWindowSize);
    bal::trim(arg);
    if (!arg.empty())
        throw runtime_error("[demultiplex] unhandled text remaining in argument string: \"" + arg + "\"");

    if (demuxParams.massError.value <= 0 ||
        demuxParams.nnlsEps <= 0 ||
        demuxParams.demuxBlockExtra < 0)
    {
        cerr << "massError, nnlsEps must be positive real numbers; demuxBlockExtra must be a positive real number or 0" << endl;
        return SpectrumListPtr();
    }

    demuxParams.optimization = SpectrumList_Demux::Params::stringToOptimization(optimization);

    return SpectrumListPtr(new SpectrumList_Demux(msd.run.spectrumListPtr, demuxParams));
}
UsageInfo usage_demux = { 
    "massError=<tolerance and units, eg 0.5Da (default 10ppm)>"
    " nnlsMaxIter=<int (50)> nnlsEps=<real (1e-10)>"
    " noWeighting=<bool (false)>"
    " demuxBlockExtra=<real (0)>"
    " variableFill=<bool (false)>"
    " noSumNormalize=<bool (false)>"
    " optimization=<(none)|overlap_only>"
    " interpolateRT=<bool (true)>"
    " minWindowSize=<real (0.2)>",
    "Separates overlapping or MSX multiplexed spectra into several demultiplexed spectra by inferring from adjacent multiplexed spectra. Optionally handles variable fill times (for Thermo)." };

SpectrumListPtr filterCreator_diaUmpire(const MSData& msd, const string& carg, pwiz::util::IterationListenerRegistry* ilr)
{
    string arg = carg;

    string paramsFilepath = parseKeyValuePair<string>(arg, "params=", "");
    if (!bfs::exists(paramsFilepath))
        throw user_error("[diaUmpire] params filepath is required (params=path/to/diaumpire.params)");

    bal::trim(arg);
    if (!arg.empty())
        throw runtime_error("[demultiplex] unhandled text remaining in argument string: \"" + arg + "\"");

    return SpectrumListPtr(new SpectrumList_DiaUmpire(msd, msd.run.spectrumListPtr, DiaUmpire::Config(paramsFilepath), ilr));
}
UsageInfo usage_diaUmpire = {
    "params=<filepath to DiaUmpire .params file>",
    "Separates DIA spectra into pseudo-DDA spectra using the DIA Umpire algorithm." };

SpectrumListPtr filterCreator_precursorRefine(const MSData& msd, const string& arg, pwiz::util::IterationListenerRegistry* ilr)
{
    return SpectrumListPtr(new SpectrumList_PrecursorRefine(msd));
}
UsageInfo usage_precursorRefine = {"", "This filter recalculates the precursor m/z and charge for MS2 spectra. "
    "It looks at the prior MS1 scan to better infer the parent mass.  It only works on orbitrap, FT, and TOF data. "
    "It does not use any 3rd party (vendor DLL) code."};

SpectrumListPtr filterCreator_mzWindow(const MSData& msd, const string& arg, pwiz::util::IterationListenerRegistry* ilr)
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

SpectrumListPtr filterCreator_mzPrecursors(const MSData& msd, const string& carg, pwiz::util::IterationListenerRegistry* ilr)
{
    string arg = carg;

    auto mzTol = parseKeyValuePair<MZTolerance, 2>(arg, "mzTol=", MZTolerance(10, MZTolerance::PPM));
    auto targetStr = parseKeyValuePair<string>(arg, "target=", "selected");
    auto mode = parseKeyValuePair<SpectrumList_Filter::Predicate::FilterMode>(arg, "mode=", SpectrumList_Filter::Predicate::FilterMode_Include);

    auto target = SpectrumList_FilterPredicate_PrecursorMzSet::TargetMode_Selected;
    if (targetStr == "isolated")
        target = SpectrumList_FilterPredicate_PrecursorMzSet::TargetMode_Isolated;
    else if (targetStr != "selected")
        throw user_error("[SpectrumListFactory::filterCreator_mzPrecursors()] invalid value for 'target' parameter: " + targetStr);

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
        throw user_error("[SpectrumListFactory::filterCreator_mzPrecursors()] expected a list of m/z values formatted like \"[123.4,567.8,789.0]\"");

    return SpectrumListPtr(new
        SpectrumList_Filter(msd.run.spectrumListPtr,
                            SpectrumList_FilterPredicate_PrecursorMzSet(setMz, mzTol, mode, target), ilr));
}
UsageInfo usage_mzPrecursors = {"<precursor_mz_list> [mzTol=<mzTol (10 ppm)>] [target=<selected|isolated> (selected)] [mode=<include|exclude (include)>]",
    "Filters spectra based on precursor m/z values found in the <precursor_mz_list>, with <mzTol> m/z tolerance. To retain "
    "only spectra with precursor m/z values of 123.4 and 567.8, use --filter \"mzPrecursors [123.4,567.8]\". "
    "Note that this filter will drop MS1 scans unless you include 0.0 in the list of precursor values."
    "   <mzTol> is optional and must be specified as a number and units (PPM or MZ). For example, \"5 PPM\" or \"2.1 MZ\".\n"
    "   <target> is optional and must be either \"selected\" (the default) or \"isolated\". It determines whether the isolated m/z or the selected m/z is used for the \"precursor m/z\"\n"
    "   <mode> is optional and must be either \"include\" (the default) or \"exclude\".  If \"exclude\" is "
    "used, the filter drops spectra that match the various criteria instead of keeping them."
    };

SpectrumListPtr filterCreator_msLevel(const MSData& msd, const string& arg, pwiz::util::IterationListenerRegistry* ilr)
{
    IntegerSet msLevelSet;
    msLevelSet.parse(arg);

    return SpectrumListPtr(new 
        SpectrumList_Filter(msd.run.spectrumListPtr, 
                            SpectrumList_FilterPredicate_MSLevelSet(msLevelSet), ilr));
}
UsageInfo usage_msLevel = {"<mslevels>",
    "This filter selects only spectra with the indicated <mslevels>, expressed as an int_set."}; 


SpectrumListPtr filterCreator_mzPresent(const MSData& msd, const string& carg, pwiz::util::IterationListenerRegistry* ilr)
{
    string arg = carg;

    auto mzTol = parseKeyValuePair<MZTolerance, 2>(arg, "mzTol=", MZTolerance(0.5, MZTolerance::MZ));
    auto mode = parseKeyValuePair<SpectrumList_Filter::Predicate::FilterMode>(arg, "mode=", SpectrumList_Filter::Predicate::FilterMode_Include);
    string byTypeArg = parseKeyValuePair<string>(arg, "type=", "count");
    double threshold = parseKeyValuePair<double>(arg, "threshold=", 10000.0);
    string orientationArg = parseKeyValuePair<string>(arg, "orientation=", "most-intense");

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
        throw user_error("[SpectrumListFactory::filterCreator_mzPresent()] invalid 'type' parameter (expected 'count', 'count-after-ties', 'absolute', 'tic-relative', 'bpi-relative', or 'tic-cutoff')");

    ThresholdFilter::ThresholdingOrientation orientation;
    if (orientationArg == "most-intense")
        orientation = ThresholdFilter::Orientation_MostIntense;
    else if (orientationArg == "least-intense")
        orientation = ThresholdFilter::Orientation_LeastIntense;
    else
        throw user_error("[SpectrumListFactory::filterCreator_mzPresent()] invalid 'orientation' parameter (expected \"most-intense\" or \"least-intense\")");


    char open='\0', comma='\0', close='\0';
    std::set<double> setMz;

    istringstream parser(bal::trim_copy(arg));
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

    if (open!='[' || close!=']')
       throw user_error("[SpectrumListFactory::filterCreator_mzPresent()] mzPresent filter expected a list of mz values like \"[100,200,300.4]\"");

    return SpectrumListPtr(new
        SpectrumList_Filter(msd.run.spectrumListPtr,
                        SpectrumList_FilterPredicate_MzPresent(mzTol, setMz, ThresholdFilter(byType, threshold, orientation), mode), ilr));
}
UsageInfo usage_mzPresent = {"<mz_list> [mzTol=<tolerance> (0.5 mz)] [type=<type> (count)] [threshold=<threshold> (10000)] [orientation=<orientation> (most-intense)] [mode=<include|exclude (include)>]",
    "This filter includes or excludes spectra depending on whether the specified peaks are present.\n"
    "   <mz_list> is a list of mz values of the form [mz1,mz2, ... mzn] (for example, \"[100, 300, 405.6]\"). "
    "Spectra which contain peaks within <tolerance> of any of these values will be kept.\n"
    "   <tolerance> is specified as a number and units (PPM or MZ). For example, \"5 PPM\" or \"2.1 MZ\".\n"
    "   <type>, <threshold>, and <orientation> operate as in the \"threshold\" filter (see above).\n"
    "   <include|exclude> is optional and has value \"include\" (the default) or \"exclude\".  If \"exclude\" is "
    "used the filter drops spectra that match the various criteria instead of keeping them."
};

SpectrumListPtr filterCreator_chargeState(const MSData& msd, const string& arg, pwiz::util::IterationListenerRegistry* ilr)
{
    IntegerSet chargeStateSet;
    chargeStateSet.parse(arg);

    return SpectrumListPtr(new 
        SpectrumList_Filter(msd.run.spectrumListPtr, 
                            SpectrumList_FilterPredicate_ChargeStateSet(chargeStateSet), ilr));
}
UsageInfo usage_chargeState = {"<charge_states>",
    "This filter keeps spectra that match the listed charge state(s), expressed as an int_set.  Both known/single "
    "and possible/multiple charge states are tested.  Use 0 to include spectra with no charge state at all."};

SpectrumListPtr filterCreator_defaultArrayLength(const MSData& msd, const string& arg, pwiz::util::IterationListenerRegistry* ilr)
{
    IntegerSet defaultArrayLengthSet;
    defaultArrayLengthSet.parse(arg);

    return SpectrumListPtr(new 
        SpectrumList_Filter(msd.run.spectrumListPtr, 
                            SpectrumList_FilterPredicate_DefaultArrayLengthSet(defaultArrayLengthSet), ilr));
}
UsageInfo usage_defaultArrayLength = { "<peak_count_range>",
    "Keeps only spectra with peak counts within <peak_count_range>, expressed as an int_set. (In mzML the peak list "
    "length is expressed as \"defaultArrayLength\", hence the name.)  For example, to include only spectra with 100 "
    "or more peaks, you would use filter \"defaultArrayLength 100-\" ."
    };


SpectrumListPtr filterCreator_metadataFixer(const MSData& msd, const string& arg, pwiz::util::IterationListenerRegistry* ilr)
{
    return SpectrumListPtr(new SpectrumList_MetadataFixer(msd.run.spectrumListPtr));
}
UsageInfo usage_metadataFixer={"","This filter is used to add or replace a spectra's TIC/BPI metadata, usually after "
    "peakPicking where the change from profile to centroided data may make the TIC and BPI values inconsistent with "
    "the revised scan data.  The filter traverses the m/z intensity arrays to find the sum and max. For example, in "
    "msconvert it can be used as: --filter \"peakPicking true 1-\" --filter metadataFixer.  It can also be used without "
    "peak picking for some strange results. Certainly adding up all the samples of profile data to get the TIC is "
    "just wrong, but we do it anyway."};

SpectrumListPtr filterCreator_titleMaker(const MSData& msd, const string& arg, pwiz::util::IterationListenerRegistry* ilr)
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

SpectrumListPtr filterCreator_chargeStatePredictor(const MSData& msd, const string& carg, pwiz::util::IterationListenerRegistry* ilr)
{
    const string overrideExistingChargeToken("overrideExistingCharge=");
    const string maxMultipleChargeToken("maxMultipleCharge=");
    const string minMultipleChargeToken("minMultipleCharge=");
    const string singleChargeFractionTICToken("singleChargeFractionTIC=");
    const string maxKnownChargeToken("maxKnownCharge=");
    const string makeMS2Token("makeMS2=");

    string arg = carg;
    bool overrideExistingCharge = parseKeyValuePair<LocaleBool>(arg, overrideExistingChargeToken, false);
    int maxMultipleCharge = parseKeyValuePair<int>(arg, maxMultipleChargeToken, 3);
    int minMultipleCharge = parseKeyValuePair<int>(arg, minMultipleChargeToken, 2);
    double singleChargeFractionTIC = parseKeyValuePair<double>(arg, singleChargeFractionTICToken, 0.9);
    int maxKnownCharge = parseKeyValuePair<int>(arg, maxKnownChargeToken, 0);
    bool makeMS2 = parseKeyValuePair<LocaleBool>(arg, makeMS2Token, false);
    bal::trim(arg);
    if (!arg.empty())
        throw runtime_error("[chargeStatePredictor] unhandled text remaining in argument string: \"" + arg + "\"");

    return SpectrumListPtr(new
        SpectrumList_ChargeStateCalculator(msd.run.spectrumListPtr,
                                           overrideExistingCharge,
                                           maxMultipleCharge,
                                           minMultipleCharge,
                                           singleChargeFractionTIC,
                                           maxKnownCharge,
                                           makeMS2));
}
UsageInfo usage_chargeStatePredictor = {"[overrideExistingCharge=<true|false (false)>] [maxMultipleCharge=<int (3)>] [minMultipleCharge=<int (2)>] [singleChargeFractionTIC=<real (0.9)>] [maxKnownCharge=<int (0)>] [makeMS2=<true|false (false)>]",
    "Predicts MSn spectrum precursors to be singly or multiply charged depending on the ratio of intensity above and below the precursor m/z, or optionally using the \"makeMS2\" algorithm\n"
    "  <overrideExistingCharge> : always override existing charge information (default:\"false\")\n"
    "  <maxMultipleCharge> (default 3) and <minMultipleCharge> (default 2): range of values to add to the spectrum's existing \"MS_possible_charge_state\" values."
    "If these are the same values, the spectrum's MS_possible_charge_state values are removed and replaced with this single value.\n"
    "  <singleChargeFractionTIC> : is a percentage expressed as a value between 0 and 1 (the default is 0.9, or 90 percent). "
    "This is the value used as the previously mentioned ratio of intensity above and below the precursor m/z.\n"
    "  <maxKnownCharge> (default is 0, meaning no maximum): the maximum charge allowed for \"known\" charges even if override existing charge is false. "
    "This allows overriding junk charge calls like +15 peptides.\n"
    "  <algorithmMakeMS2> : default is \"false\", when set to \"true\" the \"makeMS2\" algorithm is used instead of the one described above."
    };

SpectrumListPtr filterCreator_chargeFromIsotope(const MSData& msd, const string& arg, pwiz::util::IterationListenerRegistry* ilr)
{

    // defaults
    int maxCharge = 8;
    int minCharge = 1;
    int precursorsBefore = 2;
    int precursorsAfter = 0;
    double isolationWidth = 1.25;
    int defaultChargeMax = 0;
    int defaultChargeMin = 0;

    istringstream parser(arg);
    string nextStr;

    while ( parser >> nextStr )
    {

        if ( string::npos == nextStr.rfind('=') )
            throw user_error("[filterCreator_turbocharger] = sign required after keyword argument");

        string keyword = nextStr.substr(0,nextStr.rfind('='));
        string paramVal = nextStr.substr(nextStr.rfind('=')+1);

        if ( boost::iequals(keyword,"minCharge") )
        {
            try { lexical_cast<int>(paramVal); }
            catch (...) { throw user_error("[filterCreator_turbocharger] An integer must follow the minCharge argument."); }
            minCharge = lexical_cast<int>(paramVal);
            if ( minCharge < 0 ) { throw user_error("[filterCreator_turbocharger] minCharge must be a positive integer."); }
        }
        else if ( boost::iequals(keyword,"maxCharge") )
        {
            try { lexical_cast<int>(paramVal); }
            catch (...) { throw user_error("[filterCreator_turbocharger] An integer must follow the maxCharge argument."); }
            maxCharge = lexical_cast<int>(paramVal);    
            if ( maxCharge < 0 ) { throw user_error("[filterCreator_turbocharger] maxCharge must be a positive integer."); }
        }
        else if ( boost::iequals(keyword,"precursorsBefore") )
        {
            try { lexical_cast<int>(paramVal); }
            catch (...) { throw user_error("[filterCreator_turbocharger] An integer must follow the precursorsBefore argument."); }
            precursorsBefore = lexical_cast<int>(paramVal);
            if ( precursorsBefore < 0 ) { throw user_error("[filterCreator_turbocharger] precursorsBefore must be a positive integer."); }
        }
        else if ( boost::iequals(keyword,"precursorsAfter") )
        {
            try { lexical_cast<int>(paramVal); }
            catch (...) { throw user_error("[filterCreator_turbocharger] An integer must follow the precursorsAfter argument."); }
            precursorsAfter = lexical_cast<int>(paramVal);
            if ( precursorsAfter < 0 ) { throw user_error("[filterCreator_turbocharger] precursorsAfter must be a positive integer."); }
        }
        else if ( boost::iequals(keyword,"halfIsoWidth") )
        {
            try { lexical_cast<double>(paramVal); }
            catch (...) { throw user_error("[filterCreator_turbocharger] A numeric value must follow the halfIsoWidth argument."); }
            isolationWidth = lexical_cast<double>(paramVal);
            if ( isolationWidth <= 0 ) { throw user_error("[filterCreator_turbocharger] halfIsoWidth must be positive."); }
        }
        else if ( boost::iequals(keyword,"defaultMinCharge") )
        {
            try { lexical_cast<int>(paramVal); }
            catch (...) { throw user_error("[filterCreator_turbocharger] An integer must follow the defaultChargeMin argument."); }
            defaultChargeMin = lexical_cast<int>(paramVal);
            if ( defaultChargeMin < 0 ) { throw user_error("[filterCreator_turbocharger] defaultChargeMin must be a positive integer."); }
        }
        else if ( boost::iequals(keyword,"defaultMaxCharge") )
        {
            try { lexical_cast<int>(paramVal); }
            catch (...) { throw user_error("[filterCreator_turbocharger] An integer must follow the defaultChargeMax argument."); }
            defaultChargeMax = lexical_cast<int>(paramVal);
            if ( defaultChargeMax < 0 ) { throw user_error("[filterCreator_turbocharger] defaultChargeMax must be a positive integer."); }
        }
        else
        {
            throw user_error("[filterCreator_turbocharger] Invalid keyword entered.");
        }

            

    }

    // a few more sanity checks
    if ( minCharge > maxCharge ) { throw user_error("[filterCreator_turbocharger] maxCharge must be greater than or equal to minCharge."); }
    if ( defaultChargeMin > defaultChargeMax ) { throw user_error("[filterCreator_turbocharger] defaultMaxCharge must be greater than or equal to defaultMinCharge."); }

    // TODO: give the user some feedback regarding parameter selection
    //cout << "***turbocharger parameters***" << endl;
    //cout << "minCharge: " << minCharge << endl;
    //cout << "maxCharge: " << maxCharge << endl;
    //cout << "parentsBefore: " << parentsBefore << endl;
    //cout << "parentsAfter: " << parentsAfter << endl;
    //cout << "halfIsoWidth: " << isolationWidth << endl;
    //cout << "defaultMinCharge: " << defaultChargeMin << endl;
    //cout << "defaultMaxCharge: " << defaultChargeMax << endl;
    

    return SpectrumListPtr(new
        SpectrumList_ChargeFromIsotope(msd,maxCharge,minCharge,precursorsBefore,precursorsAfter,isolationWidth,
                                           defaultChargeMax,defaultChargeMin));
}
UsageInfo usage_chargeFromIsotope = {"[minCharge=<minCharge>] [maxCharge=<maxCharge>] [precursorsBefore=<before>] [precursorsAfter=<after>] [halfIsoWidth=<half-width of isolation window>] [defaultMinCharge=<defaultMinCharge>] [defaultMaxCharge=<defaultMaxCharge>] [useVendorPeaks=<useVendorPeaks>]",
    "Predicts MSn spectrum precursor charge based on the isotopic distribution associated with the survey scan(s) of the selected precursor\n"
    "  <maxCharge> (default: 8) and <minCharge> (default 1): defines range of possible precursor charge states.\n"
    "  <before> (default: 2) and <after> (default 0): number of survey (MS1) scans to check for precursor isotopes, before and after a MS/MS in retention time.\n"
    "  <half-width of isolation window> (default: 1.25): half-width of the isolation window (in Th.) from which precursor is derived. Window is centered at target m/z with a total size of +/- the value entered.\n"
    "  <defaultMinCharge> (default: 0) and <defaultMaxCharge> (default: 0): in the event that no isotope is found in the isolation window, a range of charges between these two values will be assigned to the spectrum. If both values are left at zero, no charge will be assigned to the spectrum."
    };

/** 
  *  filter on the basis of ms2 activation type
  *
  *   handler for --filter Activation option.  Use it to create
  *   output files containing only ETD or CID MSn data where both activation modes have been
  *   interleaved within a given input vendor data file (eg: Thermo's Decision Tree acquisition mode).
  */ 
SpectrumListPtr filterCreator_ActivationType(const MSData& msd, const string& arg, pwiz::util::IterationListenerRegistry* ilr)
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
        cvIDs.insert(MS_higher_energy_beam_type_collision_induced_dissociation);
        cvIDs.insert(MS_HCD);
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
    else if (activationType == "HECID") cvIDs.insert(MS_higher_energy_beam_type_collision_induced_dissociation);
    else if (activationType == "HCD") cvIDs.insert(MS_HCD);
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
                                                   SpectrumList_FilterPredicate_ActivationType(cvIDs, hasNot), ilr));
}
UsageInfo usage_activation = { "<precursor_activation_type>",
    "Keeps only spectra whose precursors have the specifed activation type.  It doesn't affect non-MS spectra, and doesn't "
    "affect MS1 spectra. Use it to create output files containing only ETD or CID MSn data where both activation modes "
    "have been interleaved within a given input vendor data file (eg: Thermo's Decision Tree acquisition mode).\n"
    "   <precursor_activation_type> is any one of: ETD CID SA HCD HECID BIRD ECD IRMPD PD PSD PQD SID or SORI."
    };

SpectrumListPtr filterCreator_AnalyzerType(const MSData& msd, const string& arg, pwiz::util::IterationListenerRegistry* ilr)
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
                                                   SpectrumList_FilterPredicate_AnalyzerType(cvIDs), ilr));

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

SpectrumListPtr filterCreator_thresholdFilter(const MSData& msd, const string& arg, pwiz::util::IterationListenerRegistry* ilr)
{
    istringstream parser(arg);
    string byTypeArg, orientationArg;
    double threshold;
    IntegerSet msLevels(1, INT_MAX);

    parser >> byTypeArg >> threshold >> orientationArg;

    if (parser)
    {
        string msLevelSets;
        getlinePortable(parser, msLevelSets);

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

SpectrumListPtr filterCreator_polarityFilter(const MSData& msd, const string& arg, pwiz::util::IterationListenerRegistry* ilr)
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

    return SpectrumListPtr(new SpectrumList_Filter(msd.run.spectrumListPtr, SpectrumList_FilterPredicate_Polarity(polarity), ilr));
}
UsageInfo usage_polarity = { "<polarity>",
    "Keeps only spectra with scan of the selected <polarity>.\n"
    "   <polarity> is any one of \"positive\" \"negative\" \"+\" or \"-\"."
};

SpectrumListPtr filterCreator_collisionEnergy(const MSData& msd, const string& carg, pwiz::util::IterationListenerRegistry* ilr)
{
    string arg(carg);
    double low = parseKeyValuePair<double>(arg, "low=", -1);
    double high = parseKeyValuePair<double>(arg, "high=", -1);
    if (low < 0 || high < 0)
        throw user_error("[SpectrumListFactory::filterCreator_collisionEnergy] low and high CE both must be specified and greater than or equal to 0");

    bool acceptNonCID = parseKeyValuePair<bool>(arg, "acceptNonCID=", true);
    bool acceptMissingCE = parseKeyValuePair<bool>(arg, "acceptMissingCE=", false);
    auto mode = parseKeyValuePair<SpectrumList_Filter::Predicate::FilterMode>(arg, "mode=", SpectrumList_Filter::Predicate::FilterMode_Include);

    return SpectrumListPtr(new SpectrumList_Filter(msd.run.spectrumListPtr, SpectrumList_FilterPredicate_CollisionEnergy(min(low, high), max(low, high), acceptNonCID, acceptMissingCE, mode), ilr));
}
UsageInfo usage_collisionEnergy = { "low=<real> high=<real> [mode=<include|exclude (include)>] [acceptNonCID=<true|false (true)] [acceptMissingCE=<true|false (false)]",
    "Includes/excludes MSn spectra with CID collision energy within the specified range [<low>, <high>].\n"
    "   Non-MS and MS1 spectra are always included. Non-CID MS2s and MS2s with missing CE are optionally included/excluded."
};

SpectrumListPtr filterCreator_thermoScanFilterFilter(const MSData& msd, const string& arg, pwiz::util::IterationListenerRegistry* ilr)
{
    istringstream parser(arg);
    
    string matchExactArg;
    string includeArg;
    string matchStringArg;
    parser >> matchExactArg >> includeArg;
    getlinePortable(parser, matchStringArg);
    bal::trim(matchStringArg);

    bool matchExact;
    bool inverse;

    bal::to_upper(matchExactArg);
    bal::to_upper(includeArg);

    if (matchExactArg == "EXACT")
        matchExact = true;
    else if (matchExactArg == "CONTAINS")
        matchExact = false;
    else
        throw user_error("[SpectrumListFactory::filterCreator_thermoScanFilterFilter()] invalid filter argument for <exact or contains>. Valid options are: exact, contains");

    if (includeArg == "INCLUDE")
        inverse = false;
    else if (includeArg == "EXCLUDE")
        inverse = true;
    else
        throw user_error("[SpectrumListFactory::filterCreator_thermoScanFilterFilter()] invalid filter argument for <include or exclude>. Valid options are: include, exclude");

    //cout << "matchExactArg[" << matchExactArg << "]" << endl;
    //cout << "includeArg[" << includeArg << "]" << endl;
    //cout << "matchStringArg[" << matchStringArg << "]" << endl;

    return SpectrumListPtr(new SpectrumList_Filter(msd.run.spectrumListPtr, SpectrumList_FilterPredicate_ThermoScanFilter(matchStringArg, matchExact, inverse), ilr));
}

UsageInfo usage_thermoScanFilter = { "<exact|contains> <include|exclude> <match string>",
    "   Includes or excludes scans based on the scan filter (Thermo instrumentation only).\n"
    "   <exact|contains>: If \"exact\" is set, spectra only match the search string if there is an exact match.\n"
    "                     If \"contains\" is set, spectra match the search string if the search string is \n"
    "                     contained somewhere in the scan filter.\n"
    "   <include|exclude>: If \"include\" is set, only spectra that match the filter will be kept.\n"
    "                      If \"exclude\" is set, only spectra that do NOT match the filter will be kept.\n"
    "                     used the filter drops data points that match the various criteria instead of keeping them."
    "   <match string> specifies the search string to be compared to each scan filter (it may contain spaces)\n"
};

struct JumpTableEntry
{
    const char* command;
    const UsageInfo& usage; // {const char *usage,const char &details}
    FilterCreator creator;
};


JumpTableEntry jumpTable_[] =
{
    {"index", usage_index, filterCreator_index},
    {"id", usage_id, filterCreator_id},
    {"msLevel", usage_msLevel, filterCreator_msLevel},
    {"chargeState", usage_chargeState, filterCreator_chargeState},
    {"precursorRecalculation", usage_precursorRecalculation, filterCreator_precursorRecalculation},
    {"mzRefiner", usage_mzRefine, filterCreator_mzRefine},
    {"lockmassRefiner", usage_lockmassRefiner, filterCreator_lockmassRefiner},
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
    {"mzPresent", usage_mzPresent, filterCreator_mzPresent},
    {"scanSumming", usage_scanSummer, filterCreator_scanSummer},
    {"thermoScanFilter", usage_thermoScanFilter, filterCreator_thermoScanFilterFilter},

    // MSn Spectrum Processing/Filtering
    {"MS2Denoise", usage_MS2Denoise, filterCreator_MS2Denoise},
    {"MS2Deisotope", usage_MS2Deisotope, filterCreator_MS2Deisotope},
    {"ETDFilter", usage_ETDFilter, filterCreator_ETDFilter},
    {"demultiplex", usage_demux, filterCreator_demux},
    {"chargeStatePredictor", usage_chargeStatePredictor, filterCreator_chargeStatePredictor},
    {"turbocharger", usage_chargeFromIsotope, filterCreator_chargeFromIsotope},
    {"activation", usage_activation, filterCreator_ActivationType},
    {"collisionEnergy", usage_collisionEnergy, filterCreator_collisionEnergy},
    {"analyzer", usage_analyzerType, filterCreator_AnalyzerType},
    {"analyzerType", usage_analyzerTypeOld, filterCreator_AnalyzerType},
    {"polarity", usage_polarity, filterCreator_polarityFilter},
    {"diaUmpire", usage_diaUmpire, filterCreator_diaUmpire}
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
void SpectrumListFactory::wrap(MSData& msd, const string& wrapper, pwiz::util::IterationListenerRegistry* ilr)
{
    if (!msd.run.spectrumListPtr)
        return;

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
        throw user_error("[SpectrumListFactory] Unknown wrapper: " + wrapper);
    }

    SpectrumListPtr filter = entry->creator(msd, arg, ilr);
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
void SpectrumListFactory::wrap(msdata::MSData& msd, const vector<string>& wrappers, pwiz::util::IterationListenerRegistry* ilr)
{
    for (vector<string>::const_iterator it=wrappers.begin(); it!=wrappers.end(); ++it)
        wrap(msd, *it, ilr);
}


PWIZ_API_DECL
string SpectrumListFactory::usage(bool detailedHelp, const char *morehelp_prompt, int maxLineLength)
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
    size_t lastPos = 0;
    for (size_t curPos = maxLineLength; curPos < str.length();)
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
                curPos = spacePos + maxLineLength + 1;
            }
        } 
        else 
        {
            lastPos = curPos;
            curPos = newlinePos + maxLineLength + 1;
        }
    }

    return str;
}


string SpectrumListFactory::usage(const std::string& detailedHelpForFilter)
{
    ostringstream oss;

    for (JumpTableEntry* it = jumpTable_; it != jumpTableEnd_; ++it)
    {
        if (it->command == detailedHelpForFilter)
        {
            oss << it->command << " " << it->usage[0] << endl << it->usage[1] << endl;
            return oss.str();
        }
    }

    oss << "Invalid filter name: " << detailedHelpForFilter << endl << endl
        << "Supported filters are:" << endl;

    for (JumpTableEntry* it = jumpTable_; it != jumpTableEnd_; ++it)
        oss << it->command << endl;

    return oss.str();
}


} // namespace analysis 
} // namespace pwiz


