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


#ifndef _WIFFFILE_HPP_
#define _WIFFFILE_HPP_


#ifndef BOOST_DATE_TIME_NO_LIB
#define BOOST_DATE_TIME_NO_LIB // prevent MSVC auto-link
#endif


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/utility/misc/BinaryData.hpp"
#include <string>
#include <vector>
#include <boost/shared_ptr.hpp>
#include <boost/date_time.hpp>


namespace pwiz {
namespace vendor_api {
namespace ABI {


PWIZ_API_DECL enum ScanType
{
    ScanType_Unknown = 0,
    MRMScan,
    SIMScan,
    FullScan
};

PWIZ_API_DECL enum InstrumentModel
{
    InstrumentModel_Unknown,
    API100,
    API100LC,
    API150MCA,
    API150EX,
    API165,
    API300,
    API350,
    API365,
    API2000,
    API2000QTrap,
    API2500QTrap,
    API3000,
    API3200,
    API3200QTrap,
    API3500QTrap,
    API4000,
    API4000QTrap,
    API4500,
    API4500QTrap,
    API5000,
    API5500,
    API5500QTrap,
    API6500,
    API6500QTrap,
    NlxTof,
    QStar,
    QStarPulsarI,
    QStarXL,
    QStarElite,
    API4600TripleTOF,
    API5600TripleTOF,
    API6600TripleTOF,
    TripleQuad7500,
    X500QTOF,
    ZenoTOF7600,
    GenericQTrap,
    InstrumentModel_Count
};

PWIZ_API_DECL enum IonSourceType
{
    IonSourceType_Unknown = 0,
    Medusa = 1,
    Duo = 2,
    FlowNanoSpray = 3,
    TurboSpray = 4,
    HeatedNebulizer = 5,
    IonSpray = 6,
    None = 7,
    Maldi = 8,
    PhotoSpray = 9,
    IonSourceType_Count
};

PWIZ_API_DECL enum ExperimentType
{
    MS = 0,
    Product,
    Precursor,
    NeutralGainOrLoss,
    SIM,
    MRM
};

PWIZ_API_DECL enum Polarity
{
    Positive = 0,
    Negative = 1,
    Undefined = 2
};

enum PWIZ_API_DECL FragmentationMode 
{
    FragmentationMode_CID,
    FragmentationMode_EAD
};


struct PWIZ_API_DECL Spectrum
{
    virtual int getSampleNumber() const = 0;
    virtual int getPeriodNumber() const = 0;
    virtual int getExperimentNumber() const = 0;
    virtual int getCycleNumber() const = 0;

    virtual int getMSLevel() const = 0;

    virtual bool getHasIsolationInfo() const = 0;
    virtual void getIsolationInfo(double& centerMz, double& lowerLimit, double& upperLimit, double& collisionEnergy, double& electronKineticEnergy, FragmentationMode& fragmentationMode) const = 0;

    virtual bool getHasPrecursorInfo() const = 0;
    virtual void getPrecursorInfo(double& selectedMz, double& intensity, int& charge) const = 0;

    virtual double getStartTime() const = 0;

    virtual bool getDataIsContinuous() const = 0;
    virtual size_t getDataSize(bool doCentroid, bool ignoreZeroIntensityPoints = false) const = 0;
    virtual void getData(bool doCentroid, pwiz::util::BinaryData<double>& mz, pwiz::util::BinaryData<double>& intensities, bool ignoreZeroIntensityPoints = false) const = 0;

    virtual double getSumY() const = 0;
    virtual double getBasePeakX() const = 0;
    virtual double getBasePeakY() const = 0;
    virtual double getMinX() const = 0;
    virtual double getMaxX() const = 0;

    virtual ~Spectrum() {}
};

typedef boost::shared_ptr<Spectrum> SpectrumPtr;


enum PWIZ_API_DECL TargetType
{
    TargetType_SRM,    // selected reaction monitoring (q1 and q3)
    TargetType_SIM,    // selected ion monitoring (q1 but no q3)
    TargetType_CNL     // constant neutral loss (or gain)
};


struct PWIZ_API_DECL Target
{
    TargetType type;
    double Q1, Q3;
    double dwellTime;
    double collisionEnergy;
    double declusteringPotential;
    double startTime, endTime;
    std::string compoundID;
};


struct PWIZ_API_DECL Experiment
{
    virtual int getSampleNumber() const = 0;
    virtual int getPeriodNumber() const = 0;
    virtual int getExperimentNumber() const = 0;

    virtual size_t getSIMSize() const = 0;
    virtual void getSIM(size_t index, Target& target) const = 0;

    virtual size_t getSRMSize() const = 0;
    virtual void getSRM(size_t index, Target& target) const = 0;
    virtual double getSIC(size_t index, pwiz::util::BinaryData<double>& times, pwiz::util::BinaryData<double>& intensities, bool ignoreScheduledLimits) const = 0;
    virtual void getSIC(size_t index, pwiz::util::BinaryData<double>& times, pwiz::util::BinaryData<double>& intensities,
                        double& basePeakX, double& basePeakY, bool ignoreScheduledLimits) const = 0;

    virtual void getAcquisitionMassRange(double& startMz, double& stopMz) const = 0;
    virtual ScanType getScanType() const = 0;
    virtual ExperimentType getExperimentType() const = 0;
    virtual Polarity getPolarity() const = 0;
    virtual int getMsLevel(int cycle) const = 0;

    virtual double convertCycleToRetentionTime(int cycle) const = 0;
    virtual double convertRetentionTimeToCycle(double rt) const = 0;

    virtual void getTIC(std::vector<double>& times, std::vector<double>& intensities) const = 0;
    virtual void getBPC(std::vector<double>& times, std::vector<double>& intensities) const = 0;

    virtual ~Experiment() {}
};

typedef boost::shared_ptr<Experiment> ExperimentPtr;
typedef std::map<std::pair<int, int>, ExperimentPtr> ExperimentsMap;


class PWIZ_API_DECL WiffFile
{
    public:
    typedef boost::shared_ptr<WiffFile> Ptr;
    static Ptr create(const std::string& wiffpath);

    virtual std::string getWiffPath() const = 0;

    virtual int getSampleCount() const = 0;
    virtual int getPeriodCount(int sample) const = 0;
    virtual int getExperimentCount(int sample, int period) const = 0;
    virtual int getCycleCount(int sample, int period, int experiment) const = 0;

    virtual const std::vector<std::string>& getSampleNames() const = 0;

    virtual InstrumentModel getInstrumentModel() const = 0;
    virtual std::string getInstrumentSerialNumber() const = 0;
    virtual IonSourceType getIonSourceType() const = 0;
    virtual boost::local_time::local_date_time getSampleAcquisitionTime(int sample, bool adjustToHostTime) const = 0;

    struct ADCTrace
    {
        pwiz::util::BinaryData<double> x, y;
        std::string xUnits, yUnits;
    };

    virtual int getADCTraceCount(int sample) const = 0;
    virtual std::string getADCTraceName(int sample, int traceIndex) const = 0;
    virtual void getADCTrace(int sample, int traceIndex, ADCTrace& trace) const = 0;

    /// get total wavelength chromatogram; returned ADCTrace is empty if there is no UV data in the file
    virtual void getTWC(int sample, ADCTrace& totalWavelengthChromatogram) const = 0;

    virtual ExperimentPtr getExperiment(int sample, int period, int experiment) const = 0;
    virtual SpectrumPtr getSpectrum(int sample, int period, int experiment, int cycle) const = 0;
    virtual SpectrumPtr getSpectrum(ExperimentPtr experiment, int cycle) const = 0;

    virtual ~WiffFile() {}
};

typedef WiffFile::Ptr WiffFilePtr;


} // ABI
} // vendor_api
} // pwiz


#endif // _WIFFFILE_HPP_
