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

#include "SpectrumBinaryData.hpp"
#include "pwiz/data/msdata/TextWriter.hpp"
#include "boost/filesystem/path.hpp"
#include "boost/filesystem/fstream.hpp"
#include "pwiz/utility/misc/Std.hpp"

namespace pwiz {
namespace analysis {


namespace bfs = boost::filesystem;


PWIZ_API_DECL SpectrumBinaryData::Config::Config(const std::string& args)
:   begin(0), end(0), 
    interpretAsScanNumbers(false), 
    precision(4)
{
    istringstream iss(args);
    vector<string> tokens;
    copy(istream_iterator<string>(iss), istream_iterator<string>(), back_inserter(tokens));

    // look for newer style args
    bool newstyle = true;
    std::pair<size_t,size_t> begin_end;
    BOOST_FOREACH(const string& token, tokens)
    {
        if (parseRange(BINARY_INDEX_ARG,token,begin_end,"SpectrumBinaryData"))
            interpretAsScanNumbers = false;
        else if (parseRange(BINARY_SCAN_ARG,token,begin_end,"SpectrumBinaryData"))
            interpretAsScanNumbers = true;
        else if (parseValue(BINARY_PRECISION_ARG,token,precision,"SpectrumBinaryData"))
            ;
        else
            newstyle = false;
    }
    if (newstyle) 
    {
        begin = begin_end.first;
        end = begin_end.second;
        if (numeric_limits<size_t>::max() != end)
            end++;  // internally the end range is exclusive
        return; // we're done
    }

    // assume old style less consistent arg style
    for (vector<string>::const_iterator it=tokens.begin(); it!=tokens.end(); ++it)
    {
        if (*it == "sn")
            interpretAsScanNumbers = true;
        else if (it->find("precision=") == 0)
            precision = lexical_cast<size_t>(it->substr(10));    
        else
        {
            try
            {
                string::size_type hyphen = it->find('-');
                begin = lexical_cast<size_t>(it->substr(0,hyphen));
                if (hyphen == string::npos)
                    end = begin+1;
                else if (hyphen == it->size()-1) 
                    end = numeric_limits<size_t>::max(); 
                else
                    end = lexical_cast<size_t>(it->substr(hyphen+1)) + 1;
            }
            catch (bad_lexical_cast&)
            {
                cerr << "[SpectrumBinaryData] Unknown option: " << *it << endl;
            }
        }
    }
}


PWIZ_API_DECL SpectrumBinaryData::SpectrumBinaryData(const MSDataCache& cache, const Config& config)
:   cache_(cache), config_(config)
{}


PWIZ_API_DECL
MSDataAnalyzer::UpdateRequest 
SpectrumBinaryData::updateRequested(const DataInfo& dataInfo, 
                                    const SpectrumIdentity& spectrumIdentity) const 
{
    if (!config_.interpretAsScanNumbers &&
        spectrumIdentity.index >= config_.begin &&
        spectrumIdentity.index < config_.end)
        return UpdateRequest_Full;

    if (config_.interpretAsScanNumbers)
    {
        try
        {
            size_t scanNumber = id::valueAs<size_t>(spectrumIdentity.id, "scan");
            if (scanNumber>=config_.begin && scanNumber<config_.end)
                return UpdateRequest_Full;
        }
        catch (bad_lexical_cast&)
        {
            return UpdateRequest_None;
        }
    }

    return UpdateRequest_None;
}


PWIZ_API_DECL
void SpectrumBinaryData::update(const DataInfo& dataInfo, 
                                const Spectrum& spectrum)
{
    const SpectrumInfo& info = cache_[spectrum.index]; 

    bfs::path filename = dataInfo.outputDirectory;
    filename /= dataInfo.sourceFilename + ".binary." +
                (config_.interpretAsScanNumbers ? 
                    "sn" + lexical_cast<string>(info.scanNumber) : 
                    lexical_cast<string>(info.index)) +
                ".txt";

    bfs::ofstream os(filename);
    if (!os) throw runtime_error(("[SpectrumBinaryData] Unable to open file " + 
                                 filename.string()).c_str());

    if (dataInfo.log)
        *dataInfo.log << "[SpectrumBinaryData] Writing file " << filename.string() << endl;

    os << "# " << dataInfo.sourceFilename << endl;
    os << "#\n";
    os << "# index: " << info.index << endl;
    os << "# id: " << info.id << endl;
    os << "# scanNumber: " << info.scanNumber << endl;
    os << "# massAnalyzerType: " << cvTermInfo(info.massAnalyzerType).name << endl;
    os << "# scanEvent: " << info.scanEvent << endl;
    os << "# msLevel: " << info.msLevel << endl;
    os << "# retentionTime: " << info.retentionTime << endl;
    os << "# filterString: " << info.filterString << endl;
    os << "# mzLow: " << info.mzLow << endl;
    os << "# mzHigh: " << info.mzHigh << endl;
    os << "# basePeakMZ: " << info.basePeakMZ << endl;
    os << "# basePeakIntensity: " << info.basePeakIntensity << endl;
    os << "# totalIonCurrent: " << info.totalIonCurrent << endl;
    os << "# precursorCount: " << info.precursors.size() << endl;

    for (size_t i=0; i<info.precursors.size(); i++)
        os << "# precursor " << i << ": " 
           << info.precursors[i].mz << " " 
           << info.precursors[i].intensity << endl;

    os << "# binary (" << info.data.size() << "): \n";

    for (vector<MZIntensityPair>::const_iterator it=info.data.begin(); it!=info.data.end(); ++it)
        os << fixed << setprecision((std::streamsize)config_.precision) << setfill(' ') 
           << setw(8+(std::streamsize)config_.precision) << it->mz << "\t" 
           << setw(8+(std::streamsize)config_.precision) << it->intensity << endl;
}


} // namespace analysis 
} // namespace pwiz

