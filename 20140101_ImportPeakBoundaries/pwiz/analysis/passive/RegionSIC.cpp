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

#include "RegionSIC.hpp"
#include "pwiz/data/msdata/TextWriter.hpp"
#include "boost/filesystem/path.hpp"
#include "boost/filesystem/fstream.hpp"
#include "pwiz/utility/misc/Std.hpp"

namespace pwiz {
namespace analysis {


namespace bfs = boost::filesystem;


PWIZ_API_DECL RegionSIC::Config::Config(const string& args)
:   mzCenter(0), radius(0), radiusUnits(RadiusUnits_Unknown)
{
    vector<string> tokens;
    istringstream iss(args);
    copy(istream_iterator<string>(iss), istream_iterator<string>(), back_inserter(tokens));

    // look for newer style args
    int nParsed = 0;
    string radiusUnitsVal;
    BOOST_FOREACH(const string& token, tokens)
    {
        if (checkDelimiter(token))
            ; // that was delimiter=...
        else if (parseValue(SIC_MZCENTER_ARG,token,mzCenter,"RegionSIC::Config"))
           nParsed++;
        else if (parseValue(SIC_RADIUS_ARG,token,radius,"RegionSIC::Config"))
           nParsed++;
        else if (parseValue(SIC_RADIUSUNITS_ARG,token,radiusUnitsVal,"RegionSIC::Config"))
        {
           nParsed++;
           if (radiusUnitsVal=="amu") radiusUnits = RadiusUnits_amu;
           else if (radiusUnitsVal=="ppm") radiusUnits = RadiusUnits_ppm;
           else std::cerr << "unknown " << SIC_RADIUSUNITS_ARG << " value " << radiusUnitsVal;
        }
        else
            nParsed = 999; // fall through to old style
    }
    if (nParsed!=3)
    {
        // look for traditional, less cohesive style args    
        size_t rangeFirst = 0;
        if(tokens.size() && checkDelimiter(tokens[0])) 
        {
            rangeFirst++; // that was a valid delimter arg
        }
        if (tokens.size() != rangeFirst+3)
            throw runtime_error(("[RegionSIC::Config] Invalid args: " + args).c_str());

        mzCenter = lexical_cast<double>(tokens[rangeFirst]);
        radius = lexical_cast<double>(tokens[rangeFirst+1]);
        if (tokens[rangeFirst+2]=="amu") radiusUnits = RadiusUnits_amu;
        if (tokens[rangeFirst+2]=="ppm") radiusUnits = RadiusUnits_ppm;
    }

    if (radiusUnits == RadiusUnits_Unknown)
        throw runtime_error(("[RegionSIC::Config] Invalid args: " + args).c_str());
}


PWIZ_API_DECL RegionSIC::RegionSIC(const MSDataCache& cache, const Config& config)
:   cache_(cache), config_(config)
{
    RegionAnalyzer::Config regionAnalyzerConfig;
    regionAnalyzerConfig.copyDelimiterConfig(config);
    double delta = config.radius;
    if (config.radiusUnits == Config::RadiusUnits_ppm)
        delta *= config.mzCenter * 1e-6; 

    regionAnalyzerConfig.mzRange = make_pair(config.mzCenter-delta, config.mzCenter+delta); 
    regionAnalyzerConfig.dumpRegionData = true;

    ostringstream suffix;
    suffix << ".sic." << fixed << setprecision(4) << config.mzCenter << ".data"+config_.getFileExtension();
    regionAnalyzerConfig.filenameSuffix = suffix.str(); 
    
    regionAnalyzer_ = shared_ptr<RegionAnalyzer>(new RegionAnalyzer(regionAnalyzerConfig, cache));
}


PWIZ_API_DECL void RegionSIC::open(const DataInfo& dataInfo)
{
    regionAnalyzer_->open(dataInfo);
}


PWIZ_API_DECL
MSDataAnalyzer::UpdateRequest 
RegionSIC::updateRequested(const DataInfo& dataInfo, 
                             const SpectrumIdentity& spectrumIdentity) const 
{
    return regionAnalyzer_->updateRequested(dataInfo, spectrumIdentity); 
}


PWIZ_API_DECL
void RegionSIC::update(const DataInfo& dataInfo, 
                       const Spectrum& spectrum)
{
    return regionAnalyzer_->update(dataInfo, spectrum); 
}


PWIZ_API_DECL void RegionSIC::close(const DataInfo& dataInfo)
{
    regionAnalyzer_->close(dataInfo);

    ostringstream base;
    base << dataInfo.sourceFilename << ".sic." << fixed << setprecision(4) << config_.mzCenter;

    // write peaks info

    bfs::path outputPeaks = (bfs::path)dataInfo.outputDirectory / (base.str() + ".peaks"+ config_.getFileExtension());
    if (dataInfo.log) *dataInfo.log << "[RegionSIC] Writing file " << outputPeaks.string() << endl;
    bfs::ofstream osPeaks(outputPeaks);

    const size_t width_index = 7;
    const size_t width_id = 12;
    const size_t width_scanEvent = 7;
    const size_t width_massAnalyzerType = 9;
    const size_t width_msLevel = 8;
    const size_t width_retentionTime = 12;
    const size_t width_sumIntensity = 16;
    const size_t width_peakMZ = 16;
    const size_t width_peakIntensity = 16;

    char delimiter = config_.getDelimiterChar();
#define DELIMWRITE(w,txt) if (delimiter) {osPeaks << txt << delimiter ;} else { osPeaks << setw(w) << txt;}
#define DELIMWRITE_EOL(w,txt) if (delimiter) {osPeaks << txt << endl ;} else { osPeaks << setw(w) << txt << endl;}

    osPeaks << "# " << dataInfo.sourceFilename << endl;
    DELIMWRITE(width_index,"# index");
    DELIMWRITE(width_id,"id");
    DELIMWRITE(width_scanEvent,"event");
    DELIMWRITE(width_massAnalyzerType,"analyzer");
    DELIMWRITE(width_msLevel,"msLevel");
    DELIMWRITE(width_retentionTime,"rt");
    DELIMWRITE(width_sumIntensity,"sumIntensity");
    DELIMWRITE(width_peakMZ,"peakMZ");
    DELIMWRITE_EOL(width_peakIntensity,"peakIntensity");

    if (cache_.size() != regionAnalyzer_->spectrumStats().size())
        throw runtime_error("[RegionSIC::close()] Cache sizes do not match.");

    for (size_t i=0, end=cache_.size(); i!=end; ++i)
    {
        const SpectrumInfo& info = cache_[i];
        const RegionAnalyzer::SpectrumStats& spectrumStats = regionAnalyzer_->spectrumStats()[i];

        if (spectrumStats.sumIntensity)
        {
            DELIMWRITE(width_index,info.index);
            DELIMWRITE(width_id,info.id);
            DELIMWRITE(width_scanEvent,info.scanEvent);
            DELIMWRITE(width_massAnalyzerType,info.massAnalyzerTypeAbbreviation());
            DELIMWRITE(width_msLevel,"ms" + lexical_cast<string>(info.msLevel));
            DELIMWRITE(width_retentionTime,fixed << setprecision(2) << info.retentionTime);
            DELIMWRITE(width_sumIntensity,fixed << setprecision(4) << spectrumStats.sumIntensity);
            DELIMWRITE(width_peakMZ,fixed << setprecision(4) << spectrumStats.peak.mz);
            DELIMWRITE_EOL(width_peakIntensity,fixed << setprecision(4) << spectrumStats.peak.intensity);
        }
    }

    // write summary

    bfs::path outputSummary = (bfs::path)dataInfo.outputDirectory / (base.str() + ".summary.txt");
    if (dataInfo.log) *dataInfo.log << "[RegionSIC] Writing file " << outputSummary.string() << endl;
    bfs::ofstream osSummary(outputSummary);

    const RegionAnalyzer::Stats& stats = regionAnalyzer_->stats();

    osSummary << setprecision(12) 
              << "nonzeroCount: " << stats.nonzeroCount << endl
              << "mean_peak_mz: " << stats.mean_peak_mz << endl
              << "sum_sumIntensity: " << stats.sum_sumIntensity << endl
              << "sum_peak_intensity: " << stats.sum_peak_intensity << endl
              << "mean_peak_mz: " << stats.mean_peak_mz << endl
              << "variance_peak_mz: " << stats.variance_peak_mz << endl
              << "sd_peak_mz: " << stats.sd_peak_mz << endl
              << "apex_index: " << stats.indexApex << endl;
    
    const SpectrumInfo& apexInfo = cache_[stats.indexApex];
    const RegionAnalyzer::SpectrumStats& apexStats = regionAnalyzer_->spectrumStats()[stats.indexApex];
    osSummary << "apex_id: " << apexInfo.id << endl
              << "apex_rt: " << apexInfo.retentionTime << endl
              << "apex_mz: " << apexStats.peak.mz << endl
              << "apex_intensity: " << apexStats.peak.intensity << endl;
}


} // namespace analysis 
} // namespace pwiz

