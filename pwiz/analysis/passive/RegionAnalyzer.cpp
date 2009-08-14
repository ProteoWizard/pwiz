//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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


#define PWIZ_SOURCE

#include "RegionAnalyzer.hpp"
#include "pwiz/utility/math/Parabola.hpp"
#include "boost/filesystem/path.hpp"
#include "boost/filesystem/fstream.hpp"
#include <iostream>
#include <iomanip>
#include <cmath>
#include <stdexcept>


namespace pwiz {
namespace analysis {


using namespace std;
using namespace pwiz::math;
using boost::lexical_cast;
namespace bfs = boost::filesystem;


//
// RegionAnalyzer data structure constructors
//


PWIZ_API_DECL RegionAnalyzer::Config::Config()
:   mzRange(make_pair(0, numeric_limits<double>::max())),
    indexRange(make_pair(0, numeric_limits<size_t>::max())),
    scanNumberRange(make_pair(0, numeric_limits<int>::max())),
    rtRange(make_pair(0, numeric_limits<double>::max())),
    dumpRegionData(false)
{}


PWIZ_API_DECL RegionAnalyzer::SpectrumStats::SpectrumStats()
:   sumIntensity(0)
{}


PWIZ_API_DECL RegionAnalyzer::Stats::Stats()
:
    nonzeroCount(0),
    sum_sumIntensity(0),
    sum_peak_intensity(0),
    mean_peak_mz(0),
    variance_peak_mz(0),
    sd_peak_mz(0),
    indexApex(0)
{}


//
// RegionAnalyzer::Impl
//


struct RegionAnalyzer::Impl
{
    Config config;
    const MSDataCache& cache;
    vector<SpectrumStats> spectrumStats;
    Stats stats;
    bool done;
    bfs::ofstream osDump;

    Impl(const Config& _config, const MSDataCache& _cache)
    :   config(_config), cache(_cache), done(false)
    {}
};


//
// RegionAnalyzer
//


PWIZ_API_DECL RegionAnalyzer::RegionAnalyzer(const Config& config, const MSDataCache& cache)
:   impl_(new Impl(config, cache))
{}


PWIZ_API_DECL const vector<RegionAnalyzer::SpectrumStats>& RegionAnalyzer::spectrumStats() const 
{
    return impl_->spectrumStats;
}


PWIZ_API_DECL const RegionAnalyzer::Stats& RegionAnalyzer::stats() const
{
    return impl_->stats;
}


namespace {
const size_t width_index_ = 7;
const size_t width_id_ = 12;
const size_t width_scanEvent_ = 7;
const size_t width_massAnalyzerType_ = 9;
const size_t width_msLevel_ = 8;
const size_t width_retentionTime_ = 12;
const size_t width_mz_ = 14;
const size_t width_intensity_ = 17;
} // namespace


PWIZ_API_DECL void RegionAnalyzer::open(const DataInfo& dataInfo)
{
    impl_->spectrumStats.clear();
    impl_->stats = Stats();
    impl_->done = false;

    if (dataInfo.msd.run.spectrumListPtr.get())
        impl_->spectrumStats.resize(dataInfo.msd.run.spectrumListPtr->size());

    if (impl_->config.dumpRegionData)
    {
        bfs::path outputFilename = dataInfo.outputDirectory;
        outputFilename /= (dataInfo.sourceFilename + impl_->config.filenameSuffix);

        if (dataInfo.log) 
            *dataInfo.log << "[RegionAnalyzer] Writing file " << outputFilename.string() << endl;

        impl_->osDump.open(outputFilename);
        
        impl_->osDump << "# " << dataInfo.sourceFilename << endl
            << setw(width_index_) << "# index"
            << setw(width_id_) << "id"
            << setw(width_scanEvent_) << "event"
            << setw(width_massAnalyzerType_) << "analyzer"
            << setw(width_msLevel_) << "msLevel"
            << setw(width_retentionTime_) << "rt"
            << setw(width_mz_) << "m/z"
            << setw(width_intensity_) << "intensity"
            << endl;
    }
}


PWIZ_API_DECL
MSDataAnalyzer::UpdateRequest 
RegionAnalyzer::updateRequested(const DataInfo& dataInfo,
                                const SpectrumIdentity& spectrumIdentity) const
{
    return impl_->done ? UpdateRequest_None : UpdateRequest_Full;
}


struct HasLowerMZ
{
    bool operator()(const MZIntensityPair& a, const MZIntensityPair& b) {return a.mz<b.mz;}
};


MZIntensityPair interpolatedPeak(vector<MZIntensityPair>::const_iterator begin,
                                 vector<MZIntensityPair>::const_iterator end,
                                 vector<MZIntensityPair>::const_iterator max)
{
    // return max if we're at the edge
    if (max==begin || max+1==end) return *max;

    // fit parabola to (max-1, max, max+1)
    vector< pair<double,double> > samples;
    for (vector<MZIntensityPair>::const_iterator it=max-1; it<=max+1; ++it)
        samples.push_back(make_pair(it->mz, it->intensity));
    Parabola p(samples);

    // peak is the vertex of the parabola
    return MZIntensityPair(p.center(), p(p.center())); 
}


PWIZ_API_DECL
void RegionAnalyzer::update(const DataInfo& dataInfo, 
                            const Spectrum& spectrum)
{
    const SpectrumInfo& info = impl_->cache[spectrum.index];

    // make sure we're in the region

    if (info.index < impl_->config.indexRange.first ||
        info.scanNumber < impl_->config.scanNumberRange.first ||
        info.retentionTime < impl_->config.rtRange.first)
        return;

    if (info.index > impl_->config.indexRange.second ||
        info.scanNumber > impl_->config.scanNumberRange.second ||
        info.retentionTime > impl_->config.rtRange.second)
    {
        impl_->done = true;
        return;
    }

    // find m/z range via binary search 

    vector<MZIntensityPair>::const_iterator begin = 
        lower_bound(info.data.begin(), info.data.end(), MZIntensityPair(impl_->config.mzRange.first, 0), HasLowerMZ());

    vector<MZIntensityPair>::const_iterator end = 
        upper_bound(info.data.begin(), info.data.end(), MZIntensityPair(impl_->config.mzRange.second, 0), HasLowerMZ());

    // calculate

    double sumIntensity = 0;
    vector<MZIntensityPair>::const_iterator max = begin;
    for (vector<MZIntensityPair>::const_iterator it=begin; it!=end; ++it)
    {
        sumIntensity += it->intensity;
        if (max->intensity < it->intensity) max = it;

        if (impl_->osDump) impl_->osDump
            << setw(width_index_) << info.index
            << setw(width_id_) << info.id
            << setw(width_scanEvent_) << info.scanEvent
            << setw(width_massAnalyzerType_) << info.massAnalyzerTypeAbbreviation()
            << setw(width_msLevel_) << "ms" + lexical_cast<string>(info.msLevel)
            << setw(width_retentionTime_) << fixed << setprecision(2) << info.retentionTime
            << setw(width_mz_) << fixed << setprecision(4) << it->mz
            << setw(width_intensity_) << fixed << setprecision(4) << it->intensity
            << endl;
    }

    // fill in SpectrumStats

    SpectrumStats& spectrumStats = impl_->spectrumStats[spectrum.index];
    spectrumStats.sumIntensity = sumIntensity;    
    if (begin != end)
    {
        spectrumStats.max = *max;
        spectrumStats.peak = interpolatedPeak(begin, end, max);
    }
}


PWIZ_API_DECL void RegionAnalyzer::close(const DataInfo& dataInfo)
{
    int count = 0;
    double sum_peak_mz = 0;
    double sum2_peak_mz = 0;
    double sum_peak_intensity = 0;
    double sum_sumIntensity = 0;
    size_t indexApex = 0;
    const double zero = numeric_limits<double>::epsilon();

    for (size_t i=0, end=impl_->spectrumStats.size(); i<end; ++i)
    {
        const SpectrumStats& ss = impl_->spectrumStats[i];
        if (ss.sumIntensity > zero)
        {        
            count++;
            sum_peak_mz += ss.peak.mz * ss.peak.intensity; 
            sum2_peak_mz += ss.peak.mz * ss.peak.mz * ss.peak.intensity; 
            sum_peak_intensity += ss.peak.intensity;
            sum_sumIntensity += ss.sumIntensity;

            if (impl_->spectrumStats[indexApex].peak.intensity < ss.peak.intensity)
                indexApex = i;
        } 
    }

    double mean_peak_mz = sum_peak_mz/sum_peak_intensity;
    double variance_peak_mz = max(sum2_peak_mz/sum_peak_intensity - mean_peak_mz*mean_peak_mz, 0.);
    double sd_peak_mz = sqrt(variance_peak_mz);

    impl_->stats.nonzeroCount = count;
    impl_->stats.sum_sumIntensity = sum_sumIntensity;
    impl_->stats.sum_peak_intensity = sum_peak_intensity;
    impl_->stats.mean_peak_mz = mean_peak_mz;
    impl_->stats.variance_peak_mz = variance_peak_mz;
    impl_->stats.sd_peak_mz = sd_peak_mz;
    impl_->stats.indexApex = indexApex;

    if (impl_->osDump)
        impl_->osDump.close();
}


} // namespace analysis 
} // namespace pwiz

