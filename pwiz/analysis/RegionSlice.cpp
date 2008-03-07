//
// RegionSlice.cpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#include "RegionSlice.hpp"
#include "msdata/TextWriter.hpp"
#include "boost/regex.hpp"
#include "boost/filesystem/path.hpp"
#include "boost/filesystem/fstream.hpp"
#include <iostream>
#include <iomanip>
#include <fstream>


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
    static const boost::regex e("\\[([^,]+),([^,]+)\\]");

    boost::smatch what; 
    if (!regex_match(text, what, e))
    {
        cerr << "[RegionSlice::parseRange()] Unable to parse range: " << text << endl;
        return false;
    }

    try
    {
        result.first = lexical_cast<value_type>(what[1]);
        result.second = lexical_cast<value_type>(what[2]);
        return true;
    }
    catch (bad_lexical_cast&)
    {
        cerr << "[RegionSlice::parseRange()] Unable to parse range: " << text << endl;
        return false;
    }
}
} // namespace


RegionSlice::Config::Config(const string& args)
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


RegionSlice::RegionSlice(const MSDataCache& cache, const Config& config)
:   cache_(cache),
    regionAnalyzer_(new RegionAnalyzer(config, cache_))
{}


void RegionSlice::open(const DataInfo& dataInfo)
{
    regionAnalyzer_->open(dataInfo);
}


MSDataAnalyzer::UpdateRequest 
RegionSlice::updateRequested(const DataInfo& dataInfo, 
                             const SpectrumIdentity& spectrumIdentity) const 
{
    return regionAnalyzer_->updateRequested(dataInfo, spectrumIdentity); 
}


void RegionSlice::update(const DataInfo& dataInfo, 
                         const Spectrum& spectrum)
{
    return regionAnalyzer_->update(dataInfo, spectrum); 
}


void RegionSlice::close(const DataInfo& dataInfo)
{
    regionAnalyzer_->close(dataInfo);
}


} // namespace analysis 
} // namespace pwiz

