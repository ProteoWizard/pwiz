//
// DemuxTestData.hpp
//
//
// Original author: Austin Keller <atkeller .@. uw.edu>
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

#ifndef _DEMUXTESTDATA_HPP
#define _DEMUXTESTDATA_HPP

#include "pwiz/utility/misc/Std.hpp"
#include <boost/smart_ptr/shared_ptr.hpp>
#include "pwiz/data/msdata/MSData.hpp"


namespace pwiz {
namespace msdata {
struct MSData;
}
}

namespace pwiz
{
namespace analysis
{
namespace test
{

struct SimulatedDemuxParams
{
    SimulatedDemuxParams(size_t numPrecursorsPerSpectrum = 3,
        size_t numOverlaps = 1,
        size_t numCycles = 10,
        size_t numMs2ScansPerCycle = 25,
        double startPrecursorMz = 400.0,
        double endPrecursorMz = 1000.0,
        bool overlapOnly = false,
        int randomDataSeed = 0) :
        numPrecursorsPerSpectrum(numPrecursorsPerSpectrum),
        numOverlaps(numOverlaps),
        numCycles(numCycles),
        numMs2ScansPerCycle(numMs2ScansPerCycle),
        startPrecursorMz(startPrecursorMz),
        endPrecursorMz(endPrecursorMz),
        overlapOnly(overlapOnly),
        randomDataSeed(randomDataSeed) {}

    size_t numPrecursorsPerSpectrum;
    size_t numOverlaps;
    size_t numCycles;
    size_t numMs2ScansPerCycle;
    double startPrecursorMz;
    double endPrecursorMz;
    bool overlapOnly;
    int randomDataSeed;
};

class IScanEvent
{
public:
    virtual ~IScanEvent() {}
    virtual int mslevel() const = 0;
};

class AbstractMSScan : public IScanEvent
{
public:
    AbstractMSScan(double startProductMz, double endProductMz) :
        _startProductMz(startProductMz),
        _endProductMz(endProductMz) {}
    virtual ~AbstractMSScan() {}
    int mslevel() const override = 0;
    double startProductMz() const { return _startProductMz; }
    double endProductMz() const { return _endProductMz; }

protected:
    double _startProductMz;
    double _endProductMz;
};

class MS1Scan : public AbstractMSScan
{
public:
    MS1Scan(double startProductMz, double endProductMz);
    virtual ~MS1Scan() {}
    int mslevel() const final { return 1; }
};

class MS2Scan : public AbstractMSScan
{
public:
    MS2Scan(double startProductMz, double endProductMz);
    virtual ~MS2Scan() {}
    int mslevel() const final { return 2; }
    boost::shared_ptr<const vector<msdata::Precursor>> precursors() const;
    void setPrecursors(const vector<pair<double, double>>& mzCentersAndWidths);

private:
    boost::shared_ptr<vector<msdata::Precursor>> _precursors;
};

class IAcquisitionScheme
{
public:
    virtual ~IAcquisitionScheme() {}

    virtual size_t numScans() const = 0;
    // Number of precursors per scan
    virtual size_t numPrecursors() const = 0;
    virtual boost::shared_ptr<IScanEvent> scan(size_t scanNum) const = 0;
};

class SimpleAcquisitionScheme : public IAcquisitionScheme
{
public:
    struct Params
    {
        Params(
            size_t ms2ScansPerCycle = 9,
            double startPrecursorMz = 500.0,
            double endPrecursorMz = 900.0,
            double startProductMz = 400.0,
            double endProductMz = 1200.0,
            int randomSeed = 0) :
            ms2ScansPerCycle(ms2ScansPerCycle),
            startPrecursorMz(startPrecursorMz),
            endPrecursorMz(endPrecursorMz),
            startProductMz(startProductMz),
            endProductMz(endProductMz),
            randomSeed(randomSeed) {}

        size_t ms2ScansPerCycle;
        double startPrecursorMz;
        double endPrecursorMz;
        double startProductMz;
        double endProductMz;
        int randomSeed;
    };
    SimpleAcquisitionScheme(Params params = Params());
    virtual ~SimpleAcquisitionScheme() {}
    size_t numScans() const override;
    size_t numPrecursors() const override;
    boost::shared_ptr<IScanEvent> scan(size_t scanNum) const override;
private:
    std::vector<boost::shared_ptr<IScanEvent>> _scans;
};

class OverlapAcquisitionScheme : public IAcquisitionScheme
{
public:
    struct Params
    {
        Params(size_t ms2ScansPerCycle = 9,
            size_t overlapsPerSpectrum = 1,
            double startPrecursorMz = 500.0,
            double endPrecursorMz = 900.0,
            double startProductMz = 400.0,
            double endProductMz = 1200.0,
            int randomSeed = 0) :
            ms2ScansPerCycle(ms2ScansPerCycle),
            overlapsPerSpectrum(overlapsPerSpectrum),
            startPrecursorMz(startPrecursorMz),
            endPrecursorMz(endPrecursorMz),
            startProductMz(startProductMz),
            endProductMz(endProductMz),
            randomSeed(randomSeed) {}

        size_t ms2ScansPerCycle;
        size_t overlapsPerSpectrum;
        double startPrecursorMz;
        double endPrecursorMz;
        double startProductMz;
        double endProductMz;
        int randomSeed;
    };
    OverlapAcquisitionScheme(Params params = Params());
    virtual ~OverlapAcquisitionScheme() {}
    size_t numScans() const override;
    size_t numPrecursors() const override;
    boost::shared_ptr<IScanEvent> scan(size_t scanNum) const override;
private:
    std::vector<boost::shared_ptr<IScanEvent>> _scans;
    Params _params;
};

class IAnalyte
{
public:
    virtual ~IAnalyte() {}
    virtual double precursorMz() const = 0;
    virtual boost::shared_ptr<vector<double>> fragmentMzs() const = 0;
    virtual boost::shared_ptr<vector<double>> fragmentRelIntensities() const = 0;
};

class SimpleAnalyte : public IAnalyte
{
public:
    SimpleAnalyte(
        int randomSeed = 0,
        double startPrecursorMz = 400.0,
        double endPrecursorMz = 900.0,
        double startFragmentMz = 200.0,
        double endFragmentMz = 1200.0);
    virtual ~SimpleAnalyte() {}
    double precursorMz() const override;
    boost::shared_ptr<vector<double>> fragmentMzs() const override;
    boost::shared_ptr<vector<double>> fragmentRelIntensities() const override;
private:
    double _precursorMz;
    boost::shared_ptr<vector<double>> _fragmentMzs;
    boost::shared_ptr<vector<double>> _fragmentRelintensities;
    int _randomSeed;
};

class IElutionScheme
{
public:
    virtual ~IElutionScheme() {}
    // Returns the analyte index and the number of ions per millisecond for that analyte
    virtual vector<pair<size_t, double>> indexedAnalyteIntensity(double time) const = 0;
    virtual boost::shared_ptr<IAnalyte> analyte(size_t index) const = 0;
};

class RegularSineElutionScheme : public IElutionScheme
{
public:
    RegularSineElutionScheme();
    virtual ~RegularSineElutionScheme() {}
    vector<pair<size_t, double>> indexedAnalyteIntensity(double time) const override;
    boost::shared_ptr<IAnalyte> analyte(size_t index) const override;

    void setSigma(double sigmaInTime);
    void setPeriod(double timeBetweenPeaks);
    void setSinePeriod(double timeBetweenLongOscillations);

private:
    double intensity(double time) const;
    /// Index for the eluting peak. This can be used to determine which analyte is eluting.
    size_t peakIndex(double time) const;

    mutable std::map<size_t, boost::shared_ptr<IAnalyte>> _analyteCache;
    double _sigmaInTime;
    double _timeBetweenPeaks;
    double _timeBetweenLongOscillations;
};

class SimulatedSpectrum
{
public:
    SimulatedSpectrum(boost::shared_ptr<IScanEvent> scan);
    int msLevel() const;
    boost::shared_ptr<vector<double>> mzs() const;
    void setMzs(boost::shared_ptr<vector<double>> mzs);
    boost::shared_ptr<vector<double>> intensities() const;
    void setIntensities(boost::shared_ptr<vector<double>> intensities);
    boost::shared_ptr<IScanEvent> scan() const;
private:
    boost::shared_ptr<IScanEvent> _scan;
    boost::shared_ptr<vector<double>> _mzs;
    boost::shared_ptr<vector<double>> _intensities;
};

class SimulatedMassSpec
{
public:
    SimulatedMassSpec();
    boost::shared_ptr<SimulatedSpectrum> nextScan();
    void initialize(boost::shared_ptr<IAcquisitionScheme> acquisitionScheme, boost::shared_ptr<IElutionScheme> elutionScheme);
    void setRunDuration(double runDuration);
    double runDurationPerCycle() const;

private:
    boost::shared_ptr<IElutionScheme> _elutionScheme;
    boost::shared_ptr<IAcquisitionScheme> _acquisitionScheme;
    double _maxRunDuration;
    double _scanRate;
    size_t _currentScanNum;
};

void initializeMSDDemux(msdata::MSData& msd, SimulatedDemuxParams params = SimulatedDemuxParams());
boost::shared_ptr<IAcquisitionScheme> acquisitionSchemeFactory(SimulatedDemuxParams params);
} // namespace test
} // namespace analysis
} // namespace pwiz
#endif // _DEMUXTESTDATA_HPP