//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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


#ifndef _SPECTRUM_PROCESSING_HPP_CLI_
#define _SPECTRUM_PROCESSING_HPP_CLI_


#pragma warning( push )
#pragma warning( disable : 4634 4635 )
//#include "SpectrumListWrapper.hpp"

#ifdef PWIZ_BINDINGS_CLI_COMBINED
    #include "../msdata/MSData.hpp"
    #include "../common/IterationListener.hpp"
#else
    #include "../common/SharedCLI.hpp"
    #using "pwiz_bindings_cli_common.dll" as_friend
    #using "pwiz_bindings_cli_msdata.dll" as_friend
#endif
#include "../chemistry/chemistry.hpp"

#include "pwiz/data/msdata/SpectrumListWrapper.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_Filter.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_Sorter.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_Smoother.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_PeakPicker.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_LockmassRefiner.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_ChargeStateCalculator.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_3D.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_IonMobility.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_DiaUmpire.hpp"
#include "pwiz/analysis/chromatogram_processing/ChromatogramList_XICGenerator.hpp"
#include <boost/shared_ptr.hpp>
#include <boost/logic/tribool.hpp>
#pragma warning( pop )


#define DEFINE_INTERNAL_LIST_WRAPPER_CODE(CLIType, NativeType) \
INTERNAL: virtual ~CLIType() {/* base class destructor will delete the shared pointer */} \
          NativeType* base_; \
          NativeType& base() new {return *base_;}


using boost::shared_ptr;


namespace pwiz {
namespace CLI {

using namespace data;


namespace msdata {


/// <summary>
/// Inheritable pass-through implementation for wrapping a SpectrumList 
/// </summary>
public ref class SpectrumListWrapper : public msdata::SpectrumList
{
    DEFINE_INTERNAL_LIST_WRAPPER_CODE(SpectrumListWrapper, pwiz::msdata::SpectrumListWrapper)

    public:

    SpectrumListWrapper(SpectrumList^ inner);
};


} // namespace msdata


namespace analysis {


/// <summary>
/// Factory for instantiating and wrapping SpectrumLists
/// </summary>
public ref class SpectrumListFactory abstract
{
    public:

    /// <summary>
    /// instantiate the SpectrumListWrapper indicated by wrapper
    /// </summary>
    static void wrap(msdata::MSData^ msd, System::String^ wrapper);

    /// <summary>
    /// instantiate the SpectrumListWrapper indicated by wrapper
    /// </summary>
    static void wrap(msdata::MSData^ msd, System::String^ wrapper, util::IterationListenerRegistry^ ilr);

    /// <summary>
    /// instantiate a list of SpectrumListWrappers
    /// </summary>
    static void wrap(msdata::MSData^ msd, System::Collections::Generic::IList<System::String^>^ wrappers);

    /// <summary>
    /// instantiate a list of SpectrumListWrappers
    /// </summary>
    static void wrap(msdata::MSData^ msd, System::Collections::Generic::IList<System::String^>^ wrappers, util::IterationListenerRegistry^ ilr);

    /// <summary>
    /// user-friendly documentation
    /// </summary>
    static System::String^ usage();
};


/// <summary>
/// Delegate for filtering spectra in SpectrumList_Filter
/// </summary>
public delegate System::Nullable<bool> SpectrumList_FilterAcceptSpectrum(msdata::Spectrum^);

public ref class SpectrumList_FilterPredicate abstract
{
    /// <summary>controls whether spectra that pass the predicate are included or excluded from the result</summary>
    public: enum class FilterMode
    {
        Include,
        Exclude
    };

    internal:
    pwiz::analysis::SpectrumList_Filter::Predicate* base_;
    ~SpectrumList_FilterPredicate() {SAFEDELETE(base_);}
};


public ref class SpectrumList_FilterPredicate_IndexSet : SpectrumList_FilterPredicate
{
    public: SpectrumList_FilterPredicate_IndexSet(System::String^ indexSet);
};


public ref class SpectrumList_FilterPredicate_ScanNumberSet : SpectrumList_FilterPredicate
{
    public: SpectrumList_FilterPredicate_ScanNumberSet(System::String^ scanNumberSet);
};


public ref class SpectrumList_FilterPredicate_ScanEventSet : SpectrumList_FilterPredicate
{
    public: SpectrumList_FilterPredicate_ScanEventSet(System::String^ scanEventSet);
};


public ref class SpectrumList_FilterPredicate_ScanTimeRange : SpectrumList_FilterPredicate
{
    public: SpectrumList_FilterPredicate_ScanTimeRange(double scanTimeLow, double scanTimeHigh);
};


public ref class SpectrumList_FilterPredicate_MSLevelSet : SpectrumList_FilterPredicate
{
    public: SpectrumList_FilterPredicate_MSLevelSet(System::String^ msLevelSet);
};


public ref class SpectrumList_FilterPredicate_ChargeStateSet : SpectrumList_FilterPredicate
{
    public: SpectrumList_FilterPredicate_ChargeStateSet(System::String^ chargeStateSet);
};


public ref class SpectrumList_FilterPredicate_PrecursorMzSet : SpectrumList_FilterPredicate
{
    public:  SpectrumList_FilterPredicate_PrecursorMzSet(System::Collections::Generic::IEnumerable<double>^ precursorMzSet, chemistry::MZTolerance tolerance, FilterMode mode);
};


public ref class SpectrumList_FilterPredicate_DefaultArrayLengthSet : SpectrumList_FilterPredicate
{
    public: SpectrumList_FilterPredicate_DefaultArrayLengthSet(System::String^ defaultArrayLengthSet);
};


public ref class SpectrumList_FilterPredicate_ActivationType : SpectrumList_FilterPredicate
{
public: SpectrumList_FilterPredicate_ActivationType(System::Collections::Generic::IEnumerable<CVID>^ filterItem, bool hasNoneOf);
};


public ref class SpectrumList_FilterPredicate_AnalyzerType : SpectrumList_FilterPredicate
{
    public: SpectrumList_FilterPredicate_AnalyzerType(System::Collections::Generic::IEnumerable<CVID>^ filterItem);
};


public ref class SpectrumList_FilterPredicate_Polarity : SpectrumList_FilterPredicate
{
    public: SpectrumList_FilterPredicate_Polarity(CVID polarity);
};


/*public ref class SpectrumList_FilterPredicate_MzPresent : SpectrumList_FilterPredicate
{
    public: SpectrumList_FilterPredicate_MzPresent(chemistry::MZTolerance mzt, System::Collections::Generic::Generic::ISet<double> mzSet, ThresholdFilter tf, FilterMode mode);
};*/


public ref class SpectrumList_FilterPredicate_ThermoScanFilter : SpectrumList_FilterPredicate
{
    public: SpectrumList_FilterPredicate_ThermoScanFilter(System::String^ matchString, bool matchExact, bool inverse);
};


/*public interface IPredicate
{
    /// return values:
    ///  true: accept the Spectrum
    ///  false: reject the Spectrum
    ///  indeterminate: need to see the full Spectrum object to decide
    public pwiz::CLI::util::tribool accept(msdata::SpectrumIdentity^ spectrumIdentity);

    /// return true iff Spectrum is accepted
    public bool accept(msdata::Spectrum^ spectrum);

    /// return true iff done accepting spectra; 
    /// this allows early termination of the iteration through the original
    /// SpectrumList, possibly using assumptions about the order of the
    /// iteration (e.g. index is increasing, nativeID interpreted as scan number is
    /// increasing, ...)
    public bool done();
}*/


/// <summary>
/// SpectrumList implementation filtered by a user's predicate
/// </summary>
public ref class SpectrumList_Filter : public msdata::SpectrumList
{
    DEFINE_INTERNAL_LIST_WRAPPER_CODE(SpectrumList_Filter, pwiz::analysis::SpectrumList_Filter)

    public:

    SpectrumList_Filter(msdata::SpectrumList^ inner,
                        SpectrumList_FilterAcceptSpectrum^ predicate);

    SpectrumList_Filter(msdata::SpectrumList^ inner,
                        SpectrumList_FilterPredicate^ predicate);

    private:

    ref class Impl;
    Impl^ impl_;
};

/*
/// <summary>
/// Delegate for comparing spectra in SpectrumList_Sorter
/// </summary>
public delegate System::Nullable<bool> SpectrumList_Sorter_LessThan(msdata::Spectrum^, msdata::Spectrum^);


/// <summary>
/// SpectrumList implementation sorted by a user's predicate
/// </summary>
public ref class SpectrumList_Sorter : public msdata::SpectrumList
{
    DEFINE_INTERNAL_LIST_WRAPPER_CODE(SpectrumList_Sorter, pwiz::analysis::SpectrumList_Sorter)

    public:

    SpectrumList_Sorter(msdata::SpectrumList^ inner,
                        SpectrumList_Sorter_LessThan^ predicate);
    

    private:

    SpectrumList_Sorter_LessThan^ managedPredicate;
    System::Nullable<bool> marshal(const pwiz::msdata::Spectrum* lhs, const pwiz::msdata::Spectrum* rhs);
};*/


public ref class Smoother abstract
{
    internal:
    pwiz::analysis::SmootherPtr* base_;
};

public ref class SavitzkyGolaySmoother : public Smoother
{
    public:
    SavitzkyGolaySmoother(int polynomialOrder, int windowSize)
    {
        base_ = new pwiz::analysis::SmootherPtr(
            new pwiz::analysis::SavitzkyGolaySmoother(polynomialOrder, windowSize));
    }
};

public ref class WhittakerSmoother : public Smoother
{
    public:
    WhittakerSmoother(double lambdaCoefficient)
    {
        base_ = new pwiz::analysis::SmootherPtr(
            new pwiz::analysis::WhittakerSmoother(lambdaCoefficient));
    }
};

/// <summary>
/// SpectrumList implementation to smooth intensities
/// </summary>
public ref class SpectrumList_Smoother : public msdata::SpectrumList
{
    DEFINE_INTERNAL_LIST_WRAPPER_CODE(SpectrumList_Smoother, pwiz::analysis::SpectrumList_Smoother)

    public:

    SpectrumList_Smoother(msdata::SpectrumList^ inner,
                          Smoother^ algorithm,
                          System::Collections::Generic::IEnumerable<int>^ msLevelsToSmooth);

    static bool accept(msdata::SpectrumList^ inner);
};


public ref class PeakDetector abstract
{
internal:
    pwiz::analysis::PeakDetectorPtr* base_;
};

/// <summary>
/// For use when you want to use vendor centroiding, and throw if it isn't available
/// </summary>
public ref class VendorOnlyPeakDetector : public PeakDetector
{
public:
    VendorOnlyPeakDetector()
    {
        base_ = new pwiz::analysis::PeakDetectorPtr(NULL); // No algorithm, so we can throw in the no vendor peak picking case
    }
};

/// <summary>
/// Simple local maxima peak detector; does not centroid
/// </summary>
public ref class LocalMaximumPeakDetector : public PeakDetector
{
    public:
    LocalMaximumPeakDetector(unsigned int windowSize)
    {
        base_ = new pwiz::analysis::PeakDetectorPtr(new pwiz::analysis::LocalMaximumPeakDetector(windowSize));
    }
};

/// <summary>
/// Wavelet-based algorithm for performing peak-picking with a minimum wavelet-space signal-to-noise ratio; does not centroid
/// </summary>
public ref class CwtPeakDetector : public PeakDetector
{
    public:
    CwtPeakDetector(double minSignalNoiseRatio, double minPeakSpace)
    {
        base_ = new pwiz::analysis::PeakDetectorPtr(new pwiz::analysis::CwtPeakDetector(minSignalNoiseRatio, 0, minPeakSpace));
    }
};

/// <summary>
/// SpectrumList implementation to replace peak profiles with picked peaks
/// </summary>
public ref class SpectrumList_PeakPicker : public msdata::SpectrumList
{
    DEFINE_INTERNAL_LIST_WRAPPER_CODE(SpectrumList_PeakPicker, pwiz::analysis::SpectrumList_PeakPicker)

    public:

    SpectrumList_PeakPicker(msdata::SpectrumList^ inner,
                            PeakDetector^ algorithm,
                            bool preferVendorPeakPicking,
                            System::Collections::Generic::IEnumerable<int>^ msLevelsToPeakPick);

    static bool accept(msdata::SpectrumList^ inner);

    /// <summary>
    /// returns true iff the given file supports vendor peak picking
    /// </summary>
    static bool supportsVendorPeakPicking(System::String^ rawpath);
};


/// <summary>
/// SpectrumList implementation to refine m/z accuracy using external lockmass scans.
/// </summary>
public ref class SpectrumList_LockmassRefiner : public msdata::SpectrumList
{
    DEFINE_INTERNAL_LIST_WRAPPER_CODE(SpectrumList_LockmassRefiner, pwiz::analysis::SpectrumList_LockmassRefiner)

    public:

        SpectrumList_LockmassRefiner(msdata::SpectrumList^ inner, double lockmassMzPosScans, double lockmassMzNegScans, double lockmassTolerance);

    static bool accept(msdata::SpectrumList^ inner);
};


/// <summary>
/// SpectrumList implementation that assigns (probable) charge states to tandem mass spectra
/// </summary>
public ref class SpectrumList_ChargeStateCalculator : public msdata::SpectrumList
{
    DEFINE_INTERNAL_LIST_WRAPPER_CODE(SpectrumList_ChargeStateCalculator, pwiz::analysis::SpectrumList_ChargeStateCalculator)

    public:

    SpectrumList_ChargeStateCalculator(msdata::SpectrumList^ inner,
                                       bool overrideExistingChargeState,
                                       int maxMultipleCharge,
                                       int minMultipleCharge,
                                       double intensityFractionBelowPrecursorForSinglyCharged);

    /// charge calculation works on any SpectrumList
    static bool accept(msdata::SpectrumList^ inner);
};


public value class ContinuousInterval
{
    public:
    ContinuousInterval(double begin, double end);
    property double Begin;
    property double End;
};

typedef System::Collections::Generic::KeyValuePair<double, double> MzIntensityPair;
typedef System::Collections::Generic::List<MzIntensityPair> Spectrum3DValue;

/// <summary>
/// A List of pairs of ion mobility drift time and spectra (a List of MzIntensityPairs)
/// </summary>
typedef System::Collections::Generic::List<System::Collections::Generic::KeyValuePair<double, Spectrum3DValue^>> Spectrum3D;

/// <summary>
/// SpectrumList implementation that can create 3D spectra of ion mobility drift time and m/z
/// </summary>
public ref class SpectrumList_3D : public msdata::SpectrumList
{
    DEFINE_INTERNAL_LIST_WRAPPER_CODE(SpectrumList_3D, pwiz::analysis::SpectrumList_3D)

    public:

    SpectrumList_3D(msdata::SpectrumList^ inner);

    /// <summary>
    /// creates a 3d spectrum at the given scan start time (specified in seconds) and including the given ion mobility ranges (units depend on equipment type)
    /// </summary>
    virtual Spectrum3D^ spectrum3d(double scanStartTime, System::Collections::Generic::IEnumerable<ContinuousInterval>^ ionMobilityRanges);

    /// <summary>
    /// return true if underlying data supports a 2nd degree of separation (drift time, inverse ion mobility, etc)
    /// </summary>
    static bool accept(msdata::SpectrumList^ inner);
};

/// <summary>
/// SpectrumList implementation that provides access to vendor-specific ion mobility functions
/// </summary>
public ref class SpectrumList_IonMobility : public msdata::SpectrumList
{
    DEFINE_INTERNAL_LIST_WRAPPER_CODE(SpectrumList_IonMobility, pwiz::analysis::SpectrumList_IonMobility)

    public:

    SpectrumList_IonMobility(msdata::SpectrumList^ inner);

    /// <summary>
    /// works only on SpectrumList_Agilent
    /// </summary>
    static bool accept(msdata::SpectrumList^ inner);

    enum class IonMobilityUnits { none, drift_time_msec, inverse_reduced_ion_mobility_Vsec_per_cm2, compensation_V };

    virtual IonMobilityUnits getIonMobilityUnits();

    // returns true if 3-array IMS representation is in use
    virtual bool hasCombinedIonMobility(); 

    // returns true if the data file actually has necessary info for CCS/ion mobility conversion handling
    virtual bool canConvertIonMobilityAndCCS(IonMobilityUnits units);

    /// returns collisional cross-section associated with the ion mobility value (units depend on equipment type)
    virtual double ionMobilityToCCS(double ionMobility, double mz, int charge);

    /// returns the ion mobility (units depend on equipment type) associated with the given collisional cross-section
    virtual double ccsToIonMobility(double ccs, double mz, int charge);
};

/// <summary>
/// SpectrumList wrapper that generates pseudo-MS/MS spectra from DIA spectra using the DiaUmpire algorithm
/// </summary>
public ref class SpectrumList_DiaUmpire : public msdata::SpectrumList
{
    DEFINE_INTERNAL_LIST_WRAPPER_CODE(SpectrumList_DiaUmpire, pwiz::analysis::SpectrumList_DiaUmpire)

    public:

    ref class Config
    {
        DEFINE_INTERNAL_BASE_CODE(Config, DiaUmpire::Config)

        public:

        ref class InstrumentParameter
        {
            DEFINE_INTERNAL_BASE_CODE(InstrumentParameter, DiaUmpire::InstrumentParameter)

            public:
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(int, Resolution);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(float, MS1PPM);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(float, MS2PPM);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(float, SNThreshold);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(float, MinMSIntensity);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(float, MinMSMSIntensity);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(int, NoPeakPerMin);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(float, MinRTRange);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(int, StartCharge);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(int, EndCharge);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(int, MS2StartCharge);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(int, MS2EndCharge);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(float, MaxCurveRTRange);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(float, RTtol);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(float, MS2SNThreshold);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(int, MaxNoPeakCluster);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(int, MinNoPeakCluster);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(int, MaxMS2NoPeakCluster);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(int, MinMS2NoPeakCluster);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(bool, Denoise);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(bool, EstimateBG);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(bool, DetermineBGByID);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(bool, RemoveGroupedPeaks);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(bool, Deisotoping);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(bool, BoostComplementaryIon);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(bool, AdjustFragIntensity);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(int, PrecursorRank);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(int, FragmentRank);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(float, RTOverlapThreshold);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(float, CorrThreshold);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(float, ApexDelta);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(float, SymThreshold);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(int, NoMissedScan);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(int, MinPeakPerPeakCurve);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(float, MinMZ);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(int, MinFrag);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(float, MiniOverlapP);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(bool, CheckMonoIsotopicApex);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(bool, DetectByCWT);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(bool, FillGapByBK);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(float, IsoCorrThreshold);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(float, RemoveGroupedPeaksCorr);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(float, RemoveGroupedPeaksRTOverlap);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(float, HighCorrThreshold);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(int, MinHighCorrCnt);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(int, TopNLocal);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(int, TopNLocalRange);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(float, IsoPattern);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(float, startRT);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(float, endRT);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(bool, TargetIDOnly);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(bool, MassDefectFilter);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(float, MinPrecursorMass);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(float, MaxPrecursorMass);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(bool, UseOldVersion);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(float, RT_window_Targeted);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(int, SmoothFactor);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(bool, DetectSameChargePairOnly);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(float, MassDefectOffset);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(int, MS2PairTopN);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(bool, MS2Pairing);
        };

        ref class MzRange
        {
            DEFINE_INTERNAL_BASE_CODE(MzRange, DiaUmpire::MzRange)

            public:
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(float, begin);
            DEFINE_SIMPLE_PRIMITIVE_PROPERTY(float, end);
        };

        ref class TargetWindow
        {
            DEFINE_INTERNAL_BASE_CODE(TargetWindow, DiaUmpire::TargetWindow)

            public:
            enum class Scheme
            {
                SWATH_Fixed,
                SWATH_Variable
            };

            TargetWindow(double start, double end);

            DEFINE_REFERENCE_PROPERTY(MzRange, mzRange);
        };
        
        DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(TargetWindowList, DiaUmpire::TargetWindow, TargetWindow, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);

        DEFINE_REFERENCE_PROPERTY(InstrumentParameter, instrumentParameters);

        DEFINE_PRIMITIVE_PROPERTY(DiaUmpire::TargetWindow::Scheme, TargetWindow::Scheme, diaTargetWindowScheme);

        property TargetWindowList^ diaVariableWindows
        {
            TargetWindowList^ get();
        }

        Config();

        DEFINE_SIMPLE_PRIMITIVE_PROPERTY(int, diaFixedWindowSize);
        DEFINE_SIMPLE_PRIMITIVE_PROPERTY(bool, exportMs1ClusterTable);
        DEFINE_SIMPLE_PRIMITIVE_PROPERTY(bool, exportMs2ClusterTable);
        DEFINE_SIMPLE_PRIMITIVE_PROPERTY(bool, exportSeparateQualityMGFs);
    };

    SpectrumList_DiaUmpire(pwiz::CLI::msdata::MSData^ msd, pwiz::CLI::msdata::SpectrumList^ inner, Config^ config);
    SpectrumList_DiaUmpire(pwiz::CLI::msdata::MSData^ msd, pwiz::CLI::msdata::SpectrumList^ inner, Config^ config, util::IterationListenerRegistry^ ilr);
};

public ref class ChromatogramList_XICGenerator : public msdata::ChromatogramList
{
    DEFINE_INTERNAL_LIST_WRAPPER_CODE(ChromatogramList_XICGenerator, pwiz::analysis::ChromatogramList_XICGenerator)

    public:

    ChromatogramList_XICGenerator(msdata::ChromatogramList^ inner);

    virtual msdata::Chromatogram^ xic(double startTime, double endTime, System::Collections::Generic::IEnumerable<ContinuousInterval>^ massRanges, int msLevel);

    /// <summary>
    /// works only on ChromatogramList_Thermo
    /// </summary>
    static bool accept(msdata::ChromatogramList^ inner);
};

} // namespace analysis
} // namespace CLI
} // namespace pwiz

#endif
