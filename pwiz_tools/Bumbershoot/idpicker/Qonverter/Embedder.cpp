//
// $Id$
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
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2011 Vanderbilt University
//
// Contributor(s):
//


#include "Embedder.hpp"
#include "Qonverter.hpp"
#include "sqlite3pp.h"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/DateTime.hpp"
#include "pwiz/utility/chemistry/Chemistry.hpp"
#include "pwiz/utility/chemistry/MZTolerance.hpp"
#include "pwiz/data/msdata/MSDataFile.hpp"
#include "pwiz/data/vendor_readers/ExtendedReaderList.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_Filter.hpp"
#include "pwiz/analysis/spectrum_processing/ThresholdFilter.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_PeakFilter.hpp"
#include "boost/foreach_field.hpp"
#include "boost/throw_exception.hpp"
#include "boost/xpressive/xpressive.hpp"

# ifdef BOOST_POSIX_API
#   include <fcntl.h>
# else // BOOST_WINDOWS_API
#   include <windows.h>
#   include <wincrypt.h>
#   pragma comment(lib, "Advapi32.lib")
# endif

namespace {
    
// boost::filesystem::unique_path() adapted from boost 1.46 until pwiz updates to filesystem v3
void fail(int err, boost::system::error_code* ec)
{
  if (ec == 0)
    BOOST_THROW_EXCEPTION(boost::system::system_error(err, boost::system::system_category,
                          "boost::filesystem::unique_path"));
  ec->assign(err, boost::system::system_category);
  return;
}

void system_crypt_random(void* buf, std::size_t len, boost::system::error_code* ec)
{
# ifdef BOOST_POSIX_API

  int file = open("/dev/urandom", O_RDONLY);
  if (file == -1)
  {
    file = open("/dev/random", O_RDONLY);
    if (file == -1)
    {
      fail(errno, ec);
      return;
    }
  }

  size_t bytes_read = 0;
  while (bytes_read < len)
  {
    ssize_t n = read(file, buf, len - bytes_read);
    if (n == -1)
    {
      close(file);
      fail(errno, ec);
      return;
    }
    bytes_read += n;
    buf = static_cast<char*>(buf) + n;
  }

  close(file);

# else // BOOST_WINDOWS_API

  HCRYPTPROV handle;
  int errval = 0;

  if (!::CryptAcquireContextA(&handle, 0, 0, PROV_RSA_FULL, 0))
  {
    errval = ::GetLastError();
    if (errval == NTE_BAD_KEYSET)
    {
      if (!::CryptAcquireContextA(&handle, 0, 0, PROV_RSA_FULL, CRYPT_NEWKEYSET))
      {
        errval = ::GetLastError();
      }
      else errval = 0;
    }
  }

  if (!errval)
  {
    BOOL gen_ok = ::CryptGenRandom(handle, len, static_cast<unsigned char*>(buf));
    if (!gen_ok)
      errval = ::GetLastError();
    ::CryptReleaseContext(handle, 0);
  }

  if (!errval) return;

  fail(errval, ec);
# endif
}

} // namespace

// boost::filesystem::unique_path() adapted from boost 1.46 until pwiz updates to filesystem v3
namespace boost { namespace filesystem {

path unique_path(const path& model = "%%%%-%%%%-%%%%-%%%%", system::error_code* ec = 0)
{
  std::string s (model.string());  // std::string ng for MBCS encoded POSIX
  const char hex[] = "0123456789abcdef";
  const int n_ran = 16;
  const int max_nibbles = 2 * n_ran;   // 4-bits per nibble
  char ran[n_ran];

  int nibbles_used = max_nibbles;
  for(std::string::size_type i=0; i < s.size(); ++i)
  {
    if (s[i] == '%')                        // digit request
    {
      if (nibbles_used == max_nibbles)
      {
        system_crypt_random(ran, sizeof(ran), ec);
        if (ec != 0 && *ec)
          return "";
        nibbles_used = 0;
      }
      int c = ran[nibbles_used/2];
      c >>= 4 * (nibbles_used++ & 1);  // if odd, shift right 1 nibble
      s[i] = hex[c & 0xf];             // convert to hex digit and replace
    }
  }

  if (ec != 0) ec->clear();

  return s;
}

}} // namespace boost::filesystem


using namespace pwiz::msdata;
using namespace pwiz::analysis;
using namespace pwiz::util;
using namespace pwiz::chemistry;
namespace sqlite = sqlite3pp;


BEGIN_IDPICKER_NAMESPACE
namespace Embedder {


#ifdef WIN32
const string defaultSourceExtensionPriorityList("mz5;mzML;mzXML;RAW;WIFF;d;t2d;ms2;cms2;mgf");
#else
const string defaultSourceExtensionPriorityList("mz5;mzML;mzXML;ms2;cms2;mgf");
#endif


// convenient macro for one-line status and cancellation updates
#define ITERATION_UPDATE(ilr, index, count, message) \
{ \
    if (ilr && ilr->broadcastUpdateMessage(IterationListener::UpdateMessage((index), (count), (message))) == IterationListener::Status_Cancel) \
        return; \
}


namespace {

struct SpectrumSource
{
    sqlite3_int64 id;
    string name;
    vector<sqlite3_int64> spectrumIds;
    vector<string> spectrumNativeIds;
    QuantitationMethod quantitationMethod;

    string filepath;
};

// return the first existing filepath with one of the given extensions in the search path
string findNameInPath(const string& filenameWithoutExtension,
                      const vector<string>& extensions,
                      const vector<string>& searchPath)
{
    ExtendedReaderList readerList;

    BOOST_FOREACH(const string& extension, extensions)
    BOOST_FOREACH(const string& path, searchPath)
    {
        bfs::path filepath(path);
        filepath /= filenameWithoutExtension + "." + extension;

        // if the path exists, check whether MSData can handle it
        if (bfs::exists(filepath) && !readerList.identify(filepath.string()).empty())
            return filepath.string();
    }
    return "";
}

void getSources(sqlite::database& idpDb,
                vector<SpectrumSource>& sources,
                const string& idpDbFilepath,
                const string& sourceSearchPath,
                const string& sourceExtensionPriorityList,
                const map<int, QuantitationMethod>& quantitationMethodBySource,
                pwiz::util::IterationListenerRegistry* ilr)
{
    string databaseName = bfs::path(idpDbFilepath).replace_extension("").filename();

    // parse the search path
    vector<string> paths;
    bal::split(paths, sourceSearchPath, bal::is_any_of(";"));

    if (paths.empty())
        throw runtime_error("empty search path");

    // parse the extension list
    vector<string> extensions;
    bal::split(extensions, sourceExtensionPriorityList, bal::is_any_of(";"));

    if (extensions.empty())
        throw runtime_error("empty source extension list");

    ITERATION_UPDATE(ilr, 0, 0, "opening database \"" + databaseName + "\"");

    // open the database
    idpDb.connect(idpDbFilepath, sqlite::no_mutex);

    ITERATION_UPDATE(ilr, 0, 0, "querying sources and spectra");

    // get a list of sources from the database
    sqlite::query sourceQuery(idpDb, "SELECT Id, Name, QuantitationMethod FROM SpectrumSource ORDER BY Id");
    BOOST_FOREACH(sqlite::query::rows row, sourceQuery)
    {
        sources.push_back(SpectrumSource());
        SpectrumSource& ss = sources.back();
        ss.id = row.get<sqlite3_int64>(0);
        ss.name = row.get<string>(1);
        ss.quantitationMethod = QuantitationMethod::get_by_index(row.get<int>(2)).get();
    }

    if (sources.empty())
        throw runtime_error("query returned no sources for \"" + databaseName + "\"");

    // use UnfilteredSpectrum table if present
    string spectrumTable = "UnfilteredSpectrum";
    try { sqlite::query(idpDb, "SELECT Id FROM UnfilteredSpectrum LIMIT 1").begin(); }
    catch (sqlite::database_error&) { spectrumTable = "Spectrum"; }

    // get a list of spectra for each source
    vector<SpectrumSource>::iterator itr = sources.begin();
    sqlite::query spectrumQuery(idpDb, ("SELECT Source, Id, NativeID FROM " + spectrumTable + " ORDER BY Source").c_str());
    BOOST_FOREACH(sqlite::query::rows row, spectrumQuery)
    {
        sqlite3_int64 sourceId = row.get<sqlite3_int64>(0);
        if (itr->id != sourceId)
        {
            if (itr->spectrumNativeIds.empty())
                throw runtime_error("query returned no spectra for source \"" + itr->name + "\"");
            ++itr;
        }
        itr->spectrumIds.push_back(row.get<sqlite3_int64>(1));
        itr->spectrumNativeIds.push_back(row.get<string>(2));
    }

    ITERATION_UPDATE(ilr, 0, 0, "searching for spectrum sources");

    // look for files for each source
    vector<string> missingSources;
    BOOST_FOREACH(SpectrumSource& source, sources)
    {
        vector<string> perSourcePaths(paths);
        string rootInputDirectory = bfs::path(idpDbFilepath).parent_path().string();
        BOOST_FOREACH(string& path, perSourcePaths)
            bal::replace_all(path, "<RootInputDirectory>", rootInputDirectory);

        source.filepath = findNameInPath(source.name, extensions, perSourcePaths);
        if (source.filepath.empty())
            missingSources.push_back(source.name);
    }

    if (missingSources.size() == 1)
        throw runtime_error("no filepath could be found corresponding to source \"" + missingSources[0] + "\"");
    else if (missingSources.size() > 1)
        throw runtime_error("no filepath could be found corresponding these sources:\n" + bal::join(missingSources, "\n"));
}

struct SpectrumList_Quantifier
{
    struct SpectrumRow
    {
        sqlite3_int64 id;
        double precursorMz;
        vector<double> reporterIonIntensities;
    };

    map<int, int> totalSpectraByMSLevel;
    map<int, double> totalIonCurrentByMSLevel;
    vector<SpectrumRow> spectrumQuantitationRows;

    SpectrumList_Quantifier(const SpectrumListPtr& sl, const IntegerSet& filteredIndexes,
                            const map<string, int>& rowIdByNativeID,
                            QuantitationMethod quantitationMethod)
        : rowIdByNativeID(rowIdByNativeID),
          quantitationMethod(quantitationMethod),
          tolerance(10, MZTolerance::PPM)
    {
        double iTRAQ_masses[8] = { 113.107873, 114.111228, 115.108263, 116.111618, 117.114973, 118.112008, 119.115363, 121.122072 };
        for (int i=1; i < 5; ++i) itraq4plexIons.push_back(iTRAQ_masses[i]);
        for (int i=0; i < 8; ++i) itraq8plexIons.push_back(iTRAQ_masses[i]);

        // the 127 and 129 ions come in two flavors
        double TMT_masses[8] = { 126.1277, 127.1248, 127.1316, 128.1344, 129.1314, 129.1383, 130.1411, 131.1382 };
        for (int i=0; i < 2; ++i) tmt2plexIons.push_back(TMT_masses[i]);
        for (int i=0; i < 8; ++i) tmt6plexIons.push_back(TMT_masses[i]);

        itraqReporterIonIntensities.resize(8);
        itraqTotalReporterIonIntensities.resize(8, 0);
        tmtReporterIonIntensities.resize(6);
        tmtTotalReporterIonIntensities.resize(6, 0);

        if (quantitationMethod == QuantitationMethod::None)
            return;

        for (size_t i=0, end=sl->size(); i < end; ++i)
        {
            SpectrumPtr s = sl->spectrum(i, true);

            int msLevel = s->cvParam(MS_ms_level).valueAs<int>();
            BinaryDataArrayPtr mz = s->getMZArray();
            BinaryDataArrayPtr intensities = s->getIntensityArray();
            if (msLevel == 0 || !mz.get() || !intensities.get())
                continue;

            double tic = s->cvParam(MS_TIC).valueAs<double>();
            if (tic == 0.0)
                tic = accumulate(intensities->data.begin(), intensities->data.end(), 0.0);
        
            ++totalSpectraByMSLevel[msLevel];
            totalIonCurrentByMSLevel[msLevel] += tic;

            if (msLevel > 1 && filteredIndexes.contains(i))
            {
                if (quantitationMethod != QuantitationMethod::None)
                {
                    switch (quantitationMethod.value())
                    {
                        case QuantitationMethod::ITRAQ4plex:
                            // skip the 113 ion only used in 8plex
                            findReporterIons(s->id, mz->data, intensities->data, itraq4plexIons, itraqReporterIonIntensities, itraqTotalReporterIonIntensities, 1);
                            break;

                        case QuantitationMethod::ITRAQ8plex:
                            findReporterIons(s->id, mz->data, intensities->data, itraq8plexIons, itraqReporterIonIntensities, itraqTotalReporterIonIntensities);
                            break;

                        case QuantitationMethod::TMT2plex:
                            findReporterIons(s->id, mz->data, intensities->data, tmt2plexIons, tmtReporterIonIntensities, tmtTotalReporterIonIntensities);
                            break;

                        case QuantitationMethod::TMT6plex:
                            findReporterIons(s->id, mz->data, intensities->data, tmt6plexIons, tmtReporterIonIntensities, tmtTotalReporterIonIntensities);
                            break;

                        default: break;
                    }
                }
            }
        }

        double itraq4plexIsotopeCorrectionFactors[4][4] =
        {
            // -2    -1     +1     +2
            { 0.000, 0.01, 0.059, 0.002 }, // 114 (icf[0])
            { 0.000, 0.02, 0.056, 0.001 }, // 115 (icf[1])
            { 0.000, 0.03, 0.045, 0.001 }, // 116 (icf[2])
            { 0.001, 0.04, 0.035, 0.001 }  // 117 (icf[3])
        };

        double itraq8plexIsotopeCorrectionFactors[8][4] =
        {
            //  -2      -1      +1      +2
            { 0.0000, 0.0000, 0.0689, 0.0024 }, // 113 (icf[0])
            { 0.0000, 0.0094, 0.0590, 0.0016 }, // 114 (icf[1])
            { 0.0000, 0.0188, 0.0490, 0.0010 }, // 115 (icf[2])
            { 0.0000, 0.0282, 0.0390, 0.0007 }, // 116 (icf[3])
            { 0.0006, 0.0377, 0.0288, 0.0000 }, // 117 (icf[4])
            { 0.0009, 0.0471, 0.0191, 0.0000 }, // 118 (icf[5])
            { 0.0014, 0.0566, 0.0087, 0.0000 }, // 119 (icf[6])
            { 0.0027, 0.0744, 0.0018, 0.0000 }  // 121 (icf[7])
        };
        //return;
        // normalize reporter ion intensities to the total for each channel
        if (quantitationMethod != QuantitationMethod::None)
        {
            switch (quantitationMethod.value())
            {
                case QuantitationMethod::ITRAQ4plex:
                {
                    normalizeReporterIons(itraqTotalReporterIonIntensities);
                    double (&icf)[4][4] = itraq4plexIsotopeCorrectionFactors;
                    for (size_t i=0; i < spectrumQuantitationRows.size(); ++i)
                    {
                        vector<double>& rii = spectrumQuantitationRows[i].reporterIonIntensities;
                        /*114*/ rii[1] = max(0.0, rii[1] - icf[1][1]*rii[2]);
                        /*115*/ rii[2] = max(0.0, rii[2] - (icf[0][2]*rii[1] + icf[2][1]*rii[3] + icf[3][0]*rii[4]));
                        /*116*/ rii[3] = max(0.0, rii[3] - (icf[0][3]*rii[1] + icf[1][2]*rii[2] + icf[3][1]*rii[4]));
                        /*117*/ rii[4] = max(0.0, rii[4] - (icf[1][3]*rii[2] + icf[2][2]*rii[3]));
                    }
                    break;
                }

                case QuantitationMethod::ITRAQ8plex:
                {
                    normalizeReporterIons(itraqTotalReporterIonIntensities);
                    double (&icf)[8][4] = itraq8plexIsotopeCorrectionFactors;
                    for (size_t i=0; i < spectrumQuantitationRows.size(); ++i)
                    {
                        vector<double>& rii = spectrumQuantitationRows[i].reporterIonIntensities;
                        /*113*/ rii[0] = max(0.0, rii[0] - icf[1][1]*rii[1]);
                        /*114*/ rii[1] = max(0.0, rii[1] - (icf[0][2]*rii[0] + icf[2][1]*rii[2]));
                        /*115*/ rii[2] = max(0.0, rii[2] - (icf[0][3]*rii[0] + icf[1][2]*rii[1] + icf[3][1]*rii[3] + icf[4][0]*rii[4]));
                        /*116*/ rii[3] = max(0.0, rii[3] - (icf[1][3]*rii[1] + icf[2][2]*rii[2] + icf[4][1]*rii[4] + icf[5][0]*rii[5]));
                        /*117*/ rii[4] = max(0.0, rii[4] - (icf[2][3]*rii[2] + icf[3][2]*rii[3] + icf[5][1]*rii[5] + icf[6][0]*rii[6]));
                        /*118*/ rii[5] = max(0.0, rii[5] - (icf[3][3]*rii[3] + icf[4][2]*rii[4] + icf[6][1]*rii[6]));
                        /*119*/ rii[6] = max(0.0, rii[6] - icf[5][2]*rii[5]);
                        /*121*/ //rii[7] -= icf[][1] *rii[1];
                    }
                    break;
                }

                case QuantitationMethod::TMT2plex:
                case QuantitationMethod::TMT6plex:
                    normalizeReporterIons(tmtTotalReporterIonIntensities);
                    break;

                default: break;
            }
        }
    }

    private:

    void findReporterIons(const string& nativeID, const vector<double>& mzArray, const vector<double>& intensityArray,
                          const vector<double>& reporterIonMZs, vector<double>& reporterIonIntensities,
                          vector<double>& totalReporterIonIntensities,
                          int offset = 0)
    {
        std::fill(reporterIonIntensities.begin(), reporterIonIntensities.end(), 0);

        vector<double>::const_iterator begin = reporterIonMZs.begin(), end = reporterIonMZs.end(), itr = begin;
        for (size_t i=0; i < mzArray.size(); ++i)
        {
            if (mzArray[i] + tolerance < *itr)
                continue;
            else if (mzArray[i] - tolerance > *itr)
            {
                ++itr;
                if (itr == end)
                    break;

                // if the next reporter ion is less than half a dalton away from the last one, re-use the same intensity bin
                if (*itr - *(itr-1) < 0.5)
                    --offset;

                --i;
            }
            else
            {
                // use the highest intensity in the tolerance window
                double& currentIonIntensity = reporterIonIntensities[(itr - begin) + offset];
                currentIonIntensity = max(intensityArray[i], currentIonIntensity);
            }
        }

        for (size_t i=0; i < reporterIonIntensities.size(); ++i)
            totalReporterIonIntensities[i] += reporterIonIntensities[i];

        map<string, int>::const_iterator findItr = rowIdByNativeID.find(nativeID);
        if (findItr == rowIdByNativeID.end())
            throw runtime_error("[findReporterIons] nativeID '" + nativeID + "' not found");

        spectrumQuantitationRows.push_back(SpectrumRow());
        spectrumQuantitationRows.back().id = findItr->second;
        spectrumQuantitationRows.back().precursorMz = 0; // TODO
        spectrumQuantitationRows.back().reporterIonIntensities = reporterIonIntensities;
    }

    void normalizeReporterIons(const vector<double>& totalReporterIonIntensities)
    {
        double maxChannelTotal = *std::max_element(totalReporterIonIntensities.begin(), totalReporterIonIntensities.end());
        vector<double> channelCorrectionFactors(totalReporterIonIntensities.size());
        for(size_t j=0; j < totalReporterIonIntensities.size(); ++j)
            if (totalReporterIonIntensities[j] > 0)
                channelCorrectionFactors[j] = maxChannelTotal / totalReporterIonIntensities[j];

        for (size_t i=0; i < spectrumQuantitationRows.size(); ++i)
            for(size_t j=0; j < channelCorrectionFactors.size(); ++j)
                spectrumQuantitationRows[i].reporterIonIntensities[j] *= channelCorrectionFactors[j];
    }

    const map<string, int>& rowIdByNativeID;    
    const QuantitationMethod quantitationMethod;
    MZTolerance tolerance;
    vector<double> itraq4plexIons, itraq8plexIons;
    vector<double> tmt2plexIons, tmt6plexIons;
    vector<double> itraqReporterIonIntensities, itraqTotalReporterIonIntensities;
    vector<double> tmtReporterIonIntensities, tmtTotalReporterIonIntensities;
};

struct SpectrumList_FilterPredicate_ScanStartTimeUpdater : public SpectrumList_Filter::Predicate
{
    SpectrumList_FilterPredicate_ScanStartTimeUpdater(sqlite::database& idpDb,
                                                      int sourceId,
                                                      const map<string, int>& rowIdByNativeID)
        : idpDb(idpDb),
          sourceId(sourceId),
          rowIdByNativeID(rowIdByNativeID),
          updateScanTime(idpDb, "UPDATE Spectrum SET ScanTimeInSeconds = ? WHERE Source = ? AND Id = ?")
    {
        try
        {
            sqlite::query(idpDb, "SELECT Id FROM UnfilteredSpectrum LIMIT 1").begin();
            updateUnfilteredScanTime.reset(new sqlite::command(idpDb, "UPDATE UnfilteredSpectrum SET ScanTimeInSeconds = ? WHERE Source = ? AND Id = ?"));
            hasUnfilteredTables = true;
        }
        catch (sqlite::database_error&)
        {
            hasUnfilteredTables = false;
        }
    }

    virtual boost::logic::tribool accept(const SpectrumIdentity& spectrumIdentity) const
    {
        return boost::logic::indeterminate;
    }

    virtual boost::logic::tribool accept(const Spectrum& spectrum) const
    {
        if (spectrum.scanList.scans.empty())
            return boost::logic::indeterminate;

        double scanTime = spectrum.scanList.scans[0].cvParam(MS_scan_start_time).timeInSeconds();

        map<string, int>::const_iterator findItr = rowIdByNativeID.find(spectrum.id);
        if (findItr == rowIdByNativeID.end())
            throw runtime_error("[findReporterIons] nativeID '" + spectrum.id + "' not found");

        updateScanTime.bind(1, scanTime);
        updateScanTime.bind(2, sourceId);
        updateScanTime.bind(3, findItr->second);
        updateScanTime.execute();
        updateScanTime.reset();

        if (hasUnfilteredTables)
        {
            updateUnfilteredScanTime->bind(1, scanTime);
            updateUnfilteredScanTime->bind(2, sourceId);
            updateUnfilteredScanTime->bind(3, findItr->second);
            updateUnfilteredScanTime->execute();
            updateUnfilteredScanTime->reset();
        }

        return true;
    }

    private:
    sqlite::database& idpDb;
    int sourceId;
    const map<string, int>& rowIdByNativeID;    
    mutable sqlite::command updateScanTime;
    mutable boost::scoped_ptr<sqlite::command> updateUnfilteredScanTime;
    bool hasUnfilteredTables;
};

} // namespace

void embed(const string& idpDbFilepath,
           const string& sourceSearchPath,
           const map<int, QuantitationMethod>& quantitationMethodBySource,
           pwiz::util::IterationListenerRegistry* ilr)
{
    embed(idpDbFilepath, sourceSearchPath, defaultSourceExtensionPriorityList, quantitationMethodBySource, ilr);
}

void embed(const string& idpDbFilepath,
           const string& sourceSearchPath,
           const string& sourceExtensionPriorityList,
           const map<int, QuantitationMethod>& quantitationMethodBySource,
           pwiz::util::IterationListenerRegistry* ilr)
{
    sqlite::database idpDb;

    // get a list of sources from the database
    vector<SpectrumSource> sources;
    try
    {
        getSources(idpDb, sources, idpDbFilepath, sourceSearchPath, sourceExtensionPriorityList, quantitationMethodBySource, ilr);
    }
    catch (runtime_error& e)
    {
        throw runtime_error(string("[embed] ") + e.what());
    }

    ExtendedReaderList readerList;

    for(size_t i=0; i < sources.size(); ++i)
    {
        SpectrumSource& source = sources[i];

        string sourceFilename = bfs::path(source.filepath).filename();

        ITERATION_UPDATE(ilr, i, sources.size(), "opening source \"" + sourceFilename + "\"");

        MSDataFile msd(source.filepath, &readerList);

        if (!msd.run.spectrumListPtr.get())
            throw runtime_error("[embed] null spectrum list in \"" + sourceFilename + "\"");

        ITERATION_UPDATE(ilr, i, sources.size(), "filtering spectra from \"" + sourceFilename + "\"");

        // create a filtered spectrum list
        IntegerSet filteredIndexes;
        map<string, int> rowIdByNativeID;
        const SpectrumList& sl = *msd.run.spectrumListPtr;
        for (size_t j=0; j < source.spectrumIds.size(); ++j)
        {
            const string& nativeID = source.spectrumNativeIds[j];
            size_t index = sl.find(nativeID);
            if (index == sl.size())
                throw runtime_error("[embed] nativeID '" + nativeID + "' not found in \"" + sourceFilename + "\"");
            filteredIndexes.insert((int) index);
            rowIdByNativeID[sl.spectrumIdentity(index).id] = source.spectrumIds[j];
        }
        
        sqlite::transaction transaction(idpDb);

        scoped_ptr<SpectrumList_Quantifier> slq;

        QuantitationMethod newQuantitationMethod;

        if (quantitationMethodBySource.count(source.id) > 0)
            newQuantitationMethod = quantitationMethodBySource.find(source.id)->second;
        else if (quantitationMethodBySource.count(0) > 0)
            newQuantitationMethod = quantitationMethodBySource.find(0)->second; // applies to all sources
        else
            newQuantitationMethod = QuantitationMethod::None;

        if (newQuantitationMethod != source.quantitationMethod)
        {
            ITERATION_UPDATE(ilr, i, sources.size(), "gathering quantitation data from \"" + sourceFilename + "\"");
            slq.reset(new SpectrumList_Quantifier(msd.run.spectrumListPtr, filteredIndexes, rowIdByNativeID, newQuantitationMethod));
        }

        ITERATION_UPDATE(ilr, i, sources.size(), "embedding scan times for \"" + sourceFilename + "\"");

        SpectrumList_FilterPredicate_IndexSet slfp(filteredIndexes);
        SpectrumList_FilterPredicate_ScanStartTimeUpdater slstu(idpDb, source.id, rowIdByNativeID);
        SpectrumDataFilterPtr sdf(new ThresholdFilter(ThresholdFilter::ThresholdingBy_Count, 150.));
        msd.run.spectrumListPtr.reset(new SpectrumList_Filter(msd.run.spectrumListPtr, slfp));
        msd.run.spectrumListPtr.reset(new SpectrumList_Filter(msd.run.spectrumListPtr, slstu));
        msd.run.spectrumListPtr.reset(new SpectrumList_PeakFilter(msd.run.spectrumListPtr, sdf));

        ITERATION_UPDATE(ilr, i, sources.size(), "creating subset spectra of \"" + sourceFilename + "\"");

        // write a subset mz5 file
        string tmpFilepath = bfs::unique_path("%%%%%%%%.mz5").string();
        MSDataFile::WriteConfig config(MSDataFile::Format_MZ5);
        config.binaryDataEncoderConfig.precisionOverrides[pwiz::cv::MS_intensity_array] = BinaryDataEncoder::Precision_32;
        config.binaryDataEncoderConfig.compression = BinaryDataEncoder::Compression_Zlib;
        msd.write(tmpFilepath, config);

        // read entire file into memory
        ifstream tmpFile(tmpFilepath.c_str(), ios::binary|ios::ate);
        if (!tmpFile)
            throw runtime_error("[embed] error opening temporary file at \"" + tmpFilepath + "\"");

        streamsize tmpSize = tmpFile.tellg();
        char* tmpBuffer = new char[tmpSize];
        tmpFile.seekg(0, ios::beg);
        tmpFile.read(tmpBuffer, tmpSize);
        tmpFile.close();
        bfs::remove(tmpFilepath);

        ITERATION_UPDATE(ilr, i, sources.size(), "embedding subset spectra for \"" + source.name + "\"");

        // embed the file as a blob in the database
        sqlite::command cmd(idpDb, "UPDATE SpectrumSourceMetadata SET MsDataBytes = ? WHERE Id = ?");
        cmd.bind(1, static_cast<void*>(tmpBuffer), tmpSize);
        cmd.bind(2, source.id);
        cmd.execute();
        cmd.reset();

        try { idpDb.execute("DELETE FROM SpectrumQuantitation WHERE Id IN (SELECT Id FROM UnfilteredSpectrum WHERE Source=" + lexical_cast<string>(source.id) + ")"); }
        catch (sqlite::database_error&) { idpDb.execute("DELETE FROM SpectrumQuantitation WHERE Id IN (SELECT Id FROM Spectrum WHERE Source=" + lexical_cast<string>(source.id) + ")"); }
        
        if (newQuantitationMethod == source.quantitationMethod)
        {
            transaction.commit();
            continue;
        }

        if (newQuantitationMethod != QuantitationMethod::None)
        {
            ITERATION_UPDATE(ilr, i, sources.size(), "adding spectrum quantitation data for \"" + source.name + "\"");

            scoped_ptr<sqlite::command> insertSpectrumQuantitation;
            if (newQuantitationMethod == QuantitationMethod::ITRAQ4plex ||
                newQuantitationMethod == QuantitationMethod::ITRAQ8plex)
                insertSpectrumQuantitation.reset(new sqlite::command(idpDb, "INSERT INTO SpectrumQuantitation (Id, iTRAQ_ReporterIonIntensities) VALUES (?,?)"));
            else if (newQuantitationMethod == QuantitationMethod::TMT2plex ||
                     newQuantitationMethod == QuantitationMethod::TMT6plex)
                insertSpectrumQuantitation.reset(new sqlite::command(idpDb, "INSERT INTO SpectrumQuantitation (Id, TMT_ReporterIonIntensities) VALUES (?,?)"));

            for (size_t i=0; i < slq->spectrumQuantitationRows.size(); ++i)
            {
                SpectrumList_Quantifier::SpectrumRow& row = slq->spectrumQuantitationRows[i];
                insertSpectrumQuantitation->bind(1, row.id);
                insertSpectrumQuantitation->bind(2, static_cast<void*>(&row.reporterIonIntensities[0]), row.reporterIonIntensities.size() * sizeof(double));
                insertSpectrumQuantitation->execute();
                insertSpectrumQuantitation->reset();
            }
        }

        // populate the source statistics
        sqlite::command cmd2(idpDb, "UPDATE SpectrumSource SET "
                                    "TotalSpectraMS1 = ?, TotalIonCurrentMS1 = ?, "
                                    "TotalSpectraMS2 = ?, TotalIonCurrentMS2 = ?, "
                                    "QuantitationMethod = ? "
                                    "WHERE Id = ?");
        cmd2.binder() << slq->totalSpectraByMSLevel[1] << slq->totalIonCurrentByMSLevel[1] <<
                         slq->totalSpectraByMSLevel[2] << slq->totalIonCurrentByMSLevel[2] <<
                         newQuantitationMethod.value() <<
                         source.id;
        cmd2.execute();
        cmd2.reset();

        transaction.commit();
    }
}


void embedScanTime(const string& idpDbFilepath,
                   const string& sourceSearchPath,
                   const map<int, QuantitationMethod>& quantitationMethodBySource,
                   pwiz::util::IterationListenerRegistry* ilr)
{
    embedScanTime(idpDbFilepath, sourceSearchPath, defaultSourceExtensionPriorityList, quantitationMethodBySource, ilr);
}

void embedScanTime(const string& idpDbFilepath,
                   const string& sourceSearchPath,
                   const string& sourceExtensionPriorityList,
                   const map<int, QuantitationMethod>& quantitationMethodBySource,
                   pwiz::util::IterationListenerRegistry* ilr)
{
    sqlite::database idpDb;

    // get a list of sources from the database
    vector<SpectrumSource> sources;
    try
    {
        getSources(idpDb, sources, idpDbFilepath, sourceSearchPath, sourceExtensionPriorityList, quantitationMethodBySource, ilr);
    }
    catch (runtime_error& e)
    {
        throw runtime_error(string("[embedScanTime] ") + e.what());
    }

    ExtendedReaderList readerList;

    for(size_t i=0; i < sources.size(); ++i)
    {
        SpectrumSource& source = sources[i];

        string sourceFilename = bfs::path(source.filepath).filename();

        ITERATION_UPDATE(ilr, i, sources.size(), "opening source \"" + sourceFilename + "\"");

        MSDataFile msd(source.filepath, &readerList);

        if (!msd.run.spectrumListPtr.get())
            throw runtime_error("[embedScanTime] null spectrum list in \"" + sourceFilename + "\"");

        ITERATION_UPDATE(ilr, i, sources.size(), "filtering spectra from \"" + sourceFilename + "\"");

        // create a filtered spectrum list
        IntegerSet filteredIndexes;
        map<string, int> rowIdByNativeID;
        const SpectrumList& sl = *msd.run.spectrumListPtr;
        for (size_t j=0; j < source.spectrumIds.size(); ++j)
        {
            const string& nativeID = source.spectrumNativeIds[j];
            size_t index = sl.find(nativeID);
            if (index == sl.size())
                throw runtime_error("[embed] nativeID '" + nativeID + "' not found in \"" + sourceFilename + "\"");
            filteredIndexes.insert((int) index);
            rowIdByNativeID[sl.spectrumIdentity(index).id] = source.spectrumIds[j];
        }

        sqlite::transaction transaction(idpDb);

        scoped_ptr<SpectrumList_Quantifier> slq;

        QuantitationMethod newQuantitationMethod;
        
        if (quantitationMethodBySource.count(source.id) > 0)
            newQuantitationMethod = quantitationMethodBySource.find(source.id)->second;
        else if (quantitationMethodBySource.count(0) > 0)
            newQuantitationMethod = quantitationMethodBySource.find(0)->second; // applies to all sources
        else
            newQuantitationMethod = QuantitationMethod::None;

        if (newQuantitationMethod != source.quantitationMethod)
        {
            ITERATION_UPDATE(ilr, i, sources.size(), "gathering quantitation data from \"" + sourceFilename + "\"");
            slq.reset(new SpectrumList_Quantifier(msd.run.spectrumListPtr, filteredIndexes, rowIdByNativeID, newQuantitationMethod));
        }

        ITERATION_UPDATE(ilr, i, sources.size(), "embedding scan times for \"" + sourceFilename + "\"");

        SpectrumList_FilterPredicate_IndexSet slfp(filteredIndexes);
        SpectrumList_FilterPredicate_ScanStartTimeUpdater slstu(idpDb, source.id, rowIdByNativeID);
        msd.run.spectrumListPtr.reset(new SpectrumList_Filter(msd.run.spectrumListPtr, slfp));
        msd.run.spectrumListPtr.reset(new SpectrumList_Filter(msd.run.spectrumListPtr, slstu));

        try { idpDb.execute("DELETE FROM SpectrumQuantitation WHERE Id IN (SELECT Id FROM UnfilteredSpectrum WHERE Source=" + lexical_cast<string>(source.id) + ")"); }
        catch (sqlite::database_error&) { idpDb.execute("DELETE FROM SpectrumQuantitation WHERE Id IN (SELECT Id FROM Spectrum WHERE Source=" + lexical_cast<string>(source.id) + ")"); }

        if (newQuantitationMethod == source.quantitationMethod)
            continue;

        if (newQuantitationMethod != QuantitationMethod::None)
        {
            ITERATION_UPDATE(ilr, i, sources.size(), "adding spectrum quantitation data for \"" + source.name + "\"");

            scoped_ptr<sqlite::command> insertSpectrumQuantitation;
            if (newQuantitationMethod == QuantitationMethod::ITRAQ4plex ||
                newQuantitationMethod == QuantitationMethod::ITRAQ8plex)
                insertSpectrumQuantitation.reset(new sqlite::command(idpDb, "INSERT INTO SpectrumQuantitation (Id, iTRAQ_ReporterIonIntensities) VALUES (?,?)"));
            else if (newQuantitationMethod == QuantitationMethod::TMT2plex ||
                     newQuantitationMethod == QuantitationMethod::TMT6plex)
                insertSpectrumQuantitation.reset(new sqlite::command(idpDb, "INSERT INTO SpectrumQuantitation (Id, TMT_ReporterIonIntensities) VALUES (?,?)"));

            for (size_t i=0; i < slq->spectrumQuantitationRows.size(); ++i)
            {
                SpectrumList_Quantifier::SpectrumRow& row = slq->spectrumQuantitationRows[i];
                insertSpectrumQuantitation->bind(1, row.id);
                insertSpectrumQuantitation->bind(2, static_cast<void*>(&row.reporterIonIntensities[0]), row.reporterIonIntensities.size() * sizeof(double));
                insertSpectrumQuantitation->execute();
                insertSpectrumQuantitation->reset();
            }
        }

        // populate the source statistics
        sqlite::command cmd(idpDb, "UPDATE SpectrumSource SET "
                                    "TotalSpectraMS1 = ?, TotalIonCurrentMS1 = ?, "
                                    "TotalSpectraMS2 = ?, TotalIonCurrentMS2 = ?, "
                                    "QuantitationMethod = ? "
                                    "WHERE Id = ?");
        cmd.binder() << slq->totalSpectraByMSLevel[1] << slq->totalIonCurrentByMSLevel[1] <<
                        slq->totalSpectraByMSLevel[2] << slq->totalIonCurrentByMSLevel[2] <<
                        newQuantitationMethod.value() <<
                        source.id;
        cmd.execute();
        cmd.reset();

        transaction.commit();
    }
}


void extract(const string& idpDbFilepath, const string& sourceName, const string& outputFilepath)
{
    // open the database
    sqlite::database idpDb(idpDbFilepath, sqlite::no_mutex);

    // write the associated MSDataBytes to the given filepath
    sqlite::query blobQuery(idpDb, ("SELECT ssmd.MsDataBytes FROM SpectrumSource ss JOIN SpectrumSourceMetadata ssmd ON ss.Id=ssmd.Id AND Name = \"" + sourceName + "\"").c_str());
    BOOST_FOREACH(sqlite::query::rows row, blobQuery)
    {
        const char* bytes = static_cast<const char*>(row.get<const void*>(0));
        int numBytes = row.column_bytes(0);
        ofstream os(outputFilepath.c_str(), ios::binary);
        os.write(bytes, numBytes);
        return;
    }

    throw runtime_error("[extract] source \"" + sourceName + "\" not found in \"" + idpDbFilepath + "\"");
}


void embedGeneMetadata(const string& idpDbFilepath, pwiz::util::IterationListenerRegistry* ilr)
{
    if (!bfs::exists("gene2protein.db3"))
        throw runtime_error("[loadGeneMetadata] gene2protein.db3 not found: download it from http://fenchurch.mc.vanderbilt.edu/bin/g2p/gene2protein.db3 and put it in the IDPicker directory.");

    typedef boost::tuple<string, string, string, boost::optional<string>, boost::optional<string> > GeneTuple;
    typedef map<string, GeneTuple> ProteinToGeneMap;
    ProteinToGeneMap proteinToGeneMap;
    {
        sqlite::database g2pDb("gene2protein.db3");
        sqlite::query proteinToGeneQuery(g2pDb, "SELECT ProteinAccession, ApprovedId, ApprovedName, Chromosome, GeneFamily, GeneDescription FROM GeneToProtein");
        BOOST_FOREACH(sqlite::query::rows row, proteinToGeneQuery)
        {
            string geneId, geneName, chromosome, geneFamily, geneDescription;
            boost::tie(geneId, geneName, chromosome, geneFamily, geneDescription) = row.get_columns<string, string, string, string, string>(1, 2, 3, 4, 5);
            proteinToGeneMap[row.get<string>(0)] = boost::make_tuple(geneId, geneName, chromosome,
                                                                     boost::make_optional(!geneFamily.empty(), geneFamily),
                                                                     boost::make_optional(!geneDescription.empty(), geneDescription));
        }
    }

    // open the database
    sqlite::database idpDb(idpDbFilepath, sqlite::no_mutex);
    
    idpDb.execute("PRAGMA journal_mode=OFF; PRAGMA synchronous=OFF");

    // drop filtered tables
    Qonverter q;
    q.dropFilters(idpDb.connected());

    // reset GeneId column
    idpDb.execute("UPDATE Protein SET GeneId=NULL");

    using namespace boost::xpressive;

    sqlite::transaction transaction(idpDb);
    sqlite::query proteinIdAccessions(idpDb, "SELECT Id, Accession FROM Protein WHERE IsDecoy=0");
    sqlite::command proteinQuery(idpDb, "UPDATE Protein SET GeneId=? WHERE Id=?");
    sqlite::command proteinMetadataQuery(idpDb, "UPDATE ProteinMetadata SET TaxonomyId=9606, GeneName=?, Chromosome=?, GeneFamily=?, GeneDescription=? WHERE Id=?");
    sregex refseqRegex = sregex::compile("^(?:gi\\|\\d+\\|ref\\|)?(\\S+?)(?:\\|)?$");
    BOOST_FOREACH(sqlite::query::rows row, proteinIdAccessions)
    {
        sqlite3_int64 id = row.get<sqlite3_int64>(0);
        string accession = regex_replace(row.get<string>(1), refseqRegex, "$1");

        ProteinToGeneMap::const_iterator findItr = proteinToGeneMap.find(accession);
        if (findItr == proteinToGeneMap.end())
            continue;

        const GeneTuple& geneTuple = findItr->second;

        proteinQuery.binder() << geneTuple.get<0>() << id;
        proteinQuery.step();
        proteinQuery.reset();

        if (geneTuple.get<3>().is_initialized())
            proteinMetadataQuery.binder() << geneTuple.get<1>() << geneTuple.get<2>() << geneTuple.get<3>().get() << geneTuple.get<4>().get() << id;
        else
            proteinMetadataQuery.binder() << geneTuple.get<1>() << geneTuple.get<2>() << sqlite::ignore << sqlite::ignore << id;
        proteinMetadataQuery.step();
        proteinMetadataQuery.reset();
    }
    idpDb.execute("UPDATE Protein SET GeneId='Unmapped_'||Accession WHERE GeneId IS NULL");
    //idpDb.execute("UPDATE Protein SET GeneId='Unmapped' WHERE GeneId IS NULL");
    transaction.commit();
}


} // namespace Embedder
END_IDPICKER_NAMESPACE
