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

#include "RegionTIC.hpp"
#include "pwiz/data/msdata/TextWriter.hpp"
#include "boost/filesystem/path.hpp"
#include "boost/filesystem/fstream.hpp"
#include <iostream>
#include <iomanip>
#include <fstream>
#include <stdexcept>


namespace pwiz {
namespace analysis {


using namespace std;
using boost::shared_ptr;
using boost::lexical_cast;
namespace bfs = boost::filesystem;


PWIZ_API_DECL RegionTIC::Config::Config(const string& args)
:   mzRange(make_pair(0, 10000))
{
    vector<string> tokens;
    istringstream iss(args);
    copy(istream_iterator<string>(iss), istream_iterator<string>(), back_inserter(tokens));
    if (tokens.size() > 2)
        throw runtime_error(("[RegionTIC::Config] Invalid args: " + args).c_str());

    if (tokens.size()>0) mzRange.first = lexical_cast<double>(tokens[0]);
    if (tokens.size()>1) mzRange.second = lexical_cast<double>(tokens[1]);
}


PWIZ_API_DECL RegionTIC::RegionTIC(const MSDataCache& cache, const Config& config)
:   cache_(cache), config_(config)
{
    RegionAnalyzer::Config regionAnalyzerConfig;
    regionAnalyzerConfig.mzRange = config.mzRange;
   
    regionAnalyzer_ = shared_ptr<RegionAnalyzer>(new RegionAnalyzer(regionAnalyzerConfig, cache));
}


PWIZ_API_DECL void RegionTIC::open(const DataInfo& dataInfo)
{
    regionAnalyzer_->open(dataInfo);
}


PWIZ_API_DECL
MSDataAnalyzer::UpdateRequest 
RegionTIC::updateRequested(const DataInfo& dataInfo, 
                             const SpectrumIdentity& spectrumIdentity) const 
{
    return regionAnalyzer_->updateRequested(dataInfo, spectrumIdentity); 
}


PWIZ_API_DECL
void RegionTIC::update(const DataInfo& dataInfo, 
                       const Spectrum& spectrum)
{
    return regionAnalyzer_->update(dataInfo, spectrum); 
}


PWIZ_API_DECL void RegionTIC::close(const DataInfo& dataInfo)
{
    regionAnalyzer_->close(dataInfo);

    ostringstream oss;
    oss << dataInfo.sourceFilename << ".tic." 
        << fixed << setprecision(2) << config_.mzRange.first << "-"
        << fixed << setprecision(2) << config_.mzRange.second << ".txt";

    bfs::path outputFilename = dataInfo.outputDirectory;
    outputFilename /= oss.str();

    if (dataInfo.log) 
        *dataInfo.log << "[RegionTIC] Writing file " << outputFilename.string() << endl;

    bfs::ofstream os(outputFilename);

    const size_t width_index = 7;
    const size_t width_id = 12;
    const size_t width_scanEvent = 7;
    const size_t width_massAnalyzerType = 9;
    const size_t width_msLevel = 8;
    const size_t width_retentionTime = 12;
    const size_t width_sumIntensity = 16;

    os << "# " << dataInfo.sourceFilename << endl
        << setw(width_index) << "# index"
        << setw(width_id) << "id"
        << setw(width_scanEvent) << "event"
        << setw(width_massAnalyzerType) << "analyzer"
        << setw(width_msLevel) << "msLevel"
        << setw(width_retentionTime) << "rt"
        << setw(width_sumIntensity) << "sumIntensity"
        << endl;

    if (cache_.size() != regionAnalyzer_->spectrumStats().size())
        throw runtime_error("[RegionTIC::close()] Cache sizes do not match.");

    for (size_t i=0, end=cache_.size(); i!=end; ++i)
    {
        const SpectrumInfo& info = cache_[i];
        const RegionAnalyzer::SpectrumStats& spectrumStats = regionAnalyzer_->spectrumStats()[i];

        os  << setw(width_index) << info.index
            << setw(width_id) << info.id
            << setw(width_scanEvent) << info.scanEvent
            << setw(width_massAnalyzerType) << info.massAnalyzerTypeAbbreviation()
            << setw(width_msLevel) << "ms" + lexical_cast<string>(info.msLevel)
            << setw(width_retentionTime) << fixed << setprecision(2) << info.retentionTime
            << setw(width_sumIntensity) << fixed << setprecision(4) << spectrumStats.sumIntensity
            << endl;
    }
}


} // namespace analysis 
} // namespace pwiz

