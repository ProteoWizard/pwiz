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
#include "../Lib/SQLite/sqlite3pp.h"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/DateTime.hpp"
#include "pwiz/data/msdata/MSDataFile.hpp"
#include "pwiz/data/vendor_readers/ExtendedReaderList.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_Filter.hpp"
#include "pwiz/analysis/spectrum_processing/ThresholdFilter.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_PeakFilter.hpp"
#include "boost/foreach_field.hpp"
#include "boost/throw_exception.hpp"

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
    vector<string> spectrumNativeIds;

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
    sqlite::query sourceQuery(idpDb, "SELECT Id, Name FROM SpectrumSource ORDER BY Id");
    BOOST_FOREACH(sqlite::query::rows row, sourceQuery)
    {
        sources.push_back(SpectrumSource());
        sources.back().id = row.get<sqlite3_int64>(0);
        sources.back().name = row.get<string>(1);
    }

    if (sources.empty())
        throw runtime_error("query returned no sources for \"" + databaseName + "\"");

    // use UnfilteredSpectrum table if present
    string spectrumTable = "UnfilteredSpectrum";
    try { sqlite::query(idpDb, "SELECT Id FROM UnfilteredSpectrum LIMIT 1").begin(); }
    catch (sqlite::database_error&) { spectrumTable = "Spectrum"; }

    // get a list of spectra for each source
    vector<SpectrumSource>::iterator itr = sources.begin();
    sqlite::query spectrumQuery(idpDb, ("SELECT Source, NativeID FROM " + spectrumTable + " ORDER BY Source").c_str());
    BOOST_FOREACH(sqlite::query::rows row, spectrumQuery)
    {
        sqlite3_int64 sourceId = row.get<sqlite3_int64>(0);
        if (itr->id != sourceId)
        {
            if (itr->spectrumNativeIds.empty())
                throw runtime_error("query returned no spectra for source \"" + itr->name + "\"");
            ++itr;
        }
        itr->spectrumNativeIds.push_back(row.get<string>(1));
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

struct SpectrumList_FilterPredicate_ScanStartTimeUpdater : public SpectrumList_Filter::Predicate
{
    SpectrumList_FilterPredicate_ScanStartTimeUpdater(sqlite::database& idpDb, int sourceId)
        : idpDb(idpDb),
          sourceId(sourceId),
          updateScanTime(idpDb, "UPDATE Spectrum SET ScanTimeInSeconds = ? WHERE Source = ? AND NativeID = ?")
    {
        try
        {
            sqlite::query(idpDb, "SELECT Id FROM UnfilteredSpectrum LIMIT 1").begin();
            updateUnfilteredScanTime.reset(new sqlite::command(idpDb, "UPDATE UnfilteredSpectrum SET ScanTimeInSeconds = ? WHERE SOURCE = ? AND NativeID = ?"));
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

        updateScanTime.bind(1, scanTime);
        updateScanTime.bind(2, sourceId);
        updateScanTime.bind(3, spectrum.id);
        updateScanTime.execute();
        updateScanTime.reset();

        if (hasUnfilteredTables)
        {
            updateUnfilteredScanTime->bind(1, scanTime);
            updateUnfilteredScanTime->bind(2, sourceId);
            updateUnfilteredScanTime->bind(3, spectrum.id);
            updateUnfilteredScanTime->execute();
            updateUnfilteredScanTime->reset();
        }

        return true;
    }

    private:
    sqlite::database& idpDb;
    int sourceId;
    mutable sqlite::command updateScanTime;
    mutable boost::scoped_ptr<sqlite::command> updateUnfilteredScanTime;
    bool hasUnfilteredTables;
};

} // namespace

void embed(const string& idpDbFilepath, const string& sourceSearchPath, pwiz::util::IterationListenerRegistry* ilr)
{
    embed(idpDbFilepath, sourceSearchPath, defaultSourceExtensionPriorityList, ilr);
}

void embed(const string& idpDbFilepath,
           const string& sourceSearchPath,
           const string& sourceExtensionPriorityList,
           pwiz::util::IterationListenerRegistry* ilr)
{
    sqlite::database idpDb;

    // get a list of sources from the database
    vector<SpectrumSource> sources;
    try
    {
        getSources(idpDb, sources, idpDbFilepath, sourceSearchPath, sourceExtensionPriorityList, ilr);
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
        const SpectrumList& sl = *msd.run.spectrumListPtr;
        BOOST_FOREACH(const string& nativeID, source.spectrumNativeIds)
        {
            size_t index = sl.find(nativeID);
            if (index == sl.size())
                throw runtime_error("[embed] nativeID '" + nativeID + "' not found in \"" + sourceFilename + "\"");
            filteredIndexes.insert((int) index);
        }
        
        sqlite::transaction transaction(idpDb);

        SpectrumList_FilterPredicate_IndexSet slfp(filteredIndexes);
        SpectrumList_FilterPredicate_ScanStartTimeUpdater slstu(idpDb, source.id);
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
        sqlite::command cmd(idpDb, "UPDATE SpectrumSource SET MSDataBytes = ? WHERE Id = ?");
        cmd.bind(1, static_cast<void*>(tmpBuffer), tmpSize);
        cmd.bind(2, source.id);
        cmd.execute();
        cmd.reset();
        transaction.commit();
    }
}


void embedScanTime(const string& idpDbFilepath, const string& sourceSearchPath, pwiz::util::IterationListenerRegistry* ilr)
{
    embedScanTime(idpDbFilepath, sourceSearchPath, defaultSourceExtensionPriorityList, ilr);
}

void embedScanTime(const string& idpDbFilepath,
                   const string& sourceSearchPath,
                   const string& sourceExtensionPriorityList,
                   pwiz::util::IterationListenerRegistry* ilr)
{
    sqlite::database idpDb;

    // get a list of sources from the database
    vector<SpectrumSource> sources;
    try
    {
        getSources(idpDb, sources, idpDbFilepath, sourceSearchPath, sourceExtensionPriorityList, ilr);
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
        const SpectrumList& sl = *msd.run.spectrumListPtr;
        BOOST_FOREACH(const string& nativeID, source.spectrumNativeIds)
        {
            size_t index = sl.find(nativeID);
            if (index == sl.size())
                throw runtime_error("[embedScanTime] nativeID '" + nativeID + "' not found in \"" + sourceFilename + "\"");
            filteredIndexes.insert((int) index);
        }
        
        sqlite::transaction transaction(idpDb);

        SpectrumList_FilterPredicate_IndexSet slfp(filteredIndexes);
        SpectrumList_FilterPredicate_ScanStartTimeUpdater slstu(idpDb, source.id);
        msd.run.spectrumListPtr.reset(new SpectrumList_Filter(msd.run.spectrumListPtr, slfp));
        msd.run.spectrumListPtr.reset(new SpectrumList_Filter(msd.run.spectrumListPtr, slstu));

        transaction.commit();
    }
}


void extract(const string& idpDbFilepath, const string& sourceName, const string& outputFilepath)
{
    // open the database
    sqlite::database idpDb(idpDbFilepath, sqlite::no_mutex);

    // write the associated MSDataBytes to the given filepath
    sqlite::query blobQuery(idpDb, ("SELECT MSDataBytes FROM SpectrumSource WHERE Name = \"" + sourceName + "\"").c_str());
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


} // namespace Embedder
END_IDPICKER_NAMESPACE
