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
#include <string>
#include <vector>
#include <boost/shared_ptr.hpp>
#include <boost/date_time.hpp>


namespace pwiz {
namespace vendor_api {
namespace ABI {


PWIZ_API_DECL enum ScanType
{
    UNDEFINED_SCAN = -1,
    Q1_SCAN = 0,
    SIM_Q1 = 1,
    Q3_SCAN = 2,
    SIM_Q3 = 3,
    MRM = 4,
    PRECURSOR_ION_SCAN = 5,
    PRODUCT_ION_SCAN = 6,
    NEUTRAL_LOSS_SCAN = 7,
    TOF_MS_SCAN = 8,
    TOF_PRODUCT_ION_SCAN = 9,
    TOF_PRECURSOR_ION_SCAN = 10,
    ENHANCED_PRODUCT_ION_SCAN = 11,
    ENHANCED_RESOLUTION_SCAN = 12,
    MSMSMS_SCAN = 13,
    TIMEDELAY_FRAGMENTATION_SCAN = 14,
    ENHANCED_MS_SCAN = 15,
    ENHANCED_MULTCHARGE_SCAN = 16
};

PWIZ_API_DECL enum InstrumentModel
{
    API100 = 1,
    API100LC = 2,
    API150MCA = 3,
    API150EX = 4,
    API165 = 5,
    API300 = 6,
    API350 = 7,
    API365 = 8,
    API2000 = 9,
    API3000 = 10,
    API4000 = 11,
    GenericSingleQuad = 12,
    API2000QTrap = 13,
    API4000QTrap = 14,
    API3200 = 15,
    API3200QTrap = 16,
    API5000 = 17,
    CaribouQTrap = 21,
    API5500QTrap = 22,
    QStar = 2001,
    NlxTof = 2002,
    QStarPulsarI = 2003,
    QStarXL = 2004,
    QStarElite = 2005,
    API5600TripleTOF = 2006,
};

PWIZ_API_DECL enum InstrumentType
{
    SingleQuad = 0,
    TripleQuad = 1,
    Trap = 2,
    CaribouTrap = 3,
    Tof = 4
};

PWIZ_API_DECL enum IonSourceType
{
    Medusa = 1,
    Duo = 2,
    FlowNanoSpray = 3,
    TurboSpray = 4,
    HeatedNebulizer = 5,
    IonSpray = 6,
    None = 7,
    Maldi = 8,
    PhotoSpray = 9
};

PWIZ_API_DECL enum Polarity
{
    NotDefined = -10,
    Negative = -1,
    Positive = 1
};


struct PWIZ_API_DECL Spectrum
{
    virtual int getSampleNumber() const = 0;
    virtual int getPeriodNumber() const = 0;
    virtual int getExperimentNumber() const = 0;
    virtual int getCycleNumber() const = 0;

    virtual bool getHasIsolationInfo() const = 0;
    virtual void getIsolationInfo(double& centerMz, double& lowerLimit, double& upperLimit) const = 0;

    virtual bool getHasPrecursorInfo() const = 0;
    virtual void getPrecursorInfo(double& selectedMz, double& intensity, int& charge) const = 0;

    virtual double getStartTime() const = 0;

    virtual bool getDataIsContinuous() const = 0;
    virtual size_t getDataSize(bool doCentroid) const = 0;
    virtual void getData(bool doCentroid, std::vector<double>& mz, std::vector<double>& intensities) const = 0;

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

    virtual double getCycleStartTime(int cycle) const = 0;

    virtual size_t getSRMSize() const = 0;
    virtual void getSRM(size_t index, Target& target) const = 0;
    virtual void getSIC(size_t index, std::vector<double>& times, std::vector<double>& intensities) const = 0;

    virtual void getAcquisitionMassRange(double& startMz, double& stopMz) const = 0;
    virtual ScanType getScanType() const = 0;
    virtual Polarity getPolarity() const = 0;

    virtual void getTIC(std::vector<double>& times, std::vector<double>& intensities) const = 0;

    virtual ~Experiment() {}
};

typedef boost::shared_ptr<Experiment> ExperimentPtr;


class PWIZ_API_DECL WiffFile
{
    public:
    typedef boost::shared_ptr<WiffFile> Ptr;
    static Ptr create(const std::string& wiffpath);

    virtual int getSampleCount() const = 0;
    virtual int getPeriodCount(int sample) const = 0;
    virtual int getExperimentCount(int sample, int period) const = 0;
    virtual int getCycleCount(int sample, int period, int experiment) const = 0;

    virtual const std::vector<std::string>& getSampleNames() const = 0;

    virtual InstrumentModel getInstrumentModel() const = 0;
    virtual InstrumentType getInstrumentType() const = 0;
    virtual IonSourceType getIonSourceType() const = 0;
    virtual boost::local_time::local_date_time getSampleAcquisitionTime() const = 0;

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
