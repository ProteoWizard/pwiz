//
// DemuxTestData.cpp
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

#include "pwiz/analysis/demux/DemuxTestData.hpp"
#include <pwiz/data/msdata/MSData.hpp>
#include <pwiz/utility/misc/Export.hpp>
#ifndef _USE_MATH_DEFINES
#define _USE_MATH_DEFINES
#endif
#include <math.h>
#include <random>
#include <boost/smart_ptr/make_shared.hpp>
#include "DemuxHelpers.hpp"

namespace pwiz
{
namespace analysis
{
namespace test
{
using namespace pwiz::msdata;

MS1Scan::MS1Scan(double startProductMz, double endProductMz) :
AbstractMSScan(startProductMz, endProductMz)
{
}

MS2Scan::MS2Scan(double startProductMz, double endProductMz) :
AbstractMSScan(startProductMz, endProductMz),
_precursors(boost::make_shared<vector<Precursor>>())
{
}

boost::shared_ptr<const vector<msdata::Precursor>> MS2Scan::precursors() const
{
    return _precursors;
}

void MS2Scan::setPrecursors(const vector<pair<double, double>>& mzCentersAndWidths)
{
    _precursors->clear();
    for (auto mzCenterAndWidth : mzCentersAndWidths)
    {
        auto mzCenter = mzCenterAndWidth.first;
        auto mzWidth = mzCenterAndWidth.second;

        Precursor p(mzCenter);
        p.isolationWindow.cvParams.push_back(CVParam(cv::MS_isolation_window_target_m_z, mzCenter, cv::MS_m_z));
        p.isolationWindow.cvParams.push_back(CVParam(cv::MS_isolation_window_upper_offset, mzWidth / 2.0, cv::MS_m_z));
        p.isolationWindow.cvParams.push_back(CVParam(cv::MS_isolation_window_lower_offset, mzWidth / 2.0, cv::MS_m_z));
        _precursors->push_back(p);
    }
}

void initializeMSDDemuxMeta(msdata::MSData& msd)
{
    msd.id = "urn:lsid:psidev.info:mzML.instanceDocuments.simdemux.pwiz";

    // cvList

    msd.cvs = defaultCVList();

    // fileDescription

    FileContent& fc = msd.fileDescription.fileContent;
    fc.set(MS_MS1_spectrum);
    fc.set(MS_MSn_spectrum);

    SourceFilePtr sfp(new SourceFile);
    sfp->id = "DEMUX_RAW";
    sfp->name = "simulated_demux.raw";
    sfp->location = "file://D:/data/Exp01";
    sfp->set(MS_Thermo_nativeID_format);
    sfp->set(MS_Thermo_RAW_format);
    sfp->set(MS_SHA_1, "1234567890123456789012345678901234567890");
    msd.fileDescription.sourceFilePtrs.push_back(sfp);

    msd.fileDescription.contacts.resize(1);

    // paramGroupList

    ParamGroupPtr pg1(new ParamGroup);
    pg1->id = "CommonMS1SpectrumParams";
    pg1->set(MS_Exactive);
    pg1->set(MS_instrument_serial_number, "Exactive Series slot #1");
    msd.paramGroupPtrs.push_back(pg1);

    // instrumentConfigurationList

    InstrumentConfigurationPtr instrumentConfigurationPtr(new InstrumentConfiguration("IC1"));
    instrumentConfigurationPtr->set(MS_Exactive);
    instrumentConfigurationPtr->componentList.push_back(Component(MS_nanoelectrospray, 1));
    instrumentConfigurationPtr->componentList.push_back(Component(MS_orbitrap, 2));
    instrumentConfigurationPtr->componentList.push_back(Component(MS_inductive_detector, 3));

    SoftwarePtr softwareXcalibur(new Software);
    softwareXcalibur->id = "Xcalibur";
    softwareXcalibur->set(MS_Xcalibur);
    softwareXcalibur->version = "2.0-148000/2.0.2.1461";
    instrumentConfigurationPtr->softwarePtr = softwareXcalibur;

    msd.instrumentConfigurationPtrs.push_back(instrumentConfigurationPtr);

    // softwareList

    SoftwarePtr softwarepwiz(new Software);
    softwarepwiz->id = "pwiz";
    softwarepwiz->set(MS_pwiz);
    softwarepwiz->version = "3.0.3505";

    msd.softwarePtrs.push_back(softwarepwiz);
    msd.softwarePtrs.push_back(softwareXcalibur);

    // dataProcessingList

    // (none)

    // run

    msd.run.id = "Experiment 1";
    msd.run.defaultInstrumentConfigurationPtr = instrumentConfigurationPtr;
    msd.run.startTimeStamp = "2018-01-01T12:00:00.00000";
    msd.run.defaultSourceFilePtr = sfp;
}

double normalPDF(double x, double mu = 0.0, double sigma = 1.0)
{
    return 1.0 / (sqrt(2.0 * M_PI) * sigma) * exp(-pow(x - mu, 2) / (2.0 * pow(sigma, 2.0)));
}

SimpleAcquisitionScheme::SimpleAcquisitionScheme(Params params)
{
    _scans.push_back(boost::make_shared<MS1Scan>(params.startProductMz, params.endProductMz));
    double mzWidth = (params.endPrecursorMz - params.startPrecursorMz) / double(params.ms2ScansPerCycle);
    for (size_t scanNum = 0; scanNum < params.ms2ScansPerCycle; ++scanNum)
    {
        auto scanPtr = boost::make_shared<MS2Scan>(params.startProductMz, params.endProductMz);
        _scans.push_back(scanPtr);
        MS2Scan& ms2scan = *scanPtr;
        double mzCenter = params.startPrecursorMz + mzWidth / 2.0 + scanNum * mzWidth;
        vector<pair<double, double>> mzCentersAndWidths;
        mzCentersAndWidths.push_back(pair<double, double>(mzCenter, mzWidth));
        ms2scan.setPrecursors(mzCentersAndWidths);
    }
}

size_t SimpleAcquisitionScheme::numScans() const
{
    return _scans.size();
}

size_t SimpleAcquisitionScheme::numPrecursors() const
{
    return 1;
}

boost::shared_ptr<IScanEvent> SimpleAcquisitionScheme::scan(size_t scanNum) const
{
    return _scans.at(scanNum);
}

OverlapAcquisitionScheme::OverlapAcquisitionScheme(Params params) :
_params(params)
{
    for (size_t overlapNum = 0; overlapNum < params.overlapsPerSpectrum + 1; ++overlapNum)
    {
        _scans.push_back(boost::make_shared<MS1Scan>(params.startProductMz, params.endProductMz));
        double mzWidth = (params.endPrecursorMz - params.startPrecursorMz) / double(params.ms2ScansPerCycle);
        double overlapOffset = double(overlapNum) * mzWidth / double(params.overlapsPerSpectrum + 1);
        for (size_t scanNum = 0; scanNum < params.ms2ScansPerCycle; ++scanNum)
        {
            auto scanPtr = boost::make_shared<MS2Scan>(params.startProductMz, params.endProductMz);
            _scans.push_back(scanPtr);
            MS2Scan& ms2scan = *scanPtr;
            double mzCenter = overlapOffset + params.startPrecursorMz + mzWidth / 2.0 + scanNum * mzWidth;
            vector<pair<double, double>> mzCentersAndWidths;
            mzCentersAndWidths.push_back(pair<double, double>(mzCenter, mzWidth));
            ms2scan.setPrecursors(mzCentersAndWidths);
        }
    }
}

size_t OverlapAcquisitionScheme::numScans() const
{
    return _scans.size();
}

size_t OverlapAcquisitionScheme::numPrecursors() const
{
    return 1;
}

boost::shared_ptr<IScanEvent> OverlapAcquisitionScheme::scan(size_t scanNum) const
{
    return _scans.at(scanNum);
}

SimpleAnalyte::SimpleAnalyte(int randomSeed, double startPrecursorMz, double endPrecursorMz, double startFragmentMz, double endFragmentMz) :
_randomSeed(randomSeed),
_fragmentMzs(boost::make_shared<vector<double>>()),
_fragmentRelintensities(boost::make_shared<vector<double>>())
{
    const size_t numFragments = 5;
    std::mt19937 gen;
    gen.seed(randomSeed);
    std::uniform_real_distribution<double> precursorDist(startPrecursorMz, endPrecursorMz);
    _precursorMz = precursorDist(gen);

    std::uniform_real_distribution<double> fragmentMzDist(startFragmentMz, endFragmentMz);
    std::uniform_real_distribution<double> fragmentRelIntensityDist(0.0, 1.0);
    for (size_t i = 0; i < numFragments; ++i)
    {
        _fragmentMzs->push_back(fragmentMzDist(gen));
        _fragmentRelintensities->push_back(fragmentRelIntensityDist(gen));
    }

    std::sort(_fragmentRelintensities->begin(), _fragmentRelintensities->end(),
        [this](size_t i, size_t j)
    { return (this->_fragmentMzs->at(i) < this->_fragmentMzs->at(j)); }
    );
    std::sort(_fragmentMzs->begin(), _fragmentMzs->end());
}

double SimpleAnalyte::precursorMz() const
{
    return _precursorMz;
}

boost::shared_ptr<vector<double>> SimpleAnalyte::fragmentMzs() const
{
    return _fragmentMzs;
}

boost::shared_ptr<vector<double>> SimpleAnalyte::fragmentRelIntensities() const
{
    return _fragmentRelintensities;
}

RegularSineElutionScheme::RegularSineElutionScheme() :
_sigmaInTime(1.0),
_timeBetweenPeaks(5.0),
_timeBetweenLongOscillations(50.0)
{
}

vector<pair<size_t, double>> RegularSineElutionScheme::indexedAnalyteIntensity(double time) const
{
    vector<pair<size_t, double>> indexedAnalyteIntensities;
    indexedAnalyteIntensities.push_back(pair<size_t, double>(peakIndex(time), intensity(time)));
    return std::move(indexedAnalyteIntensities);
}

boost::shared_ptr<IAnalyte> RegularSineElutionScheme::analyte(size_t index) const
{
    auto it = _analyteCache.find(index);
    if (it == _analyteCache.end())
    {
        _analyteCache.insert(std::pair<size_t, boost::shared_ptr<IAnalyte>>(index, boost::make_shared<SimpleAnalyte>(index)));
        it = _analyteCache.find(index);
    }
    return it->second;
}

void RegularSineElutionScheme::setSigma(double sigmaInTime)
{
    _sigmaInTime = sigmaInTime;
}

void RegularSineElutionScheme::setPeriod(double timeBetweenPeaks)
{
    _timeBetweenPeaks = timeBetweenPeaks;
}

void RegularSineElutionScheme::setSinePeriod(double timeBetweenLongOscillations)
{
    _timeBetweenLongOscillations = timeBetweenLongOscillations;
}

double RegularSineElutionScheme::intensity(double time) const
{
    double sineMinimumOffset = 1.5;
    double sineScalingFactor = sineMinimumOffset + sin(time * 2.0 * M_PI / _timeBetweenLongOscillations);
    double mu = _timeBetweenPeaks / 2.0;
    double timeWithinGaussianPeak = fmod(time, _timeBetweenPeaks);
    double normalDistValue = normalPDF(timeWithinGaussianPeak, mu, _sigmaInTime);
    return sineScalingFactor * normalDistValue;
}

size_t RegularSineElutionScheme::peakIndex(double time) const
{
    return (size_t)floorl(time / _timeBetweenPeaks);
}

SimulatedSpectrum::SimulatedSpectrum(boost::shared_ptr<IScanEvent> scan)
{
    _scan = scan;
}

int SimulatedSpectrum::msLevel() const
{
    return _scan->mslevel();
}

boost::shared_ptr<vector<double>> SimulatedSpectrum::mzs() const
{
    return _mzs;
}

void SimulatedSpectrum::setMzs(boost::shared_ptr<vector<double>> mzs)
{
    _mzs = mzs;
}

boost::shared_ptr<vector<double>> SimulatedSpectrum::intensities() const
{
    return _intensities;
}

void SimulatedSpectrum::setIntensities(boost::shared_ptr<vector<double>> intensities)
{
    _intensities = intensities;
}

boost::shared_ptr<IScanEvent> SimulatedSpectrum::scan() const
{
    return _scan;
}

SimulatedMassSpec::SimulatedMassSpec() :
_maxRunDuration(100.0),
_scanRate(20.0),
_currentScanNum(0)
{
}

class own_double_less : public std::binary_function<double, double, bool>
{
public:
    own_double_less(double arg_ = 1e-7) : epsilon(arg_) {}
    bool operator()(const double &left, const double &right) const
    {
        // you can choose other way to make decision
        // (The original version is: return left < right;) 
        return (abs(left - right) > epsilon) && (left < right);
    }
    double epsilon;
};

class own_double_equal : public std::binary_function<double, double, bool>
{
public:
    own_double_equal(double arg_ = 1e-7) : epsilon(arg_) {}
    bool operator()(const double &left, const double &right) const
    {
        auto lt = own_double_less(epsilon);
        return !lt(left, right) && !lt(right, left);
    }
    double epsilon;
};

void addPointToSpectrum(double mz, double ionsPerMs, boost::shared_ptr<vector<double>> mzs, boost::shared_ptr<vector<double>> intensities)
{
    // find index of mz in list of mzs. Add if it doesn't exist
    auto it = std::lower_bound(std::begin(*mzs), std::end(*mzs), mz, own_double_less());

    if (it == std::end(*mzs))
    {
        // add the mz to the list
        mzs->insert(it, mz);
        intensities->insert(std::end(*intensities), 0.0);

        // update iterator
        it = std::lower_bound(std::begin(*mzs), std::end(*mzs), mz, own_double_less());
    }
    else if (!own_double_equal()(*it, mz))
    {
        // add the mz to the list
        mzs->insert(it, mz);
        auto dist = std::distance(std::begin(*mzs), it);
        intensities->insert(std::begin(*intensities) + dist, 0.0);

        // update iterator
        it = std::lower_bound(std::begin(*mzs), std::end(*mzs), mz, own_double_less());
    }

    // use index of mz to add ions
    intensities->insert(std::begin(*intensities) + (it - std::begin(*mzs)), ionsPerMs);
}

void addAnalyteFragments(boost::shared_ptr<vector<double>> analyteMzs, boost::shared_ptr<vector<double>> analyteIntensities, boost::shared_ptr<vector<double>> mzs, boost::shared_ptr<vector<double>> intensities)
{
    assert(analyteIntensities->size() == analyteMzs->size());
    for (size_t i = 0; i < analyteIntensities->size(); ++i)
    {
        addPointToSpectrum(analyteMzs->at(i), analyteIntensities->at(i), mzs, intensities);
    }
}

boost::shared_ptr<SimulatedSpectrum> SimulatedMassSpec::nextScan()
{
    auto spectrum = boost::shared_ptr<SimulatedSpectrum>();
    assert(!spectrum);
    auto scanTime = double(_currentScanNum) / _scanRate;
    if (scanTime < _maxRunDuration)
    {
        spectrum.reset(new SimulatedSpectrum(_acquisitionScheme->scan(_currentScanNum % _acquisitionScheme->numScans())));
        auto indexedAnalyteIntensity = _elutionScheme->indexedAnalyteIntensity(scanTime);
        auto mzs = boost::make_shared<vector<double>>();
        auto intensities = boost::make_shared<vector<double>>();
        for (const auto analytePair : indexedAnalyteIntensity)
        {
            const auto analyteIndex = analytePair.first;
            auto analyteIonsPerMillisecond = analytePair.second;

            const auto analyte = _elutionScheme->analyte(analyteIndex);

            auto fragmentMzs = boost::make_shared<vector<double>>();
            auto fragmentRelIntensities = boost::make_shared<vector<double>>();
            if (spectrum->scan()->mslevel() == 1)
            {

                fragmentMzs->push_back(analyte->precursorMz());
                fragmentRelIntensities->push_back(1.0);
            }
            else if (spectrum->scan()->mslevel() == 2 && spectrum->scan())
            {
                auto ms2scan = boost::dynamic_pointer_cast<MS2Scan>(spectrum->scan());
                if (!ms2scan)
                    throw runtime_error("Unknown MS2 scan type");
                auto precursorMz = analyte->precursorMz();
                if (!std::any_of(ms2scan->precursors()->begin(), ms2scan->precursors()->end(), [precursorMz](Precursor p)
                {
                    return precursor_mz_low(p) <= precursorMz && precursorMz <= precursor_mz_high(p);
                })
                    )
                    continue; // Analyte is not in the precursor range. Go to next analyte.

                *fragmentMzs = *analyte->fragmentMzs();
                *fragmentRelIntensities = *analyte->fragmentRelIntensities();
            }
            else
            {
                throw runtime_error("[SimulatedMassSpec::nextScan()] Unknown ms level");
            }
            std::transform(std::begin(*fragmentRelIntensities), std::end(*fragmentRelIntensities),
                std::begin(*fragmentRelIntensities),
                std::bind(std::multiplies<double>(), std::placeholders::_1, analyteIonsPerMillisecond));
            addAnalyteFragments(fragmentMzs, fragmentRelIntensities, mzs, intensities);

        }
        spectrum->setMzs(mzs);
        spectrum->setIntensities(intensities);
        ++_currentScanNum;
    }
    return spectrum;
}

void SimulatedMassSpec::initialize(boost::shared_ptr<IAcquisitionScheme> acquisitionScheme, boost::shared_ptr<IElutionScheme> elutionScheme)
{
    _acquisitionScheme = acquisitionScheme;
    _elutionScheme = elutionScheme;
}

double SimulatedMassSpec::runDurationPerCycle() const
{
    if (!_acquisitionScheme)
        throw runtime_error("[SimulatedMassSpec::runDurationPerCycle()] SimulatedMassSpec must first be initialized.");
    return double(_acquisitionScheme->numScans()) / _scanRate;
}

void SimulatedMassSpec::setRunDuration(double runDuration)
{
    if (runDuration < 0.0)
        throw runtime_error("runDuration must be positive");
    _maxRunDuration = runDuration;
}

boost::shared_ptr<IAcquisitionScheme> acquisitionSchemeFactory(SimulatedDemuxParams params)
{
    boost::shared_ptr<IAcquisitionScheme> scheme;
    if (params.numOverlaps > 0)
    {
        OverlapAcquisitionScheme::Params schemeParams;
        schemeParams.overlapsPerSpectrum = params.numOverlaps;
        schemeParams.ms2ScansPerCycle = params.numMs2ScansPerCycle;
        scheme.reset(new OverlapAcquisitionScheme(schemeParams));
    }
    else
    {
        SimpleAcquisitionScheme::Params schemeParams;
        scheme.reset(new SimpleAcquisitionScheme(schemeParams));
    }
    return scheme;
}

void initializeMSDDemux(msdata::MSData& msd, SimulatedDemuxParams params)
{
    initializeMSDDemuxMeta(msd);

    InstrumentConfigurationPtr instrumentConfigurationPtr(msd.run.defaultInstrumentConfigurationPtr);
    ParamGroupPtr pg1(msd.paramGroupPtrs.front());

    boost::shared_ptr<SpectrumListSimple> spectrumList(new SpectrumListSimple);
    msd.run.spectrumListPtr = spectrumList;

    // Create acquisition scheme
    boost::shared_ptr<IAcquisitionScheme> acquisitionScheme = acquisitionSchemeFactory(params);

    // Create elution scheme
    boost::shared_ptr<IElutionScheme> elutionScheme = boost::make_shared<RegularSineElutionScheme>();

    SimulatedMassSpec massSpec;
    massSpec.initialize(acquisitionScheme, elutionScheme);
    massSpec.setRunDuration(massSpec.runDurationPerCycle() * params.numCycles);

    // Create spectra
    size_t scanNum = 0;
    while (true)
    {
        auto spectrum = massSpec.nextScan();
        if (!spectrum)
            break;

        if (!(spectrum->msLevel() == 1 || spectrum->msLevel() == 2))
        {
            throw runtime_error("MS level other than 1 and 2 not implemented");
        }

        // Write spectrum
        spectrumList->spectra.push_back(boost::make_shared<Spectrum>());
        Spectrum& ms = *spectrumList->spectra.back();
        boost::format scanfmt("scan=%1%");
        scanfmt % scanNum;
        ms.id = scanfmt.str();
        ms.index = scanNum;
        ms.set(MS_ms_level, spectrum->msLevel());
        ms.set(MS_centroid_spectrum);
        if (!spectrum->mzs()->empty())
        {
            ms.set(MS_lowest_observed_m_z, *std::min_element(std::begin(*spectrum->mzs()), std::end(*spectrum->mzs())), MS_m_z);
            ms.set(MS_highest_observed_m_z, *std::max_element(std::begin(*spectrum->mzs()), std::end(*spectrum->mzs())), MS_m_z);
            ms.set(MS_base_peak_m_z, 445.347, MS_m_z); // junk value
            ms.set(MS_base_peak_intensity, 120053, MS_number_of_detector_counts); // junk value
            ms.set(MS_total_ion_current, 1.66755e+007); // junk value
        }
        ms.paramGroupPtrs.push_back(pg1);
        ms.scanList.scans.push_back(Scan());
        ms.scanList.set(MS_no_combination);
        Scan& scan = ms.scanList.scans.back();
        scan.instrumentConfigurationPtr = instrumentConfigurationPtr;
        scan.set(MS_scan_start_time, 5.890500, UO_minute); // junk value
        scan.set(MS_preset_scan_configuration, 3);
        scan.scanWindows.resize(1);

        ScanWindow& window = ms.scanList.scans.back().scanWindows.front();
        const auto abstractmsscan = boost::dynamic_pointer_cast<AbstractMSScan>(spectrum->scan());
        if (!abstractmsscan)
            throw logic_error("Failed downcast to AbstractMSScan, unknown scan type");

        window.set(MS_scan_window_lower_limit, abstractmsscan->startProductMz(), MS_m_z);
        window.set(MS_scan_window_upper_limit, abstractmsscan->endProductMz(), MS_m_z);

        BinaryDataArrayPtr ms1_mz(new BinaryDataArray);
        ms1_mz->set(MS_m_z_array, "", MS_m_z);
        ms1_mz->data = *spectrum->mzs();

        BinaryDataArrayPtr ms1_intensity(new BinaryDataArray);
        ms1_intensity->set(MS_intensity_array, "", MS_number_of_detector_counts);
        ms1_intensity->data = *spectrum->intensities();

        ms.binaryDataArrayPtrs.push_back(ms1_mz);
        ms.binaryDataArrayPtrs.push_back(ms1_intensity);
        ms.defaultArrayLength = ms1_mz->data.size();

        if (spectrum->msLevel() == 1)
        {
            // do nothing
        }
        else if (spectrum->msLevel() == 2)
        {
            const auto ms2scan = boost::dynamic_pointer_cast<MS2Scan>(spectrum->scan());
            if (!ms2scan)
                throw logic_error("Failed downcast, unknown scan type");

            ms.precursors = *ms2scan->precursors();
        }

        // Increment scan index
        ++scanNum;
    }

    // chromatograms
    // (none)
}
} // namespace test
} // namespace analysis
} // namespace pwiz