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


#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "Reader.hpp"
#include "examples.hpp"
#include "MSDataFile.hpp"
#include "pwiz/data/vendor_readers/ExtendedReaderList.hpp"
#include <cstring>


using namespace pwiz::util;
using namespace pwiz::cv;
using namespace pwiz::msdata;


ostream* os_ = 0;


class Reader1 : public Reader
{
    public:

    struct ReaderConfig
    {
        string name;
        mutable bool done;
        ReaderConfig() : name("default"), done(false) {}
    };

    ReaderConfig readerConfig;

    virtual std::string identify(const std::string& filename, const std::string& head) const
    {
        bool result = (filename == "1"); 
        if (os_) *os_ << "Reader1::identify(): " << boolalpha << result << endl;
        return std::string (result?filename:std::string("")); 
    }

    virtual void read(const std::string& filename, 
                      const std::string& head,
                      MSData& result,
                      int runIndex = 0,
                      const Config& config = Config()) const 
    {
        if (os_) *os_ << "Reader1::read()\n";
        readerConfig.done = true;
    }

    virtual void read(const std::string& filename,
                      const std::string& head,
                      std::vector<MSDataPtr>& results,
                      const Config& config = Config()) const
    {
        results.push_back(MSDataPtr(new MSData));
        read(filename, head, *results.back(), 0, config);
    }

    virtual const char *getType() const {return "Reader1";} // satisfy inheritance
    CVID getCvType() const { return CVID_Unknown; } // satisfy inheritance
    virtual std::vector<std::string> getFileExtensions() const { return { ".t1" }; }
};


class Reader2 : public Reader
{
    public:

    struct ReaderConfig
    {
        string color;
        mutable bool done;
        ReaderConfig() : color("orange"), done(false) {}
    };

    ReaderConfig readerConfig;

    virtual std::string identify(const std::string& filename, const std::string& head) const
    {
        bool result = (filename == "2"); 
        if (os_) *os_ << "Reader2::identify(): " << boolalpha << result << endl;
        return std::string (result?filename:std::string("")); 
    }

    virtual void read(const std::string& filename, 
                      const std::string& head,
                      MSData& result,
                      int runIndex = 0,
                      const Config& config = Config()) const
    {
        if (os_) *os_ << "Reader2::read()\n";
        readerConfig.done = true;
    }

    virtual void read(const std::string& filename,
                      const std::string& head,
                      std::vector<MSDataPtr>& results,
                      const Config& config = Config()) const
    {
        results.push_back(MSDataPtr(new MSData));
        read(filename, head, *results.back(), 0, config);
    }

    const char *getType() const {return "Reader2";} // satisfy inheritance
    CVID getCvType() const { return CVID_Unknown; } // satisfy inheritance
    virtual std::vector<std::string> getFileExtensions() const { return { ".t2" }; }
};


void testGet()
{
    if (os_) *os_ << "testGet()\n";

    ReaderList readers;
    readers.push_back(ReaderPtr(new Reader1));
    readers.push_back(ReaderPtr(new Reader2));

    unit_assert(readers.size() == 2);

    Reader1* reader1 = readers.get<Reader1>();
    unit_assert(reader1);
    if (os_) *os_ << "reader1 config: " << reader1->readerConfig.name << endl; 
    unit_assert(reader1->readerConfig.name == "default");
    reader1->readerConfig.name = "raw";
    if (os_) *os_ << "reader1 config: " << reader1->readerConfig.name << endl; 
    unit_assert(reader1->readerConfig.name == "raw");

    Reader2* reader2 = readers.get<Reader2>();
    unit_assert(reader2);
    if (os_) *os_ << "reader2 config: " << reader2->readerConfig.color << endl; 
    unit_assert(reader2->readerConfig.color == "orange");
    reader2->readerConfig.color = "purple";
    if (os_) *os_ << "reader2 config: " << reader2->readerConfig.color << endl; 
    unit_assert(reader2->readerConfig.color == "purple");

    const ReaderList& const_readers = readers;
    const Reader2* constReader2 = const_readers.get<Reader2>();
    unit_assert(constReader2);
    if (os_) *os_ << "constReader2 config: " << constReader2->readerConfig.color << endl; 

    if (os_) *os_ << endl;
}


void testAccept()
{
    if (os_) *os_ << "testAccept()\n";

    ReaderList readers;
    readers.push_back(ReaderPtr(new Reader1));
    readers.push_back(ReaderPtr(new Reader2));

    if (os_) *os_ << "accept 1:\n";
    unit_assert(readers.accept("1", "head"));
    if (os_) *os_ << "accept 2:\n";
    unit_assert(readers.accept("2", "head"));
    if (os_) *os_ << "accept 3:\n";
    unit_assert(!readers.accept("3", "head"));

    if (os_) *os_ << endl;
}


void testRead()
{
    if (os_) *os_ << "testRead()\n";

    ReaderList readers;
    readers.push_back(ReaderPtr(new Reader1));
    readers.push_back(ReaderPtr(new Reader2));

    MSData msd;

    // note: composite pattern with accept/read will cause two calls
    // to accept(); the alternative is to maintain state between accept()
    // and read(), which opens possibility for misuse. 

    unit_assert(readers.get<Reader1>()->readerConfig.done == false);
    if (readers.accept("1", "head"))
        readers.read("1", "head", msd);
    unit_assert(readers.get<Reader1>()->readerConfig.done == true);

    readers.get<Reader1>()->readerConfig.done = false;
    unit_assert(readers.get<Reader2>()->readerConfig.done == false);
    if (readers.accept("2", "head"))
        readers.read("2", "head", msd);
    unit_assert(readers.get<Reader1>()->readerConfig.done == false);
    unit_assert(readers.get<Reader2>()->readerConfig.done == true);

    if (os_) *os_ << endl;
}


void testIdentifyFileFormat()
{
    ExtendedReaderList readerList;

    {ofstream fs("testSpectraDataFile.mzedML"); fs << "<?xml?><mzML>";}
    unit_assert_operator_equal(MS_mzML_format, readerList.identifyAsReader("testSpectraDataFile.mzedML")->getCvType());
    bfs::remove("testSpectraDataFile.mzedML");

    {ofstream fs("testSpectraDataFile.mzedXML"); fs << "<?xml?><mzXML>";}
    unit_assert_operator_equal(MS_ISB_mzXML_format, readerList.identifyAsReader("testSpectraDataFile.mzedXML")->getCvType());
    bfs::remove("testSpectraDataFile.mzedXML");

    
    {
        MSData msd;
        examples::initializeTiny(msd);
        MSDataFile::WriteConfig config;
        config.format = MSDataFile::Format_MZ5;
#ifndef WITHOUT_MZ5
        MSDataFile::write(msd, "testSpectraDataFile.Mz5", config);
        unit_assert_operator_equal(MS_mz5_format, readerList.identifyAsReader("testSpectraDataFile.Mz5")->getCvType());
#endif
    }
    bfs::remove("testSpectraDataFile.Mz5");

    {ofstream fs("testSpectraDataFile.mGF"); fs << "MGF";}
    unit_assert_operator_equal(MS_Mascot_MGF_format, readerList.identifyAsReader("testSpectraDataFile.mGF")->getCvType());
    bfs::remove("testSpectraDataFile.mGF");
    
    {ofstream fs("testSpectraDataFile.Ms2"); fs << "MS2";}
    unit_assert_operator_equal(MS_MS2_format, readerList.identifyAsReader("testSpectraDataFile.Ms2")->getCvType());
    bfs::remove("testSpectraDataFile.Ms2");
    
    {ofstream fs("testSpectraDataFile.wiFF"); fs << "WIFF";}
    unit_assert_operator_equal(MS_ABI_WIFF_format, readerList.identifyAsReader("testSpectraDataFile.wiFF")->getCvType());
    bfs::remove("testSpectraDataFile.wiFF");

    {ofstream fs("_FUNC42.DAT"); fs << "Life, the Universe, and Everything";}
    unit_assert_operator_equal(MS_Waters_raw_format, readerList.identifyAsReader(".")->getCvType());
    bfs::remove("_FUNC42.DAT");


    // test types and extensions
    auto extByType = readerList.getFileExtensionsByType();

    {
        auto readerTypes = readerList.getTypes();
        set<string> readerTypeSet(readerTypes.begin(), readerTypes.end());
        set<string> expectedTypeSet{ "mzML", "mzXML", "MS1", "MS2", "Mascot Generic", "Bruker Data Exchange", "MZ5",
                                     "Sciex WIFF/WIFF2", "AB/Sciex T2D", "Agilent MassHunter", "Bruker FID", "Bruker YEP", "Bruker BAF", "Bruker U2", "Bruker TDF",
                                     "Shimadzu LCD", "Thermo RAW", "UIMF", "Waters RAW", "Waters UNIFI" };
        auto expectedButNotFound = expectedTypeSet - readerTypeSet;
        auto foundButNotExpected = readerTypeSet - expectedTypeSet;
        unit_assert_operator_equal(set<string>(), expectedButNotFound);
        unit_assert_operator_equal(set<string>(), foundButNotExpected);

        unit_assert_operator_equal(expectedTypeSet.size(), extByType.size());
    }

    {
        auto readerTypes = readerList.getCvTypes();
        set<CVID> readerCvTypeSet(readerTypes.begin(), readerTypes.end());
        set<CVID> expectedCvTypeSet{ MS_mzML_format, MS_ISB_mzXML_format, MS_MS1_format, MS_MS2_format, MS_Mascot_MGF_format, MS_Bruker_XML_format, MS_mz5_format,
                                     MS_ABI_WIFF_format, MS_SCIEX_TOF_TOF_T2D_format, MS_Agilent_MassHunter_format,
                                     MS_Bruker_FID_format, MS_Bruker_Agilent_YEP_format, MS_Bruker_BAF_format, MS_Bruker_U2_format, MS_Bruker_TDF_format,
                                     MS_mass_spectrometer_file_format, MS_Thermo_RAW_format, MS_UIMF_format, MS_Waters_raw_format };
        auto expectedButNotFound = expectedCvTypeSet - readerCvTypeSet;
        auto foundButNotExpected = readerCvTypeSet - expectedCvTypeSet;
        unit_assert_operator_equal(set<CVID>(), expectedButNotFound);
        unit_assert_operator_equal(set<CVID>(), foundButNotExpected);
    }

    unit_assert_operator_equal(2, extByType["Sciex WIFF/WIFF2"].size());
    unit_assert_operator_equal(".wiff", extByType["Sciex WIFF/WIFF2"][0]);
    unit_assert_operator_equal(".wiff2", extByType["Sciex WIFF/WIFF2"][1]);
    unit_assert_operator_equal(0, extByType["Waters UNIFI"].size());
    unit_assert_operator_equal(2, extByType["Bruker BAF"].size());
    unit_assert_operator_equal(2, extByType["Bruker YEP"].size());
    unit_assert_operator_equal(".d", extByType["Bruker YEP"][0]);
    unit_assert_operator_equal(".yep", extByType["Bruker YEP"][1]);
}


void test()
{
    testGet();
    testAccept();
    testRead();
    testIdentifyFileFormat();
}


int main(int argc, char* argv[])
{
    TEST_PROLOG_EX(argc, argv, "_MSData")

    try
    {
        if (argc==2 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
    }
    catch (exception& e)
    {
        TEST_FAILED(e.what())
    }
    catch (...)
    {
        TEST_FAILED("Caught unknown exception.")
    }

    TEST_EPILOG
}

