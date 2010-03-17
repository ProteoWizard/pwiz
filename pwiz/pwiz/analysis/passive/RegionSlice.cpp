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
#include <iostream>
#include <iomanip>
#include <fstream>
#include <stdexcept>


namespace pwiz {
namespace analysis {


using namespace std;
using boost::shared_ptr;
using boost::lexical_cast;
using boost::bad_lexical_cast;


namespace {
template <typename value_type>
bool parseRange(const string& text, pair<value_type,value_type>& result)
{
    string::size_type indexComma = text.find(',');

    if (text.empty() ||
        text[0] != '[' || 
        text[text.size()-1] != ']' ||
        indexComma == string::npos)
    {
        cerr << "[RegionSlice::parseRange()] Unable to parse range: " << text << endl;
        return false;
    }
    
    try
    {
        string first = text.substr(1, indexComma-1);
        string second = text.substr(indexComma+1, text.size()-indexComma-2);
        result.first = lexical_cast<value_type>(first);
        result.second = lexical_cast<value_type>(second);
        return true;
    }
    catch (bad_lexical_cast&)
    {
        cerr << "[RegionSlice::parseRange()] Unable to parse range: " << text << endl;
        return false;
    }
}
} // namespace


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
        if (it->find("mz=[")==0)
        {
            if (parseRange(it->substr(3), mzRange))
                suffix << ".mz_" << fixed << setprecision(4) << mzRange.first 
                       << "-" << mzRange.second;
        }
        else if (it->find("rt=[")==0)
        {
            if (parseRange(it->substr(3), rtRange))
                suffix << ".rt_" << fixed << setprecision(2) << rtRange.first 
                       << "-" << rtRange.second;
        }
        else if (it->find("index=[")==0)
        {
            if (parseRange(it->substr(6), indexRange))
                suffix << ".index_" << indexRange.first << "-" << indexRange.second;
        }
        else if (it->find("sn=[")==0)
        {
            if (parseRange(it->substr(3), scanNumberRange))
                suffix << ".sn_" << scanNumberRange.first << "-" << scanNumberRange.second;
        }
        else
        {
            cerr << "[RegionSlice::Config] Ignoring argument: " << *it << endl;
        }
    }

    suffix << ".txt";
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

