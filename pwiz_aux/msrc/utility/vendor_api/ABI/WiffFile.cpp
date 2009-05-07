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


#define WIFFFILE_SOURCE

#pragma unmanaged
#include "WiffFile.hpp"
#include <iostream>
using namespace std;

//#ifdef _MANAGED
#pragma managed
#include <gcroot.h>
#define GCHANDLE(T) gcroot<T>
#using "Interop.ParameterSvrLib.dll"
#using "Interop.MSMethodSvrLib.dll"
#using "Interop.AAOBatchLib.dll"
#using "Interop.AAOLib.dll"
#using "Interop.SampleStatusServerLib.dll"
#using "Clearcore.Storage.dll"
#using "Interop.AcqMethodSvrLib.dll"
#using "Interop.IDAMethodSvr.dll"
#using "Interop.AnalystBridge.dll"
#using "nunit.framework.dll"
#using "Clearcore.dll"
#using "ABSciex.DataAccess.WiffFileDataReader.dll"
#using "System.Xml.dll"

using System::String;
using System::Math;
using System::Console;
//using ABSciex::WiffFileDataReader::DataReader;
using namespace ABSciex::WiffFileDataReader;
using namespace ClearCore::MassSpectrometry;
using ClearCore::Science::RetentionTime;
using ClearCore::Science::PeakCollection;
using ClearCore::Science::Ion;
using ClearCore::Obsolete::MSExperiment;
using ClearCore::Utility::ZeroBasedInt;
using ClearCore::Utility::OneBasedInt;
using namespace System;
using namespace System::Xml;
//#else
//#define GCHANDLE(T) intptr_t
//#endif


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

}


namespace pwiz {
namespace wiff {


class WiffFileImpl : public WiffFile
{
    public:
    WiffFileImpl(const std::string& wiffpath);
    ~WiffFileImpl();

    GCHANDLE(DataReader^) reader;

    virtual int getSampleCount() const;
    virtual int getPeriodCount(int sample) const;
    virtual int getExperimentCount(int sample, int period) const;
    virtual int getCycleCount(int sample, int period, int experiment) const;

    virtual double getCycleStartTime(int sample, int period, int experiment, int cycle) const;

    virtual std::vector<std::string> getSampleNames() const;

    virtual InstrumentModel getInstrumentModel() const;
    virtual InstrumentType getInstrumentType() const;
    virtual IonSourceType getIonSourceType() const;
    virtual std::string getSampleAcquisitionTime() const;

    virtual ExperimentPtr getExperiment(int sample, int period, int experiment) const;
    virtual SpectrumPtr getSpectrum(int sample, int period, int experiment, int cycle) const;
    virtual SpectrumPtr getSpectrum(ExperimentPtr experiment, int cycle) const;

    void setSample(int sample) const;
    void setPeriod(int sample, int period) const;
    void setExperiment(int sample, int period, int experiment) const;
    void setCycle(int sample, int period, int experiment, int cycle) const;

    mutable int currentSample, currentPeriod, currentExperiment, currentCycle;
};

typedef boost::shared_ptr<WiffFileImpl> WiffFileImplPtr;


struct ExperimentImpl : public Experiment
{
    ExperimentImpl(const WiffFileImpl* wifffile, int sample, int period, int experiment)
        : wifffile_(wifffile), sample(sample), period(period), experiment(experiment)
    {
        wifffile_->setExperiment(sample, period, experiment);
        msExperiment = wifffile_->reader->MSExperiment;
    }

    virtual int getSampleNumber() const {return sample;}
    virtual int getPeriodNumber() const {return period;}
    virtual int getExperimentNumber() const {return experiment;}

    virtual size_t getSRMSize() const;
    virtual void getSRM(size_t index, double& Q1, double& Q3, double& dwellTime, std::string* compoundID) const;
    virtual void getSIC(size_t index, std::vector<double>& times, std::vector<double>& intensities) const;

    virtual void getAcquisitionMassRange(double& startMz, double& stopMz) const;
    virtual ScanType getScanType() const;
    virtual Polarity getPolarity() const;

    virtual void getTIC(std::vector<double>& times, std::vector<double>& intensities) const;

    const WiffFileImpl* wifffile_;
    GCHANDLE(MSExperiment^) msExperiment;
    int sample, period, experiment;
};

typedef boost::shared_ptr<ExperimentImpl> ExperimentImplPtr;


struct SpectrumImpl : public Spectrum
{
    SpectrumImpl(ExperimentImplPtr experiment, int cycle)
        : experiment(experiment), cycle(cycle),
          sumY(0), bpX(0), bpY(0), minX(0), maxX(0)
    {
        spectrum = experiment->msExperiment->SpectrumAt((OneBasedInt^)cycle);
        SpectrumDetails::XYSpectrumPoints^ points = spectrum->Points;

        if (points->Count > 0)
        {
            minX = points[0]->X;
            maxX = points[points->Count-1]->X;

            for (int i=0, end = points->Count; i < end; ++i)
            {
                double x = points[i]->X;
                double y = points[i]->Y;

                if (bpY < y)
                {
                    bpY = y;
                    bpX = x;
                }
                sumY += y;
            }
        }
    }

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
    GCHANDLE(MassSpectrum^) spectrum;
    int cycle;

    double sumY, bpX, bpY, minX, maxX;
};

typedef boost::shared_ptr<SpectrumImpl> SpectrumImplPtr;


WiffFileImpl::WiffFileImpl(const string& wiffpath)
: currentSample(-1), currentPeriod(-1), currentExperiment(-1), currentCycle(-1)
{
    reader = gcnew DataReader();
    reader->FilePath = gcnew String(wiffpath.c_str());
}

WiffFileImpl::~WiffFileImpl()
{
}


int WiffFileImpl::getSampleCount() const
{
    return reader->SampleCount;
}

int WiffFileImpl::getPeriodCount(int sample) const
{
    setSample(sample);
    return reader->Provider->PeriodCount;
}

int WiffFileImpl::getExperimentCount(int sample, int period) const
{
    setPeriod(sample, period);
    return reader->ExperimentCount;
}

int WiffFileImpl::getCycleCount(int sample, int period, int experiment) const
{
    setExperiment(sample, period, experiment);
    return reader->MSExperiment->GetRetentionTimes(0, Double::MaxValue)->Length;
}

vector<string> WiffFileImpl::getSampleNames() const
{
    vector<string> sampleNames(reader->SampleNames->Length);
    for (int i=0; i < reader->SampleNames->Length; ++i)
        sampleNames[i] = ToStdString(reader->SampleNames[i]);
    return sampleNames;
}

double WiffFileImpl::getCycleStartTime(int sample, int period, int experiment, int cycle) const
{
    setCycle(sample, period, experiment, cycle);
    return reader->TimeInMinsStart;
}

InstrumentModel WiffFileImpl::getInstrumentModel() const
{
    return (InstrumentModel) reader->Provider->InstrumentModel;
}

InstrumentType WiffFileImpl::getInstrumentType() const
{
    return (InstrumentType) reader->Provider->InstrumentType;
}

IonSourceType WiffFileImpl::getIonSourceType() const
{
    return (IonSourceType) reader->Provider->IonSource;
}

std::string WiffFileImpl::getSampleAcquisitionTime() const
{
    return ToStdString(XmlConvert::ToString(reader->Provider->SampleAcquisitionDateTime,
                                            XmlDateTimeSerializationMode::RoundtripKind));
}


ExperimentPtr WiffFileImpl::getExperiment(int sample, int period, int experiment) const
{
    setExperiment(sample, period, experiment);
    ExperimentImplPtr msExperiment(new ExperimentImpl(this, sample, period, experiment));
    return msExperiment;
}


size_t ExperimentImpl::getSRMSize() const
{
    return msExperiment->MRMTransitions->Count;
}

void ExperimentImpl::getSRM(size_t index, double& Q1, double& Q3, double& dwellTime, string* compoundID) const
{
    if (index > (size_t) msExperiment->MRMTransitions->Count)
        throw std::out_of_range("[Experiment::getSRM()] index out of range");

    MRMTransition^ transition = msExperiment->MRMTransitions[index];
    Q1 = transition->Q1Mass;
    Q3 = transition->Q3Mass;
    dwellTime = transition->DwellTime;
    if (compoundID != NULL)
        *compoundID = ToStdString(transition->CompoundID);
}

void ExperimentImpl::getSIC(size_t index, std::vector<double>& times, std::vector<double>& intensities) const
{
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
}

void ExperimentImpl::getAcquisitionMassRange(double& startMz, double& stopMz) const
{
    startMz = msExperiment->AcquisitionMassRange->StartMZ;
    stopMz = msExperiment->AcquisitionMassRange->StopMZ;
}

ScanType ExperimentImpl::getScanType() const
{
    return (ScanType) msExperiment->ScanType->ScanTypeEnumeration;
}

Polarity ExperimentImpl::getPolarity() const
{
    return (Polarity) msExperiment->Polarity->PolarityEnumeration;
}

void ExperimentImpl::getTIC(std::vector<double>& times, std::vector<double>& intensities) const
{
    Chromatogram^ tic = msExperiment->Tic;
    times.resize(tic->Points->Count);
    intensities.resize(tic->Points->Count);
    for (int i=0; i < tic->Points->Count; ++i)
    {
        times[i] = tic->Points[i]->X;
        intensities[i] = tic->Points[i]->Y;
    }
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
    experiment->wifffile_->setCycle(getSampleNumber(), getPeriodNumber(), getExperimentNumber(), cycle);
    return experiment->wifffile_->reader->Precursor != nullptr;
}

void SpectrumImpl::getPrecursorInfo(double& selectedMz, double& intensity, int& charge) const
{
    experiment->wifffile_->setCycle(getSampleNumber(), getPeriodNumber(), getExperimentNumber(), cycle);
    Ion^ precursor = experiment->wifffile_->reader->Precursor;
    selectedMz = precursor->MassToChargeRatio;
    intensity = 0;
    charge = precursor->Charge;
}

double SpectrumImpl::getStartTime() const
{
    experiment->wifffile_->setCycle(getSampleNumber(), getPeriodNumber(), getExperimentNumber(), cycle);
    return experiment->wifffile_->reader->TimeInMinsStart;
}

bool SpectrumImpl::getDataIsContinuous() const
{
    return spectrum->Points->ContinuumData;
}

size_t SpectrumImpl::getDataSize(bool doCentroid) const
{
    SpectrumDetails::XYSpectrumPoints^ points = spectrum->Points;
    if (doCentroid && points->ContinuumData && spectrum->AllPeakCount > 0)
        return (size_t) spectrum->AllPeakCount;
    else
        return (size_t) points->Count;
}

void SpectrumImpl::getData(bool doCentroid, std::vector<double>& mz, std::vector<double>& intensities) const
{
    SpectrumDetails::XYSpectrumPoints^ points = spectrum->Points;
    PeakCollection^ peaks = spectrum->AllPeaks;

    if (doCentroid)
        doCentroid = points->ContinuumData && spectrum->AllPeakCount > 0;

    size_t numPoints = doCentroid ? peaks->Count : points->Count;

    mz.resize(numPoints);
    intensities.resize(numPoints);
    if (doCentroid)
    {
        for (size_t i=0; i < numPoints; ++i)
        {
            mz[i] = peaks[(int)i]->ApexX;
            intensities[i] = peaks[(int)i]->ApexY;
        }
    }
    else
    {
        for (size_t i=0; i < numPoints; ++i)
        {
            mz[i] = points[i]->X;
            intensities[i] = points[i]->Y;
        }
    }
}


SpectrumPtr WiffFileImpl::getSpectrum(int sample, int period, int experiment, int cycle) const
{
    ExperimentPtr msExperiment = getExperiment(sample, period, experiment);
    return getSpectrum(msExperiment, cycle);
}

SpectrumPtr WiffFileImpl::getSpectrum(ExperimentPtr experiment, int cycle) const
{
    SpectrumImplPtr spectrum(new SpectrumImpl(boost::static_pointer_cast<ExperimentImpl>(experiment), cycle));
    return spectrum;
}


void WiffFileImpl::setSample(int sample) const
{
    if (sample != currentSample)
    {
        reader->SampleNum = sample;
        currentSample = sample;
    }
    // TODO: refresh other experiments?
}

void WiffFileImpl::setPeriod(int sample, int period) const
{
    setSample(sample);

    if (period != currentPeriod)
    {
        reader->PeriodNum = period;
        currentPeriod = period;
    }
}

void WiffFileImpl::setExperiment(int sample, int period, int experiment) const
{
    setPeriod(sample, period);

    if (experiment != currentExperiment)
    {
        reader->ExperimentNum = experiment;
        currentExperiment = experiment;
    }
}

void WiffFileImpl::setCycle(int sample, int period, int experiment, int cycle) const
{
    setExperiment(sample, period, experiment);

    if (cycle != currentCycle)
    {
        reader->SetCycles(cycle);
        currentCycle = cycle;
    }
}


WIFFFILE_API
WiffFilePtr WiffFile::create(const string& wiffpath)
{
    WiffFileImplPtr wifffile(new WiffFileImpl(wiffpath));
    return boost::static_pointer_cast<WiffFile>(wifffile);
}


} // wiff
} // pwiz
