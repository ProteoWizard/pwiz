//
// WiffFile.cpp
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
#include "pwiz/utility/misc/DateTime.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Container.hpp"

#pragma managed
#include <gcroot.h>

using System::String;
using System::Math;
using System::Console;
using namespace ABSciex::WiffFileDataReader;
using namespace ClearCore::MassSpectrometry;
using ClearCore::Science::RetentionTime;
using ClearCore::Science::PeakCollection;
using ClearCore::Science::Peak;
using ClearCore::Science::Ion;
using ClearCore::Obsolete::MSExperiment;
using ClearCore::Utility::ZeroBasedInt;
using ClearCore::Utility::OneBasedInt;
using namespace System;


// forwards managed exception to unmanaged code
#define CATCH_AND_FORWARD(x) \
    try \
    { x } \
    catch (System::ApplicationException^ e) \
    { throw std::runtime_error(ToStdString(e->Message)); }

#include <vcclr.h>
namespace {

std::string ToStdString(System::String^ source)
{
	int len = (( source->Length+1) * 2);
	char *ch = new char[ len ];
	bool result ;
	{
		pin_ptr<const wchar_t> wch = PtrToStringChars( source );
		result = wcstombs( ch, wch, len ) != -1;
	}
	std::string target = ch;
	delete ch;
	if(!result)
        throw gcnew System::Exception("error converting System::String to std::string");
	return target;
}

template<typename value_type>
std::vector<value_type> ToStdVector(cli::array<value_type>^ valueArray)
{
    pin_ptr<value_type> pin = &valueArray[0];
    value_type* begin = (value_type*) pin;
    return std::vector<value_type>(begin, begin + valueArray->Length);
}

}


namespace pwiz {
namespace vendor_api {
namespace ABI {


class WiffFileImpl : public WiffFile
{
    public:
    WiffFileImpl(const std::string& wiffpath);
    ~WiffFileImpl();

    gcroot<DataReader^> reader;

    virtual int getSampleCount() const;
    virtual int getPeriodCount(int sample) const;
    virtual int getExperimentCount(int sample, int period) const;
    virtual int getCycleCount(int sample, int period, int experiment) const;

    virtual const vector<string>& getSampleNames() const;

    virtual InstrumentModel getInstrumentModel() const;
    virtual InstrumentType getInstrumentType() const;
    virtual IonSourceType getIonSourceType() const;
    virtual blt::local_date_time getSampleAcquisitionTime() const;

    virtual ExperimentPtr getExperiment(int sample, int period, int experiment) const;
    virtual SpectrumPtr getSpectrum(int sample, int period, int experiment, int cycle) const;
    virtual SpectrumPtr getSpectrum(ExperimentPtr experiment, int cycle) const;

    void setSample(int sample) const;
    void setPeriod(int sample, int period) const;
    void setExperiment(int sample, int period, int experiment) const;
    void setCycle(int sample, int period, int experiment, int cycle) const;

    mutable int currentSample, currentPeriod, currentExperiment, currentCycle;

    private:
    // on first access, sample names are made unique (giving duplicates a count suffix) and cached
    mutable vector<string> sampleNames;
};

typedef boost::shared_ptr<WiffFileImpl> WiffFileImplPtr;


struct ExperimentImpl : public Experiment
{
    ExperimentImpl(const WiffFileImpl* wifffile, int sample, int period, int experiment);

    virtual int getSampleNumber() const {return sample;}
    virtual int getPeriodNumber() const {return period;}
    virtual int getExperimentNumber() const {return experiment;}

    virtual double getCycleStartTime(int cycle) const;

    virtual size_t getSRMSize() const;
    virtual void getSRM(size_t index, Target& target) const;
    virtual void getSIC(size_t index, std::vector<double>& times, std::vector<double>& intensities) const;

    virtual void getAcquisitionMassRange(double& startMz, double& stopMz) const;
    virtual ScanType getScanType() const;
    virtual Polarity getPolarity() const;

    virtual void getTIC(std::vector<double>& times, std::vector<double>& intensities) const;

    const WiffFileImpl* wifffile_;
    gcroot<MSExperiment^> msExperiment;
    int sample, period, experiment;
    vector<double> cycleStartTimes;
};

typedef boost::shared_ptr<ExperimentImpl> ExperimentImplPtr;


struct SpectrumImpl : public Spectrum
{
    SpectrumImpl(ExperimentImplPtr experiment, int cycle);

    virtual int getSampleNumber() const {return experiment->sample;}
    virtual int getPeriodNumber() const {return experiment->period;}
    virtual int getExperimentNumber() const {return experiment->experiment;}
    virtual int getCycleNumber() const {return cycle;}

    virtual bool getHasIsolationInfo() const;
    virtual void getIsolationInfo(double& centerMz, double& lowerLimit, double& upperLimit) const;

    virtual bool getHasPrecursorInfo() const;
    virtual void getPrecursorInfo(double& selectedMz, double& intensity, int& charge) const;

    virtual double getStartTime() const;

    virtual bool getDataIsContinuous() const;
    size_t getDataSize(bool doCentroid) const;
    virtual void getData(bool doCentroid, std::vector<double>& mz, std::vector<double>& intensities) const;

    virtual double getSumY() const {return sumY;}
    virtual double getBasePeakX() const {return bpX;}
    virtual double getBasePeakY() const {return bpY;}
    virtual double getMinX() const {return minX;}
    virtual double getMaxX() const {return maxX;}

    ExperimentImplPtr experiment;
    gcroot<MassSpectrum^> spectrum;
    int cycle;

    // data points
    double sumY, bpX, bpY, minX, maxX;
    vector<double> x, y;
    bool pointsAreContinuous;

    // precursor info
    double selectedMz, intensity;
    int charge;
};

typedef boost::shared_ptr<SpectrumImpl> SpectrumImplPtr;


WiffFileImpl::WiffFileImpl(const string& wiffpath)
: currentSample(-1), currentPeriod(-1), currentExperiment(-1), currentCycle(-1)
{
    CATCH_AND_FORWARD
    (
        reader = gcnew DataReader();
        reader->FilePath = gcnew String(wiffpath.c_str());
    )
}

WiffFileImpl::~WiffFileImpl()
{
}


int WiffFileImpl::getSampleCount() const
{
    CATCH_AND_FORWARD(return (sampleNames.size() == 0 ? reader->SampleCount : sampleNames.size());)
}

int WiffFileImpl::getPeriodCount(int sample) const
{
    CATCH_AND_FORWARD
    (
        setSample(sample);
        return reader->Provider->PeriodCount;
    )
}

int WiffFileImpl::getExperimentCount(int sample, int period) const
{
    CATCH_AND_FORWARD
    (
        setPeriod(sample, period);
        return reader->ExperimentCount;
    )
}

int WiffFileImpl::getCycleCount(int sample, int period, int experiment) const
{
    CATCH_AND_FORWARD
    (
        setExperiment(sample, period, experiment);
        return reader->MSExperiment->GetRetentionTimes(0, Double::MaxValue)->Length;
    )
}

const vector<string>& WiffFileImpl::getSampleNames() const
{
    //CATCH_AND_FORWARD
    //(
        if (sampleNames.size() == 0)
        {
            // make duplicate sample names unique by appending the duplicate count
            // e.g. foo, bar, foo (2), foobar, bar (2), foo (3)
            map<string, int> duplicateCountMap;
            array<System::String^>^ sampleNamesManaged = reader->SampleNames;
            sampleNames.resize(sampleNamesManaged->Length);
            for (int i=0; i < sampleNamesManaged->Length; ++i)
            {
                sampleNames[i] = ToStdString(sampleNamesManaged[i]);
                int duplicateCount = duplicateCountMap[sampleNames[i]]++; // increment after getting current count
                if (duplicateCount)
                    sampleNames[i] += " (" + lexical_cast<string>(duplicateCount+1) + ")";
            }
        }
        return sampleNames;
    //)
}

InstrumentModel WiffFileImpl::getInstrumentModel() const
{
    CATCH_AND_FORWARD(return (InstrumentModel) reader->Provider->InstrumentModel;)
}

InstrumentType WiffFileImpl::getInstrumentType() const
{
    CATCH_AND_FORWARD(return (InstrumentType) reader->Provider->InstrumentType;)
}

IonSourceType WiffFileImpl::getIonSourceType() const
{
    CATCH_AND_FORWARD(return (IonSourceType) reader->Provider->IonSource;)
}

blt::local_date_time WiffFileImpl::getSampleAcquisitionTime() const
{
    CATCH_AND_FORWARD
    (
        bpt::ptime pt(bdt::time_from_OADATE<bpt::ptime>(reader->Provider->SampleAcquisitionDateTime.ToOADate()));
        return blt::local_date_time(pt, blt::time_zone_ptr()); // keep time as UTC
    )
}


ExperimentPtr WiffFileImpl::getExperiment(int sample, int period, int experiment) const
{
    CATCH_AND_FORWARD
    (
        setExperiment(sample, period, experiment);
        ExperimentImplPtr msExperiment(new ExperimentImpl(this, sample, period, experiment));
        return msExperiment;
    )
}


ExperimentImpl::ExperimentImpl(const WiffFileImpl* wifffile, int sample, int period, int experiment)
: wifffile_(wifffile), sample(sample), period(period), experiment(experiment)
{
    CATCH_AND_FORWARD
    (
        wifffile_->setExperiment(sample, period, experiment);
        msExperiment = wifffile_->reader->MSExperiment;

        array<RetentionTime>^ retentionTimes = msExperiment->GetRetentionTimes(0, Double::MaxValue);
        cycleStartTimes.resize(retentionTimes->Length);
        for (int i=0; i < retentionTimes->Length; ++i)
            cycleStartTimes[i] = retentionTimes[i];
    )
}

double ExperimentImpl::getCycleStartTime(int cycle) const
{
    return cycleStartTimes[cycle-1]; // cycle is 1-based
}

size_t ExperimentImpl::getSRMSize() const
{
    CATCH_AND_FORWARD(return msExperiment->MRMTransitions->Count;)
}

void ExperimentImpl::getSRM(size_t index, Target& target) const
{
    CATCH_AND_FORWARD
    (
        if (index > (size_t) msExperiment->MRMTransitions->Count)
            throw std::out_of_range("[Experiment::getSRM()] index out of range");

        MRMTransition^ transition = msExperiment->MRMTransitions[index];
        CompoundDependentParametersDictionary^ parameters = wifffile_->reader->Provider->GetMRMTransitionsForAcquisition()[index]->Parameters;
        target.type = TargetType_SRM;
        target.Q1 = transition->Q1Mass;
        target.Q3 = transition->Q3Mass;
        target.dwellTime = transition->DwellTime;
        target.collisionEnergy = (double) parameters["CE"];
        target.declusteringPotential = (double) parameters["DP"];
        if (transition->CompoundID != nullptr)
            target.compoundID = ToStdString(transition->CompoundID);
    )
}

void ExperimentImpl::getSIC(size_t index, std::vector<double>& times, std::vector<double>& intensities) const
{
    CATCH_AND_FORWARD
    (
        if (index > (size_t) msExperiment->MRMTransitions->Count)
            throw std::out_of_range("[Experiment::getSIC()] index out of range");

        ChromatogramDetails::ChromatogramPoints^ cp =
            wifffile_->reader->Provider->GetXICPoints(index+1); // index is 1-based

        times.resize(cp->Count);
        intensities.resize(cp->Count);
        for (int i=0; i < cp->Count; ++i)
        {
            times[i] = cp[i]->X;
            intensities[i] = cp[i]->Y;
        }
    )
}

void ExperimentImpl::getAcquisitionMassRange(double& startMz, double& stopMz) const
{
    CATCH_AND_FORWARD
    (
        startMz = msExperiment->AcquisitionMassRange->StartMZ;
        stopMz = msExperiment->AcquisitionMassRange->StopMZ;
    )
}

ScanType ExperimentImpl::getScanType() const
{
    CATCH_AND_FORWARD(return (ScanType) msExperiment->ScanType->ScanTypeEnumeration;)
}

Polarity ExperimentImpl::getPolarity() const
{
    CATCH_AND_FORWARD(return (Polarity) msExperiment->Polarity->PolarityEnumeration;)
}

void ExperimentImpl::getTIC(std::vector<double>& times, std::vector<double>& intensities) const
{
    CATCH_AND_FORWARD
    (
        Chromatogram^ tic = msExperiment->Tic;
        times.resize(tic->Points->Count);
        intensities.resize(tic->Points->Count);
        for (int i=0; i < tic->Points->Count; ++i)
        {
            times[i] = tic->Points[i]->X;
            intensities[i] = tic->Points[i]->Y;
        }
    )
}


SpectrumImpl::SpectrumImpl(ExperimentImplPtr experiment, int cycle)
: experiment(experiment), cycle(cycle),
  sumY(0), bpX(0), bpY(0), minX(0), maxX(0),
  selectedMz(0)
{
    CATCH_AND_FORWARD
    (
        experiment->wifffile_->reader->SetCycles((OneBasedInt^)cycle);
        spectrum = experiment->wifffile_->reader->MassSpectrum;
        //spectrum = experiment->msExperiment->SpectrumAt((OneBasedInt^)cycle);
        SpectrumDetails::XYSpectrumPoints^ points = spectrum->Points;

        pointsAreContinuous = points->ContinuumData;

        x.resize(points->Count);
        y.resize(x.size());

        if (!x.empty())
        {
            minX = points->MinX;
            maxX = points->MaxX;

            for (int i=0, end = x.size(); i < end; ++i)
            {
                SpectrumDetails::XYPoint^ point = points[i];
                double& x = this->x[i] = point->X;
                double& y = this->y[i] = point->Y;

                if (bpY < y)
                {
                    bpY = y;
                    bpX = x;
                }
                sumY += y;
            }
        }

        if (experiment->wifffile_->reader->Precursor != nullptr)
        {
            Ion^ precursor = experiment->wifffile_->reader->Precursor;
            selectedMz = precursor->MassToChargeRatio;
            intensity = 0;
            charge = precursor->Charge;
        }
    )
}

bool SpectrumImpl::getHasIsolationInfo() const
{
    return false;
}

void SpectrumImpl::getIsolationInfo(double& centerMz, double& lowerLimit, double& upperLimit) const
{
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
    return experiment->getCycleStartTime(cycle);
}

bool SpectrumImpl::getDataIsContinuous() const
{
    CATCH_AND_FORWARD(return pointsAreContinuous;)
}

size_t SpectrumImpl::getDataSize(bool doCentroid) const
{
    CATCH_AND_FORWARD
    (
        if (doCentroid && pointsAreContinuous && spectrum->AllPeakCount > 0)
            return (size_t) spectrum->AllPeakCount;
        else
            return x.size();
    )
}

void SpectrumImpl::getData(bool doCentroid, std::vector<double>& mz, std::vector<double>& intensities) const
{
    CATCH_AND_FORWARD
    (
        if (doCentroid)
            doCentroid = pointsAreContinuous && spectrum->AllPeakCount > 0;

        if (doCentroid)
        {
            size_t numPoints = spectrum->AllPeakCount;
            mz.resize(numPoints);
            intensities.resize(numPoints);
            PeakCollection^ peaks = spectrum->AllPeaks;
            for (size_t i=0; i < numPoints; ++i)
            {
                Peak^ peak = peaks[(int)i];
                mz[i] = peak->ApexX;
                intensities[i] = peak->ApexY;
            }
        }
        else
        {
            mz.assign(x.begin(), x.end());
            intensities.assign(y.begin(), y.end());
        }
    )
}


SpectrumPtr WiffFileImpl::getSpectrum(int sample, int period, int experiment, int cycle) const
{
    CATCH_AND_FORWARD
    (
        ExperimentPtr msExperiment = getExperiment(sample, period, experiment);
        return getSpectrum(msExperiment, cycle);
    )
}

SpectrumPtr WiffFileImpl::getSpectrum(ExperimentPtr experiment, int cycle) const
{
    CATCH_AND_FORWARD
    (
        SpectrumImplPtr spectrum(new SpectrumImpl(boost::static_pointer_cast<ExperimentImpl>(experiment), cycle));
        return spectrum;
    )
}


void WiffFileImpl::setSample(int sample) const
{
    CATCH_AND_FORWARD
    (
        if (sample != currentSample)
        {
            reader->SampleNum = sample;
            currentSample = sample;
        }
        // TODO: refresh other experiments?
    )
}

void WiffFileImpl::setPeriod(int sample, int period) const
{
    CATCH_AND_FORWARD
    (
        setSample(sample);

        if (period != currentPeriod)
        {
            reader->PeriodNum = period;
            currentPeriod = period;
        }
    )
}

void WiffFileImpl::setExperiment(int sample, int period, int experiment) const
{
    CATCH_AND_FORWARD
    (
        setPeriod(sample, period);

        if (experiment != currentExperiment)
        {
            reader->ExperimentNum = experiment;
            currentExperiment = experiment;
        }
    )
}

void WiffFileImpl::setCycle(int sample, int period, int experiment, int cycle) const
{
    CATCH_AND_FORWARD
    (
        setExperiment(sample, period, experiment);

        if (cycle != currentCycle)
        {
            reader->SetCycles(cycle);
            currentCycle = cycle;
        }
    )
}


PWIZ_API_DECL
WiffFilePtr WiffFile::create(const string& wiffpath)
{
    WiffFileImplPtr wifffile(new WiffFileImpl(wiffpath));
    return boost::static_pointer_cast<WiffFile>(wifffile);
}


} // ABI
} // vendor_api
} // pwiz

#endif