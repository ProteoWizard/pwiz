//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/msdata/SpectrumListBase.hpp"
#include "Reader_Waters_Detail.hpp"
#include "pwiz/utility/misc/Container.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Stream.hpp"
#include "pwiz/data/msdata/Reader.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_3D.hpp"
#include <boost/thread.hpp>


using boost::shared_ptr;


namespace pwiz {
namespace msdata {
namespace detail {


//
// SpectrumList_Waters
//
class PWIZ_API_DECL SpectrumList_Waters : public SpectrumListIonMobilityBase
{
    public:
        struct VendorOptions
        {
            pwiz::util::IntegerSet msLevelsToCentroid;

        };
    virtual size_t size() const;
    virtual const SpectrumIdentity& spectrumIdentity(size_t index) const;
    virtual size_t find(const string& id) const;
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData) const;
    virtual SpectrumPtr spectrum(size_t index, DetailLevel detailLevel) const;

    // TODO: convert vendor-specific parameters to a single config struct, but all non-vendor wrappers are going to have to pass it along :(
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData, const pwiz::util::IntegerSet& msLevelsToCentroid) const;
    virtual SpectrumPtr spectrum(size_t index, DetailLevel detailLevel, const pwiz::util::IntegerSet& msLevelsToCentroid) const;
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData, double lockmassMzPosScans, double lockmassMzNegScans, double lockmassTolerance, const pwiz::util::IntegerSet& msLevelsToCentroid) const;
    virtual SpectrumPtr spectrum(size_t index, DetailLevel detailLevel, double lockmassMzPosScans, double lockmassMzNegScans, double lockmassTolerance, const pwiz::util::IntegerSet& msLevelsToCentroid) const;

    virtual pwiz::analysis::Spectrum3DPtr spectrum3d(double scanStartTime, const boost::icl::interval_set<double>& driftTimeRanges) const;
    
    /// returns true if any functions are SONAR-enabled
    virtual bool hasSonarFunctions() const;
    virtual pair<int, int> sonarMzToDriftBinRange(int function, float precursorMz, float precursorTolerance) const;

    virtual bool hasIonMobility() const;
    virtual bool hasCombinedIonMobility() const;
    virtual bool canConvertIonMobilityAndCCS() const;
    virtual double ionMobilityToCCS(double ionMobility, double mz, int charge) const;
    virtual double ccsToIonMobility(double ccs, double mz, int charge) const;

#ifdef PWIZ_READER_WATERS
    SpectrumList_Waters(MSData& msd, RawDataPtr rawdata, const Reader::Config& config);

    private:

    MSData& msd_;
    RawDataPtr rawdata_;
    size_t size_;
    Reader::Config config_;

    struct IndexEntry : public SpectrumIdentity
    {
        int function;
        int process;
        int scan;
        int block; // block < 0 is not ion mobility
    };

    mutable vector<IndexEntry> index_;
    mutable map<string, size_t> idToIndexMap_;
    mutable boost::container::flat_map<double, vector<pair<int, int> > > scanTimeToFunctionAndBlockMap_;

    void getCombinedSpectrumData(int function, int block, BinaryData<double>& mz, BinaryData<double>& intensity, BinaryData<double>& driftTime) const;

    void initializeCoefficients() const;
    double calibrate(const double &mz) const;
    mutable vector<double> calibrationCoefficients_;
    mutable vector<double> imsCalibratedMasses_;
    mutable vector<float> imsMasses_;
    mutable vector<int> massIndices_;
    mutable vector<float> imsIntensities_;
    mutable boost::mutex readMutex;

    void createIndex();
#endif // PWIZ_READER_WATERS
};

} // detail
} // msdata
} // pwiz
