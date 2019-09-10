//
// $Id: WiffFile2.cpp 10494 2017-02-21 16:53:20Z pcbrefugee $
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

#include <msclr/auto_gcroot.h>

using namespace System::Collections::Generic;

#ifdef __INTELLISENSE__
#using <SciexToolKit.dll>
#endif

using namespace Clearcore2::DataReader;

namespace pwiz {
namespace vendor_api {
namespace ABI {


class WiffFile2Impl : public WiffFile
{
    public:
    WiffFile2Impl(const std::string& wiffpath);
    ~WiffFile2Impl()
    {
        delete dataReader;
    }

    // prevent multiple Wiff2 files from opening at once, because it currently rewrites DataReader.config every time
    static gcroot<System::Object^> mutex;

    msclr::auto_gcroot<IDataReader^> dataReader;
    mutable gcroot<IList<ISampleInformation^>^> allSamples;
    mutable gcroot<ISampleInformation^> msSample;

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

    virtual int getADCTraceCount(int sampleIndex) const { return 0; }
    virtual std::string getADCTraceName(int sampleIndex, int traceIndex) const { throw std::out_of_range("WIFF2 does not support ADC traces"); }
    virtual void getADCTrace(int sampleIndex, int traceIndex, ADCTrace& trace) const { throw std::out_of_range("WIFF2 does not support ADC traces"); };

    void setSample(int sample) const;
    void setPeriod(int sample, int period) const;
    void setExperiment(int sample, int period, int experiment) const;
    void setCycle(int sample, int period, int experiment, int cycle) const;

    mutable int currentSampleIndex, currentPeriod, currentExperiment, currentCycle;

    private:
    std::string wiffpath_;
    // on first access, sample names are made unique (giving duplicates a count suffix) and cached
    mutable vector<string> sampleNames;
};

typedef boost::shared_ptr<WiffFile2Impl> WiffFile2ImplPtr;


struct Experiment2Impl : public Experiment
{
    Experiment2Impl(const WiffFile2Impl* WiffFile2, int sample, int period, int experiment);

    virtual int getSampleNumber() const {return sample;}
    virtual int getPeriodNumber() const {return period;}
    virtual int getExperimentNumber() const {return experiment;}

    virtual size_t getSIMSize() const;
    virtual void getSIM(size_t index, Target& target) const;

    virtual size_t getSRMSize() const;
    virtual void getSRM(size_t index, Target& target) const;

    virtual void getSIC(size_t index, pwiz::util::BinaryData<double>& times, pwiz::util::BinaryData<double>& intensities) const;
    virtual void getSIC(size_t index, pwiz::util::BinaryData<double>& times, pwiz::util::BinaryData<double>& intensities,
                        double& basePeakX, double& basePeakY) const;

    virtual bool getHasIsolationInfo() const;
    virtual void getIsolationInfo(int cycle, double& centerMz, double& lowerLimit, double& upperLimit) const;
    virtual void getPrecursorInfo(int cycle, double& centerMz, int& charge) const;

    virtual void getAcquisitionMassRange(double& startMz, double& stopMz) const;
    virtual ScanType getScanType() const;
    virtual ExperimentType getExperimentType() const;
    virtual Polarity getPolarity() const;
    virtual int getMsLevel(int cycle) const;

    virtual double convertCycleToRetentionTime(int cycle) const;
    virtual double convertRetentionTimeToCycle(double rt) const;

    virtual void getTIC(std::vector<double>& times, std::vector<double>& intensities) const;
    virtual void getBPC(std::vector<double>& times, std::vector<double>& intensities) const;

    const WiffFile2Impl* wiffFile_;
    gcroot<IExperiment^> msExperiment;
    gcroot<IList<MrmInfo^>^> mrmInfo;
    int sample, period, experiment;

    ExperimentType experimentType;
    size_t simCount;
    size_t transitionCount;

    typedef map<pair<double, double>, pair<int, int> > TransitionParametersMap;
    TransitionParametersMap transitionParametersMap;

    const vector<double>& cycleTimes() const {initializeTIC(); return cycleTimes_;}
    const vector<double>& cycleIntensities() const {initializeTIC(); return cycleIntensities_;}
    const vector<double>& basePeakMZs() const {initializeBPC(); return basePeakMZs_;}
    const vector<double>& basePeakIntensities() const {initializeBPC(); return basePeakIntensities_;}

    private:
    void initializeTIC() const;
    void initializeBPC() const;
    mutable bool initializedTIC_;
    mutable bool initializedBPC_;
    mutable pwiz::util::BinaryData<double> cycleTimes_;
    mutable pwiz::util::BinaryData<double> cycleIntensities_;
    mutable vector<double> basePeakMZs_;
    mutable pwiz::util::BinaryData<double> basePeakIntensities_;
};

typedef boost::shared_ptr<Experiment2Impl> Experiment2ImplPtr;


struct Spectrum2Impl : public Spectrum
{
    Spectrum2Impl(Experiment2ImplPtr experiment, int cycle);

    virtual int getSampleNumber() const {return experiment->sample;}
    virtual int getPeriodNumber() const {return experiment->period;}
    virtual int getExperimentNumber() const {return experiment->experiment;}
    virtual int getCycleNumber() const {return cycle;}

    virtual int getMSLevel() const;

    virtual bool getHasIsolationInfo() const;
    virtual void getIsolationInfo(double& centerMz, double& lowerLimit, double& upperLimit) const;

    virtual bool getHasPrecursorInfo() const;
    virtual void getPrecursorInfo(double& selectedMz, double& intensity, int& charge) const;

    virtual double getStartTime() const;
    
    virtual size_t getDataSize(bool doCentroid, bool ignoreZeroIntensityPoints = false) const;
    virtual void getData(bool doCentroid, pwiz::util::BinaryData<double>& mz, pwiz::util::BinaryData<double>& intensities, bool ignoreZeroIntensityPoints) const;

    virtual double getSumY() const {return sumY;}
    virtual double getBasePeakX() const {initializeBasePeak(); return bpX;}
    virtual double getBasePeakY() const {initializeBasePeak(); return bpY;}
    virtual double getMinX() const {return minX;}
    virtual double getMaxX() const {return maxX;}

    Experiment2ImplPtr experiment;

    mutable gcroot<IXyData<double>^> peakList;
    mutable gcroot<IXyData<double>^> spectrumData;

    int cycle;

    // data points
    double sumY, minX, maxX;

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

typedef boost::shared_ptr<Spectrum2Impl> Spectrum2ImplPtr;


void ToStdVectorsFromXyData(IXyData<double>^ xyData, pwiz::util::BinaryData<double>& xVector, pwiz::util::BinaryData<double>& yVector, double yScaleFactor = 1.0)
{
    xVector.clear();
    yVector.clear();
    if (xyData->Count > 0)
    {
        xVector.reserve(xyData->Count);
        yVector.reserve(xyData->Count);
        for (size_t i = 0, end = xyData->Count; i < end; ++i)
        {
            xVector.push_back(xyData->GetXValue(i));
            yVector.push_back(xyData->GetYValue(i) * yScaleFactor);
        }
    }
}


gcroot<System::Object^> WiffFile2Impl::mutex = gcnew System::Object();

WiffFile2Impl::WiffFile2Impl(const string& wiffpath)
: currentSampleIndex(-1), currentPeriod(-1), currentExperiment(-1), currentCycle(-1), wiffpath_(wiffpath)
{
    try
    {
        System::Threading::Monitor::Enter(mutex);
        dataReader = DataReaderFactory::CreateReader();
        allSamples = dataReader->ExtractSampleInformation(ToSystemString(wiffpath));
        System::Threading::Monitor::Exit(mutex);

        // This caused WIFF files where the first sample had been interrupted to
        // throw before they could be successfully constructed, which made investigators
        // unhappy when they were seeking access to later, successfully acquired samples.
        // setSample(1);
    }
    catch (std::exception&) { System::Threading::Monitor::Exit(mutex); throw; }
    catch (System::Exception^ e) { System::Threading::Monitor::Exit(mutex); throw std::runtime_error(trimFunctionMacro(__FUNCTION__, "") + pwiz::util::ToStdString(e->Message)); }
}


int WiffFile2Impl::getSampleCount() const
{
    try {return getSampleNames().size();} CATCH_AND_FORWARD
}

int WiffFile2Impl::getPeriodCount(int sample) const
{
    try
    {
        setSample(sample);
        return 1;
    }
    CATCH_AND_FORWARD
}

int WiffFile2Impl::getExperimentCount(int sample, int period) const
{
    try
    {
        setPeriod(sample, period);        
        return msSample->NumberOfExperiments;
    }
    CATCH_AND_FORWARD
}

int WiffFile2Impl::getCycleCount(int sample, int period, int experiment) const
{
    try
    {
        setExperiment(sample, period, experiment);
        return msSample->GetExperiment(experiment-1)->NumberOfScans;
    }
    CATCH_AND_FORWARD
}

const vector<string>& WiffFile2Impl::getSampleNames() const
{
    try
    {
        if (sampleNames.size() == 0)
        {
            // make duplicate sample names unique by appending the duplicate count
            // e.g. foo, bar, foo (2), foobar, bar (2), foo (3)
            map<string, int> duplicateCountMap;
            IList<ISampleInformation^> ^unwrappedAllSamples = allSamples;
            sampleNames.resize(unwrappedAllSamples->Count, "");
            for (int i = 0; i < unwrappedAllSamples->Count; ++i)
                sampleNames[i] = ToStdString(unwrappedAllSamples[i]->SampleName);

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

InstrumentModel WiffFile2Impl::getInstrumentModel() const
{
    try
    {
        String^ modelName = msSample->InstrumentName->ToUpperInvariant()->Replace(" ", "")->Replace("API", "");
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
        throw gcnew Exception("unknown instrument type: " + msSample->InstrumentName);
    }
    CATCH_AND_FORWARD
}

std::string WiffFile2Impl::getInstrumentSerialNumber() const
{
    try
    {
        using namespace System::Reflection;

        // HACK: the current version of SciexToolKit has instrument serial number hidden in internal types
        auto sciexToolkit = IExperiment::typeid->Assembly;
        auto sampleInformationType = sciexToolkit->GetType("Clearcore2.DataReader.SampleInformation");
        auto sampleDataSampleType = sciexToolkit->GetType("Clearcore2.Data.DataAccess.SampleData.Sample");
        auto sampleDataSampleInfoType = sciexToolkit->GetType("Clearcore2.Data.DataAccess.SampleData.SampleInfo");
        auto sampleDataSample = sampleInformationType->GetProperty("Sample")->GetMethod->Invoke(msSample, nullptr);
        auto sampleDataSampleInfo = sampleDataSampleType->GetProperty("Details")->GetMethod->Invoke(sampleDataSample, nullptr);
        auto serialNumber = (String^) sampleDataSampleInfoType->GetProperty("InstrumentSerialNumber")->GetMethod->Invoke(sampleDataSampleInfo, nullptr);

        return ToStdString(serialNumber);
    }
    CATCH_AND_FORWARD
}

IonSourceType WiffFile2Impl::getIonSourceType() const
{
    try {return (IonSourceType) 0;} CATCH_AND_FORWARD
}

blt::local_date_time WiffFile2Impl::getSampleAcquisitionTime(int sample, bool adjustToHostTime) const
{
    try
    {
        setSample(sample);

        System::DateTime acquisitionTime = this->msSample->AcquisitionDateTime;
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


ExperimentPtr WiffFile2Impl::getExperiment(int sample, int period, int experiment) const
{
    setExperiment(sample, period, experiment);
    Experiment2ImplPtr msExperiment(new Experiment2Impl(this, sample, period, experiment));
    return msExperiment;
}


Experiment2Impl::Experiment2Impl(const WiffFile2Impl* wiffFile, int sample, int period, int experiment)
: wiffFile_(wiffFile), sample(sample), period(period), experiment(experiment), transitionCount(0), simCount(0), initializedTIC_(false), initializedBPC_(false)
{
    try
    {
        wiffFile_->setExperiment(sample, period, experiment);
        msExperiment = wiffFile_->msSample->GetExperiment(experiment-1);

        experimentType = getExperimentType();
        if (experimentType == MRM)
        {
            transitionCount = msExperiment->NumberOfMrm;

            auto mrmEnumerable = msExperiment->GetMrmInfo();
            mrmInfo = gcnew List<MrmInfo^>();
            for each (MrmInfo^ info in mrmEnumerable)
            {
                mrmInfo->Add(info);
            }
        }
        //else if (experimentType == SIM)
        //    simCount = msExperiment->NumberOfMrm;

    }
    CATCH_AND_FORWARD
}

void Experiment2Impl::initializeTIC() const
{
    if (initializedTIC_)
        return;

    try
    {
        IXyData<double>^ tic = msExperiment->GetTotalIonChromatogram();
        

        for (size_t i = 0; i < tic->Count; i++)
        {
            tic->GetXValue(i);
        }        

        ToStdVectorsFromXyData(tic, cycleTimes_, cycleIntensities_);

        initializedTIC_ = true;
    }
    CATCH_AND_FORWARD
}

void Experiment2Impl::initializeBPC() const
{
    if (initializedBPC_)
        return;

    try
    {
        using namespace System::Reflection;

        // HACK: the current version version of SciexToolKit has Clearcore2.Data.DataAccess.SampleData.BasePeakChromatogramSettings set to internal but it must be used to get the BPC
        auto bpcsType = IExperiment::typeid->Assembly->GetType("Clearcore2.Data.DataAccess.SampleData.BasePeakChromatogramSettings");
        auto bpcsCtor = bpcsType->GetConstructor(gcnew array<Type^>{ Double::typeid, array<double>::typeid, array<double>::typeid });
        auto bpcs = bpcsCtor->Invoke(gcnew array<Object^>{ gcnew Double(0), nullptr, nullptr });
        auto bpcsMethod = IExperiment::typeid->GetMethod("GetBasePeakChromatogram", gcnew array<Type^>{ bpcsType });
        auto bpmMethod = IExperiment::typeid->GetMethod("GetBasePeakMass", gcnew array<Type^>{ Int32::typeid, bpcsType });

        IXyData<double>^ bpc = (IXyData<double>^) bpcsMethod->Invoke(msExperiment, gcnew array<Object^>{ bpcs });
        ToStdVectorsFromXyData(bpc, cycleTimes_, basePeakIntensities_);
        
        basePeakMZs_.resize(cycleTimes_.size());
        auto bpmArgs = gcnew array<Object^> { nullptr, nullptr };
        for (size_t i = 0; i < cycleTimes_.size(); ++i)
        {
            //basePeakMZs_[i] = msExperiment->GetBasePeakMass(i, nullptr);
            bpmArgs[0] = (int) i;
            double mz = (double) bpmMethod->Invoke(msExperiment, bpmArgs);
            basePeakMZs_[i] = mz;
        }
        
        initializedBPC_ = true;
    }
    CATCH_AND_FORWARD
}

size_t Experiment2Impl::getSIMSize() const
{
    try {return simCount;} CATCH_AND_FORWARD
}

void Experiment2Impl::getSIM(size_t index, Target& target) const
{
    try
    {
        if (experimentType != SIM)
            return;

        if (index >= simCount)
            throw std::out_of_range("[Experiment::getSIM()] index out of range");

        /*SIMMassRange^ transition = (SIMMassRange^) msExperiment->Details->MassRangeInfo[index];

        target.type = TargetType_SIM;
        target.Q1 = transition->Mass;
        target.dwellTime = transition->DwellTime;
        // TODO: store RTWindow?

        // TODO: use NaN to indicate these values should be considered missing?
        target.collisionEnergy = 0;
        target.declusteringPotential = 0;*/
    }
    CATCH_AND_FORWARD
}

size_t Experiment2Impl::getSRMSize() const
{
    try {return transitionCount;} CATCH_AND_FORWARD
}

void Experiment2Impl::getSRM(size_t index, Target& target) const
{
    try
    {
        if (experimentType != MRM)
            return;

        if (index >= transitionCount)
            throw std::out_of_range("[Experiment::getSRM()] index out of range");

        MrmInfo^ transition = ((IList<MrmInfo^>^) mrmInfo)[index];
        //const pair<int, int>& e = transitionParametersMap.find(make_pair(transition->Q1Mass->MassAsDouble, transition->Q3Mass->MassAsDouble))->second;

        target.type = TargetType_SRM;
        target.Q1 = transition->Q1;
        target.Q3 = transition->Q3;
        target.dwellTime = transition->Dwell;
        target.compoundID = ToStdString(transition->Name);

        target.collisionEnergy = 0; // transition->CE;
        target.declusteringPotential = 0; // transition->DP;
        // TODO: store RTWindow?

    }
    CATCH_AND_FORWARD
}

void Experiment2Impl::getSIC(size_t index, pwiz::util::BinaryData<double>& times, pwiz::util::BinaryData<double>& intensities) const
{
    double x, y;
    getSIC(index, times, intensities, x, y);
}

void Experiment2Impl::getSIC(size_t index, pwiz::util::BinaryData<double>& times, pwiz::util::BinaryData<double>& intensities,
                            double& basePeakX, double& basePeakY) const
{
    try
    {
        if (index >= transitionCount && index >= simCount)
            throw std::out_of_range("[Experiment::getSIC()] index out of range");

        if (experimentType == MRM)
        {
            IXyData<double>^ xic = msExperiment->GetMrmChromatogram(index);

            ToStdVectorsFromXyData(xic, times, intensities);

            int indexOfMax = std::distance(std::begin(intensities), std::max_element(std::begin(intensities), std::end(intensities)));
            basePeakX = times[indexOfMax];
            basePeakY = intensities[indexOfMax];
        }
    }
    CATCH_AND_FORWARD
}

void Experiment2Impl::getAcquisitionMassRange(double& startMz, double& stopMz) const
{
    try
    {
        if (experimentType != MRM)
        {
            startMz = msExperiment->MassRange->Start;
            stopMz = msExperiment->MassRange->End;
        }
        else
            startMz = stopMz = 0;
    }
    CATCH_AND_FORWARD
}

ScanType Experiment2Impl::getScanType() const
{
    try 
    {
        if (msExperiment->SpectrumType == "MRMScan")
            return ScanType::MRMScan;
        if (msExperiment->SpectrumType == "SIMScan")
            return ScanType::SIMScan;
        if (msExperiment->SpectrumType == "FullScan")
            return ScanType::FullScan;
                
        return ScanType::ScanType_Unknown;

    } CATCH_AND_FORWARD
}

ExperimentType Experiment2Impl::getExperimentType() const
{
    try
    {
        if (msExperiment->ExperimentType == "MS")
            return ExperimentType::MS;
        if (msExperiment->ExperimentType == "Product")
            return ExperimentType::Product;
        if (msExperiment->ExperimentType == "Precursor")
            return ExperimentType::Precursor;
        if (msExperiment->ExperimentType == "NeutralGainOrLoss")
            return ExperimentType::NeutralGainOrLoss;
        if (msExperiment->ExperimentType == "SIM")
            return ExperimentType::SIM;
        if (msExperiment->ExperimentType == "MRM")
            return ExperimentType::MRM;

        return ExperimentType::MS;

    } CATCH_AND_FORWARD
}

Polarity Experiment2Impl::getPolarity() const
{
    try {return msExperiment->IsPolarityPositive ? Positive : Negative;} CATCH_AND_FORWARD
}

int Experiment2Impl::getMsLevel(int cycle) const
{
    try { return msExperiment->GetMsLevel(cycle - 1); } CATCH_AND_FORWARD
}

double Experiment2Impl::convertCycleToRetentionTime(int cycle) const
{
    try {return msExperiment->GetRetentionTimeFromScanIndex(cycle - 1);} CATCH_AND_FORWARD
}

double Experiment2Impl::convertRetentionTimeToCycle(double rt) const
{
    try {return msExperiment->GetScanIndexFromRetentionTime(rt) + 1;} CATCH_AND_FORWARD
}

void Experiment2Impl::getTIC(std::vector<double>& times, std::vector<double>& intensities) const
{
    try
    {
        times = cycleTimes();
        intensities = cycleIntensities();
    }
    CATCH_AND_FORWARD
}

void Experiment2Impl::getBPC(std::vector<double>& times, std::vector<double>& intensities) const
{
    try
    {
        times = cycleTimes();
        intensities = basePeakIntensities();
    }
    CATCH_AND_FORWARD
}

bool Experiment2Impl::getHasIsolationInfo() const
{
    return experimentType == Product;
}

void Experiment2Impl::getIsolationInfo(int cycle, double& centerMz, double& lowerLimit, double& upperLimit) const
{
    if (!getHasIsolationInfo())
        return;

    try
    {
        double isolationWidth = msExperiment->IsolationWidth;
        centerMz = msExperiment->GetPrecursorMz(cycle - 1);
        lowerLimit = centerMz - isolationWidth / 2;
        upperLimit = centerMz + isolationWidth / 2;
    }
    CATCH_AND_FORWARD
}

void Experiment2Impl::getPrecursorInfo(int cycle, double& centerMz, int& charge) const
{
    if (!getHasIsolationInfo() || basePeakIntensities().at(cycle - 1) == 0)
        return;

    try
    {
        centerMz = msExperiment->GetPrecursorMz(cycle - 1);
        charge = msExperiment->GetCharge(cycle - 1);
    }
    CATCH_AND_FORWARD
}


Spectrum2Impl::Spectrum2Impl(Experiment2ImplPtr experiment, int cycle)
: experiment(experiment), cycle(cycle), selectedMz(0), bpY(-1), bpX(-1)
{
    try
    {
        sumY = experiment->cycleIntensities()[cycle-1];
        //minX = experiment->; // TODO Mass range?
        //maxX = spectrum->MaximumXValue;

        if (experiment->getExperimentType() == Product)
        {
            experiment->getPrecursorInfo(cycle, selectedMz, charge);
            intensity = 0;
        }
    }
    CATCH_AND_FORWARD
}

int Spectrum2Impl::getMSLevel() const
{
    try { return experiment->getMsLevel(cycle) == 0 ? 1 : experiment->getMsLevel(cycle); } CATCH_AND_FORWARD
}

bool Spectrum2Impl::getHasIsolationInfo() const { return experiment->getHasIsolationInfo(); }

void Spectrum2Impl::getIsolationInfo(double& centerMz, double& lowerLimit, double& upperLimit) const
{
    if (!getHasIsolationInfo())
        return;

    try
    {
        experiment->getIsolationInfo(cycle, centerMz, lowerLimit, upperLimit);
    }
    CATCH_AND_FORWARD
}

bool Spectrum2Impl::getHasPrecursorInfo() const
{
    return selectedMz != 0;
}

void Spectrum2Impl::getPrecursorInfo(double& selectedMz, double& intensity, int& charge) const
{
    selectedMz = this->selectedMz;
    intensity = this->intensity;
    charge = this->charge;
}

double Spectrum2Impl::getStartTime() const
{
    return experiment->convertCycleToRetentionTime(cycle);
}

size_t Spectrum2Impl::getDataSize(bool doCentroid, bool ignoreZeroIntensityPoints) const
{
    try
    {
        if (static_cast<IXyData<double>^>(spectrumData) == nullptr)
        {
            if (doCentroid)
                spectrumData = experiment->msExperiment->GetSpectrumPeakList(cycle - 1);
            else
            {
                auto experimentType = experiment->getExperimentType();
                int paddZeros = ignoreZeroIntensityPoints || experimentType == MRM || experimentType == SIM ? 0 : 1;
                spectrumData = experiment->msExperiment->GetSpectrum(cycle - 1, paddZeros);
            }
        }

        return spectrumData->Count;
    }
    CATCH_AND_FORWARD
}

void Spectrum2Impl::getData(bool doCentroid, pwiz::util::BinaryData<double>& mz, pwiz::util::BinaryData<double>& intensities, bool ignoreZeroIntensityPoints) const
{
    try
    {
        if (static_cast<IXyData<double>^>(spectrumData) == nullptr)
        {
            if (doCentroid)
                spectrumData = experiment->msExperiment->GetSpectrumPeakList(cycle - 1);
            else
            {
                auto experimentType = experiment->getExperimentType();
                int paddZeros = ignoreZeroIntensityPoints || experimentType == MRM || experimentType == SIM ? 0 : 1;
                spectrumData = experiment->msExperiment->GetSpectrum(cycle - 1, paddZeros);
            }
        }

        ToStdVectorsFromXyData(spectrumData, mz, intensities, doCentroid ? PEAK_AREA_SCALE_FACTOR : 1);        
    }
    CATCH_AND_FORWARD
}


SpectrumPtr WiffFile2Impl::getSpectrum(int sample, int period, int experiment, int cycle) const
{
    try
    {
        ExperimentPtr msExperiment = getExperiment(sample, period, experiment);
        return getSpectrum(msExperiment, cycle);
    }
    CATCH_AND_FORWARD
}

SpectrumPtr WiffFile2Impl::getSpectrum(ExperimentPtr experiment, int cycle) const
{
    Spectrum2ImplPtr spectrum(new Spectrum2Impl(boost::static_pointer_cast<Experiment2Impl>(experiment), cycle));
    return spectrum;
}


void WiffFile2Impl::setSample(int sample) const
{
    try
    {
        if (sample != currentSampleIndex)
        {
            //this->msSample = static_cast<IList<ISampleInformation^>^>(allSamples)[0];
            IList<ISampleInformation^>^ unwrappedAllSamples = allSamples;
            this->msSample = (ISampleInformation^)unwrappedAllSamples[sample - 1];

            currentSampleIndex = sample;
            currentPeriod = currentExperiment = currentCycle = -1;
        }
    }
    CATCH_AND_FORWARD
}

void WiffFile2Impl::setPeriod(int sample, int period) const
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

void WiffFile2Impl::setExperiment(int sample, int period, int experiment) const
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

void WiffFile2Impl::setCycle(int sample, int period, int experiment, int cycle) const
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


} // ABI
} // vendor_api
} // pwiz

#endif
