//
// SpectrumTable.cpp
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

#include "SpectrumTable.hpp"
#include "pwiz/data/msdata/TextWriter.hpp"
#include "boost/filesystem/path.hpp"
#include "boost/filesystem/fstream.hpp"
#include <iostream>
#include <stdexcept>
#include <iomanip>
#include <fstream>


namespace pwiz {
namespace analysis {


using namespace std;
using boost::lexical_cast;
namespace bfs = boost::filesystem;


PWIZ_API_DECL SpectrumTable::SpectrumTable(const MSDataCache& cache)
:   cache_(cache)
{}


PWIZ_API_DECL
MSDataAnalyzer::UpdateRequest 
SpectrumTable::updateRequested(const DataInfo& dataInfo, 
                               const SpectrumIdentity& spectrumIdentity) const 
{
    // make sure everything gets cached by MSDataCache, even though we don't 
    // actually look at the update() message

    return UpdateRequest_NoBinary;
}


PWIZ_API_DECL void SpectrumTable::close(const DataInfo& dataInfo)
{
    bfs::path filename = dataInfo.outputDirectory;
    filename /= dataInfo.sourceFilename + ".spectrum_table.txt";
    bfs::ofstream os(filename);
    if (!os) throw runtime_error(("[SpectrumTable] Unable to open file " + 
                                 filename.string()).c_str());

    if (dataInfo.log)
        *dataInfo.log << "[SpectrumTable] Writing file " << filename.string() << endl;

    os << "# " << dataInfo.sourceFilename << endl;

    const size_t width_index = 7;
    const size_t width_id = 12;
    const size_t width_massAnalyzerType = 9;
    const size_t width_scanEvent = 7;
    const size_t width_msLevel = 8;
    const size_t width_retentionTime = 12;
    const size_t width_mzLow = 7;
    const size_t width_mzHigh = 7;
    const size_t width_basePeakMZ = 12;
    const size_t width_basePeakIntensity = 14;
    const size_t width_totalIonCurrent = 14;
    const size_t width_precursorMZ = 12;
    const size_t width_thermoMonoisotopicMZ = 15;
    const size_t width_filterStringMZ = 15;

    os << setfill(' ')
       << setw(width_index) << "# index"
       << setw(width_id) << "id"
       << setw(width_scanEvent) << "event"
       << setw(width_massAnalyzerType) << "analyzer"
       << setw(width_msLevel) << "msLevel"
       << setw(width_retentionTime) << "rt"
       << setw(width_mzLow) << "mzLow"
       << setw(width_mzHigh) << "mzHigh"
       << setw(width_basePeakMZ) << "basePeakMZ"
       << setw(width_basePeakIntensity) << "basePeakInt"
       << setw(width_totalIonCurrent) << "TIC"
       << setw(width_precursorMZ) << "precursorMZ"
       << setw(width_thermoMonoisotopicMZ) << "thermo_monoMZ"
       << setw(width_filterStringMZ) << "filterStringMZ"
       << endl;

    for (vector<SpectrumInfo>::const_iterator it=cache_.begin(); it!=cache_.end(); ++it)
    {
        os << setw(width_index) << it->index 
           << setw(width_id) << it->id 
           << setw(width_scanEvent) << it->scanEvent 
           << setw(width_massAnalyzerType) << it->massAnalyzerTypeAbbreviation()
           << setw(width_msLevel) << "ms" + lexical_cast<string>(it->msLevel)
           << setw(width_retentionTime) << fixed << setprecision(2) << it->retentionTime
           << setw(width_mzLow) << fixed << setprecision(0) << it->mzLow 
           << setw(width_mzHigh) << fixed << setprecision(0) << it->mzHigh 
           << setw(width_basePeakMZ) << fixed << setprecision(4) << it->basePeakMZ 
           << setw(width_basePeakIntensity) << fixed << setprecision(2) << it->basePeakIntensity
           << setw(width_totalIonCurrent) << fixed << setprecision(2) << it->totalIonCurrent
           << setw(width_precursorMZ) << fixed << setprecision(4) << (!it->precursors.empty() ? it->precursors[0].mz : 0)
           << setw(width_thermoMonoisotopicMZ) << fixed << setprecision(4) << it->thermoMonoisotopicMZ
           << setw(width_filterStringMZ) << fixed << setprecision(4) << it->mzFromFilterString()
           << endl;
    }
}


} // namespace analysis 
} // namespace pwiz

