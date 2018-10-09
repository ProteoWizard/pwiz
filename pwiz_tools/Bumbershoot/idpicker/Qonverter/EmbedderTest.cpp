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
#include "boost/variant.hpp"
#include "boost/variant/get.hpp"
#include "boost/bind.hpp"
#include "Embedder.hpp"
#include "SchemaUpdater.hpp"
#include "Parser.hpp"
#include "sqlite3pp.h"


using namespace pwiz::cv;
using namespace pwiz::util;
using namespace pwiz::chemistry;
using namespace boost::assign;
using namespace IDPicker;
namespace sqlite = sqlite3pp;


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
        if (reporterIonCount > 0) unit_assert_equal(expectedIntensities[0], reporterIons[0], 1e-5);
        if (reporterIonCount > 1) unit_assert_equal(expectedIntensities[1], reporterIons[1], 1e-5);
        if (reporterIonCount > 2) unit_assert_equal(expectedIntensities[2], reporterIons[2], 1e-5);
        if (reporterIonCount > 3) unit_assert_equal(expectedIntensities[3], reporterIons[3], 1e-5);
        if (reporterIonCount > 4) unit_assert_equal(expectedIntensities[4], reporterIons[4], 1e-5);
        if (reporterIonCount > 5) unit_assert_equal(expectedIntensities[5], reporterIons[5], 1e-5);
        if (reporterIonCount > 6) unit_assert_equal(expectedIntensities[6], reporterIons[6], 1e-5);
        if (reporterIonCount > 7) unit_assert_equal(expectedIntensities[7], reporterIons[7], 1e-5);
        if (reporterIonCount > 8) unit_assert_equal(expectedIntensities[8], reporterIons[8], 1e-5);
        if (reporterIonCount > 9) unit_assert_equal(expectedIntensities[9], reporterIons[9], 1e-5);
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
    /*{
        vector<double> mzArray; mzArray += 100, 126.128, 127.131, 128.134, 129.132, 130.143, 131.137, 200, 300, 400, 500, 600, 700;
        vector<double> inArray; inArray +=  10, 10,      90,      20,      100,     10,      1,       20,  30,  40,  30,  20,  10;
        bfs::remove("testEmbedder.mzML");
        createExampleWithArrays(mzArray, inArray);

        quantitationMethodBySource[1] = Embedder::QuantitationConfiguration(QuantitationMethod::TMT6plex);
        Embedder::embed("testEmbedder.idpDB", "testEmbedder.dir", quantitationMethodBySource);
        //                                                         126  127N 127C 128N 128C 129N 129C 130N 130C 131
        vector<double> expectedIntensities; expectedIntensities += 92.8333, 0, 92.8333, 0, 92.8333, 0, 92.8333, 0, 92.8333, 92.8333;
        unit_assert_operator_equal("", testReporterIons(expectedIntensities, "testEmbedder.idpDB", "TMT_ReporterIonIntensities"));
    }*/

    // remove test files
    bfs::remove("testEmbedder.dir/testEmbedder.mzML");
    bfs::remove("testEmbedder.dir");


    // test gene metadata embedding
    unit_assert(!Embedder::hasGeneMetadata("testEmbedder.idpDB"));
    Embedder::embedGeneMetadata("testEmbedder.idpDB");
    unit_assert(Embedder::hasGeneMetadata("testEmbedder.idpDB"));
    Embedder::dropGeneMetadata("testEmbedder.idpDB");
    unit_assert(!Embedder::hasGeneMetadata("testEmbedder.idpDB"));


    // test isobaric sample mapping
    {
        // create dummy test sources and groups
        {
            sqlite3pp::database idpDb("testEmbedder.idpDB");

            sqlite3pp::command addSpectrumSource(idpDb, "INSERT INTO SpectrumSource (Id, Name, Group_, QuantitationMethod) VALUES (?,?,?,?)");
            sqlite3pp::command addSpectrumSourceGroup(idpDb, "INSERT INTO SpectrumSourceGroup (Id, Name) VALUES (?,?)");

            addSpectrumSource.binder() << 2 << "A12_B34_C56_Ref_f01" << 2 << (int) QuantitationMethod::ITRAQ4plex;
            addSpectrumSource.step();
            addSpectrumSource.reset();

            addSpectrumSource.binder() << 3 << "A12_B34_C56_Ref_f02" << 2 << (int) QuantitationMethod::ITRAQ4plex;
            addSpectrumSource.step();
            addSpectrumSource.reset();

            addSpectrumSource.binder() << 4 << "Ref_D78_E90_F12_f01" << 3 << (int) QuantitationMethod::ITRAQ4plex;
            addSpectrumSource.step();
            addSpectrumSource.reset();

            addSpectrumSource.binder() << 5 << "Ref_D78_E90_F12_f02" << 3 << (int) QuantitationMethod::ITRAQ4plex;
            addSpectrumSource.step();
            addSpectrumSource.reset();

            addSpectrumSourceGroup.binder() << 2 << "A12_B34_C56";
            addSpectrumSourceGroup.step();
            addSpectrumSourceGroup.reset();

            addSpectrumSourceGroup.binder() << 3 << "D78_E90_F12";
            addSpectrumSourceGroup.step();
            addSpectrumSourceGroup.reset();
        }

        map<string, vector<string> > testMapping;
        testMapping["A12_B34_C56"] += "A12", "B34", "C56", "Reference";
        testMapping["D78_E90_F12"] += "Reference", "D78", "E90", "F12";
        Embedder::embedIsobaricSampleMapping("testEmbedder.idpDB", testMapping);

        map<string, vector<string> > result = Embedder::getIsobaricSampleMapping("testEmbedder.idpDB");
        unit_assert_operator_equal(2, result.size());
        unit_assert_operator_equal(4, result["A12_B34_C56"].size());
        unit_assert_operator_equal(4, result["D78_E90_F12"].size());
        unit_assert_operator_equal("A12", result["A12_B34_C56"][0]);
        unit_assert_operator_equal("B34", result["A12_B34_C56"][1]);
        unit_assert_operator_equal("C56", result["A12_B34_C56"][2]);
        unit_assert_operator_equal("Reference", result["A12_B34_C56"][3]);
        unit_assert_operator_equal("Reference", result["D78_E90_F12"][0]);
        unit_assert_operator_equal("D78", result["D78_E90_F12"][1]);
        unit_assert_operator_equal("E90", result["D78_E90_F12"][2]);
        unit_assert_operator_equal("F12", result["D78_E90_F12"][3]);
    }


    bfs::remove("testEmbedder.mz5");
    bfs::remove("testEmbedder.idpDB");
}


struct VariantRowVisitor : boost::static_visitor<> { template <typename T> void operator()(const T& t, sqlite::statement::bindstream& os) const { os << t; } };
void testGeneMapping()
{
    if (bfs::exists("embedderDoctest.idpDB")) bfs::remove("embedderDoctest.idpDB");
    sqlite::database db("embedderDoctest.idpDB");
    db.load_extension("IdpSqlExtensions");

    db.execute("CREATE TABLE About AS SELECT " + lexical_cast<string>(CURRENT_SCHEMA_REVISION) + " AS SchemaRevision;"
               "CREATE TABLE IF NOT EXISTS Protein(Id INTEGER PRIMARY KEY, Accession TEXT, IsDecoy INT, Cluster INT, ProteinGroup INT, Length INT, GeneId TEXT, GeneGroup INT);"
               "CREATE TABLE IF NOT EXISTS ProteinMetadata (Id INTEGER PRIMARY KEY, Description TEXT, Hash BLOB, TaxonomyId INT, GeneName TEXT, Chromosome TEXT, GeneFamily TEXT, GeneDescription TEXT);"
               "CREATE TABLE IF NOT EXISTS FilterHistory (Id INTEGER PRIMARY KEY, MaximumQValue NUMERIC, MinimumDistinctPeptides INT, MinimumSpectra INT,  MinimumAdditionalPeptides INT, GeneLevelFiltering INT, PrecursorMzTolerance TEXT,\n"
               "                                          DistinctMatchFormat TEXT, MinimumSpectraPerDistinctMatch INT, MinimumSpectraPerDistinctPeptide INT, MaximumProteinGroupsPerPeptide INT,\n"
               "                                          Clusters INT, ProteinGroups INT, Proteins INT, GeneGroups INT, Genes INT, DistinctPeptides INT, DistinctMatches INT, FilteredSpectra INT, ProteinFDR NUMERIC, PeptideFDR NUMERIC, SpectrumFDR NUMERIC);"
               );

    auto proteins = vector<vector<boost::variant<int, const char*>>>
    {
        // A2M     alpha-2-macroglobulin     12p13.31
        { 1, "NP_000005.2", 0 },
        { 2, "NP_001334352", 0 },
        { 3, "P01023", 0 },
        { 4, "ENSP00000323929", 0 },
        { 5, "ENSP00000385710_F123R,M234F,T1234S", 0 },

        // ABCA2     ATP binding cassette subfamily A member 2    9q34.3      ABCA
        { 6, "generic|NP_997698.1", 0 },
        { 7, "XP_006717059", 0 },
        { 8, "generic|ENSP00000344155", 0 },
        { 9, "sp|Q9BZC7", 0 },
        { 10, "NP_997698_F123R", 0 },

        // Abca1     ATP-binding cassette, sub-family A (ABC1), member 1     4:53030787-53159895
        { 11, "gi|123456|ref|NP_038482.3", 0 },
        { 12, "ENSMUSP00000030010", 0 },
        { 13, "P41233_F123R", 0 },

        // decoys
        { 14, "rev_NP_038482", 1 },
        { 15, "DECOY_ENSP00000344155_F123R", 1 },
        { 16, "r-P41233", 1 }
    };

    sqlite::command proteinInsert(db, "INSERT INTO Protein (Id, Accession, IsDecoy, Cluster, ProteinGroup, Length, GeneId, GeneGroup) VALUES (?, ?, ?, 1, 1, 123, NULL, NULL)");
    sqlite::command proteinMetadataInsert(db, "INSERT INTO ProteinMetadata (Id) VALUES (?)");
    for (const auto& proteinRow : proteins)
    {
        auto binder = proteinInsert.binder();
        for (size_t i = 0; i < proteinRow.size(); ++i)
            boost::apply_visitor(boost::bind(VariantRowVisitor(), _1, boost::ref(binder)), proteinRow[i]);
        proteinInsert.step();
        proteinInsert.reset();

        proteinMetadataInsert.bind(0, boost::get<int>(proteinRow[0]));
        proteinMetadataInsert.step();
        proteinMetadataInsert.reset();
    }

    Embedder::embedGeneMetadata("embedderDoctest.idpDB");

    sqlite::query q(db, "SELECT GeneId, CAST(TaxonomyId AS TEXT), GeneName, Chromosome, IFNULL(GeneFamily, '') FROM Protein pro, ProteinMetadata pmd WHERE pro.Id=pmd.Id");
    auto itr = q.begin();

    for (int i = 0; i < 5; ++i) // 0-4 are A2M
    {
        int column = 0;
        for (const string& value : vector<string>{ "A2M", "9606", "alpha-2-macroglobulin", "12p13.31", "" })
        {
            CHECK(itr->get<string>(column++) == value);
        }
        ++itr;
    }

    for (int i = 0; i < 5; ++i) // 5-9 are ABCA2
    {
        int column = 0;
        for (const string& value : vector<string>{ "ABCA2", "9606", "ATP binding cassette subfamily A member 2", "9q34.3", "ABCA" })
        {
            CHECK(itr->get<string>(column++) == value);
        }
        ++itr;
    }

    for (int i = 0; i < 3; ++i) // 10-12 are Abca1
    {
        int column = 0;
        for (const string& value : vector<string>{ "Abca1", "10090", "ATP-binding cassette, sub-family A (ABC1), member 1", "4:53030787-53159895", "" })
        {
            CHECK(itr->get<string>(column++) == value);
        }
        ++itr;
    }

    for (int i = 0; i < 3; ++i) // 13-15 are decoys
    {
        int column = 0;
        for (const auto& value : vector<sqlite::null_type>(5))
        {
            CHECK(itr->get<sqlite::null_type>(column++) == value);
        }
        ++itr;
    }
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        // change to test executable's directory so that embedGeneMetadata can find gene2protein.db3
        bfs::current_path(bfs::path(argv[0]).parent_path());

        test();
		testGeneMapping();
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
