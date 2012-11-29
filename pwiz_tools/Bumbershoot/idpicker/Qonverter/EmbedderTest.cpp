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

const ExampleDBSequence HSP71_RAT = {"HSP71_RAT",     "MAKKTAIGIDLGTTYSCVGVFQHGKVEIIANDQGNRTTPSYVAFTDTERLIGDAAKNQVALNPQNTVFDAKRLIGRKFGDPVVQSDMKHWPFQVVNDGDKPKVQVNYKGENRSFYPEEISSMVLTKMKEIAEAYLGHPVTNAVITVPAYFNDSQRQATKDAGVIAGLNVLRIINEPTAAAIAYGLDRTGKGERNVLIFDLGGGTFDVSILTIDDGIFEVKATAGDTHLGGEDFDNRLVSHFVEEFKRKHKKDISQNKRAVRRLRTACERAKRTLSSSTQASLEIDSLFEGIDFYTSITRARFEELCSDLFRGTLEPVEKALRDAKLDKAQIHDLVLVGGSTRIPKVQKLLQDFFNGRDLNKSINPDEAVAYGAAVQAAILMGDKSENVQDLLLLDVAPLSLGLETAGGVMTALIKRNSTIPTKQTQTFTTYSDNQPGVLIQVYEGERAMTRDNNLLGRFELSGIPPAPRGVPQIEVTFDIDANGILNVTATDKSTGKANKITITNDKGRLSKEEIERMVQEAERYKAEDEVQRERVAAKNALESYAFNMKSAVEDEGLKGKISEADKKKVLDKCQEVISWLDSNTLAEKEEFVHKREELERVCNPIISGLYQGAGAPGAGGFGAQAPKGGSGSGPTIEEVD", "Heat shock 70 kDa protein 1A/1B (Heat shock 70 kDa protein 1/2) (HSP70.1/2) - Rattus norvegicus (Rat)"};
const ExampleDBSequence HSP71_HUMAN = {"HSP71_HUMAN", "MAKAAAIGIDLGTTYSCVGVFQHGKVEIIANDQGNRTTPSYVAFTDTERLIGDAAKNQVALNPQNTVFDAKRLIGRKFGDPVVQSDMKHWPFQVINDGDKPKVQVSYKGETKAFYPEEISSMVLTKMKEIAEAYLGYPVTNAVITVPAYFNDSQRQATKDAGVIAGLNVLRIINEPTAAAIAYGLDRTGKGERNVLIFDLGGGTFDVSILTIDDGIFEVKATAGDTHLGGEDFDNRLVNHFVEEFKRKHKKDISQNKRAVRRLRTACERAKRTLSSSTQASLEIDSLFEGIDFYTSITRARFEELCSDLFRSTLEPVEKALRDAKLDKAQIHDLVLVGGSTRIPKVQKLLQDFFNGRDLNKSINPDEAVAYGAAVQAAILMGDKSENVQDLLLLDVAPLSLGLETAGGVMTALIKRNSTIPTKQTQIFTTYSDNQPGVLIQVYEGERAMTKDNNLLGRFELSGIPPAPRGVPQIEVTFDIDANGILNVTATDKSTGKANKITITNDKGRLSKEEIERMVQEAEKYKAEDEVQRERVSAKNALESYAFNMKSAVEDEGLKGKISEADKKKVLDKCQEVISWLDANTLAEKDEFEHKRKELEQVCNPIISGLYQGAGGPGPGGFGAQGPKGGSGSGPTIEEVD", "Heat shock 70 kDa protein 1A/1B OS=Homo sapiens GN=HSPA1A PE=1 SV=5"};

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
        pl->proteins.push_back(ProteinPtr(new Protein(decoyPrefix + HSP71_RAT.accession, 0, HSP71_RAT.description, HSP71_RAT.sequence)));
        pl->proteins.push_back(ProteinPtr(new Protein(HSP71_HUMAN.accession, 1, HSP71_HUMAN.description, HSP71_HUMAN.sequence)));
        ProteomeDataFile::write(pdd, "testEmbedder.fasta");
    }

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
        sfp->set(MS_Thermo_RAW_file);
        sfp->set(MS_Thermo_nativeID_format);
        msd.fileDescription.sourceFilePtrs.push_back(sfp);

        InstrumentConfigurationPtr instrumentConfigurationPtr(new InstrumentConfiguration("LCQ Deca"));
        instrumentConfigurationPtr->set(MS_LCQ_Deca);
        instrumentConfigurationPtr->componentList.push_back(Component(MS_nanoelectrospray, 1));
        instrumentConfigurationPtr->componentList.push_back(Component(MS_quadrupole_ion_trap, 2));
        instrumentConfigurationPtr->componentList.push_back(Component(MS_electron_multiplier, 3));

        msd.instrumentConfigurationPtrs.push_back(instrumentConfigurationPtr);

        vector<double> mzArray; mzArray += 100, 113.1, 114.1, 115.15, 115.18, 116.05, 200, 300, 400, 500, 600, 700;
        vector<double> intensityArray; intensityArray += 10, 3.12, 4.2, 2.4, 42.0, 24.42, 20, 20, 40, 1, 5, 3;

        SpectrumListSimple* sl = new SpectrumListSimple;
        msd.run.spectrumListPtr.reset(sl);
        for (size_t i=0; i < 500; ++i)
        {
            sl->spectra.push_back(SpectrumPtr(new Spectrum));
            sl->spectra.back()->id = "controllerType=0 controllerNumber=1 scan=" + lexical_cast<string>(i+1);
            sl->spectra.back()->index = i;
            sl->spectra.back()->set((i%10 == 0) ? MS_MS1_spectrum : MS_MSn_spectrum);
            sl->spectra.back()->set(MS_ms_level, (i%10 == 0) ? 1 : 2);
            sl->spectra.back()->set(MS_centroid_spectrum);
            sl->spectra.back()->setMZIntensityArrays(mzArray, intensityArray, MS_number_of_counts);
            sl->spectra.back()->scanList.scans.push_back(Scan());
            sl->spectra.back()->scanList.scans[0].set(MS_scan_start_time, i / 100.0, UO_second);
        }

        bfs::create_directories("testEmbedder.dir");
        MSDataFile::write(msd, "testEmbedder.dir/testEmbedder.mzML");
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

    // run embed on intermediate mzML
    map<int, QuantitationMethod> quantitationMethodBySource;
    quantitationMethodBySource[1] = QuantitationMethod::ITRAQ4plex;
    Embedder::embed("testEmbedder.idpDB", "testEmbedder.dir", quantitationMethodBySource);

    // remove test files
    bfs::remove("testEmbedder.dir/testEmbedder.mzML");
    bfs::remove("testEmbedder.dir");

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

    bfs::remove("testEmbedder.mz5");
    bfs::remove("testEmbedder.idpDB");
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
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
