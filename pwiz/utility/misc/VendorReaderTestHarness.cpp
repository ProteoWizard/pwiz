//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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


#include "VendorReaderTestHarness.hpp"
#include "pwiz/data/msdata/TextWriter.hpp"
#include "pwiz/data/msdata/MSDataFile.hpp"
#ifndef WITHOUT_MZ5
#include "pwiz/data/msdata/Serializer_mz5.hpp"
#endif
#include "pwiz/data/msdata/Serializer_mzXML.hpp"
#include "pwiz/data/msdata/Serializer_MGF.hpp"
#include "pwiz/data/msdata/Diff.hpp"
#include "pwiz/data/msdata/SpectrumListBase.hpp"
#include "pwiz/data/msdata/ChromatogramListBase.hpp"
#include "pwiz/data/msdata/SpectrumListWrapper.hpp"
#include "pwiz/data/msdata/Version.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumListFactory.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/SHA1Calculator.hpp"
#include "boost/thread/thread.hpp"
#include "boost/thread/barrier.hpp"


using namespace pwiz::util;
using namespace pwiz::data::diff_impl;
using namespace pwiz::msdata;


ostream* os_ = 0;


namespace pwiz {
namespace util {


namespace {

void testAccept(const Reader& reader, const string& rawpath)
{
    if (os_) *os_ << "testAccept(): " << rawpath << endl;

    bool accepted = reader.accept(rawpath, pwiz::util::read_file_header(rawpath, 512));
    if (os_) *os_ << "accepted: " << boolalpha << accepted << endl;

    unit_assert(accepted);
}


void mangleSourceFileLocations(const string& sourceName, vector<SourceFilePtr>& sourceFiles, const string& newSourceName = "")
{
    // mangling the absolute paths is necessary for the test to work from any path
    BOOST_FOREACH(SourceFilePtr& sourceFilePtr, sourceFiles)
    {
        // if the sourceName or newSourceName is in the location, preserve it (erase everything preceding it)
        size_t sourceNameInLocation = newSourceName.empty() ? sourceFilePtr->location.find(sourceName) : min(sourceFilePtr->location.find(sourceName), sourceFilePtr->location.find(newSourceName));
        if (sourceNameInLocation != string::npos)
        {
            sourceFilePtr->location.erase(0, sourceNameInLocation);
            sourceFilePtr->location = "file:///" + newSourceName.empty() ? sourceName : newSourceName;
        }
        else
            sourceFilePtr->location = "file:///";

        if (!newSourceName.empty())
        {
            if (!bal::contains(sourceFilePtr->id, newSourceName))
                bal::replace_all(sourceFilePtr->id, sourceName, newSourceName);
            if (!bal::contains(sourceFilePtr->name, newSourceName))
                bal::replace_all(sourceFilePtr->name, sourceName, newSourceName);
        }
    }
}


void manglePwizSoftware(MSData& msd)
{
    // a pwiz version change isn't worth regenerating the test data
    vector<size_t> oldPwizSoftwarePtrs;
    SoftwarePtr pwizSoftware;
    for (size_t i=0; i < msd.softwarePtrs.size(); ++i)
        if (msd.softwarePtrs[i]->hasCVParam(MS_pwiz))
        {
            if (msd.softwarePtrs[i]->version != pwiz::msdata::Version::str())
                oldPwizSoftwarePtrs.push_back(i);
            else
                pwizSoftware = msd.softwarePtrs[i];
        }

    pwizSoftware->id = "current pwiz";

    msd.dataProcessingPtrs = msd.allDataProcessingPtrs();
    msd.dataProcessingPtrs.resize(1);

    SpectrumListBase* sl = dynamic_cast<SpectrumListBase*>(msd.run.spectrumListPtr.get());
    ChromatogramListBase* cl = dynamic_cast<ChromatogramListBase*>(msd.run.chromatogramListPtr.get());
    if (sl && !msd.dataProcessingPtrs.empty()) sl->setDataProcessingPtr(msd.dataProcessingPtrs[0]);
    if (cl && !msd.dataProcessingPtrs.empty()) cl->setDataProcessingPtr(msd.dataProcessingPtrs[0]);

    for (DataProcessingPtr& dp : msd.dataProcessingPtrs)
        for (ProcessingMethod& pm : dp->processingMethods)
            pm.softwarePtr = pwizSoftware;

    for (vector<size_t>::reverse_iterator itr = oldPwizSoftwarePtrs.rbegin();
         itr != oldPwizSoftwarePtrs.rend();
         ++itr)
         msd.softwarePtrs.erase(msd.softwarePtrs.begin()+(*itr));
}


void calculateSourceFileChecksums(vector<SourceFilePtr>& sourceFiles)
{
    const string uriPrefix = "file://";
    bfs::detail::utf8_codecvt_facet utf8;
    BOOST_FOREACH(const SourceFilePtr& sourceFile, sourceFiles)
    {
        if (!bal::istarts_with(sourceFile->location, uriPrefix)) return;
        string location = sourceFile->location.substr(uriPrefix.size());
        bal::trim_if(location, bal::is_any_of("/"));
        bfs::path p(location, utf8);
        p /= bfs::path(sourceFile->name, utf8);

        string sha1 = SHA1Calculator::hashFile(p.string(utf8));
        sourceFile->set(MS_SHA_1, sha1);
    }
}


void hackInMemoryMSData(const string& sourceName, MSData& msd, const ReaderTestConfig& config, const string& newSourceName = "")
{
    // remove metadata ptrs appended on read
    vector<SourceFilePtr>& sfs = msd.fileDescription.sourceFilePtrs;
    if (!sfs.empty()) sfs.erase(sfs.end()-1);

    mangleSourceFileLocations(sourceName, sfs, newSourceName);

    // if given a new source name, use it for the run id
    if (!newSourceName.empty())
    {
        bal::replace_all(msd.id, sourceName, newSourceName);
        bal::replace_all(msd.run.id, sourceName, newSourceName);
    }

    if (config.peakPickingCWT)
        pwiz::analysis::SpectrumListFactory::wrap(msd, "peakPicking cwt msLevel=1-");

    if (config.thresholdCount > 0)
        pwiz::analysis::SpectrumListFactory::wrap(msd, "threshold count " + lexical_cast<string>(config.thresholdCount) + " most-intense");

    manglePwizSoftware(msd);

    // set current DataProcessing to the original conversion
    // NOTE: this only works for vendor readers that use a single dataProcessing element
    SpectrumListBase* sl = dynamic_cast<SpectrumListBase*>(msd.run.spectrumListPtr.get());
    ChromatogramListBase* cl = dynamic_cast<ChromatogramListBase*>(msd.run.chromatogramListPtr.get());
    if (sl) sl->setDataProcessingPtr(msd.dataProcessingPtrs[0]);
    if (cl) cl->setDataProcessingPtr(msd.dataProcessingPtrs[0]);
}


template<typename DiffType>
string headDiff(const DiffType& diff, size_t maxLength)
{
    stringstream diffStream;
    diffStream << diff;
    string diffString = diffStream.str();
    if (diffString.length() > maxLength)
        return diffString.substr(0, maxLength) + "\n...snip...\n";
    return diffString;
}


string headStream(istream& is, size_t maxLength)
{
    is.clear();
    is.seekg(0);
    string buf(maxLength, '\0');
    size_t bytesRead = is.readsome(&buf[0], maxLength);
    buf.resize(bytesRead);
    if (bytesRead > maxLength)
        return buf + "\n...snip...\n";
    return buf;
}


// filters out non-MSn spectra, MS1 spectra, and filters the metadata from MSn spectra
class SpectrumList_MGF_Filter : public SpectrumListWrapper
{
    vector<size_t> msnIndex;

    public:

    SpectrumList_MGF_Filter(const SpectrumListPtr& inner) : SpectrumListWrapper(inner)
    {
        for (size_t index=0; index < inner->size(); ++index)
        {
            SpectrumPtr s = inner->spectrum(index);
            string msLevel = s->cvParam(MS_ms_level).value;
            if (!msLevel.empty() && msLevel != "1" &&
                !s->precursors.empty() && !s->precursors[0].selectedIons.empty())
                msnIndex.push_back(index);
        }
    }

    virtual size_t size() const {return msnIndex.size();}
    virtual bool empty() const {return msnIndex.empty();}
    virtual const SpectrumIdentity& spectrumIdentity(size_t index) const {return inner_->spectrumIdentity(msnIndex[index]);}
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData = false) const
    {
        SpectrumPtr result = inner_->spectrum(msnIndex[index], getBinaryData);

        // replace profile term with centroid (MGF is always considered to be centroid)
        vector<CVParam>& cvParams = result->cvParams;
        vector<CVParam>::iterator itr = std::find(cvParams.begin(), cvParams.end(), MS_profile_spectrum);
        if (itr != cvParams.end())
        {
            *itr = MS_centroid_spectrum;

            if (result->getMZArray() && result->getIntensityArray())
            {
                // take only the first 100 points (100k points in MGF is not fun)
                BinaryData<double>& mzArray = result->getMZArray()->data;
                BinaryData<double>& intensityArray = result->getIntensityArray()->data;
                if (result->defaultArrayLength > 100)
                {
                    result->defaultArrayLength = 100;
                    mzArray.resize(100);
                    intensityArray.resize(100);
                }
            }
        }

        // MGF only supports 1 precursor
        if (result->precursors.size() > 1)
            result->precursors.resize(1);

        return result;
    }
};


void testRead(const Reader& reader, const string& rawpath, const bfs::path& parentPath, bool requireUnicodeSupport, const ReaderTestConfig& config)
{
    if (os_) *os_ << "testRead(): " << rawpath << endl;

    Reader::Config readerConfig(config);
    readerConfig.adjustUnknownTimeZonesToHostTimeZone = false; // do not adjust times, because we don't want the test to depend on the time zone of the test agent

    DiffConfig diffConfig;
    if (config.diffPrecision)
        diffConfig.precision = config.diffPrecision.get();

    // read file into MSData object
    vector<MSDataPtr> msds;
    string rawheader = pwiz::util::read_file_header(rawpath, 512);
    reader.read(rawpath, rawheader, msds, readerConfig);

    string sourceName = BFS_STRING(bfs::path(rawpath).filename());

    auto runRange = config.runIndex ? make_pair(config.runIndex.get(), config.runIndex.get()+1) : make_pair(0, (int) msds.size());
    for (auto runItr = runRange; runItr.first < runItr.second; ++runItr.first)
    {
        MSData& msd = *msds[runItr.first];
        if (os_) (*os_) << "MzML serialization test of " << config.resultFilename(msd.run.id + ".mzML") << endl;

        calculateSourceFileChecksums(msd.fileDescription.sourceFilePtrs);
        mangleSourceFileLocations(sourceName, msd.fileDescription.sourceFilePtrs);
        config.wrap(msd);
        manglePwizSoftware(msd);

        if (os_) TextWriter(*os_,0)(msd);

        bfs::path targetResultFilename = parentPath / config.resultFilename(msd.run.id + ".mzML");
        MSDataFile targetResult(targetResultFilename.string());
        hackInMemoryMSData(sourceName, targetResult, config);

        // test for 1:1 equality with the target mzML
        Diff<MSData, DiffConfig> diff(msd, targetResult, diffConfig);
        if (diff) cerr << headDiff(diff, 5000) << endl;
        unit_assert(!diff);

        // test ion mobility conversion
        auto imsl = boost::dynamic_pointer_cast<SpectrumListIonMobilityBase>(msd.run.spectrumListPtr);
        if (imsl != nullptr && imsl->canConvertIonMobilityAndCCS())
        {
            double imTestValue = 0.832;
            double ccs = imsl->ionMobilityToCCS(imTestValue, 678.9, 2);
            double imValue = imsl->ccsToIonMobility(ccs, 678.9, 2);
            unit_assert_equal(imValue, imTestValue, 1e-5); // some vendors use 32-bit float so accuracy can't be too stringent
        }
      
        // test that non-IMS peak picked data have unique m/z values
        if (config.peakPicking && !config.combineIonMobilitySpectra && msd.run.spectrumListPtr)
        {
            const auto& sl = *msd.run.spectrumListPtr;
            ostringstream ss;

            for (size_t i = 0; i < sl.size(); ++i)
            {
                map<double, vector<size_t>> duplicateIndicesByMz;
                auto s = sl.spectrum(i, true);
                auto mzArray = s->getMZArray()->data;
                for (size_t j=0; j < mzArray.size(); ++j)
                    duplicateIndicesByMz[mzArray[j]].push_back(j);

                bool hasDuplicates = false;
                for (auto& mzIndicesPair : duplicateIndicesByMz)
                    if (mzIndicesPair.second.size() > 1)
                    {
                        hasDuplicates = true;
                        break;
                    }

                if (hasDuplicates)
                {
                    ss << "Spectrum " << s->id << " - " << duplicateIndicesByMz.size() << " duplicates (";
                    for (auto& mzIndicesPair : duplicateIndicesByMz)
                    {
                        ss << mzIndicesPair.first << " [" << mzIndicesPair.second[0];
                        for (size_t k = 0; k < mzIndicesPair.second.size(); ++k)
                            ss << " " << mzIndicesPair.second[k];
                        ss << "]";
                    }
                    ss << ")";
                }
            }

            if (ss.tellp() > 0)
                throw runtime_error(unit_assert_message(__FILE__, __LINE__, ("Duplicate m/z values detected in peak picked spectra:\n" + ss.str()).c_str()));
        }

        // test serialization of this vendor format in and out of pwiz's supported open formats
        stringstream* stringstreamPtr = new stringstream;
        boost::shared_ptr<std::iostream> serializedStreamPtr(stringstreamPtr);
#ifndef WITHOUT_MZ5
        // mzML <-> mz5
        if (findUnicodeBytes(rawpath) == rawpath.end())
        {
            if (os_) (*os_) << "MZ5 serialization test of " << config.resultFilename(msd.run.id + ".mzML") << endl;
            string targetResultFilename_mz5 = bfs::change_extension(targetResultFilename, ".mz5").string();
            {
                MSData msd_mz5;
                Serializer_mz5 serializer_mz5;
                serializer_mz5.write(targetResultFilename_mz5, msd);
                serializer_mz5.read(targetResultFilename_mz5, msd_mz5);

                DiffConfig diffConfig_mz5(diffConfig);
                diffConfig_mz5.ignoreExtraBinaryDataArrays = true;
                Diff<MSData, DiffConfig> diff_mz5(msd, msd_mz5, diffConfig_mz5);
                if (diff_mz5) cerr << headDiff(diff_mz5, 5000) << endl;
                unit_assert(!diff_mz5);
            }
            bfs::remove(targetResultFilename_mz5);
        }
#endif

        // test SpectrumList::min_level_accepted() for vendors
        if (msd.run.spectrumListPtr && msd.run.spectrumListPtr->size() > 0)
        {
            DetailLevel msLevelDetailLevel = msd.run.spectrumListPtr->min_level_accepted([](const Spectrum& s) { return s.hasCVParam(MS_ms_level); });
            unit_assert_operator_equal(DetailLevel_InstantMetadata, msLevelDetailLevel);

            if (!bal::iequals(reader.getType(), "UIMF"))
            {
                DetailLevel polarityDetailLevel = msd.run.spectrumListPtr->min_level_accepted([](const Spectrum& s) { return s.hasCVParamChild(MS_scan_polarity); });
                unit_assert(DetailLevel_FastMetadata >= polarityDetailLevel);
            }
        }

        DiffConfig diffConfig_non_mzML(diffConfig);
        diffConfig_non_mzML.ignoreMetadata = true;
        diffConfig_non_mzML.ignoreExtraBinaryDataArrays = true;
        diffConfig_non_mzML.ignoreChromatograms = true;

        // check if the file type is one that loses nativeIDs in translation
        string fileType = reader.identify(rawpath, rawheader);
        if (bal::contains(fileType, "WIFF") ||
            bal::contains(fileType, "Waters") ||
            bal::contains(fileType, "MassHunter") ||
            fileType == "Bruker FID" ||
            fileType == "Bruker TDF" ||
            fileType == "UIMF" ||
            bal::contains(fileType, "T2D"))
            diffConfig_non_mzML.ignoreIdentity = true;

        if (msd.run.spectrumListPtr)
        {
            // mzML <-> mzXML
            MSData msd_mzXML;
            Serializer_mzXML::Config config_mzXML;
            if (os_)
            {
                config_mzXML.binaryDataEncoderConfig.compression = BinaryDataEncoder::Compression_Zlib;
                config_mzXML.binaryDataEncoderConfig.precision = BinaryDataEncoder::Precision_32;
            }
            Serializer_mzXML serializer_mzXML(config_mzXML);
            serializer_mzXML.write(*stringstreamPtr, msd);
            if (os_) *os_ << "mzXML:\n" << stringstreamPtr->str() << endl;
            serializer_mzXML.read(serializedStreamPtr, msd_mzXML);

            Diff<MSData, DiffConfig> diff_mzXML(msd, msd_mzXML, diffConfig_non_mzML);
            if (diff_mzXML && !os_) cerr << "mzXML:\n" << headStream(*serializedStreamPtr, 5000) << endl;
            if (diff_mzXML) cerr << headDiff(diff_mzXML, 5000) << endl;
            unit_assert(!diff_mzXML);
        }

        stringstreamPtr->str(" ");
        stringstreamPtr->clear();
        stringstreamPtr->seekp(0);

        if (msd.run.spectrumListPtr)
        {
            // mzML <-> MGF
            msd.run.spectrumListPtr = SpectrumListPtr(new SpectrumList_MGF_Filter(msd.run.spectrumListPtr));
            MSData msd_MGF;
            Serializer_MGF serializer_MGF;
            serializer_MGF.write(*stringstreamPtr, msd);
            if (os_) *os_ << "MGF:\n" << stringstreamPtr->str() << endl;
            serializer_MGF.read(serializedStreamPtr, msd_MGF);

            diffConfig_non_mzML.ignoreIdentity = true;
            Diff<MSData, DiffConfig> diff_MGF(msd, msd_MGF, diffConfig_non_mzML);
            if (diff_MGF && !os_) cerr << "MGF:\n" << headStream(*serializedStreamPtr, 5000) << endl;
            if (diff_MGF) cerr << headDiff(diff_MGF, 5000) << endl;
            unit_assert(!diff_MGF);
        }
    }

    msds.clear();

    // test reverse iteration of metadata on a fresh document;
    // this tests that caching optimization for forward iteration doesn't hide problems;
    // i.e. SpectrumList_Thermo::findPrecursorSpectrumIndex()
    for (auto runItr = runRange; runItr.first < runItr.second; ++runItr.first)
    {
        MSData msd_reverse;
        reader.read(rawpath, rawheader, msd_reverse, runItr.first, readerConfig);
        if (os_) (*os_) << "Reverse iteration test of " << config.resultFilename(msd_reverse.run.id + ".mzML") << endl;

        if (msd_reverse.run.spectrumListPtr.get())
            for (size_t j = 0, end = msd_reverse.run.spectrumListPtr->size(); j < end; ++j)
                msd_reverse.run.spectrumListPtr->spectrum(end - j - 1);

        if (msd_reverse.run.chromatogramListPtr.get())
            for (size_t j = 0, end = msd_reverse.run.chromatogramListPtr->size(); j < end; ++j)
                msd_reverse.run.chromatogramListPtr->chromatogram(end - j - 1);
    }

    // no unicode test for HTTP paths
    if (isHTTP(rawpath))
        return;

    // test non-ASCII characters in the source name, which in case of failure is conditionally an error or warning;
    // create a copy of the rawpath (file or directory) with non-ASCII characters in it
    bfs::path::string_type unicodeTestString(boost::locale::conv::utf_to_utf<bfs::path::value_type>(L".试验"));
    bfs::path rawpathPath(rawpath);
    bfs::path newRawPath = bfs::current_path() / rawpathPath.filename();
    newRawPath.replace_extension(unicodeTestString + newRawPath.extension().native());
    if (bfs::exists(newRawPath))
        bfs::remove_all(newRawPath);
    if (bfs::is_directory(rawpathPath))
        pwiz::util::copy_directory(rawpathPath, newRawPath);
    else
    {
        // special case for wiff files with accompanying .scan files
        if (bal::iends_with(rawpath, ".wiff") || bal::iends_with(rawpath, ".wiff2"))
        {
            bfs::path wiffscanPath(rawpathPath);
            wiffscanPath.replace_extension(".wiff.scan");
            if (bfs::exists(wiffscanPath))
            {
                bfs::path newWiffscanPath = bfs::current_path() / rawpathPath.filename(); // replace_extension won't work as desired on wiffscanPath
                newWiffscanPath.replace_extension(unicodeTestString + boost::locale::conv::utf_to_utf<bfs::path::value_type>(L".wiff.scan"));
                if (bfs::exists(newWiffscanPath))
                    bfs::remove(newWiffscanPath);
                bfs::copy_file(wiffscanPath, newWiffscanPath);
            }
        }
        bfs::copy_file(rawpathPath, newRawPath);
    }

    try
    {
        rawheader = pwiz::util::read_file_header(newRawPath.string(), 512);
        reader.read(newRawPath.string(), rawheader, msds, readerConfig);

        bfs::path sourceNameAsPath(sourceName);
        sourceNameAsPath.replace_extension("");
        bfs::path newSourceName = sourceNameAsPath;
        newSourceName += unicodeTestString;

        // Compensate for the change to the filename:
        // - single-run sources will change like: <SourceName> -> <SourceName>.<UnicodeTestString>
        // - multi-run sources (e.g. WIFF) will change like: <SourceName>-<SampleName> -> <SourceName>.<UnicodeTestString>-<SampleName>
        for (auto runItr = runRange; runItr.first < runItr.second; ++runItr.first)
        {
            MSData& msd = *msds[runItr.first];
            if (os_) (*os_) << "Unicode support mzML serialization test of " << config.resultFilename(msd.run.id + ".mzML") << endl;

            calculateSourceFileChecksums(msd.fileDescription.sourceFilePtrs);
            mangleSourceFileLocations(sourceNameAsPath.string(), msd.fileDescription.sourceFilePtrs, newSourceName.string());
            config.wrap(msd);
            manglePwizSoftware(msd);

            if (os_) TextWriter(*os_, 0)(msd);

            bfs::path::string_type targetResultFilename = (parentPath / config.resultFilename(msd.run.id + ".mzML")).native();
            bal::replace_all(targetResultFilename, unicodeTestString, L"");
            MSDataFile targetResult(bfs::path(targetResultFilename).string());
            hackInMemoryMSData(sourceNameAsPath.string(), targetResult, config, newSourceName.string());

            // test for 1:1 equality with the target mzML
            Diff<MSData, DiffConfig> diff(msd, targetResult, diffConfig);
            if (diff) cerr << headDiff(diff, 5000) << endl;
            unit_assert(!diff);

            // test serialization of this vendor format in and out of pwiz's supported open formats
            stringstream* stringstreamPtr = new stringstream;
            boost::shared_ptr<std::iostream> serializedStreamPtr(stringstreamPtr);
#ifndef WITHOUT_MZ5
            // mzML <-> mz5
            if (findUnicodeBytes(rawpath) == rawpath.end())
            {
                string targetResultFilename_mz5 = bfs::change_extension(targetResultFilename, ".mz5").string();
                {
                    MSData msd_mz5;
                    Serializer_mz5 serializer_mz5;
                    serializer_mz5.write(targetResultFilename_mz5, msd);
                    serializer_mz5.read(targetResultFilename_mz5, msd_mz5);

                    DiffConfig diffConfig_mz5(diffConfig);
                    diffConfig_mz5.ignoreExtraBinaryDataArrays = true;
                    Diff<MSData, DiffConfig> diff_mz5(msd, msd_mz5, diffConfig_mz5);
                    if (diff_mz5) cerr << headDiff(diff_mz5, 5000) << endl;
                    unit_assert(!diff_mz5);
                }
                bfs::remove(targetResultFilename_mz5);
            }
#endif
        }
    }
    catch (exception& e)
    {
        if (requireUnicodeSupport)
            throw runtime_error(string("error while testing for Unicode support: ") + e.what());
        else
            cerr << "Warning: error while testing for Unicode support: " << e.what() << endl;
    }

    msds.clear(); // free the MSDataFiles

    try
    {
        bfs::remove_all(newRawPath); // remove the copy of the RAW file with non-ASCII characters

        // special case for wiff files with accompanying .scan files
        if (bal::iequals(rawpathPath.extension().string(), ".wiff"))
        {
            bfs::path wiffscanPath(rawpathPath);
            wiffscanPath.replace_extension(".wiff.scan");
            if (bfs::exists(wiffscanPath))
            {
                bfs::path newWiffscanPath = bfs::current_path() / rawpathPath.filename(); // replace_extension won't work as desired on wiffscanPath
                newWiffscanPath.replace_extension(unicodeTestString + boost::locale::conv::utf_to_utf<bfs::path::value_type>(L".wiff.scan"));
                bfs::remove(newWiffscanPath);
            }
        }
    }
    catch (bfs::filesystem_error& e)
    {
        cerr << "Warning: non-ASCII copy of test file \"" << rawpath << "\" could not be removed after testing: " << e.what() << endl;
    }
}


void test(const Reader& reader, bool testAcceptOnly, bool requireUnicodeSupport, const string& rawpath, const bfs::path& parentPath, const ReaderTestConfig& config)
{
    testAccept(reader, rawpath);

    if (!testAcceptOnly)
        testRead(reader, rawpath, parentPath, requireUnicodeSupport, config);
}


void generate(const Reader& reader, const string& rawpath, const bfs::path& parentPath, const ReaderTestConfig& config)
{
    // read file into MSData object
    vector<MSDataPtr> msds;
    Reader::Config readerConfig(config);
    readerConfig.adjustUnknownTimeZonesToHostTimeZone = false;
    reader.read(rawpath, "dummy", msds, readerConfig);
    MSDataFile::WriteConfig writeConfig;
    writeConfig.indexed = false;
    writeConfig.binaryDataEncoderConfig.precision = config.doublePrecision ? BinaryDataEncoder::Precision_64 : BinaryDataEncoder::Precision_32;
    writeConfig.binaryDataEncoderConfig.compression = BinaryDataEncoder::Compression_Zlib;
    if (os_) *os_ << "Writing mzML(s) for " << rawpath << endl;

    for (auto runItr = config.runIndex ? make_pair(config.runIndex.get(), config.runIndex.get()+1) : make_pair(0, (int) msds.size()); runItr.first < runItr.second; ++runItr.first)
    {
        auto& msd = msds[runItr.first];
        bfs::path outputFilename = parentPath / config.resultFilename(msd->run.id + ".mzML");
        calculateSourceFileChecksums(msd->fileDescription.sourceFilePtrs);

        config.wrap(*msd);

        MSDataFile::write(*msd, outputFilename.string(), writeConfig);
    }
}

void parseArgs(const vector<string>& args, bool& generateMzML, vector<string>& rawpaths)
{
    generateMzML = false;

    for (size_t i=1; i < args.size(); ++i)
    {
        if (args[i] == "-v") os_ = &cout;
        else if (args[i] == "--generate-mzML") generateMzML = true;
        else if (bal::starts_with(args[i], "--")) continue;
        else rawpaths.push_back(args[i]);
    }
}

void testThreadSafetyWorker(boost::barrier* testBarrier, const Reader* reader, bool* testAcceptOnly, bool* requireUnicodeSupport, const string* rawpath, const bfs::path* parentPath, const ReaderTestConfig* config)
{
    testBarrier->wait(); // wait until all threads have started

    try
    {
        testAccept(*reader, *rawpath);

        if (!(*testAcceptOnly))
            testRead(*reader, *rawpath, *parentPath, *requireUnicodeSupport, *config);
    }
    catch (exception& e)
    {
        cerr << "Exception in worker thread: " << e.what() << endl;
    }
    catch (...)
    {
        cerr << "Unhandled exception in worker thread." << endl;
    }
}

void testThreadSafety(const int& testThreadCount, const Reader& reader, bool testAcceptOnly, bool requireUnicodeSupport, const string& rawpath, const bfs::path& parentPath, const ReaderTestConfig& config)
{
    boost::barrier testBarrier(testThreadCount);
    boost::thread_group testThreadGroup;
    for (int i=0; i < testThreadCount; ++i)
        testThreadGroup.add_thread(new boost::thread(&testThreadSafetyWorker, &testBarrier, &reader, &testAcceptOnly, &requireUnicodeSupport, &rawpath, &parentPath, &config));
    testThreadGroup.join_all();
}

} // namespace



PWIZ_API_DECL TestResult& TestResult::operator+= (const TestResult& rhs)
{
    totalTests += rhs.totalTests;
    failedTests += rhs.failedTests;
    return *this;
}

PWIZ_API_DECL TestResult TestResult::operator+ (const TestResult& rhs) const
{
    TestResult lhs = *this;
    return lhs += rhs;
}

PWIZ_API_DECL void TestResult::check() const
{
    if (totalTests == 0)
        throw runtime_error("no vendor test data found (try running without --incremental)");

    if (failedTests > 0)
        throw runtime_error("failed " + lexical_cast<string>(failedTests) + " of " + lexical_cast<string>(totalTests) + " tests");
}



PWIZ_API_DECL
string ReaderTestConfig::resultFilename(const string& baseFilename) const
{
    string result = baseFilename;
    if (simAsSpectra) bal::replace_all(result, ".mzML", "-simSpectra.mzML");
    if (srmAsSpectra) bal::replace_all(result, ".mzML", "-srmSpectra.mzML");
    if (acceptZeroLengthSpectra) bal::replace_all(result, ".mzML", "-acceptZeroLength.mzML");
    if (ignoreZeroIntensityPoints) bal::replace_all(result, ".mzML", "-ignoreZeros.mzML");
    if (combineIonMobilitySpectra) bal::replace_all(result, ".mzML", "-combineIMS.mzML");
    if (preferOnlyMsLevel) bal::replace_all(result, ".mzML", "-ms" + lexical_cast<string>(preferOnlyMsLevel) + ".mzML");
    if (!allowMsMsWithoutPrecursor) bal::replace_all(result, ".mzML", "-noMsMsWithoutPrecursor.mzML");
    if (peakPicking) bal::replace_all(result, ".mzML", "-centroid.mzML");
    if (!isolationMzAndMobilityFilter.empty()) bal::replace_all(result, ".mzML", "-mzMobilityFilter.mzML");
    if (globalChromatogramsAreMs1Only) bal::replace_all(result, ".mzML", "-globalChromatogramsAreMs1Only.mzML");
    //if (thresholdCount > 0) bal::replace_all(result, ".mzML", "-top" + lexical_cast<string>(thresholdCount) + ".mzML");
    return result;
}

PWIZ_API_DECL
void ReaderTestConfig::wrap(MSData& msd) const
{
    using pwiz::analysis::SpectrumListFactory;
    if (peakPicking) SpectrumListFactory::wrap(msd, "peakPicking true 1-");
    if (indexRange) SpectrumListFactory::wrap(msd, "index " + lexical_cast<string>(indexRange.get().first) + "-" + lexical_cast<string>(indexRange.get().second));
    if (peakPickingCWT) SpectrumListFactory::wrap(msd, "peakPicking cwt msLevel=1-");
    if (thresholdCount > 0) SpectrumListFactory::wrap(msd, "threshold count " + lexical_cast<string>(thresholdCount) + " most-intense");

    msd.dataProcessingPtrs = msd.allDataProcessingPtrs();
    if (peakPickingCWT)
        // remove processingMethod added by thresholding
        msd.dataProcessingPtrs[0]->processingMethods.pop_back();

    if (thresholdCount > 0)
        // remove processingMethod added by thresholding
        msd.dataProcessingPtrs[0]->processingMethods.pop_back();
}


PWIZ_API_DECL
TestResult testReader(const Reader& reader, const vector<string>& args, bool testAcceptOnly, bool requireUnicodeSupport, const TestPathPredicate& isPathTestable, const ReaderTestConfig& config)
{
    bool generateMzML;
    vector<string> rawpaths;
    parseArgs(args, generateMzML, rawpaths);

    if (rawpaths.empty())
        throw runtime_error(string("Invalid arguments: ") + bal::join(args, " ") +
                            "\nUsage: " + args[0] + " [-v] [--generate-mzML] <source path 1> [source path 2] ..."); 

    TestResult result;

    bfs::detail::utf8_codecvt_facet utf8;
    for (size_t i = 0; i < rawpaths.size(); ++i)
    {
        vector<bfs::path> filepaths;
        expand_pathmask(bfs::path(rawpaths[i] + "/*", utf8), filepaths);
        vector<string> testpaths, parentPaths;
        for (const bfs::path& filepath : filepaths)
        {
            if (filepath.filename().string() == "urls.txt")
            {
                ifstream urls(filepath.string().c_str());
                string url;
                while (getline(urls, url))
                {
                    if (isPathTestable(url))
                    {
                        testpaths.push_back(url);
                        parentPaths.push_back(filepath.parent_path().string());
                    }
                }
                continue;
            }
            else
            {
                string rawpath = filepath.string(utf8);
                if (isPathTestable(rawpath))
                {
                    testpaths.push_back(rawpath);
                    parentPaths.push_back(filepath.parent_path().string());
                }
            }
        }

        for (size_t i=0; i < testpaths.size(); ++i)
        {
            ++result.totalTests;
            const string& rawpath = testpaths[i];
            const string& parentPath = parentPaths[i];
            if (generateMzML && config.autoTest)
                continue;
            else if (generateMzML && !testAcceptOnly)
                generate(reader, rawpath, parentPath, config);
            else
            {
                try
                {
                    test(reader, testAcceptOnly, requireUnicodeSupport, rawpath, parentPath, config);
                }
                catch (exception& e)
                {
                    cerr << "Error testing on " << rawpath << " (" << config.resultFilename("config.mzML") <<
                        (config.peakPickingCWT ? "-cwt" : "") <<
                        (config.thresholdCount > 0 ? "-threshold-top3" : "") <<
                        "): " << e.what() << endl;
                    ++result.failedTests;
                }

                /* TODO: there are issues to be resolved here but not just simple crashes
                testThreadSafety(1, reader, testAcceptOnly, requireUnicodeSupport, rawpath);
                testThreadSafety(2, reader, testAcceptOnly, requireUnicodeSupport, rawpath);
                testThreadSafety(4, reader, testAcceptOnly, requireUnicodeSupport, rawpath);*/

                if (bfs::exists(rawpath))
                {
                    // test that the reader releases any locks on the data so it can be moved/deleted
                    try
                    {
                        bfs::rename(rawpath, rawpath + ".renamed");
                        bfs::rename(rawpath + ".renamed", rawpath);
                    }
                    catch (...)
                    {
                        // HACK: bug in CompassXtract, used only for YEP/FID formats now, keeps directory locked after opening it but has no problem with re-opening the file
                        if (bfs::exists(bfs::path(rawpath) / "Analysis.yep"))
                            cerr << "Cannot rename " << rawpath << ": there are unreleased file locks!" << endl;
                        else
                            throw runtime_error("Cannot rename " + rawpath + ": there are unreleased file locks!");
                    }
                }
            }
        }
    }


    // run auto tests (e.g. thresholding)
    if (!config.autoTest)
    {
        ReaderTestConfig newConfig = config;
        newConfig.thresholdCount = 3;
        newConfig.autoTest = true;

        // thresholding is always tested to check that mutating SpectrumListWrappers work as expected;
        // thresholding by itself does not create a separate mzML file
        result += testReader(reader, args, testAcceptOnly, requireUnicodeSupport, isPathTestable, newConfig);

        /*if (!generateMzML && config.peakPicking)
        {
            // the config should also have thresholding (per above recursive call)
            ReaderTestConfig newConfig = config;
            newConfig.autoTest = true;
            newConfig.peakPicking = false;
            newConfig.peakPickingCWT = true;
            result += testReader(reader, args, testAcceptOnly, requireUnicodeSupport, isPathTestable, newConfig);
        }*/
    }

    /*if (!generateMzML && config.peakPicking)
    {
        // the config should also have thresholding (per above recursive call)
        ReaderTestConfig newConfig = config;
        newConfig.peakPicking = false;
        newConfig.peakPickingCWT = true;
        return testReader(reader, args, testAcceptOnly, requireUnicodeSupport, isPathTestable, newConfig);
    }*/

    return result;
}


} // namespace util
} // namespace pwiz
