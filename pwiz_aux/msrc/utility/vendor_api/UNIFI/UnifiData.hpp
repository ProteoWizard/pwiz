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


#ifndef _UNIFIDATA_HPP_
#define _UNIFIDATA_HPP_


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
namespace UNIFI {

using std::size_t;

/*
struct PWIZ_API_DECL Spectrum
{
    virtual int getSampleNumber() const = 0;
    virtual int getPeriodNumber() const = 0;
    virtual int getExperimentNumber() const = 0;
    virtual int getCycleNumber() const = 0;

    virtual int getMSLevel() const = 0;

    virtual bool getHasIsolationInfo() const = 0;
    virtual void getIsolationInfo(double& centerMz, double& lowerLimit, double& upperLimit) const = 0;

    virtual bool getHasPrecursorInfo() const = 0;
    virtual void getPrecursorInfo(double& selectedMz, double& intensity, int& charge) const = 0;

    virtual double getStartTime() const = 0;

    virtual bool getDataIsContinuous() const = 0;
    virtual size_t getDataSize(bool doCentroid, bool ignoreZeroIntensityPoints = false) const = 0;
    virtual void getData(bool doCentroid, std::vector<double>& mz, std::vector<double>& intensities, bool ignoreZeroIntensityPoints = false) const = 0;

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
    std::string compoundID;
};


struct PWIZ_API_DECL Experiment
{
    virtual int getSampleNumber() const = 0;
    virtual int getPeriodNumber() const = 0;
    virtual int getExperimentNumber() const = 0;

    virtual size_t getSRMSize() const = 0;
    virtual void getSRM(size_t index, Target& target) const = 0;
    virtual void getSIC(size_t index, std::vector<double>& times, std::vector<double>& intensities) const = 0;
    virtual void getSIC(size_t index, std::vector<double>& times, std::vector<double>& intensities,
                        double& basePeakX, double& basePeakY) const = 0;

    virtual bool getHasIsolationInfo() const = 0;
    virtual void getIsolationInfo(double& centerMz, double& lowerLimit, double& upperLimit) const = 0;

    virtual void getAcquisitionMassRange(double& startMz, double& stopMz) const = 0;
    virtual ScanType getScanType() const = 0;
    virtual ExperimentType getExperimentType() const = 0;
    virtual Polarity getPolarity() const = 0;

    virtual double convertCycleToRetentionTime(int cycle) const = 0;
    virtual double convertRetentionTimeToCycle(double rt) const = 0;

    virtual void getTIC(std::vector<double>& times, std::vector<double>& intensities) const = 0;
    virtual void getBPC(std::vector<double>& times, std::vector<double>& intensities) const = 0;

    virtual ~Experiment() {}
};

typedef boost::shared_ptr<Experiment> ExperimentPtr;
typedef std::map<std::pair<int, int>, ExperimentPtr> ExperimentsMap;*/


enum class PWIZ_API_DECL Polarity
{
    Unknown = 0,
    Negative = 1,
    Positive = 2
};

enum class PWIZ_API_DECL EnergyLevel
{
    Unknown = 0,
    Low = 1,
    High = 2
};

struct PWIZ_API_DECL UnifiSpectrum
{
    double retentionTime;
    Polarity scanPolarity;
    EnergyLevel energyLevel;
    double driftTime;
    std::pair<double, double> scanRange;

    size_t arrayLength;
    pwiz::util::BinaryData<double> mzArray;
    pwiz::util::BinaryData<double> intensityArray;
};

class PWIZ_API_DECL UnifiData
{
    public:
    UnifiData(const std::string& sampleResultUrl, bool combineIonMobilitySpectra);
    ~UnifiData();

    size_t numberOfSpectra() const;
    void getSpectrum(size_t index, UnifiSpectrum& spectrum, bool getBinaryData) const;
    
    //InstrumentModel getInstrumentModel() const;
    //IonSourceType getIonSourceType() const;
    //MassAnalyzerType getMassAnalyzerType() const;

    const boost::local_time::local_date_time& getAcquisitionStartTime() const;
    const std::string& getSampleName() const;
    const std::string& getSampleDescription() const;
    int getReplicateNumber() const;
    const std::string& getWellPosition() const;

    bool hasIonMobilityData() const;

    bool canConvertDriftTimeAndCCS() const;
    double driftTimeToCCS(double driftTimeInMilliseconds, double mz, int charge) const;
    double ccsToDriftTime(double ccs, double mz, int charge) const;

    private:
    class Impl;
    std::unique_ptr<Impl> _impl;
};

typedef boost::shared_ptr<UnifiData> UnifiDataPtr;


} // UNIFI
} // vendor_api
} // pwiz


#endif // _UNIFIDATA_HPP_
