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

#include "RegionSlice.hpp"
#include "pwiz/data/msdata/TextWriter.hpp"
#include "boost/filesystem/path.hpp"
#include "boost/filesystem/fstream.hpp"
#include "pwiz/utility/misc/Std.hpp"

namespace pwiz {
namespace analysis {
    
PWIZ_API_DECL RegionSlice::Config::Config(const string& args)
{
    mzRange = make_pair(0, numeric_limits<double>::max());
    rtRange = make_pair(0, numeric_limits<double>::max());
    indexRange = make_pair(0, numeric_limits<size_t>::max());
    scanNumberRange = make_pair(0, numeric_limits<int>::max());
    dumpRegionData = true;

    vector<string> tokens;
    istringstream iss(args);
    copy(istream_iterator<string>(iss), istream_iterator<string>(), back_inserter(tokens));

    ostringstream suffix;
    suffix << ".slice";

    for (vector<string>::const_iterator it=tokens.begin(); it!=tokens.end(); ++it)
    {
        if(checkDelimiter(*it))
        {
           // that was a valid delimiter arg
        }
        else if (parseRange("mz", *it, mzRange, "RegionSlice::Config"))
        {
                suffix << ".mz_" << fixed << setprecision(4) << mzRange.first 
                       << "-" << mzRange.second;
        }
        else if (parseRange("rt", *it, rtRange, "RegionSlice::Config"))
        {
                suffix << ".rt_" << fixed << setprecision(2) << rtRange.first 
                       << "-" << rtRange.second;
        }
        else if (parseRange("index", *it, indexRange, "RegionSlice::Config"))
        {
                suffix << ".index_" << indexRange.first << "-" << indexRange.second;
        }
        else if (parseRange("sn", *it, scanNumberRange, "RegionSlice::Config"))
        {
                suffix << ".sn_" << scanNumberRange.first << "-" << scanNumberRange.second;
        }
        else
        {
            cerr << "[RegionSlice::Config] Ignoring argument: " << *it << endl;
        }
    }

    suffix << getFileExtension(); // based on delimiter type
    filenameSuffix = suffix.str();
}


PWIZ_API_DECL RegionSlice::RegionSlice(const MSDataCache& cache, const Config& config)
:   cache_(cache),
    regionAnalyzer_(new RegionAnalyzer(config, cache_))
{}


PWIZ_API_DECL void RegionSlice::open(const DataInfo& dataInfo)
{
    regionAnalyzer_->open(dataInfo);
}


PWIZ_API_DECL
MSDataAnalyzer::UpdateRequest 
RegionSlice::updateRequested(const DataInfo& dataInfo, 
                             const SpectrumIdentity& spectrumIdentity) const 
{
    return regionAnalyzer_->updateRequested(dataInfo, spectrumIdentity); 
}


PWIZ_API_DECL
void RegionSlice::update(const DataInfo& dataInfo, 
                         const Spectrum& spectrum)
{
    return regionAnalyzer_->update(dataInfo, spectrum); 
}


PWIZ_API_DECL void RegionSlice::close(const DataInfo& dataInfo)
{
    regionAnalyzer_->close(dataInfo);
}


} // namespace analysis 
} // namespace pwiz

