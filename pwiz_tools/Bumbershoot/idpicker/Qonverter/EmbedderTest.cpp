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

#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/data/identdata/IdentDataFile.hpp"
#include "pwiz/data/identdata/examples.hpp"
#include "pwiz/data/msdata/MSDataFile.hpp"
#include "pwiz/data/msdata/Diff.hpp"
#include "pwiz/data/proteome/ProteomeDataFile.hpp"
#include "boost/assign.hpp"
#include "Embedder.hpp"
#include "Parser.hpp"


using namespace pwiz::cv;
using namespace pwiz::util;
using namespace pwiz::chemistry;
using namespace boost::assign;
using namespace IDPicker;


struct ImportSettingsHandler : public Parser::ImportSettingsCallback
{
    virtual void operator() (const vector<Parser::ConstAnalysisPtr>& distinctAnalyses, bool& cancel) const
    {
        typedef pair<string, string> AnalysisParameter;

        unit_assert_operator_equal(1, distinctAnalyses.size());
        distinctAnalyses[0]->importSettings.proteinDatabaseFilepath = "testEmbedder.fasta";

        // turn off import filtering
        distinctAnalyses[0]->importSettings.maxQValue = 1;
        distinctAnalyses[0]->importSettings.maxResultRank = 0;

        distinctAnalyses[0]->importSettings.qonverterSettings.qonverterMethod = Qonverter::QonverterMethod::StaticWeighted;
        distinctAnalyses[0]->importSettings.qonverterSettings.decoyPrefix = "rev_";

        Qonverter::Settings::ScoreInfo& scoreInfo = distinctAnalyses[0]->importSettings.qonverterSettings.scoreInfoByName["mascot:score"];
        scoreInfo.weight = 1;
        scoreInfo.normalizationMethod = Qonverter::Settings::NormalizationMethod::Off;
        scoreInfo.order = Qonverter::Settings::Order::Ascending;
    }
};

struct ExampleDBSequence
{
    const char* accession;
    const char* sequence;
    const char* description;
};

const ExampleDBSequence HS71A_MOUSE = {"NP_034609.2", "MAKNTAIGIDLGTTYSCVGVFQHGKVEIIANDQGNRTTPSYVAFTDTERLIGDAAKNQVALNPQNTVFDAKRLIGRKFGDAVVQSDMKHWPFQVVNDGDKPKVQVNYKGESRSFFPEEISSMVLTKMKEIAEAYLGHPVTNAVITVPAYFNDSQRQATKDAGVIAGLNVLRIINEPTAAAIAYGLDRTGKGERNVLIFDLGGGTFDVSILTIDDGIFEVKATAGDTHLGGEDFDNRLVSHFVEEFKRKHKKDISQNKRAVRRLRTACERAKRTLSSSTQASLEIDSLFEGIDFYTSITRARFEELCSDLFRGTLEPVEKALRDAKMDKAQIHDLVLVGGSTRIPKVQKLLQDFFNGRDLNKSINPDEAVAYGAAVQAAILMGDKSENVQDLLLLDVAPLSLGLETAGGVMTALIKRNSTIPTKQTQTFTTYSDNQPGVLIQVYEGERAMTRDNNLLGRFELSGIPPAPRGVPQIEVTFDIDANGILNVTATDKSTGKANKITITNDKGRLSKEEIERMVQEAERYKAEDEVQRDRVAAKNALESYAFNMKSAVEDEGLKGKLSEADKKKVLDKCQEVISWLDSNTLADKEEFVHKREELERVCSPIISGLYQGAGAPGAGGFGAQAPKGASGSGPTIEEVD", "Heat shock 70 kDa protein 1A OS=Mus musculus GN=Hspa1a PE=1 SV=2"};
const ExampleDBSequence HSP71_HUMAN = {"NP_005336.3", "MAKAAAIGIDLGTTYSCVGVFQHGKVEIIANDQGNRTTPSYVAFTDTERLIGDAAKNQVALNPQNTVFDAKRLIGRKFGDPVVQSDMKHWPFQVINDGDKPKVQVSYKGETKAFYPEEISSMVLTKMKEIAEAYLGYPVTNAVITVPAYFNDSQRQATKDAGVIAGLNVLRIINEPTAAAIAYGLDRTGKGERNVLIFDLGGGTFDVSILTIDDGIFEVKATAGDTHLGGEDFDNRLVNHFVEEFKRKHKKDISQNKRAVRRLRTACERAKRTLSSSTQASLEIDSLFEGIDFYTSITRARFEELCSDLFRSTLEPVEKALRDAKLDKAQIHDLVLVGGSTRIPKVQKLLQDFFNGRDLNKSINPDEAVAYGAAVQAAILMGDKSENVQDLLLLDVAPLSLGLETAGGVMTALIKRNSTIPTKQTQIFTTYSDNQPGVLIQVYEGERAMTKDNNLLGRFELSGIPPAPRGVPQIEVTFDIDANGILNVTATDKSTGKANKITITNDKGRLSKEEIERMVQEAEKYKAEDEVQRERVSAKNALESYAFNMKSAVEDEGLKGKISEADKKKVLDKCQEVISWLDANTLAEKDEFEHKRKELEQVCNPIISGLYQGAGGPGPGGFGAQGPKGGSGSGPTIEEVD", "Heat shock 70 kDa protein 1A/1B OS=Homo sapiens GN=HSPA1A PE=1 SV=5"};

void createExampleWithArrays(const vector<double>& mzArray, const vector<double>& intensityArray)
{
    // create MSData corresponding with the example
    {
        using namespace pwiz::msdata;
        using namespace pwiz::cv;

        MSData msd;
        msd.id = msd.run.id = "testEmbedder";
        msd.cvs = defaultCVList();

        FileContent& fc = msd.fileDescription.fileContent;
        fc.set(MS_MSn_spectrum);
        fc.set(MS_centroid_spectrum);

        SourceFilePtr sfp(new SourceFile);
        sfp->id = "testEmbedder.raw";
        sfp->set(MS_Thermo_RAW_format);
        sfp->set(MS_Thermo_nativeID_format);
        msd.fileDescription.sourceFilePtrs.push_back(sfp);

        InstrumentConfigurationPtr instrumentConfigurationPtr(new InstrumentConfiguration("LCQ Deca"));
        instrumentConfigurationPtr->set(MS_LCQ_Deca);
        instrumentConfigurationPtr->componentList.push_back(Component(MS_nanoelectrospray, 1));
        instrumentConfigurationPtr->componentList.push_back(Component(MS_quadrupole_ion_trap, 2));
        instrumentConfigurationPtr->componentList.push_back(Component(MS_electron_multiplier, 3));

        msd.instrumentConfigurationPtrs.push_back(instrumentConfigurationPtr);

        SpectrumListSimple* sl = new SpectrumListSimple;
        msd.run.spectrumListPtr.reset(sl);
        for (size_t i = 0; i < 500; ++i)
        {
            sl->spectra.push_back(SpectrumPtr(new Spectrum));
            sl->spectra.back()->id = "controllerType=0 controllerNumber=1 scan=" + lexical_cast<string>(i + 1);
            sl->spectra.back()->index = i;
            sl->spectra.back()->set((i % 10 == 0) ? MS_MS1_spectrum : MS_MSn_spectrum);
            sl->spectra.back()->set(MS_ms_level, (i % 10 == 0) ? 1 : 2);
            sl->spectra.back()->set(MS_centroid_spectrum);
            sl->spectra.back()->setMZIntensityArrays(mzArray, intensityArray, MS_number_of_detector_counts);
            sl->spectra.back()->scanList.scans.push_back(Scan());
            sl->spectra.back()->scanList.scans[0].set(MS_scan_start_time, (i+1) / 100.0, UO_second);
        }

        bfs::create_directories("testEmbedder.dir");
        MSDataFile::write(msd, "testEmbedder.dir/testEmbedder.mzML");
    }
}


string testReporterIons(const vector<double>& expectedIntensities, const string& idpDbFilepath, const string& columnName)
{
    try
    {
        sqlite3pp::database db(idpDbFilepath);
        sqlite3pp::query q(db, ("SELECT " + columnName + " FROM SpectrumQuantitation WHERE " + columnName + " IS NOT NULL LIMIT 1").c_str());
        sqlite3pp::query::iterator itr = q.begin();
        if (itr == q.end())
            throw runtime_error("[testReporterIons] no non-null SpectrumQuantitation rows");
        sqlite3pp::query::rows row = *itr;
        int reporterIonCount = row.column_bytes(0) / sizeof(double);
        const void* reporterIonBlob = row.get<const void*>(0);
        const double* reporterIonArray = reinterpret_cast<const double*>(reporterIonBlob);
        vector<double> reporterIons(reporterIonArray, reporterIonArray + reporterIonCount);
        unit_assert_operator_equal(expectedIntensities.size(), reporterIons.size());
        if (reporterIonCount > 0) unit_assert_operator_equal(expectedIntensities[0], reporterIons[0]);
        if (reporterIonCount > 1) unit_assert_operator_equal(expectedIntensities[1], reporterIons[1]);
        if (reporterIonCount > 2) unit_assert_operator_equal(expectedIntensities[2], reporterIons[2]);
        if (reporterIonCount > 3) unit_assert_operator_equal(expectedIntensities[3], reporterIons[3]);
        if (reporterIonCount > 4) unit_assert_operator_equal(expectedIntensities[4], reporterIons[4]);
        if (reporterIonCount > 5) unit_assert_operator_equal(expectedIntensities[5], reporterIons[5]);
        if (reporterIonCount > 6) unit_assert_operator_equal(expectedIntensities[6], reporterIons[6]);
        if (reporterIonCount > 7) unit_assert_operator_equal(expectedIntensities[7], reporterIons[7]);
        if (reporterIonCount > 8) unit_assert_operator_equal(expectedIntensities[8], reporterIons[8]);
        if (reporterIonCount > 9) unit_assert_operator_equal(expectedIntensities[9], reporterIons[9]);
        return "";
    }
    catch (exception& e)
    {
        return e.what();
    }
}


void test()
{
    string decoyPrefix = "rev_";

    // use example identifications from IdentData
    {
        using namespace pwiz::identdata;
        IdentData idd;
        examples::initializeBasicSpectrumIdentification(idd);
        idd.sequenceCollection.dbSequences[0]->accession.insert(0, decoyPrefix);
        idd.dataCollection.inputs.spectraData[0]->name = "testEmbedder";
        IdentDataFile::write(idd, "testEmbedder.mzid");
    }

    // create test FASTA via ProteomeData corresponding with the example
    {
        using namespace pwiz::proteome;
        ProteomeData pdd;
        ProteinListSimple* pl = new ProteinListSimple;
        pdd.proteinListPtr.reset(pl);
        pl->proteins.push_back(ProteinPtr(new Protein(decoyPrefix + HS71A_MOUSE.accession, 0, HS71A_MOUSE.description, HS71A_MOUSE.sequence)));
        pl->proteins.push_back(ProteinPtr(new Protein(HSP71_HUMAN.accession, 1, HSP71_HUMAN.description, HSP71_HUMAN.sequence)));
        ProteomeDataFile::write(pdd, "testEmbedder.fasta");
    }

    // parse test data to idpDB
    {
        if (bfs::exists("testEmbedder.idpDB"))
            bfs::remove("testEmbedder.idpDB");
        Parser parser;
        parser.importSettingsCallback = Parser::ImportSettingsCallbackPtr(new ImportSettingsHandler);
        parser.parse("testEmbedder.mzid");
    }

    // remove intermediate files
    bfs::remove("testEmbedder.mzid");
    bfs::remove("testEmbedder.fasta");
    bfs::remove("testEmbedder.fasta.index");

    // test embedding scan times only
    {
        vector<double> mzArray; mzArray += 100, 200, 300, 400, 500, 600, 700;
        vector<double> inArray; inArray += 10,  20,  30,  40,  30,  20,  10;
        createExampleWithArrays(mzArray, inArray);

        Embedder::embedScanTime("testEmbedder.idpDB", "testEmbedder.dir");
        sqlite3pp::database db("testEmbedder.idpDB");
        sqlite3pp::query q(db, "SELECT COUNT(*) FROM Spectrum WHERE ScanTimeInSeconds IS NULL OR ScanTimeInSeconds = 0");
        unit_assert_operator_equal(0, q.begin()->get<int>(0));
    }

    // test embedding without quantitation
    {
        Embedder::embed("testEmbedder.idpDB", "testEmbedder.dir");

        // extract embedded mz5
        if (bfs::exists("testEmbedder.mz5"))
            bfs::remove("testEmbedder.mz5");
        Embedder::extract("testEmbedder.idpDB", "testEmbedder", "testEmbedder.mz5");

        // test extracted mz5
        {
            pwiz::msdata::MSDataFile testEmbedder("testEmbedder.mz5");
            unit_assert(testEmbedder.run.spectrumListPtr.get());
            unit_assert_operator_equal(4, testEmbedder.run.spectrumListPtr->size());
            unit_assert(0 < testEmbedder.run.spectrumListPtr->spectrum(1)->scanList.scans[0].cvParam(MS_scan_start_time).timeInSeconds());
        }
    }

    map<int, Embedder::QuantitationConfiguration> quantitationMethodBySource;

    // test iTRAQ4
    // Label  Mass        4plex  8plex
    // 114    114.111228  1      1
    // 115    115.108263  1      1
    // 116    116.111618  1      1
    // 117    117.114973  1      1
    {
        vector<double> mzArray; mzArray += 100, 113.1, 114.11, 115,   115.11, 116.1, 117.1, 200, 300, 400, 500, 600, 700;
        vector<double> inArray; inArray +=  10, 113,   114,    115.5, 115,    116,   117,   20,  30,  40,  30,  20,  10;
        bfs::remove("testEmbedder.mzML");
        createExampleWithArrays(mzArray, inArray);

        quantitationMethodBySource[1] = Embedder::QuantitationConfiguration(QuantitationMethod::ITRAQ4plex, MZTolerance(0.015, MZTolerance::MZ), false);
        Embedder::embed("testEmbedder.idpDB", "testEmbedder.dir", quantitationMethodBySource);
        double corrected114 = max(0.0, 114 - 0.02 * 115);
        double corrected115 = max(0.0, 115 - 0.03 * 116 - 0.059 * 114);
        double corrected116 = max(0.0, 116 - 0.04 * 117 - 0.056 * 115);
        double corrected117 = max(0.0, 117              - 0.045 * 116);
        vector<double> expectedIntensities; expectedIntensities += 0, corrected114, corrected115, corrected116, corrected117, 0, 0, 0;
        unit_assert_operator_equal("", testReporterIons(expectedIntensities, "testEmbedder.idpDB", "iTRAQ_ReporterIonIntensities"));
    }

    // test iTRAQ8
    // Label  Mass        4plex  8plex
    // 113    113.107873  0      1
    // 114    114.111228  1      1
    // 115    115.108263  1      1
    // 116    116.111618  1      1
    // 117    117.114973  1      1
    // 118    118.112008  0      1
    // 119    119.115363  0      1
    // 121    121.122072  0      1
    {
        vector<double> mzArray; mzArray += 100, 113.1, 114.1, 115.12, 116.1, 117.1, 118.1, 119.12, 121.1, 121.12, 200, 300, 400, 500, 600, 700;
        vector<double> inArray; inArray +=  10, 113,   114,   115,    116,    117,   118,   119,   121,   122,    20,  30,  40,  30,  20,  10;
        bfs::remove("testEmbedder.mzML");
        createExampleWithArrays(mzArray, inArray);

        quantitationMethodBySource[1] = Embedder::QuantitationConfiguration(QuantitationMethod::ITRAQ8plex, MZTolerance(0.015, MZTolerance::MZ), false);
        Embedder::embed("testEmbedder.idpDB", "testEmbedder.dir", quantitationMethodBySource);
        double corrected113 = max(0.0, 113 - 0.0000 * 114);
        double corrected114 = max(0.0, 114 - 0.0094 * 115 - 0.0689 * 113);
        double corrected115 = max(0.0, 115 - 0.0188 * 116 - 0.0590 * 114);
        double corrected116 = max(0.0, 116 - 0.0282 * 117 - 0.0490 * 115);
        double corrected117 = max(0.0, 117 - 0.0377 * 118 - 0.0390 * 116);
        double corrected118 = max(0.0, 118 - 0.0471 * 119 - 0.0288 * 117);
        double corrected119 = max(0.0, 119                - 0.0191 * 118);
        double corrected121 = 122;
        vector<double> expectedIntensities; expectedIntensities += corrected113, corrected114, corrected115, corrected116, corrected117, corrected118, corrected119, corrected121;
        unit_assert_operator_equal("", testReporterIons(expectedIntensities, "testEmbedder.idpDB", "iTRAQ_ReporterIonIntensities"));
    }

    // test TMT2
    // Label  Mass        2plex  6plex  10plex
    // 126    126.12773   1      1      1
    // 127C   127.13108   1      1      1
    {
        vector<double> mzArray; mzArray += 100, 126.128, 127.131, 200, 300, 400, 500, 600, 700;
        vector<double> inArray; inArray +=  10, 126,     127,     20,  30,  40,  30,  20,  10;
        bfs::remove("testEmbedder.mzML");
        createExampleWithArrays(mzArray, inArray);

        quantitationMethodBySource[1] = Embedder::QuantitationConfiguration(QuantitationMethod::TMT2plex, MZTolerance(0.015, MZTolerance::MZ), false);
        Embedder::embed("testEmbedder.idpDB", "testEmbedder.dir", quantitationMethodBySource);
        //                                                         126  127N 127C 128N 128C 129N 129C 130N 130C 131
        vector<double> expectedIntensities; expectedIntensities += 126, 0,   127, 0,   0,   0,   0,   0,   0,   0;
        unit_assert_operator_equal("", testReporterIons(expectedIntensities, "testEmbedder.idpDB", "TMT_ReporterIonIntensities"));
    }
    
    // test TMT10 with TMT6 data
    // Label  Mass        2plex  6plex  10plex
    // 126    126.12773   1      1      1
    // 127N   127.12476   0      0      1
    // 127C   127.13108   1      1      1
    // 128N   128.12811   0      0      1
    // 128C   128.13443   0      1      1
    // 129N   129.13147   0      0      1
    // 129C   129.13779   0      1      1
    // 130N   130.13482   0      0      1
    // 130C   130.14114   0      1      1
    // 131    131.13818   0      1      1
    {
        MZTolerance ht = MZTolerance(10, MZTolerance::PPM);
        vector<double> mzArray; mzArray += 100, 126.12773-ht, 127.13108+ht, 128.13443-ht, 129.13779+ht, 130.14114+ht, 131.13818, 200, 300, 400, 500, 600, 700;
        vector<double> inArray; inArray +=  10, 126,          127.1,        128.1,        129.1,        130.1,        131,       20,  30,  40,  30,  20,  10;
        bfs::remove("testEmbedder.mzML");
        createExampleWithArrays(mzArray, inArray);

        quantitationMethodBySource[1] = Embedder::QuantitationConfiguration(QuantitationMethod::TMT10plex, MZTolerance(20, MZTolerance::PPM), false);
        Embedder::embed("testEmbedder.idpDB", "testEmbedder.dir", quantitationMethodBySource);
        //                                                         126  127N   127C   128N   128C   129N   129C   130N   130C   131
        vector<double> expectedIntensities; expectedIntensities += 126, 0,     127.1, 0,     128.1, 0,     129.1, 0,     130.1, 131;
        unit_assert_operator_equal("", testReporterIons(expectedIntensities, "testEmbedder.idpDB", "TMT_ReporterIonIntensities"));
    }

    // test TMT6
    // Label  Mass        2plex  6plex  10plex
    // 126    126.12773   1      1      1
    // 127C   127.13108   1      1      1
    // 128C   128.13443   0      1      1
    // 129C   129.13779   0      1      1
    // 130C   130.14114   0      1      1
    // 131    131.13818   0      1      1
    {
        vector<double> mzArray; mzArray += 100, 126.128, 127.131, 128.134, 129.132, 130.143, 131.137, 200, 300, 400, 500, 600, 700;
        vector<double> inArray; inArray +=  10, 126,     127,     128,     129,     130,     131,     20,  30,  40,  30,  20,  10;
        bfs::remove("testEmbedder.mzML");
        createExampleWithArrays(mzArray, inArray);

        quantitationMethodBySource[1] = Embedder::QuantitationConfiguration(QuantitationMethod::TMT6plex, MZTolerance(0.015, MZTolerance::MZ), false);
        Embedder::embed("testEmbedder.idpDB", "testEmbedder.dir", quantitationMethodBySource);
        //                                                         126  127N 127C 128N 128C 129N 129C 130N 130C 131
        vector<double> expectedIntensities; expectedIntensities += 126, 0,   127, 0,   128, 0,   129, 0,   130, 131;
        unit_assert_operator_equal("", testReporterIons(expectedIntensities, "testEmbedder.idpDB", "TMT_ReporterIonIntensities"));
    }
    
    // test TMT10
    // Label  Mass        2plex  6plex  10plex
    // 126    126.12773   1      1      1
    // 127N   127.12476   0      0      1
    // 127C   127.13108   1      1      1
    // 128N   128.12811   0      0      1
    // 128C   128.13443   0      1      1
    // 129N   129.13147   0      0      1
    // 129C   129.13779   0      1      1
    // 130N   130.13482   0      0      1
    // 130C   130.14114   0      1      1
    // 131    131.13818   0      1      1
    {
        MZTolerance ht = MZTolerance(10, MZTolerance::PPM);
        vector<double> mzArray; mzArray += 100, 126.12773-ht, 127.12476+ht, 127.13108-ht, 128.12811, 128.13443-ht, 129.13147+ht, 129.13779-ht, 130.13482-ht, 130.14114+ht, 131.13818, 200, 300, 400, 500, 600, 700;
        vector<double> inArray; inArray +=  10, 126,          127.1,        127.2,        128.1,     128.2,        129.1,        129.2,        130.1,        130.2,        131,       20,  30,  40,  30,  20,  10;
        bfs::remove("testEmbedder.mzML");
        createExampleWithArrays(mzArray, inArray);

        quantitationMethodBySource[1] = Embedder::QuantitationConfiguration(QuantitationMethod::TMT10plex, MZTolerance(20, MZTolerance::PPM), false);
        Embedder::embed("testEmbedder.idpDB", "testEmbedder.dir", quantitationMethodBySource);
        //                                                         126  127N   127C   128N   128C   129N   129C   130N   130C   131
        vector<double> expectedIntensities; expectedIntensities += 126, 127.1, 127.2, 128.1, 128.2, 129.1, 129.2, 130.1, 130.2, 131;
        unit_assert_operator_equal("", testReporterIons(expectedIntensities, "testEmbedder.idpDB", "TMT_ReporterIonIntensities"));
    }
    
    // test again with normalization
    {
        vector<double> mzArray; mzArray += 100, 126.128, 127.131, 128.134, 129.132, 130.143, 131.137, 200, 300, 400, 500, 600, 700;
        vector<double> inArray; inArray +=  10, 10,      90,      20,      100,     10,      1,       20,  30,  40,  30,  20,  10;
        bfs::remove("testEmbedder.mzML");
        createExampleWithArrays(mzArray, inArray);

        quantitationMethodBySource[1] = Embedder::QuantitationConfiguration(QuantitationMethod::TMT6plex);
        Embedder::embed("testEmbedder.idpDB", "testEmbedder.dir", quantitationMethodBySource);
        //                                                         126  127N 127C 128N 128C 129N 129C 130N 130C 131
        vector<double> expectedIntensities; expectedIntensities += 100, 0,   100, 0,   100, 0,   100, 0,   100, 100;
        unit_assert_operator_equal("", testReporterIons(expectedIntensities, "testEmbedder.idpDB", "TMT_ReporterIonIntensities"));
    }

    // remove test files
    bfs::remove("testEmbedder.dir/testEmbedder.mzML");
    bfs::remove("testEmbedder.dir");


    // test gene metadata embedding
    unit_assert(!Embedder::hasGeneMetadata("testEmbedder.idpDB"));
    Embedder::embedGeneMetadata("testEmbedder.idpDB");
    unit_assert(Embedder::hasGeneMetadata("testEmbedder.idpDB"));
    Embedder::dropGeneMetadata("testEmbedder.idpDB");
    unit_assert(!Embedder::hasGeneMetadata("testEmbedder.idpDB"));

    bfs::remove("testEmbedder.mz5");
    bfs::remove("testEmbedder.idpDB");
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        // change to test executable's directory so that embedGeneMetadata can find gene2protein.db3
        bfs::current_path(bfs::path(argv[0]).parent_path());

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
