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
#include "pwiz/utility/misc/Std.hpp"

namespace pwiz {
namespace analysis {


namespace bfs = boost::filesystem;


PWIZ_API_DECL RegionTIC::Config::Config(const string& args)
:   mzRange(make_pair(0, 10000))
{
    vector<string> tokens;
    istringstream iss(args);
    copy(istream_iterator<string>(iss), istream_iterator<string>(), back_inserter(tokens));

    // look for newer style args
    bool newstyle = false;
    BOOST_FOREACH(const string& token, tokens)
    {
        if (checkDelimiter(token))
            ; // valid delimiter=...
        else
            newstyle = parseRange(TIC_MZRANGE_ARG,token,mzRange,"RegionTIC::Config");
    }
    if (newstyle)
        return; // we're done.

    // look for traditional, less cohesive style args
    size_t rangeFirst = 0;
    if(tokens.size() && checkDelimiter(tokens[0])) 
    {
        rangeFirst++; // that was a valid delimter arg
    }
    if (tokens.size() > 2+rangeFirst)
        throw runtime_error(("[RegionTIC::Config] Invalid args: " + args).c_str());

    if (tokens.size()>rangeFirst) mzRange.first = lexical_cast<double>(tokens[rangeFirst]);
    if (tokens.size()>rangeFirst+1) mzRange.second = lexical_cast<double>(tokens[rangeFirst+1]);
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
        << fixed << setprecision(2) << config_.mzRange.second << config_.getFileExtension();

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

    char delimiter = config_.getDelimiterChar();
#define DELIMWRITE(w,txt) if (delimiter) {os << txt << delimiter ;} else { os << setw(w) << txt;}
#define DELIMWRITE_EOL(w,txt) if (delimiter) {os << txt << endl ;} else { os << setw(w) << txt << endl;}
    os << "# " << dataInfo.sourceFilename << endl;
    DELIMWRITE(width_index,"# index");
    DELIMWRITE(width_id,"id");
    DELIMWRITE(width_scanEvent,"event");
    DELIMWRITE(width_massAnalyzerType,"analyzer");
    DELIMWRITE(width_msLevel,"msLevel");
    DELIMWRITE(width_retentionTime,"rt");
    DELIMWRITE_EOL(width_sumIntensity,"sumIntensity");

    if (cache_.size() != regionAnalyzer_->spectrumStats().size())
        throw runtime_error("[RegionTIC::close()] Cache sizes do not match.");

    for (size_t i=0, end=cache_.size(); i!=end; ++i)
    {
        const SpectrumInfo& info = cache_[i];
        const RegionAnalyzer::SpectrumStats& spectrumStats = regionAnalyzer_->spectrumStats()[i];

        DELIMWRITE(width_index,info.index);
        DELIMWRITE(width_id,info.id);
        DELIMWRITE(width_scanEvent,info.scanEvent);
        DELIMWRITE(width_massAnalyzerType,info.massAnalyzerTypeAbbreviation());
        DELIMWRITE(width_msLevel,"ms" + lexical_cast<string>(info.msLevel));
        DELIMWRITE(width_retentionTime,fixed << setprecision(2) << info.retentionTime);
        DELIMWRITE_EOL(width_sumIntensity,fixed << setprecision(4) << spectrumStats.sumIntensity);
    }
}


} // namespace analysis 
} // namespace pwiz

