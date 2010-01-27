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
#include "pwiz/data/msdata/Serializer_mzXML.hpp"
#include "pwiz/data/msdata/Serializer_MGF.hpp"
#include "pwiz/data/msdata/Diff.hpp"
#include "pwiz/data/msdata/SpectrumListBase.hpp"
#include "pwiz/data/msdata/ChromatogramListBase.hpp"
#include "pwiz/data/msdata/SpectrumListWrapper.hpp"
#include "pwiz/data/msdata/Version.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Stream.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/SHA1Calculator.hpp"
#include <iostream>
#include <fstream>
#include <boost/foreach.hpp>


using namespace std;
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


void mangleSourceFileLocations(const string& sourceName, vector<SourceFilePtr>& sourceFiles)
{
    // mangling the absolute paths is necessary for the test to work from any path
    BOOST_FOREACH(SourceFilePtr& sourceFilePtr, sourceFiles)
    {
        // if the sourceName is in the location, preserve it (erase everything preceding it)
        size_t sourceNameInLocation = sourceFilePtr->location.find(sourceName);
        if (sourceNameInLocation != string::npos)
        {
            sourceFilePtr->location.erase(0, sourceNameInLocation);
            sourceFilePtr->location = "file:///" + sourceFilePtr->location;
        }
        else
            sourceFilePtr->location = "file:///";
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

    BOOST_FOREACH(DataProcessingPtr& dp, msd.dataProcessingPtrs)
        BOOST_FOREACH(ProcessingMethod& pm, dp->processingMethods)
            pm.softwarePtr = pwizSoftware;

    for (vector<size_t>::reverse_iterator itr = oldPwizSoftwarePtrs.rbegin();
         itr != oldPwizSoftwarePtrs.rend();
         ++itr)
         msd.softwarePtrs.erase(msd.softwarePtrs.begin()+(*itr));
}


void calculateSourceFileChecksums(vector<SourceFilePtr>& sourceFiles)
{
    BOOST_FOREACH(SourceFilePtr sourceFile, sourceFiles)
    {
        const string uriPrefix = "file://";
        if (sourceFile->location.substr(0, uriPrefix.size()) != uriPrefix) return;
        bfs::path p(sourceFile->location.substr(uriPrefix.size()));
        p /= sourceFile->name;

        string sha1 = SHA1Calculator::hashFile(p.string());
        sourceFile->set(MS_SHA_1, sha1);
    }
}


void hackInMemoryMSData(const string& sourceName, MSData& msd)
{
    // remove metadata ptrs appended on read
    vector<SourceFilePtr>& sfs = msd.fileDescription.sourceFilePtrs;
    if (!sfs.empty()) sfs.erase(sfs.end()-1);

    mangleSourceFileLocations(sourceName, sfs);
    manglePwizSoftware(msd);

    // remove current DataProcessing created on read
    SpectrumListBase* sl = dynamic_cast<SpectrumListBase*>(msd.run.spectrumListPtr.get());
    ChromatogramListBase* cl = dynamic_cast<ChromatogramListBase*>(msd.run.chromatogramListPtr.get());
    if (sl) sl->setDataProcessingPtr(DataProcessingPtr());
    if (cl) cl->setDataProcessingPtr(DataProcessingPtr());
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

            // take only the first 100 points (100k points in MGF is not fun)
            vector<double>& mzArray = result->getMZArray()->data;
            vector<double>& intensityArray = result->getIntensityArray()->data;
            if (result->defaultArrayLength > 100)
            {
                result->defaultArrayLength = 100;
                mzArray.resize(100);
                intensityArray.resize(100);
            }
        }

        return result;
    }
};


void testRead(const Reader& reader, const string& rawpath)
{
    if (os_) *os_ << "testRead(): " << rawpath << endl;

    // read file into MSData object
    vector<MSDataPtr> msds;
    string rawheader = pwiz::util::read_file_header(rawpath, 512);
    reader.read(rawpath, rawheader, msds);

    string sourceName = bfs::path(rawpath).filename();

    for (size_t i=0; i < msds.size(); ++i)
    {
        MSData& msd = *msds[i];
        calculateSourceFileChecksums(msd.fileDescription.sourceFilePtrs);
        mangleSourceFileLocations(sourceName, msd.fileDescription.sourceFilePtrs);
        manglePwizSoftware(msd);
        if (os_) TextWriter(*os_,0)(msd);

        bfs::path targetResultFilename = bfs::path(rawpath).parent_path() / (msd.run.id + ".mzML");
        MSDataFile targetResult(targetResultFilename.string());
        hackInMemoryMSData(sourceName, targetResult);

        // test for 1:1 equality with the target mzML
        Diff<MSData, DiffConfig> diff(msd, targetResult);
        if (diff) cerr << headDiff(diff, 5000) << endl;
        unit_assert(!diff);

        // test serialization of this vendor format in and out of pwiz's supported open formats
        stringstream* stringstreamPtr = new stringstream;
        boost::shared_ptr<std::iostream> serializedStreamPtr(stringstreamPtr);
        
        DiffConfig diffConfig_non_mzML;
        diffConfig_non_mzML.ignoreMetadata = true;
        diffConfig_non_mzML.ignoreChromatograms = true;

        // check if the file type is one that loses nativeIDs in translation
        string fileType = reader.identify(rawpath, rawheader);
        if (bal::contains(fileType, "WIFF") ||
            bal::contains(fileType, "Waters") ||
            fileType == "Bruker FID" ||
            bal::contains(fileType, "T2D"))
            diffConfig_non_mzML.ignoreIdentity = true;

        // mzML <-> mzXML
        MSData msd_mzXML;
        Serializer_mzXML::Config config;
        if (os_)
        {
            config.binaryDataEncoderConfig.compression = BinaryDataEncoder::Compression_Zlib;
            config.binaryDataEncoderConfig.precision = BinaryDataEncoder::Precision_32;
        }
        Serializer_mzXML serializer_mzXML(config);
        serializer_mzXML.write(*stringstreamPtr, msd);
        if (os_) *os_ << "mzXML:\n" << stringstreamPtr->str() << endl;
        serializer_mzXML.read(serializedStreamPtr, msd_mzXML);

        Diff<MSData, DiffConfig> diff_mzXML(msd, msd_mzXML, diffConfig_non_mzML);
        if (diff_mzXML && !os_) cerr << "mzXML:\n" << headStream(*serializedStreamPtr, 5000) << endl;
        if (diff_mzXML) cerr << headDiff(diff_mzXML, 5000) << endl;
        unit_assert(!diff_mzXML);

        stringstreamPtr->str(" ");
        stringstreamPtr->clear();
        stringstreamPtr->seekp(0);

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


void test(const Reader& reader, bool testAcceptOnly, const string& rawpath)
{
    testAccept(reader, rawpath);

    if (!testAcceptOnly)
        testRead(reader, rawpath);
}


void generate(const Reader& reader, const string& rawpath)
{
    // read file into MSData object
    vector<MSDataPtr> msds;
    reader.read(rawpath, "dummy", msds);
    MSDataFile::WriteConfig config;
    config.indexed = false;
    config.binaryDataEncoderConfig.precision = BinaryDataEncoder::Precision_32;
    config.binaryDataEncoderConfig.compression = BinaryDataEncoder::Compression_Zlib;
    if (os_) *os_ << "Writing mzML(s) for " << rawpath << endl;
    for (size_t i=0; i < msds.size(); ++i)
    {
        bfs::path outputFilename = bfs::path(rawpath).parent_path() / (msds[i]->run.id + ".mzML");
        calculateSourceFileChecksums(msds[i]->fileDescription.sourceFilePtrs);
        MSDataFile::write(*msds[i], outputFilename.string(), config);
    }
}

void parseArgs(const vector<string>& args, bool& generateMzML, vector<string>& rawpaths)
{
    generateMzML = false;

    for (size_t i=1; i < args.size(); ++i)
    {
        if (args[i] == "-v") os_ = &cout;
        else if (args[i] == "--generate-mzML") generateMzML = true;
        else rawpaths.push_back(args[i]);
    }
}

} // namespace


PWIZ_API_DECL
int testReader(const Reader& reader, const vector<string>& args, bool testAcceptOnly, const TestPathPredicate& isPathTestable)
{
    try
    {
        bool generateMzML;
        vector<string> rawpaths;
        parseArgs(args, generateMzML, rawpaths);

        if (rawpaths.empty())
            throw runtime_error(string("Invalid arguments: ") + bal::join(args, " ") +
                                "\nUsage: " + args[0] + " [-v] [--generate-mzML] <source path 1> [source path 2] ..."); 

        for (size_t i=0; i < rawpaths.size(); ++i)
            for (bfs::directory_iterator itr(rawpaths[i]); itr != bfs::directory_iterator(); ++itr)
            {
                if (!isPathTestable(itr->path().string()))
                    continue;
                else if (generateMzML && !testAcceptOnly)
                    generate(reader, itr->path().string());
                else
                    test(reader, testAcceptOnly, itr->path().string());
            }
        return 0;
    }
    catch (exception& e)
    {
        throw runtime_error(string("std::exception: ") + e.what());
    }
    catch (...)
    {
        throw runtime_error("Caught unknown exception.\n");
    }
    
    return 1;
}


} // namespace util
} // namespace pwiz
