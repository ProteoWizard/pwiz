//
// SpectrumTable.cpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#include "SpectrumTable.hpp"
#include "msdata/TextWriter.hpp"
#include "boost/filesystem/path.hpp"
#include "boost/filesystem/fstream.hpp"
#include <iostream>
#include <iomanip>
#include <fstream>


namespace pwiz {
namespace analysis {


using namespace std;
using boost::lexical_cast;
namespace bfs = boost::filesystem;


SpectrumTable::SpectrumTable(const MSDataCache& cache)
:   cache_(cache)
{}


MSDataAnalyzer::UpdateRequest 
SpectrumTable::updateRequested(const DataInfo& dataInfo, 
                               const SpectrumIdentity& spectrumIdentity) const 
{
    // make sure everything gets cached by MSDataCache, even though we don't 
    // actually look at the update() message

    return UpdateRequest_NoBinary;
}


void SpectrumTable::close(const DataInfo& dataInfo)
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
    const size_t width_nativeID = 12;
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

    os << setfill(' ')
       << setw(width_index) << "# index"
       << setw(width_id) << "id"
       << setw(width_nativeID) << "nativeID"
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
       << endl;

    for (vector<SpectrumInfo>::const_iterator it=cache_.begin(); it!=cache_.end(); ++it)
    {
        os << setw(width_index) << it->index 
           << setw(width_id) << it->id 
           << setw(width_nativeID) << it->nativeID
           << setw(width_scanEvent) << it->scanEvent 
           << setw(width_massAnalyzerType) << cvinfo(it->massAnalyzerType).shortName()
           << setw(width_msLevel) << "ms" + lexical_cast<string>(it->msLevel)
           << setw(width_retentionTime) << fixed << setprecision(2) << it->retentionTime
           << setw(width_mzLow) << fixed << setprecision(0) << it->mzLow 
           << setw(width_mzHigh) << fixed << setprecision(0) << it->mzHigh 
           << setw(width_basePeakMZ) << fixed << setprecision(4) << it->basePeakMZ 
           << setw(width_basePeakIntensity) << fixed << setprecision(2) << it->basePeakIntensity
           << setw(width_totalIonCurrent) << fixed << setprecision(2) << it->totalIonCurrent
           << setw(width_precursorMZ) << fixed << setprecision(4) << (!it->precursors.empty() ? it->precursors[0].mz : 0)
           << endl;
    }
}


} // namespace analysis 
} // namespace pwiz

