//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
//
// Licensed under Creative Commons 3.0 United States License, which requires:
//  - Attribution
//  - Noncommercial
//  - No Derivative Works
//
// http://creativecommons.org/licenses/by-nc-nd/3.0/us/
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//


#define PWIZ_SOURCE

#ifndef PWIZ_READER_ABI
#error compiler is not MSVC or DLL not available
#else // PWIZ_READER_ABI

#pragma unmanaged
#include "WiffFile.hpp"
//#include "LicenseKey.h"
#include "pwiz/utility/misc/DateTime.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Container.hpp"
#include <boost/icl/interval_set.hpp>
#include <boost/icl/continuous_interval.hpp>
#include <boost/smart_ptr/make_shared.hpp>

#pragma managed
#include "pwiz/utility/misc/cpp_cli_utilities.hpp"
#include <msclr/auto_gcroot.h>
#using <System.dll>
#using <System.Xml.dll>
using namespace pwiz::util;
using namespace System;
using namespace System::Text::RegularExpressions;
using namespace Clearcore2::Data;
using namespace Clearcore2::Data::AnalystDataProvider;
using namespace Clearcore2::Data::Client;
using namespace Clearcore2::Data::DataAccess;
using namespace Clearcore2::Data::DataAccess::SampleData;

#if __CLR_VER > 40000000 // .NET 4
using namespace Clearcore2::RawXYProcessing;
#endif

// peak areas from Sciex are very small values relative to peak heights;
// multiplying them by this scaling factor makes them more comparable
const int PEAK_AREA_SCALE_FACTOR = 100;

#include "WiffFile2.ipp"

namespace pwiz {
namespace vendor_api {
namespace ABI {

class WiffFileImpl : public WiffFile
{
    public:
    WiffFileImpl(const std::string& wiffpath);
    ~WiffFileImpl();

    gcroot<DataProvider^> provider;
    gcroot<Batch^> batch;
    mutable msclr::auto_gcroot<Clearcore2::Data::DataAccess::SampleData::Sample^> sample;
    mutable msclr::auto_gcroot<MassSpectrometerSample^> msSample;

    virtual std::string getWiffPath() const { return wiffpath; }

    virtual int getSampleCount() const;
    virtual int getPeriodCount(int sample) const;
    virtual int getExperimentCount(int sample, int period) const;
    virtual int getCycleCount(int sample, int period, int experiment) const;

    virtual const vector<string>& getSampleNames() const;

    virtual InstrumentModel getInstrumentModel() const;
    virtual std::string getInstrumentSerialNumber() const;
    virtual IonSourceType getIonSourceType() const;
    virtual blt::local_date_time getSampleAcquisitionTime(int sample, bool adjustToHostTime) const;

    virtual ExperimentPtr getExperiment(int sample, int period, int experiment) const;
    virtual SpectrumPtr getSpectrum(int sample, int period, int experiment, int cycle) const;
    virtual SpectrumPtr getSpectrum(ExperimentPtr experiment, int cycle) const;

    virtual int getADCTraceCount(int sample) const;
    virtual std::string getADCTraceName(int sample, int traceIndex) const;
    virtual void getADCTrace(int sample, int traceIndex, ADCTrace& trace) const;

    virtual void getTWC(int sample, ADCTrace& totalWavelengthChromatogram) const;

    void setSample(int sample) const;
    void setPeriod(int sample, int period) const;
    void setExperiment(int sample, int period, int experiment) const;
    void setCycle(int sample, int period, int experiment, int cycle) const;

    mutable int currentSample, currentPeriod, currentExperiment, currentCycle;

    private:
    // on first access, sample names are made unique (giving duplicates a count suffix) and cached
    mutable vector<string> sampleNames;
    string wiffpath;
};

typedef boost::shared_ptr<WiffFileImpl> WiffFileImplPtr;


struct ExperimentImpl : public Experiment
{
    ExperimentImpl(const WiffFileImpl* wifffile, int sample, int period, int experiment);

    virtual int getSampleNumber() const {return sample;}
    virtual int getPeriodNumber() const {return period;}
    virtual int getExperimentNumber() const {return experiment;}

    virtual size_t getSIMSize() const;
    virtual void getSIM(size_t index, Target& target) const;

    virtual size_t getSRMSize() const;
    virtual void getSRM(size_t index, Target& target) const;

    virtual double getSIC(size_t index, pwiz::util::BinaryData<double>& times, pwiz::util::BinaryData<double>& intensities, bool ignoreScheduledLimits) const;
    virtual void getSIC(size_t index, pwiz::util::BinaryData<double>& times, pwiz::util::BinaryData<double>& intensities,
                        double& basePeakX, double& basePeakY, bool ignoreScheduledLimits) const;

    virtual void getAcquisitionMassRange(double& startMz, double& stopMz) const;
    virtual ScanType getScanType() const;
    virtual ExperimentType getExperimentType() const;
    virtual Polarity getPolarity() const;
    virtual int getMsLevel(int cycle) const;

    virtual double convertCycleToRetentionTime(int cycle) const;
    virtual double convertRetentionTimeToCycle(double rt) const;

    virtual void getTIC(std::vector<double>& times, std::vector<double>& intensities) const;
    virtual void getBPC(std::vector<double>& times, std::vector<double>& intensities) const;

    const WiffFileImpl* wifffile_;
    gcroot<MSExperiment^> msExperiment;
    int sample, period, experiment;
    bool hasHalfSizeRTWindow;

    ExperimentType experimentType;
    size_t simCount;
    size_t transitionCount;

    const vector<double>& cycleTimes() const {initializeTIC(); return cycleTimes_;}
    const vector<double>& cycleIntensities() const {initializeTIC(); return cycleIntensities_;}
    const vector<double>& basePeakMZs() const {initializeBPC(); return basePeakMZs_;}
    const vector<double>& basePeakIntensities() const {initializeBPC(); return basePeakIntensities_;}

    private:
    void initializeTIC() const;
    void initializeBPC() const;
    mutable bool initializedTIC_;
    mutable bool initializedBPC_;
    mutable vector<double> cycleTimes_;
    mutable vector<double> cycleIntensities_;
    mutable vector<double> basePeakMZs_;
    mutable vector<double> basePeakIntensities_;
};

typedef boost::shared_ptr<ExperimentImpl> ExperimentImplPtr;


struct SpectrumImpl : public Spectrum
{
    SpectrumImpl(ExperimentImplPtr experiment, int cycle);

    virtual int getSampleNumber() const {return experiment->sample;}
    virtual int getPeriodNumber() const {return experiment->period;}
    virtual int getExperimentNumber() const {return experiment->experiment;}
    virtual int getCycleNumber() const {return cycle;}

    virtual int getMSLevel() const;

    virtual bool getHasIsolationInfo() const;
    virtual void getIsolationInfo(double& centerMz, double& lowerLimit, double& upperLimit, double& collisionEnergy, double& electronKineticEnergy, FragmentationMode& fragmentationMode) const;

    virtual bool getHasPrecursorInfo() const;
    virtual void getPrecursorInfo(double& selectedMz, double& intensity, int& charge) const;

    virtual double getStartTime() const;

    virtual bool getDataIsContinuous() const {return pointsAreContinuous;}
    size_t getDataSize(bool doCentroid, bool ignoreZeroIntensityPoints) const;
    virtual void getData(bool doCentroid, pwiz::util::BinaryData<double>& mz, pwiz::util::BinaryData<double>& intensities, bool ignoreZeroIntensityPoints) const;

    virtual double getSumY() const {return sumY;}
    virtual double getBasePeakX() const {initializeBasePeak(); return bpX;}
    virtual double getBasePeakY() const {initializeBasePeak(); return bpY;}
    virtual double getMinX() const {return minX;}
    virtual double getMaxX() const {return maxX;}

    ExperimentImplPtr experiment;
    gcroot<MassSpectrumInfo^> spectrumInfo;
    mutable gcroot<MassSpectrum^> spectrum;

#if __CLR_VER > 40000000 // .NET 4
    mutable gcroot<cli::array<PeakClass^>^> peakList;
#endif

    int cycle;

    // data points
    double sumY, minX, maxX;
    vector<double> x, y;
    bool pointsAreContinuous;
    ExperimentType experimentType;

    // precursor info
    double selectedMz, intensity;
    int charge;

    private:

    mutable double bpX, bpY;
    void initializeBasePeak() const
    {
        if (bpY == -1)
        {
            bpY = experiment->basePeakIntensities()[cycle - 1];
            bpX = bpY > 0 ? experiment->basePeakMZs()[cycle - 1] : 0;
        }
    }
};

typedef boost::shared_ptr<SpectrumImpl> SpectrumImplPtr;


WiffFileImpl::WiffFileImpl(const string& wiffpath)
: currentSample(-1), currentPeriod(-1), currentExperiment(-1), currentCycle(-1), wiffpath(wiffpath)
{
    try
    {
/*#if __CLR_VER > 40000000 // .NET 4
        Clearcore2::Licensing::LicenseKeys::Keys = gcnew array<String^> {ABI_BETA_LICENSE_KEY};
#else
        Licenser::LicenseKey = ABI_BETA_LICENSE_KEY;
#endif*/

        provider = DataProviderFactory::CreateDataProvider("", true);
        //provider = gcnew AnalystWiffDataProvider();
        batch = AnalystDataProviderFactory::CreateBatch(ToSystemString(wiffpath), provider);

        // This caused WIFF files where the first sample had been interrupted to
        // throw before they could be successfully constructed, which made investigators
        // unhappy when they were seeking access to later, successfully acquired samples.
        // setSample(1);
    }
    CATCH_AND_FORWARD
}

WiffFileImpl::~WiffFileImpl()
{
    delete batch;
    provider->Close();
    delete provider;
}


int WiffFileImpl::getSampleCount() const
{
    try {return getSampleNames().size();} CATCH_AND_FORWARD
}

int WiffFileImpl::getPeriodCount(int sample) const
{
    try
    {
        setSample(sample);
        return 1;
    }
    CATCH_AND_FORWARD
}

int WiffFileImpl::getExperimentCount(int sample, int period) const
{
    try
    {
        setPeriod(sample, period);
        return msSample->ExperimentCount;
    }
    CATCH_AND_FORWARD
}

int WiffFileImpl::getCycleCount(int sample, int period, int experiment) const
{
    try
    {
        setExperiment(sample, period, experiment);
        return msSample->GetMSExperiment(experiment-1)->Details->NumberOfScans;
    }
    CATCH_AND_FORWARD
}

const vector<string>& WiffFileImpl::getSampleNames() const
{
    try
    {
        if (sampleNames.size() == 0)
        {
            // make duplicate sample names unique by appending the duplicate count
            // e.g. foo, bar, foo (2), foobar, bar (2), foo (3)
            map<string, int> duplicateCountMap;
            array<System::String^>^ sampleNamesManaged = batch->GetSampleNames();
            sampleNames.resize(sampleNamesManaged->Length, "");
            for (int i=0; i < sampleNamesManaged->Length; ++i)
                sampleNames[i] = ToStdString(sampleNamesManaged[i]);

            // inexplicably, some files have more samples than sample names;
            // pad the name vector with duplicates of the last sample name;
            // if there are no names, use empty string
            //while (sampleNames.size() < (size_t) batch->SampleCount)
            //    sampleNames.push_back(sampleNames.back());

            for (size_t i=0; i < sampleNames.size(); ++i)
            {
                int duplicateCount = duplicateCountMap[sampleNames[i]]++; // increment after getting current count
                if (duplicateCount)
                    sampleNames[i] += " (" + lexical_cast<string>(duplicateCount+1) + ")";
            }

        }
        return sampleNames;
    }
    CATCH_AND_FORWARD
}

InstrumentModel WiffFileImpl::getInstrumentModel() const
{
    try
    {
        String^ modelName = sample->Details->InstrumentName->ToUpperInvariant()->Replace(" ", "")->Replace("API", "");
        if (modelName == "UNKNOWN")                 return InstrumentModel_Unknown;
        if (modelName->Contains("2000QTRAP"))       return API2000QTrap; // predicted
        if (modelName->Contains("2000"))            return API2000;
        if (modelName->Contains("2500QTRAP"))       return API2000QTrap; // predicted
        if (modelName->Contains("3000"))            return API3000; // predicted
        if (modelName->Contains("3200QTRAP"))       return API3200QTrap;
        if (modelName->Contains("3200"))            return API3200; // predicted
        if (modelName->Contains("3500QTRAP"))       return API3500QTrap; // predicted
        if (modelName->Contains("4000QTRAP"))       return API4000QTrap;
        if (modelName->Contains("4000"))            return API4000; // predicted
        if (modelName->Contains("QTRAP4500"))       return API4500QTrap;
        if (modelName->Contains("4500"))            return API4500;
        if (modelName->Contains("5000"))            return API5000; // predicted
        if (modelName->Contains("QTRAP5500"))       return API5500QTrap; // predicted
        if (modelName->Contains("5500"))            return API5500; // predicted
        if (modelName->Contains("QTRAP6500"))       return API6500QTrap; // predicted
        if (modelName->Contains("6500"))            return API6500; // predicted
        if (modelName->Contains("QUAD7500"))        return TripleQuad7500;
        if (modelName->Contains("QTRAP"))           return GenericQTrap;
        if (modelName->Contains("QSTARPULSAR"))     return QStarPulsarI; // also covers variants like "API QStar Pulsar i, 0, Qstar"
        if (modelName->Contains("QSTARXL"))         return QStarXL;
        if (modelName->Contains("QSTARELITE"))      return QStarElite;
        if (modelName->Contains("QSTAR"))           return QStar; // predicted
        if (modelName->Contains("TRIPLETOF4600"))   return API4600TripleTOF; // predicted
        if (modelName->Contains("TRIPLETOF5600"))   return API5600TripleTOF;
        if (modelName->Contains("TRIPLETOF6600"))   return API6600TripleTOF; // predicted
        if (modelName->Contains("NLXTOF"))          return NlxTof; // predicted
        if (modelName->Contains("100LC"))           return API100LC; // predicted
        if (modelName->Contains("100"))             return API100; // predicted
        if (modelName->Contains("150MCA"))          return API150MCA; // predicted
        if (modelName->Contains("150EX"))           return API150EX; // predicted
        if (modelName->Contains("165"))             return API165; // predicted
        if (modelName->Contains("300"))             return API300; // predicted
        if (modelName->Contains("350"))             return API350; // predicted
        if (modelName->Contains("365"))             return API365; // predicted
        if (modelName->Contains("X500QTOF"))        return X500QTOF;
        if (modelName->Contains("ZENOTOF7600"))     return ZenoTOF7600;
        throw gcnew Exception("unknown instrument type: " + sample->Details->InstrumentName);
    }
    CATCH_AND_FORWARD
}

std::string WiffFileImpl::getInstrumentSerialNumber() const
{
    try {return ToStdString(sample->Details->InstrumentSerialNumber);} CATCH_AND_FORWARD
}

IonSourceType WiffFileImpl::getIonSourceType() const
{
    try {return (IonSourceType) 0;} CATCH_AND_FORWARD
}

blt::local_date_time WiffFileImpl::getSampleAcquisitionTime(int sample, bool adjustToHostTime) const
{
    try
    {
        setSample(sample);

        System::DateTime acquisitionTime = this->sample->Details->AcquisitionDateTime;
        bpt::ptime pt(boost::gregorian::date(acquisitionTime.Year, boost::gregorian::greg_month(acquisitionTime.Month), acquisitionTime.Day),
            bpt::time_duration(acquisitionTime.Hour, acquisitionTime.Minute, acquisitionTime.Second, bpt::millisec(acquisitionTime.Millisecond).fractional_seconds()));

        if (adjustToHostTime)
        {
            bpt::time_duration tzOffset = bpt::second_clock::universal_time() - bpt::second_clock::local_time();
            return blt::local_date_time(pt + tzOffset, blt::time_zone_ptr()); // treat time as if it came from host's time zone; actual time zone may not be provided by Sciex
        }
        else
            return blt::local_date_time(pt, blt::time_zone_ptr());
    }
    CATCH_AND_FORWARD
}


ExperimentPtr WiffFileImpl::getExperiment(int sample, int period, int experiment) const
{
    setExperiment(sample, period, experiment);
    try
    {
        ExperimentImplPtr msExperiment(new ExperimentImpl(this, sample, period, experiment));
        return msExperiment;
    }
    CATCH_AND_FORWARD
}


ExperimentImpl::ExperimentImpl(const WiffFileImpl* wifffile, int sample, int period, int experiment)
: wifffile_(wifffile), sample(sample), period(period), experiment(experiment), transitionCount(0), simCount(0), initializedTIC_(false), initializedBPC_(false)
{
    try
    {
        wifffile_->setExperiment(sample, period, experiment);
        msExperiment = wifffile_->msSample->GetMSExperiment(experiment-1);

        experimentType = (ExperimentType) msExperiment->Details->ExperimentType;
        if (experimentType == MRM)
            transitionCount = msExperiment->Details->MassRangeInfo->Length;
        else if (experimentType == SIM)
            simCount = msExperiment->Details->MassRangeInfo->Length;

        hasHalfSizeRTWindow = false;
        try
        {
            auto softwareVersion = wifffile_->batch->GetSample(sample)->Details->SoftwareVersion;
            auto sciexOsVersionRegex = gcnew Regex(R"(SCIEX OS (\d+)\.(\d+))");

            auto match = sciexOsVersionRegex->Match(softwareVersion);
            if (match->Success)
            {
                int major = Convert::ToInt32(match->Groups[1]->Value);
                int minor = Convert::ToInt32(match->Groups[2]->Value);
                hasHalfSizeRTWindow = !(major >= 3 && minor >= 1); // currently assumed present in SCIEX OS lower than v3.1
                //if (hasHalfSizeRTWindow)
                //    Console::Error->WriteLine("NOTE: data from " + softwareVersion + " has bugged half-width RTWindows");
            }
        }
        catch (Exception^)
        {
            // ignore read past end of stream: no version details? probably acquired with Analyst?
        }
    }
    CATCH_AND_FORWARD
}

void ExperimentImpl::initializeTIC() const
{
    if (initializedTIC_)
        return;

    try
    {
        TotalIonChromatogram^ tic = msExperiment->GetTotalIonChromatogram();
        ToStdVector(tic->GetActualXValues(), cycleTimes_);
        ToStdVector(tic->GetActualYValues(), cycleIntensities_);

        initializedTIC_ = true;
    }
    CATCH_AND_FORWARD
}

void ExperimentImpl::initializeBPC() const
{
    if (initializedBPC_)
        return;

    try
    {
        BasePeakChromatogramSettings^ bpcs = gcnew BasePeakChromatogramSettings(0, nullptr, nullptr);
        BasePeakChromatogram^ bpc = msExperiment->GetBasePeakChromatogram(bpcs);
        BasePeakChromatogramInfo^ bpci = bpc->Info;
        ToStdVector(bpc->GetActualYValues(), basePeakIntensities_);

        basePeakMZs_.resize(cycleTimes_.size());
        for (size_t i = 0; i < cycleTimes_.size(); ++i)
            basePeakMZs_[i] = bpci->GetBasePeakMass(i);
        
        initializedBPC_ = true;
    }
    catch (...)
    {
        try
        {
            int numCycles = cycleTimes_.size() > 10 ? cycleTimes_.size() - 1 : cycleTimes_.size();
                
            BasePeakChromatogramSettings^ bpcs = gcnew BasePeakChromatogramSettings(0, nullptr, nullptr, 0, cycleTimes_[numCycles - 1]);
            BasePeakChromatogram^ bpc = msExperiment->GetBasePeakChromatogram(bpcs);
            BasePeakChromatogramInfo^ bpci = bpc->Info;

            basePeakMZs_.resize(cycleTimes_.size());        
            basePeakIntensities_.resize(cycleTimes_.size());
            if (numCycles != cycleTimes_.size())
            {
                basePeakMZs_[cycleTimes_.size() - 1] = 0;
                basePeakIntensities_[cycleTimes_.size() - 1] = 0;
            }
            for (size_t i = 0; i < numCycles; ++i)
            {
                basePeakMZs_[i] = bpci->GetBasePeakMass(i);
                basePeakIntensities_[i] = bpc->GetYValue(i);
            }

            initializedBPC_ = true;
        }
        CATCH_AND_FORWARD
    }
}

size_t ExperimentImpl::getSIMSize() const
{
    try {return simCount;} CATCH_AND_FORWARD
}

void ExperimentImpl::getSIM(size_t index, Target& target) const
{
    try
    {
        if (experimentType != SIM)
            return;

        if (index >= simCount)
            throw std::out_of_range("[Experiment::getSIM()] index out of range");

        SIMMassRange^ transition = (SIMMassRange^) msExperiment->Details->MassRangeInfo[index];

        double rtWindowMultiplier = hasHalfSizeRTWindow ? 1 : 0.5;
        target.type = TargetType_SIM;
        target.Q1 = transition->Mass;
        target.dwellTime = transition->DwellTime;
        target.startTime = transition->ExpectedRT - transition->RTWindow * rtWindowMultiplier;
        target.endTime = transition->ExpectedRT + transition->RTWindow * rtWindowMultiplier;
        target.compoundID = ToStdString(transition->Name);
        
        auto parameters = transition->CompoundDepParameters;
        if (parameters->ContainsKey("CE"))
            target.collisionEnergy = fabs((float) parameters["CE"]->Start);
        else
            target.collisionEnergy = 0;

        if (parameters->ContainsKey("DP"))
            target.declusteringPotential = (float) parameters["DP"]->Start;
        else
            target.declusteringPotential = 0;
    }
    CATCH_AND_FORWARD
}

size_t ExperimentImpl::getSRMSize() const
{
    try {return transitionCount;} CATCH_AND_FORWARD
}

void ExperimentImpl::getSRM(size_t index, Target& target) const
{
    try
    {
        if (experimentType != MRM)
            return;

        if (index >= transitionCount)
            throw std::out_of_range("[Experiment::getSRM()] index out of range");

        MRMMassRange^ transition = (MRMMassRange^) msExperiment->Details->MassRangeInfo[index];

        double rtWindowMultiplier = hasHalfSizeRTWindow ? 1 : 0.5;
        target.type = TargetType_SRM;
        target.Q1 = transition->Q1Mass;
        target.Q3 = transition->Q3Mass;
        target.dwellTime = transition->DwellTime;
        target.startTime = transition->ExpectedRT - transition->RTWindow * rtWindowMultiplier;
        target.endTime = transition->ExpectedRT + transition->RTWindow * rtWindowMultiplier;
        target.compoundID = ToStdString(transition->Name);

        auto parameters = transition->CompoundDepParameters;
        if (parameters->ContainsKey("CE"))
            target.collisionEnergy = fabs((float) parameters["CE"]->Start);
        else
            target.collisionEnergy = 0;

        if (parameters->ContainsKey("DP"))
            target.declusteringPotential = (float) parameters["DP"]->Start;
        else
            target.declusteringPotential = 0;
    }
    CATCH_AND_FORWARD
}

double ExperimentImpl::getSIC(size_t index, pwiz::util::BinaryData<double>& times, pwiz::util::BinaryData<double>& intensities, bool ignoreScheduledLimits) const
{
    try
    {
        if (index >= transitionCount+simCount)
            throw std::out_of_range("[Experiment::getSIC()] index " + lexical_cast<string>(index) + " out of range");

        Target target;
        getSRM(index, target);

        ExtractedIonChromatogramSettings^ option = gcnew ExtractedIonChromatogramSettings(index);
        if (ignoreScheduledLimits)
        {
            option->StartCycle = 0;
            option->EndCycle = convertRetentionTimeToCycle(cycleTimes().back());
            option->UseStartEndCycle = true;
        }
        else if (target.startTime != target.endTime)
        {
            option->StartCycle = convertRetentionTimeToCycle(target.startTime);
            option->EndCycle = convertRetentionTimeToCycle(target.endTime);
            option->UseStartEndCycle = true;
        }

        ExtractedIonChromatogram^ xic = msExperiment->GetExtractedIonChromatogram(option);

        ToBinaryData(xic->GetActualXValues(), times);
        ToBinaryData(xic->GetActualYValues(), intensities);
        return xic->MaxYValue;
    }
    CATCH_AND_FORWARD
}

void ExperimentImpl::getSIC(size_t index, pwiz::util::BinaryData<double>& times, pwiz::util::BinaryData<double>& intensities,
                            double& basePeakX, double& basePeakY, bool ignoreScheduledLimits) const
{
    basePeakY = getSIC(index, times, intensities, ignoreScheduledLimits);

    try
    {
        basePeakX = 0;
        for (size_t i=0; i < intensities.size(); ++i)
            if (intensities[i] == basePeakY)
            {
                basePeakX = times[i];
                break;
            }
    }
    CATCH_AND_FORWARD
}

void ExperimentImpl::getAcquisitionMassRange(double& startMz, double& stopMz) const
{
    try
    {
        if ((ExperimentType) msExperiment->Details->ExperimentType != MRM &&
            (ExperimentType) msExperiment->Details->ExperimentType != SIM)
        {
            startMz = msExperiment->Details->StartMass;
            stopMz = msExperiment->Details->EndMass;
        }
        else
            startMz = stopMz = 0;
    }
    CATCH_AND_FORWARD
}

ScanType ExperimentImpl::getScanType() const
{
    try {return (ScanType) msExperiment->Details->SpectrumType;} CATCH_AND_FORWARD
}

ExperimentType ExperimentImpl::getExperimentType() const
{
    try {return (ExperimentType) msExperiment->Details->ExperimentType;} CATCH_AND_FORWARD
}

Polarity ExperimentImpl::getPolarity() const
{
    try {return (Polarity) msExperiment->Details->Polarity;} CATCH_AND_FORWARD
}

int ExperimentImpl::getMsLevel(int cycle) const
{
    return msExperiment->GetMassSpectrumInfo(cycle - 1)->MSLevel;
}

double ExperimentImpl::convertCycleToRetentionTime(int cycle) const
{
    try {return msExperiment->GetRTFromExperimentScanIndex(cycle);} CATCH_AND_FORWARD
}

double ExperimentImpl::convertRetentionTimeToCycle(double rt) const
{
    try {return msExperiment->RetentionTimeToExperimentScan(rt);} CATCH_AND_FORWARD
}

void ExperimentImpl::getTIC(std::vector<double>& times, std::vector<double>& intensities) const
{
    try
    {
        times = cycleTimes();
        intensities = cycleIntensities();
    }
    CATCH_AND_FORWARD
}

void ExperimentImpl::getBPC(std::vector<double>& times, std::vector<double>& intensities) const
{
    try
    {
        times = cycleTimes();
        intensities = basePeakIntensities();
    }
    CATCH_AND_FORWARD
}


SpectrumImpl::SpectrumImpl(ExperimentImplPtr experiment, int cycle)
: experiment(experiment), cycle(cycle), selectedMz(0), bpY(-1), bpX(-1)
{
    try
    {
        spectrumInfo = experiment->msExperiment->GetMassSpectrumInfo(cycle-1);

        experimentType = experiment->getExperimentType();
        pointsAreContinuous = !spectrumInfo->CentroidMode && experimentType != MRM && experimentType != SIM;

        sumY = experiment->cycleIntensities()[cycle-1];
        //minX = experiment->; // TODO Mass range?
        //maxX = spectrum->MaximumXValue;

        if (spectrumInfo->IsProductSpectrum)
        {
            selectedMz = spectrumInfo->ParentMZ;
            intensity = 0;
            charge = spectrumInfo->ParentChargeState;
        }
    }
    CATCH_AND_FORWARD
}

int SpectrumImpl::getMSLevel() const
{
    try {return spectrumInfo->MSLevel == 0 ? 1 : spectrumInfo->MSLevel;} CATCH_AND_FORWARD
}

bool SpectrumImpl::getHasIsolationInfo() const
{
    return ((ExperimentType)experiment->msExperiment->Details->ExperimentType == Product ||
            (ExperimentType)experiment->msExperiment->Details->ExperimentType == Precursor) &&
           experiment->msExperiment->Details->MassRangeInfo->Length > 0;
}

void SpectrumImpl::getIsolationInfo(double& centerMz, double& lowerLimit, double& upperLimit, double& collisionEnergy, double& electronKineticEnergy, FragmentationMode& fragmentationMode) const
{
    if (!getHasIsolationInfo())
        return;

    try
    {
        double isolationWidth = ((FragmentBasedScanMassRange^)(experiment->msExperiment->Details->MassRangeInfo[0]))->IsolationWindow;
        centerMz = getHasPrecursorInfo() ? selectedMz : (double)((FragmentBasedScanMassRange^)(experiment->msExperiment->Details->MassRangeInfo[0]))->FixedMasses[0];
        lowerLimit = centerMz - isolationWidth / 2;
        upperLimit = centerMz + isolationWidth / 2;

        auto parameters = experiment->msExperiment->Details->Parameters;
        if (parameters->ContainsKey("CE"))
        {
            auto ceRamp = parameters["CE"];
            if (ceRamp->Start == 0)
                collisionEnergy = fabs(ceRamp->Stop);
            else if (ceRamp->Stop == 0)
                collisionEnergy = ceRamp->Start;
            else
                collisionEnergy = (ceRamp->Stop + ceRamp->Start) / 2;
            collisionEnergy = fabs(collisionEnergy);
        }
        else
            collisionEnergy = 0;
        
        fragmentationMode = FragmentationMode_CID;        
    }
    CATCH_AND_FORWARD
}

bool SpectrumImpl::getHasPrecursorInfo() const
{
    return selectedMz != 0;
}

void SpectrumImpl::getPrecursorInfo(double& selectedMz, double& intensity, int& charge) const
{
    selectedMz = this->selectedMz;
    intensity = this->intensity;
    charge = this->charge;
}

double SpectrumImpl::getStartTime() const
{
    return spectrumInfo->StartRT;
}

size_t SpectrumImpl::getDataSize(bool doCentroid, bool ignoreZeroIntensityPoints) const
{
    try
    {
        if (experimentType == MRM || experimentType == SIM)
            return experiment->msExperiment->Details->MassRangeInfo->Length;

#if __CLR_VER > 40000000 // .NET 4
        if (doCentroid)
        {
            if ((cli::array<PeakClass^>^) peakList == nullptr) peakList = experiment->msExperiment->GetPeakArray(cycle-1);
            return (size_t) peakList->Length;
        }
        else
#endif
        {
            if ((MassSpectrum^) spectrum == nullptr)
            {
                spectrum = experiment->msExperiment->GetMassSpectrum(cycle-1);
#if __CLR_VER > 40000000 // the .NET 4 version has an efficient way to add zeros

                if (!ignoreZeroIntensityPoints && pointsAreContinuous)
                    experiment->msExperiment->AddZeros((MassSpectrum^) spectrum, 1);
#endif
            }
            return (size_t) spectrum->NumDataPoints;
        }
    }
    CATCH_AND_FORWARD
}


void SpectrumImpl::getData(bool doCentroid, pwiz::util::BinaryData<double>& mz, pwiz::util::BinaryData<double>& intensities, bool ignoreZeroIntensityPoints) const
{
    try
    {
#if __CLR_VER > 40000000 // .NET 4
        if (doCentroid && pointsAreContinuous)
        {
            if ((cli::array<PeakClass^>^) peakList == nullptr) peakList = experiment->msExperiment->GetPeakArray(cycle-1);
            size_t numPoints = peakList->Length;
            mz.resize(numPoints);
            intensities.resize(numPoints);
            for (size_t i=0; i < numPoints; ++i)
            {
                PeakClass^ peak = peakList[(int)i];
                mz[i] = peak->xValue;
                intensities[i] = peak->area * PEAK_AREA_SCALE_FACTOR;
            }
        }
        else
#endif
        {
            if ((MassSpectrum^) spectrum == nullptr)
            {
                spectrum = experiment->msExperiment->GetMassSpectrum(cycle-1);
#if __CLR_VER > 40000000 // the .NET 4 version has an efficient way to add zeros
                if (!ignoreZeroIntensityPoints && pointsAreContinuous)
                    experiment->msExperiment->AddZeros((MassSpectrum^) spectrum, 1);
#endif
            }

            // XValues are not m/z values for MRM and SIM experiments
            if (experimentType == MRM || experimentType == SIM)
            {
                auto massRangeInfo = experiment->msExperiment->Details->MassRangeInfo;
                if (massRangeInfo->Length != spectrum->NumDataPoints)
                    throw std::runtime_error(ToStdString("[WiffFile::getData] MassRangeInfo length does not equal NumDataPoints for " +
                                                         experiment->msExperiment->Details->ExperimentType.ToString() + " experiment"));
                mz.resize(massRangeInfo->Length);
                intensities.resize(mz.size());
                if (experimentType == MRM)
                    for (size_t i = 0; i < mz.size(); ++i)
                        mz[i] = ((MRMMassRange^) massRangeInfo[i])->Q3Mass;
                else
                    for (size_t i = 0; i < mz.size(); ++i)
                        mz[i] = ((SIMMassRange^) massRangeInfo[i])->Mass;
            }
            else
                ToBinaryData(spectrum->GetActualXValues(), mz);

            // YValues seem to be valid for all experiments
            ToBinaryData(spectrum->GetActualYValues(), intensities);
        }
    }
    CATCH_AND_FORWARD
}


SpectrumPtr WiffFileImpl::getSpectrum(int sample, int period, int experiment, int cycle) const
{
    try
    {
        ExperimentPtr msExperiment = getExperiment(sample, period, experiment);
        return getSpectrum(msExperiment, cycle);
    }
    CATCH_AND_FORWARD
}

SpectrumPtr WiffFileImpl::getSpectrum(ExperimentPtr experiment, int cycle) const
{
    SpectrumImplPtr spectrum(new SpectrumImpl(boost::static_pointer_cast<ExperimentImpl>(experiment), cycle));
    return spectrum;
}


int WiffFileImpl::getADCTraceCount(int sample) const
{
    try
    {
        setSample(sample);
        if (!this->sample->HasADCData || this->sample->ADCSample == nullptr)
            return 0;

        return this->sample->ADCSample->ChannelCount;
    }
    CATCH_AND_FORWARD
}

string WiffFileImpl::getADCTraceName(int sample, int traceIndex) const
{
    try
    {
        setSample(sample);
        return ToStdString(this->sample->ADCSample->GetChannelNameAt(traceIndex));
    }
    CATCH_AND_FORWARD
}

void WiffFileImpl::getADCTrace(int sample, int traceIndex, ADCTrace& trace) const
{
    try
    {
        setSample(sample);
        auto adcData = this->sample->ADCSample->GetADCData(traceIndex);
        ToBinaryData(adcData->GetActualXValues(), trace.x);
        ToBinaryData(adcData->GetActualYValues(), trace.y);
        trace.xUnits = ToStdString(adcData->XUnits);
        trace.yUnits = ToStdString(adcData->YUnits);
    }
    CATCH_AND_FORWARD
}

void WiffFileImpl::getTWC(int sample, ADCTrace& totalWavelengthChromatogram) const
{
    try
    {
        setSample(sample);

        try
        {
            if (this->sample->DADSample == nullptr)
                return;
            auto twc = this->sample->DADSample->GetTotalWavelengthChromatogram();
            ToBinaryData(twc->GetActualXValues(), totalWavelengthChromatogram.x);
            ToBinaryData(twc->GetActualYValues(), totalWavelengthChromatogram.y);
            totalWavelengthChromatogram.xUnits = ToStdString(twc->XUnits);
            totalWavelengthChromatogram.yUnits = ToStdString(twc->YUnits);
        }
        catch (...)
        {
        }
    }
    CATCH_AND_FORWARD
}


void WiffFileImpl::setSample(int sample) const
{
    try
    {
        if (sample != currentSample)
        {
            this->sample = batch->GetSample(sample-1);
            msSample = this->sample->MassSpectrometerSample;

            currentSample = sample;
            currentPeriod = currentExperiment = currentCycle = -1;
        }
    }
    CATCH_AND_FORWARD
}

void WiffFileImpl::setPeriod(int sample, int period) const
{
    try
    {
        setSample(sample);

        if (period != currentPeriod)
        {
            //reader->PeriodNum = period;
            currentPeriod = period;
            currentExperiment = currentCycle = -1;
        }
    }
    CATCH_AND_FORWARD
}

void WiffFileImpl::setExperiment(int sample, int period, int experiment) const
{
    try
    {
        setPeriod(sample, period);

        if (experiment != currentExperiment)
        {
            //reader->ExperimentNum = experiment;
            currentExperiment = experiment;
            currentCycle = -1;
        }
    }
    CATCH_AND_FORWARD
}

void WiffFileImpl::setCycle(int sample, int period, int experiment, int cycle) const
{
    try
    {
        setExperiment(sample, period, experiment);

        if (cycle != currentCycle)
        {
            //reader->SetCycles(cycle);
            currentCycle = cycle;
        }
    }
    CATCH_AND_FORWARD
}


PWIZ_API_DECL
WiffFilePtr WiffFile::create(const string& wiffpath)
{
    WiffFilePtr wiffFile;

    try
    {
        if (bal::iends_with(wiffpath, ".wiff2"))
        {
            wiffFile = boost::static_pointer_cast<WiffFile>(boost::make_shared<WiffFile2Impl>(wiffpath));
        }
        else
            wiffFile = boost::static_pointer_cast<WiffFile>(boost::make_shared<WiffFileImpl>(wiffpath));

        return wiffFile;
    }
    CATCH_AND_FORWARD
}


} // ABI
} // vendor_api
} // pwiz

#endif
