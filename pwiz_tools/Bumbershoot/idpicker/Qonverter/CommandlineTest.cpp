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
// Copyright 2014 Vanderbilt University
//
// Contributor(s):
//

#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <boost/foreach_field.hpp>
#include "pwiz/utility/chemistry/Ion.hpp"
#include "pwiz/data/proteome/Digestion.hpp"
#include "pwiz/data/proteome/AminoAcid.hpp"
#include "pwiz/utility/misc/SHA1Calculator.hpp"
#include "Embedder.hpp"
#include <boost/assign/list_of.hpp> // for 'list_of()'
#include <boost/assign.hpp>
#include <boost/range/adaptor/transformed.hpp>
#include <boost/thread.hpp>
#include <sqlite3pp.h>


using namespace pwiz::proteome;
using namespace pwiz::chemistry;
using namespace pwiz::util;
using namespace boost::assign;


inline std::string unit_assert_exception_thrown_message(const char* filename, int line, const char* expression, const std::string& exception)
{
    std::ostringstream oss;
    oss << "[" << filename << ":" << line << "] Assertion \"" << expression << "\" was not expected to throw, but threw " << exception;
    return oss.str();
}

#define unit_assert_does_not_throw(x, exception) \
    { \
        bool threw = false; \
        try { (x); } \
        catch (exception&) \
        { \
            threw = true; \
        } \
        if (threw) \
            throw std::runtime_error(unit_assert_exception_thrown_message(__FILE__, __LINE__, #x, #exception)); \
    }


#ifdef WIN32
const char* commandQuote = "\""; // workaround for weird behavior with Win32's system() call, which needs quotes around the entire command-line if the command-line has quoted arguments (like filepaths with spaces)
#else
const char* commandQuote = "";
#endif


// find filenames or file extensions matching trailingFilename
vector<size_t> findTrailingFilename(const string& trailingFilename, const vector<string>& args)
{
    vector<size_t> matches;
    for (size_t i=0; i < args.size(); ++i)
        if (bal::iends_with(args[i], trailingFilename))
            matches.push_back(i);
    return matches;
}

size_t findOneFilename(const string& filename, const vector<string>& args)
{
    vector<size_t> matches = findTrailingFilename(filename, args);
    if (matches.empty()) throw runtime_error("[findOneFilename] No match for filename \"" + filename + "\"");
    if (matches.size() > 1) throw runtime_error("[findOneFilename] More than one match for filename \"" + filename + "\"");
    return matches[0];
}

struct path_stringer
{
    typedef string result_type;
    result_type operator()(const bfs::path& x) const { return x.string(); }
};

int testCommand(string command)
{
    bal::replace_all(command, "idpQonvert.exe\"", "idpQonvert.exe\" -LogFilepath test.log ");
    bal::replace_all(command, "idpAssemble.exe\"", "idpAssemble.exe\" -LogFilepath test.log ");

    cout << "Running command: " << command << endl;
    { ofstream outputLog("output.log", ios::app); outputLog << command << endl; }

    bpt::ptime start = bpt::microsec_clock::universal_time();
    int result = bnw::system((command + " >> output.log").c_str());
    cout << endl << "Returned exit code " << result << "; time elapsed " << bpt::to_simple_string(bpt::microsec_clock::universal_time() - start) << endl;
    { ofstream outputLog("output.log", ios::app); outputLog << endl << "Returned exit code " << result << "; time elapsed " << bpt::to_simple_string(bpt::microsec_clock::universal_time() - start) << endl; }

    return result;
}


void testIdpQonvert(const string& idpQonvertPath, const bfs::path& testDataPath)
{
    // clean up existing intermediate files
    vector<bfs::path> intermediateFiles;
    pwiz::util::expand_pathmask(testDataPath / "*.idpDB", intermediateFiles);
    pwiz::util::expand_pathmask(testDataPath / "broken.pepXML", intermediateFiles);
    for(const bfs::path& intermediateFile : intermediateFiles)
        bfs::remove(intermediateFile);

    vector<bfs::path> idFiles;
    pwiz::util::expand_pathmask(testDataPath / "*.pep.xml", idFiles);
    pwiz::util::expand_pathmask(testDataPath / "*.pepXML", idFiles);
    pwiz::util::expand_pathmask(testDataPath / "*.mzid", idFiles);
    pwiz::util::expand_pathmask(testDataPath / "*.dat", idFiles);
    if (idFiles.empty())
        throw runtime_error("[testIdpQonvert] No identification files found in test path \"" + testDataPath.string() + "\"");


    // <idpQonvertPath> <matchPaths>
    string command = (format("%1%\"%2%\" \"%3%\" -DecoyPrefix \"\"%1%") % commandQuote % idpQonvertPath % bal::join(idFiles | boost::adaptors::transformed(path_stringer()), "\" \"")).str();
    unit_assert_operator_equal(0, testCommand(command));

    vector<bfs::path> idpDbFiles;
    for(const bfs::path& idFile : idFiles)
    {
        idpDbFiles.push_back(idFile);
        string idpDbFilepath = bal::replace_all_copy(idpDbFiles.back().string(), ".pep.xml", ".pepXML");
        idpDbFiles.back() = bfs::path(idpDbFilepath).replace_extension(".idpDB");

        unit_assert(bfs::exists(idpDbFiles.back()));
        sqlite3pp::database db(idpDbFiles.back().string());
        unit_assert_does_not_throw(sqlite3pp::query(db, "SELECT * FROM IntegerSet").begin(), sqlite3pp::database_error);

        // test analysis name differentiation
        string softwareName = sqlite3pp::query(db, "SELECT SoftwareName FROM Analysis").begin()->get<string>(0);
        string analysisName = sqlite3pp::query(db, "SELECT Name FROM Analysis").begin()->get<string>(0);
        if (softwareName == "MyriMatch")
        {
            unit_assert(bal::contains(analysisName, "MinTerminiCleavages"));
            if (bal::contains(analysisName, "MinTerminiCleavages=1"))
                unit_assert_operator_equal("MyriMatch 2.2.140 (MinTerminiCleavages=1, MonoPrecursorMzTolerance=50ppm, PrecursorMzToleranceRule=mono, parent tolerance minus value=50.0 ppm, parent tolerance plus value=50.0 ppm)", analysisName);
            else
                unit_assert_operator_equal("MyriMatch 2.2.140 (MinTerminiCleavages=0, MonoPrecursorMzTolerance=20ppm, PrecursorMzToleranceRule=auto, parent tolerance minus value=20.0 ppm, parent tolerance plus value=20.0 ppm)", analysisName);
        }
        else if (softwareName == "MS-GF+")
        {
            unit_assert(bal::contains(analysisName, "Instrument"));
            if (bal::contains(analysisName, "Instrument=LowRes"))
                unit_assert_operator_equal("MS-GF+ Beta (v10072) (FragmentMethod=As written in the spectrum or CID if no info, Instrument=LowRes, NumTolerableTermini=0, parent tolerance minus value=20.0 ppm, parent tolerance plus value=20.0 ppm)", analysisName);
            else
                unit_assert_operator_equal("MS-GF+ Beta (v10072) (FragmentMethod=HCD, Instrument=QExactive, NumTolerableTermini=1, parent tolerance minus value=50.0 ppm, parent tolerance plus value=50.0 ppm)", analysisName);
        }
        else if (softwareName == "Comet")
        {
            unit_assert(bal::contains(analysisName, "fragment_bin_tol"));
            if (bal::contains(analysisName, "fragment_bin_tol=1.000500"))
                unit_assert_operator_equal("Comet 2014.02 (fragment tolerance minus value=1.000500 u, fragment tolerance plus value=1.000500 u, fragment_bin_offset=0.400000, fragment_bin_tol=1.000500, theoretical_fragment_ions=1)", analysisName);
            else
                unit_assert_operator_equal("Comet 2014.02 (fragment tolerance minus value=0.020000 u, fragment tolerance plus value=0.020000 u, fragment_bin_offset=0.020000, fragment_bin_tol=0.020000, theoretical_fragment_ions=0)", analysisName);
        }
        else if (softwareName == "Mascot")
        {
            unit_assert(bal::contains(analysisName, "fragment tolerance minus value"));
            if (bal::contains(analysisName, "fragment tolerance minus value=0.6 u"))
                unit_assert_operator_equal("Mascot 2.2.06 (fragment tolerance minus value=0.6 u, fragment tolerance plus value=0.6 u, parent tolerance minus value=20.0 ppm, parent tolerance plus value=20.0 ppm)", analysisName);
            else
                unit_assert_operator_equal("Mascot 2.2.06 (fragment tolerance minus value=0.1 u, fragment tolerance plus value=0.1 u, parent tolerance minus value=50.0 ppm, parent tolerance plus value=50.0 ppm)", analysisName);
        } else
            throw runtime_error("[testIdpQonvert] Software name is not one of the expected values.");
    }


    // test overwrite of existing idpDBs (should succeed, but idpDBs should be unmodified since idpDBs exist)
    cout << endl << command << endl;
    string oldHash = SHA1Calculator::hashFile(idpDbFiles[0].string());
    unit_assert_operator_equal(0, testCommand(command));
    unit_assert_operator_equal(oldHash, SHA1Calculator::hashFile(idpDbFiles[0].string()));

    {
        // test embedding gene metadata in existing idpDB
        command = (format("%1%\"%2%\" \"%3%\" -DecoyPrefix \"\" -EmbedGeneMetadata 1%1%") % commandQuote % idpQonvertPath % idpDbFiles[0].string()).str();
        unit_assert_operator_equal(0, testCommand(command));

        sqlite3pp::database db(idpDbFiles[0].string());
        unit_assert(sqlite3pp::query(db, "SELECT GeneId FROM Protein").begin()->get<string>(0).length() > 0);
    }

    {
        // test embedding gene metadata while overwriting existing idpDBs (should succeed with OverwriteExistingFiles=1)
        command = (format("%1%\"%2%\" \"%3%\" -DecoyPrefix \"\" -OverwriteExistingFiles 1 -EmbedGeneMetadata 1%1%") % commandQuote % idpQonvertPath % bal::join(idFiles | boost::adaptors::transformed(path_stringer()), "\" \"")).str();
        unit_assert_operator_equal(0, testCommand(command));

        sqlite3pp::database db(idpDbFiles[0].string());
        unit_assert(sqlite3pp::query(db, "SELECT GeneId FROM Protein").begin()->get<string>(0).length() > 0);
    }

    {
        // test overwrite of existing idpDBs (should succeed with OverwriteExistingFiles=1, idpDB file hashes should not match due to timestamp difference, gene metadata should be gone)
        command = (format("%1%\"%2%\" \"%3%\" -DecoyPrefix \"\" -OverwriteExistingFiles 1%1%") % commandQuote % idpQonvertPath % bal::join(idFiles | boost::adaptors::transformed(path_stringer()), "\" \"")).str();
        unit_assert_operator_equal(0, testCommand(command));

        sqlite3pp::database db(idpDbFiles[0].string());
        unit_assert(sqlite3pp::query(db, "SELECT GeneId FROM Protein").begin()->get<string>(0).length() == 0);
    }

    {
        // test ONLY embedding gene metadata in existing idpDB; the bogus DecoyPrefix should be ignored
        command = (format("%1%\"%2%\" \"%3%\" -DecoyPrefix XYZ -EmbedGeneMetadata 1 -EmbedOnly 1%1%") % commandQuote % idpQonvertPath % idpDbFiles[0].string()).str();
        unit_assert_operator_equal(0, testCommand(command));

        sqlite3pp::database db(idpDbFiles[0].string());
        unit_assert(sqlite3pp::query(db, "SELECT GeneId FROM Protein").begin()->get<string>(0).length() > 0);
    }

    {
        // create a broken pepXML and check that errors or skipping errors are handled as intended
        string brokenPepXmlFilename = (testDataPath / "broken.pepXML").string();
        ofstream brokenPepXML(brokenPepXmlFilename.c_str());
        brokenPepXML << "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>\n<msms_pipeline_analysis></msms_pipeline_analysis>" << endl;
        brokenPepXML.close();

        command = (format("%1%\"%2%\" \"%3%\" -DecoyPrefix \"\"%1%") % commandQuote % idpQonvertPath % brokenPepXmlFilename).str();
        unit_assert(0 < testCommand(command));

        command = (format("%1%\"%2%\" \"%3%\" -DecoyPrefix \"\" -SkipSourceOnError 1%1%") % commandQuote % idpQonvertPath % brokenPepXmlFilename).str();
        unit_assert_operator_equal(0, testCommand(command));
    }


    // test qonversion of existing idpDB


    // test embedding scan times
    {
        // test it when importing pepXML
        {
            command = (format("%1%\"%2%\" \"%3%\" -DecoyPrefix \"\" -OverwriteExistingFiles 1 -EmbedSpectrumScanTimes 1%1%") % commandQuote % idpQonvertPath % bal::join(idFiles | boost::adaptors::transformed(path_stringer()), "\" \"")).str();
            unit_assert_operator_equal(0, testCommand(command));

            sqlite3pp::database db(idpDbFiles[0].string());
            unit_assert(sqlite3pp::query(db, "SELECT COUNT(*) FROM Spectrum WHERE ScanTimeInSeconds > 0").begin()->get<int>(0) > 0);
        }

        // test it on an existing idpDB
        {
            command = (format("%1%\"%2%\" \"%3%\" -EmbedSpectrumScanTimes 1 -EmbedOnly 1%1%") % commandQuote % idpQonvertPath % idpDbFiles[0]).str();
            unit_assert_operator_equal(0, testCommand(command));

            sqlite3pp::database db(idpDbFiles[0].string());
            unit_assert(sqlite3pp::query(db, "SELECT COUNT(*) FROM Spectrum WHERE ScanTimeInSeconds > 0").begin()->get<int>(0) > 0);
        }
    }


    // test embedding spectra
    {
        // test it when importing pepXML
        {
            command = (format("%1%\"%2%\" \"%3%\" -DecoyPrefix \"\" -OverwriteExistingFiles 1 -EmbedSpectrumSources 1%1%") % commandQuote % idpQonvertPath % bal::join(idFiles | boost::adaptors::transformed(path_stringer()), "\" \"")).str();
            unit_assert_operator_equal(0, testCommand(command));

            sqlite3pp::database db(idpDbFiles[0].string());
            unit_assert(sqlite3pp::query(db, "SELECT COUNT(*) FROM Spectrum WHERE ScanTimeInSeconds > 0").begin()->get<int>(0) > 0);
            unit_assert(sqlite3pp::query(db, "SELECT COUNT(*) FROM SpectrumSourceMetadata WHERE MsDataBytes IS NOT NULL").begin()->get<int>(0) > 0);
        }

        // test it on an existing idpDB
        {
            command = (format("%1%\"%2%\" \"%3%\" -EmbedSpectrumSources 1 -EmbedOnly 1%1%") % commandQuote % idpQonvertPath % idpDbFiles[0]).str();
            unit_assert_operator_equal(0, testCommand(command));

            sqlite3pp::database db(idpDbFiles[0].string());
            unit_assert(sqlite3pp::query(db, "SELECT COUNT(*) FROM Spectrum WHERE ScanTimeInSeconds > 0").begin()->get<int>(0) > 0);
            unit_assert(sqlite3pp::query(db, "SELECT COUNT(*) FROM SpectrumSourceMetadata WHERE MsDataBytes IS NOT NULL").begin()->get<int>(0) > 0);
        }
    }


    // test embedding quantitation
    // TMT2plex
    {
        command = (format("%1%\"%2%\" \"%3%\" -DecoyPrefix \"\" -OverwriteExistingFiles 1 -QuantitationMethod TMT2plex%1%") % commandQuote % idpQonvertPath % bal::join(idFiles | boost::adaptors::transformed(path_stringer()), "\" \"")).str();
        unit_assert_operator_equal(0, testCommand(command));

        sqlite3pp::database db(idpDbFiles[0].string());
        unit_assert(sqlite3pp::query(db, "SELECT COUNT(*) FROM SpectrumSource WHERE QuantitationMethod > 0").begin()->get<int>(0) > 0);
        unit_assert(sqlite3pp::query(db, "SELECT COUNT(*) FROM SpectrumQuantitation WHERE TMT_ReporterIonIntensities IS NOT NULL").begin()->get<int>(0) > 0);
    }

    // TMT6plex
    {
        command = (format("%1%\"%2%\" \"%3%\" -DecoyPrefix \"\" -OverwriteExistingFiles 1 -QuantitationMethod TMT6plex%1%") % commandQuote % idpQonvertPath % bal::join(idFiles | boost::adaptors::transformed(path_stringer()), "\" \"")).str();
        unit_assert_operator_equal(0, testCommand(command));

        sqlite3pp::database db(idpDbFiles[0].string());
        unit_assert(sqlite3pp::query(db, "SELECT COUNT(*) FROM SpectrumSource WHERE QuantitationMethod > 0").begin()->get<int>(0) > 0);
        unit_assert(sqlite3pp::query(db, "SELECT COUNT(*) FROM SpectrumQuantitation WHERE TMT_ReporterIonIntensities IS NOT NULL").begin()->get<int>(0) > 0);
    }

    // TMT10plex
    {
        command = (format("%1%\"%2%\" \"%3%\" -DecoyPrefix \"\" -OverwriteExistingFiles 1 -QuantitationMethod TMT10plex -ReporterIonMzTolerance 0.003mz%1%") % commandQuote % idpQonvertPath % bal::join(idFiles | boost::adaptors::transformed(path_stringer()), "\" \"")).str();
        unit_assert_operator_equal(0, testCommand(command));

        sqlite3pp::database db(idpDbFiles[0].string());
        unit_assert(sqlite3pp::query(db, "SELECT COUNT(*) FROM SpectrumSource WHERE QuantitationMethod > 0").begin()->get<int>(0) > 0);
        unit_assert(sqlite3pp::query(db, "SELECT COUNT(*) FROM SpectrumQuantitation WHERE TMT_ReporterIonIntensities IS NOT NULL").begin()->get<int>(0) > 0);
    }

    // ITRAQ4plex
    {
        command = (format("%1%\"%2%\" \"%3%\" -DecoyPrefix \"\" -OverwriteExistingFiles 1 -QuantitationMethod ITRAQ4plex%1%") % commandQuote % idpQonvertPath % bal::join(idFiles | boost::adaptors::transformed(path_stringer()), "\" \"")).str();
        unit_assert_operator_equal(0, testCommand(command));

        sqlite3pp::database db(idpDbFiles[0].string());
        unit_assert(sqlite3pp::query(db, "SELECT COUNT(*) FROM SpectrumSource WHERE QuantitationMethod > 0").begin()->get<int>(0) > 0);
        unit_assert(sqlite3pp::query(db, "SELECT COUNT(*) FROM SpectrumQuantitation WHERE iTRAQ_ReporterIonIntensities IS NOT NULL").begin()->get<int>(0) > 0);
    }

    // ITRAQ4plex without normalization
    {
        command = (format("%1%\"%2%\" \"%3%\" -DecoyPrefix \"\" -OverwriteExistingFiles 1 -QuantitationMethod ITRAQ4plex -NormalizeReporterIons 0%1%") % commandQuote % idpQonvertPath % bal::join(idFiles | boost::adaptors::transformed(path_stringer()), "\" \"")).str();
        unit_assert_operator_equal(0, testCommand(command));

        sqlite3pp::database db(idpDbFiles[0].string());
        unit_assert(sqlite3pp::query(db, "SELECT COUNT(*) FROM SpectrumSource WHERE QuantitationMethod > 0").begin()->get<int>(0) > 0);
        unit_assert(sqlite3pp::query(db, "SELECT COUNT(*) FROM SpectrumQuantitation WHERE iTRAQ_ReporterIonIntensities IS NOT NULL").begin()->get<int>(0) > 0);
    }

    // ITRAQ8plex
    {
        command = (format("%1%\"%2%\" \"%3%\" -DecoyPrefix \"\" -OverwriteExistingFiles 1 -QuantitationMethod ITRAQ8plex%1%") % commandQuote % idpQonvertPath % bal::join(idFiles | boost::adaptors::transformed(path_stringer()), "\" \"")).str();
        unit_assert_operator_equal(0, testCommand(command));

        sqlite3pp::database db(idpDbFiles[0].string());
        unit_assert(sqlite3pp::query(db, "SELECT COUNT(*) FROM SpectrumSource WHERE QuantitationMethod > 0").begin()->get<int>(0) > 0);
        unit_assert(sqlite3pp::query(db, "SELECT COUNT(*) FROM SpectrumQuantitation WHERE iTRAQ_ReporterIonIntensities IS NOT NULL").begin()->get<int>(0) > 0);
    }
}


void testIdpAssemble(const string& idpQonvertPath, const string& idpAssemblePath, const bfs::path& testDataPath)
{
    string mergedOutputFilepath = (testDataPath / "merged.idpDB").string();

    // clean up existing intermediate files
    vector<bfs::path> intermediateFiles;
    pwiz::util::expand_pathmask(mergedOutputFilepath, intermediateFiles);
    for(const bfs::path& intermediateFile : intermediateFiles)
        bfs::remove(intermediateFile);

    vector<bfs::path> idFiles;
    pwiz::util::expand_pathmask(testDataPath / "*.idpDB", idFiles);
    if (idFiles.empty())
        throw runtime_error("[testIdpAssemble] No idpDB files found in test path \"" + testDataPath.string() + "\"");

    // <idpAssemblePath> <matchPaths>
    string command = (format("%1%\"%2%\" \"%3%\" -MergedOutputFilepath \"%4%\"%1%") % commandQuote % idpAssemblePath % bal::join(idFiles | boost::adaptors::transformed(path_stringer()), "\" \"") % mergedOutputFilepath).str();
    unit_assert_operator_equal(0, testCommand(command));

    unit_assert(bfs::exists(mergedOutputFilepath));
    sqlite3pp::database db(mergedOutputFilepath);
    unit_assert_does_not_throw(sqlite3pp::query(db, "SELECT * FROM IntegerSet").begin(), sqlite3pp::database_error);

    // test that MergedFiles table contains each original idpDB filepath
    for(const bfs::path& idFile : idFiles)
    {
        cout << ("SELECT COUNT(*) FROM MergedFiles WHERE Filepath='" + idFile.string() + "'") << endl;
        unit_assert(0 < sqlite3pp::query(db, ("SELECT COUNT(*) FROM MergedFiles WHERE Filepath='" + idFile.string() + "'").c_str()).begin()->get<int>(0));
    }

    string defaultHash = SHA1Calculator::hashFile(mergedOutputFilepath);

    // test that rerunning on the merged idpDB with the same filter settings will not change the database
    command = (format("%1%\"%2%\" \"%3%\"%1%") % commandQuote % idpAssemblePath % mergedOutputFilepath).str();
    unit_assert_operator_equal(0, testCommand(command));
    unit_assert_operator_equal(defaultHash, SHA1Calculator::hashFile(mergedOutputFilepath));

    // test filter arguments: that each filter results in the correct FilterHistory changes
    {
        // [-MaxFDRScore <real>]
        mergedOutputFilepath = (testDataPath / "merged-MaxFDRScore.idpDB").string();
        command = (format("%1%\"%2%\" \"%3%\" -MergedOutputFilepath \"%4%\" -MaxFDRScore 0.1%1%") % commandQuote % idpAssemblePath % bal::join(idFiles | boost::adaptors::transformed(path_stringer()), "\" \"") % mergedOutputFilepath).str();
        unit_assert_operator_equal(0, testCommand(command));
        { sqlite3pp::database db(mergedOutputFilepath); unit_assert_operator_equal(0.1, sqlite3pp::query(db, "SELECT MaximumQValue FROM FilterHistory").begin()->get<double>(0)); }

        // [-MinDistinctPeptides <integer>]
        mergedOutputFilepath = (testDataPath / "merged-MinDistinctPeptides.idpDB").string();
        command = (format("%1%\"%2%\" \"%3%\" -MergedOutputFilepath \"%4%\" -MinDistinctPeptides 5%1%") % commandQuote % idpAssemblePath % bal::join(idFiles | boost::adaptors::transformed(path_stringer()), "\" \"") % mergedOutputFilepath).str();
        unit_assert_operator_equal(0, testCommand(command));
        { sqlite3pp::database db(mergedOutputFilepath); unit_assert_operator_equal(5, sqlite3pp::query(db, "SELECT MinimumDistinctPeptides FROM FilterHistory").begin()->get<double>(0)); }

        // [-MinSpectra <integer>]
        mergedOutputFilepath = (testDataPath / "merged-MinSpectra.idpDB").string();
        command = (format("%1%\"%2%\" \"%3%\" -MergedOutputFilepath \"%4%\" -MinSpectra 5%1%") % commandQuote % idpAssemblePath % bal::join(idFiles | boost::adaptors::transformed(path_stringer()), "\" \"") % mergedOutputFilepath).str();
        unit_assert_operator_equal(0, testCommand(command));
        { sqlite3pp::database db(mergedOutputFilepath); unit_assert_operator_equal(5, sqlite3pp::query(db, "SELECT MinimumSpectra FROM FilterHistory").begin()->get<double>(0)); }

        // [-MinAdditionalPeptides <integer>]
        mergedOutputFilepath = (testDataPath / "merged-MinAdditionalPeptides.idpDB").string();
        command = (format("%1%\"%2%\" \"%3%\" -MergedOutputFilepath \"%4%\" -MinAdditionalPeptides 15%1%") % commandQuote % idpAssemblePath % bal::join(idFiles | boost::adaptors::transformed(path_stringer()), "\" \"") % mergedOutputFilepath).str();
        unit_assert_operator_equal(0, testCommand(command));
        { sqlite3pp::database db(mergedOutputFilepath); unit_assert_operator_equal(15, sqlite3pp::query(db, "SELECT MinimumAdditionalPeptides FROM FilterHistory").begin()->get<double>(0)); }

        // [-MinSpectraPerDistinctMatch <integer>]
        mergedOutputFilepath = (testDataPath / "merged-MinSpectraPerDistinctMatch.idpDB").string();
        command = (format("%1%\"%2%\" \"%3%\" -MergedOutputFilepath \"%4%\" -MinSpectraPerDistinctMatch 2%1%") % commandQuote % idpAssemblePath % bal::join(idFiles | boost::adaptors::transformed(path_stringer()), "\" \"") % mergedOutputFilepath).str();
        unit_assert_operator_equal(0, testCommand(command));
        { sqlite3pp::database db(mergedOutputFilepath); unit_assert_operator_equal(2, sqlite3pp::query(db, "SELECT MinimumSpectraPerDistinctMatch FROM FilterHistory").begin()->get<double>(0)); }

        // [-MinSpectraPerDistinctPeptide <integer>]
        mergedOutputFilepath = (testDataPath / "merged-MinSpectraPerDistinctPeptide.idpDB").string();
        command = (format("%1%\"%2%\" \"%3%\" -MergedOutputFilepath \"%4%\" -MinSpectraPerDistinctPeptide 2%1%") % commandQuote % idpAssemblePath % bal::join(idFiles | boost::adaptors::transformed(path_stringer()), "\" \"") % mergedOutputFilepath).str();
        unit_assert_operator_equal(0, testCommand(command));
        { sqlite3pp::database db(mergedOutputFilepath); unit_assert_operator_equal(2, sqlite3pp::query(db, "SELECT MinimumSpectraPerDistinctPeptide FROM FilterHistory").begin()->get<double>(0)); }

        // [-MaxProteinGroupsPerPeptide <integer>]
        mergedOutputFilepath = (testDataPath / "merged-MaxProteinGroupsPerPeptide.idpDB").string();
        command = (format("%1%\"%2%\" \"%3%\" -MergedOutputFilepath \"%4%\" -MaxProteinGroupsPerPeptide 2%1%") % commandQuote % idpAssemblePath % bal::join(idFiles | boost::adaptors::transformed(path_stringer()), "\" \"") % mergedOutputFilepath).str();
        unit_assert_operator_equal(0, testCommand(command));
        { sqlite3pp::database db(mergedOutputFilepath); unit_assert_operator_equal(2, sqlite3pp::query(db, "SELECT MaximumProteinGroupsPerPeptide FROM FilterHistory").begin()->get<double>(0)); }
    }

    // TODO: test automatic output filename and that it is the same as the manually named one
    //command = (format("%1%\"%2%\" \"%3%\"%1%") % commandQuote % idpAssemblePath % bal::join(idFiles | boost::adaptors::transformed(path_stringer()), "\" \"")).str();
    //cout << endl << command << endl;
    //unit_assert_operator_equal(0, system(command.c_str()));
    //unit_assert(bfs::exists(testDataPath / "20120.idpDB"));
    //unit_assert_operator_equal(defaultHash, SHA1Calculator::hashFile((testDataPath / "20120.idpDB").string()));

    mergedOutputFilepath = (testDataPath / "merged.idpDB").string();

    // test assign source hierarchy
    {
        // test single layer hierarchy
        {
            string assemblyeFilepath = (testDataPath / "groups.txt").string();
            ofstream assemblyFile(assemblyeFilepath.c_str());
            assemblyFile << "/201203" << "\t201203-624176-12\n"
                         << "/201208" << "\t201208-378803\n";
            assemblyFile.close();

            command = (format("%1%\"%2%\" \"%3%\" -AssignSourceHierarchy \"%4%\"%1%") % commandQuote % idpAssemblePath % mergedOutputFilepath % assemblyeFilepath).str();
            unit_assert_operator_equal(0, testCommand(command));
            sqlite3pp::database db(mergedOutputFilepath);
            unit_assert_operator_equal(3, sqlite3pp::query(db, "SELECT COUNT(*) FROM SpectrumSourceGroup").begin()->get<int>(0));
            unit_assert_operator_equal("/", sqlite3pp::query(db, "SELECT Name FROM SpectrumSourceGroup WHERE Id=1").begin()->get<string>(0));
            unit_assert_operator_equal("/201203", sqlite3pp::query(db, "SELECT Name FROM SpectrumSourceGroup WHERE Id=2").begin()->get<string>(0));
            unit_assert_operator_equal("/201208", sqlite3pp::query(db, "SELECT Name FROM SpectrumSourceGroup WHERE Id=3").begin()->get<string>(0));
            unit_assert_operator_equal(2, sqlite3pp::query(db, "SELECT Group_ FROM SpectrumSource WHERE Name='201203-624176-12'").begin()->get<int>(0));
            unit_assert_operator_equal(3, sqlite3pp::query(db, "SELECT Group_ FROM SpectrumSource WHERE Name='201208-378803'").begin()->get<int>(0));
        }

        // test multi-layer hierarchy
        {
            string assemblyeFilepath = (testDataPath / "groups.txt").string();
            ofstream assemblyFile(assemblyeFilepath.c_str());
            assemblyFile << "/201203/624176" << "\t201203-624176-12\n"
                         << "/201208/378803" << "\t201208-378803\n";
            assemblyFile.close();

            command = (format("%1%\"%2%\" \"%3%\" -AssignSourceHierarchy \"%4%\"%1%") % commandQuote % idpAssemblePath % mergedOutputFilepath % assemblyeFilepath).str();
            unit_assert_operator_equal(0, testCommand(command));
            sqlite3pp::database db(mergedOutputFilepath);
            unit_assert_operator_equal(5, sqlite3pp::query(db, "SELECT COUNT(*) FROM SpectrumSourceGroup").begin()->get<int>(0));
            unit_assert_operator_equal(1, sqlite3pp::query(db, "SELECT COUNT(*) FROM SpectrumSourceGroup WHERE Name='/201203/624176'").begin()->get<int>(0));
            unit_assert_operator_equal(1, sqlite3pp::query(db, "SELECT COUNT(*) FROM SpectrumSourceGroup WHERE Name='/201208/378803'").begin()->get<int>(0));
            unit_assert_operator_equal("/201203/624176", sqlite3pp::query(db, "SELECT ssg.Name FROM SpectrumSource ss, SpectrumSourceGroup ssg WHERE ss.Name='201203-624176-12' AND Group_=ssg.Id").begin()->get<string>(0));
            unit_assert_operator_equal("/201208/378803", sqlite3pp::query(db, "SELECT ssg.Name FROM SpectrumSource ss, SpectrumSourceGroup ssg WHERE ss.Name='201208-378803' AND Group_=ssg.Id").begin()->get<string>(0));
        }
    }

    // test filtering a single file
    command = (format("%1%\"%2%\" \"%3%\" -MaxFDRScore 0.1%1%") % commandQuote % idpAssemblePath % mergedOutputFilepath).str();
    unit_assert_operator_equal(0, testCommand(command));
    int filteredSpectraAfterMerge; { sqlite3pp::database db((testDataPath / "merged-MaxFDRScore.idpDB").string()); filteredSpectraAfterMerge = sqlite3pp::query(db, "SELECT FilteredSpectra FROM FilterHistory").begin()->get<int>(0); }
    int filteredSpectraAfterFilter; { sqlite3pp::database db(mergedOutputFilepath); filteredSpectraAfterFilter = sqlite3pp::query(db, "SELECT FilteredSpectra FROM FilterHistory LIMIT 1 OFFSET 1").begin()->get<int>(0); }
    unit_assert_operator_equal(filteredSpectraAfterMerge, filteredSpectraAfterFilter);

    // test embedding quantitation on a new file; the values embedded here will be tested in idpQuery
    // ITRAQ8plex
    {
        string mergedQuantifiedOutputFilepath = (testDataPath / "merged-ITRAQ8plex.idpDB").string();
        bfs::copy_file(mergedOutputFilepath, mergedQuantifiedOutputFilepath);
        command = (format("%1%\"%2%\" \"%3%\" -QuantitationMethod ITRAQ8plex -EmbedOnly 1%1%") % commandQuote % idpQonvertPath % mergedQuantifiedOutputFilepath).str();
        unit_assert_operator_equal(0, testCommand(command));

        sqlite3pp::database db(mergedQuantifiedOutputFilepath);
        unit_assert(sqlite3pp::query(db, "SELECT COUNT(*) FROM SpectrumSource WHERE QuantitationMethod > 0").begin()->get<int>(0) > 0);
        unit_assert(sqlite3pp::query(db, "SELECT COUNT(*) FROM SpectrumQuantitation WHERE iTRAQ_ReporterIonIntensities IS NOT NULL").begin()->get<int>(0) > 0);
    }

    // TMT10plex
    {
        string mergedQuantifiedOutputFilepath = (testDataPath / "merged-TMT10plex.idpDB").string();
        bfs::copy_file(mergedOutputFilepath, mergedQuantifiedOutputFilepath);
        command = (format("%1%\"%2%\" \"%3%\" -QuantitationMethod TMT10plex -EmbedOnly 1 -ReporterIonMzTolerance 0.003mz%1%") % commandQuote % idpQonvertPath % mergedQuantifiedOutputFilepath).str();
        unit_assert_operator_equal(0, testCommand(command));

        sqlite3pp::database db(mergedQuantifiedOutputFilepath);
        unit_assert(sqlite3pp::query(db, "SELECT COUNT(*) FROM SpectrumSource WHERE QuantitationMethod > 0").begin()->get<int>(0) > 0);
        unit_assert(sqlite3pp::query(db, "SELECT COUNT(*) FROM SpectrumQuantitation WHERE TMT_ReporterIonIntensities IS NOT NULL").begin()->get<int>(0) > 0);
    }

    // test isobaric sample mapping
    {
        string assemblyFilepath = (testDataPath / "groups.txt").string();
        ofstream assemblyFile(assemblyFilepath.c_str());
        assemblyFile << "/201203" << "\t201203-624176-12\n"
                     << "/201208" << "\t201208-378803\n";
        assemblyFile.close();

        // the sample names are dummy values
        string isobaricSampleMappingFilepath = (testDataPath / "sample_mapping.txt").string();
        ofstream isobaricSampleMappingFile(isobaricSampleMappingFilepath.c_str());
        isobaricSampleMappingFile << "/201203" << "\tEmpty,201203-TMT2,201203-TMT3,Empty,201203-TMT5,Reference,201203-TMT7,201203-TMT8,201203-TMT9,201203-TMT10\n"
                                  << "/201208" << "\t201208-TMT1,201208-TMT2,201208-TMT3,Empty,201208-TMT5,Reference,201208-TMT7,201208-TMT8,201208-TMT9,Empty\n";
        isobaricSampleMappingFile.close();

        string mergedQuantifiedOutputFilepath = (testDataPath / "merged-TMT10plex-withIsobaricSampleMapping.idpDB").string();
        bfs::copy_file(testDataPath / "merged-TMT10plex.idpDB", mergedQuantifiedOutputFilepath);
        command = (format("%1%\"%2%\" \"%3%\" -AssignSourceHierarchy \"%4%\" -IsobaricSampleMapping \"%5%\"%1%") % commandQuote % idpAssemblePath % mergedQuantifiedOutputFilepath % assemblyFilepath % isobaricSampleMappingFilepath).str();
        unit_assert_operator_equal(0, testCommand(command));
        sqlite3pp::database db(mergedQuantifiedOutputFilepath);
        unit_assert_operator_equal(3, sqlite3pp::query(db, "SELECT COUNT(*) FROM SpectrumSourceGroup").begin()->get<int>(0));
        unit_assert_operator_equal(1, sqlite3pp::query(db, "SELECT COUNT(*) FROM SpectrumSourceGroup WHERE Name='/201203'").begin()->get<int>(0));
        unit_assert_operator_equal(1, sqlite3pp::query(db, "SELECT COUNT(*) FROM SpectrumSourceGroup WHERE Name='/201208'").begin()->get<int>(0));
        unit_assert_operator_equal("Empty,201203-TMT2,201203-TMT3,Empty,201203-TMT5,Reference,201203-TMT7,201203-TMT8,201203-TMT9,201203-TMT10", sqlite3pp::query(db, "SELECT Samples FROM IsobaricSampleMapping WHERE GroupId=2").begin()->get<string>(0));
        unit_assert_operator_equal("201208-TMT1,201208-TMT2,201208-TMT3,Empty,201208-TMT5,Reference,201208-TMT7,201208-TMT8,201208-TMT9,Empty", sqlite3pp::query(db, "SELECT Samples FROM IsobaricSampleMapping WHERE GroupId=3").begin()->get<string>(0));
    }

    // test isobaric sample count mismatch error
    {
        string assemblyFilepath = (testDataPath / "groups.txt").string();
        ofstream assemblyFile(assemblyFilepath.c_str());
        assemblyFile << "/201203" << "\t201203-624176-12\n"
                     << "/201208" << "\t201208-378803\n";
        assemblyFile.close();

        // the sample names are dummy values
        string isobaricSampleMappingFilepath = (testDataPath / "sample_mapping.txt").string();
        ofstream isobaricSampleMappingFile(isobaricSampleMappingFilepath.c_str());
        isobaricSampleMappingFile << "/201203" << "\t201203-TMT2,201203-TMT3,Empty,201203-TMT5,Reference,201203-TMT7,201203-TMT8,201203-TMT9,201203-TMT10\n"
                                  << "/201208" << "\t201208-TMT1,201208-TMT2,201208-TMT3,201208-TMT5,Reference,201208-TMT7,201208-TMT8,201208-TMT9,Empty\n";
        isobaricSampleMappingFile.close();

        string mergedQuantifiedOutputFilepath = (testDataPath / "merged-TMT10plex-withBadIsobaricSampleMapping.idpDB").string();
        bfs::copy_file(testDataPath / "merged-TMT10plex.idpDB", mergedQuantifiedOutputFilepath);
        command = (format("%1%\"%2%\" \"%3%\" -AssignSourceHierarchy \"%4%\" -IsobaricSampleMapping \"%5%\"%1%") % commandQuote % idpAssemblePath % mergedQuantifiedOutputFilepath % assemblyFilepath % isobaricSampleMappingFilepath).str();
        unit_assert_operator_equal(1, testCommand(command));
        {
            ifstream testLog("test.log");
            string testLogString(bfs::file_size("test.log"), ' ');
            testLog.read(&testLogString[0], testLogString.length());
            unit_assert(bal::contains(testLogString, "[embedIsobaricSampleMapping] number of samples (9) for group /201203 does not match number of channels in the quantitation method (10)"));
        }
    }
}


void testIdpQuery(const string& idpQonvertPath, const string& idpAssemblePath, const string& idpQueryPath, const bfs::path& testDataPath)
{
    string inputFilepath = (testDataPath / "merged.idpDB").string();
    string outputFilepath = (testDataPath / "merged.tsv").string();
    string quantifiedInputFilepath = (testDataPath / "merged-ITRAQ8plex.idpDB").string();
    string quantifiedOutputFilepath = (testDataPath / "merged-ITRAQ8plex.tsv").string();
    string command;

    // clean up existing intermediate files
    vector<bfs::path> intermediateFiles;
    pwiz::util::expand_pathmask(testDataPath / "*.tsv", intermediateFiles);
    for(const bfs::path& intermediateFile : intermediateFiles)
        bfs::remove(intermediateFile);

    vector<string> proteinGroupColumns;
    proteinGroupColumns +=  "Protein", "ProteinGroup", "Cluster";

    string mainProteinColumns = " Accession,GeneId,GeneGroup,DistinctPeptides,DistinctMatches,FilteredSpectra,IsDecoy,Cluster,ProteinGroup"
                                ",Length,PercentCoverage,Sequence,Description,TaxonomyId,GeneName,GeneFamily,Chromosome,GeneDescription,PeptideGroups,PeptideSequences";
    string expectedProteinHeader = bal::trim_copy(bal::replace_all_copy(mainProteinColumns, ",", "\t"));

    // run through all group modes with all columns when no gene metadata is embedded
    for(const string& groupColumn : proteinGroupColumns)
    {
        command = (format("%1%\"%2%\" %3% %4% \"%5%\"%1%") % commandQuote % idpQueryPath % groupColumn % mainProteinColumns % inputFilepath).str();
        unit_assert_operator_equal(0, testCommand(command));
        unit_assert(bfs::exists(outputFilepath));
        bfs::rename(outputFilepath, testDataPath / ("merged-no-quantitation-" + groupColumn + ".tsv"));
        {
            ifstream outputFile((testDataPath / ("merged-no-quantitation-" + groupColumn + ".tsv")).string().c_str());
            string line;
            getlinePortable(outputFile, line);
            unit_assert_operator_equal(expectedProteinHeader, line);
            // TODO: figure out how to test the output of the second line
            getlinePortable(outputFile, line);
            unit_assert_operator_equal(std::count(expectedProteinHeader.begin(), expectedProteinHeader.end(), '\t'), std::count(line.begin(), line.end(), '\t'));
        }
        
        // asking for summary quantitation columns without quantitation embedded will not be an error, but also won't add any columns
        command = (format("%1%\"%2%\" %3% %4%,TMT2plex \"%5%\"%1%") % commandQuote % idpQueryPath % groupColumn % mainProteinColumns % inputFilepath).str();
        unit_assert_operator_equal(0, testCommand(command));
        unit_assert(bfs::exists(outputFilepath));
        bfs::rename(outputFilepath, testDataPath / ("merged-empty-TMT2plex-" + groupColumn + ".tsv"));
        {
            ifstream outputFile((testDataPath / ("merged-empty-TMT2plex-" + groupColumn + ".tsv")).string().c_str());
            string line;
            getlinePortable(outputFile, line);
            unit_assert_operator_equal(expectedProteinHeader, line);
            // TODO: figure out how to test the output of the second line
            getlinePortable(outputFile, line);
            unit_assert_operator_equal(std::count(expectedProteinHeader.begin(), expectedProteinHeader.end(), '\t'), std::count(line.begin(), line.end(), '\t'));
        }

        // asking for pivot quantitation columns without quantitation embedded will be an error
        command = (format("%1%\"%2%\" %3% %4%,PivotITRAQBySource \"%5%\"%1%") % commandQuote % idpQueryPath % groupColumn % mainProteinColumns % inputFilepath).str();
        unit_assert_operator_equal(1, testCommand(command));
        unit_assert(!bfs::exists(outputFilepath));
        //bfs::rename(outputFilepath, testDataPath / ("merged-empty-iTRAQ8plex-" + groupColumn + ".tsv"));

        command = (format("%1%\"%2%\" %3% %4%,iTRAQ8plex,PivotITRAQBySource \"%5%\"%1%") % commandQuote % idpQueryPath % groupColumn % mainProteinColumns % quantifiedInputFilepath).str();
        unit_assert_operator_equal(0, testCommand(command));
        unit_assert(bfs::exists(quantifiedOutputFilepath));
        bfs::rename(quantifiedOutputFilepath, testDataPath / ("merged-quantified-iTRAQ8plex-" + groupColumn + ".tsv"));
        {
            ifstream outputFile((testDataPath / ("merged-quantified-iTRAQ8plex-" + groupColumn + ".tsv")).string().c_str());
            string line;
            getlinePortable(outputFile, line);
            //unit_assert_operator_equal(expectedProteinHeader + "\tTMT", line);
            // TODO: figure out how to test the output of the second line
            getlinePortable(outputFile, line);
            unit_assert_operator_equal(std::count(expectedProteinHeader.begin(), expectedProteinHeader.end(), '\t') + 24, std::count(line.begin(), line.end(), '\t'));
        }
    }


    // test that Gene and GeneGroup modes issue an error when gene metadata is not embedded
    command = (format("%1%\"%2%\" %3% %4% \"%5%\"%1%") % commandQuote % idpQueryPath % "Gene" % mainProteinColumns % inputFilepath).str();
    unit_assert(0 < testCommand(command));
    unit_assert(!bfs::exists(outputFilepath));

    command = (format("%1%\"%2%\" %3% %4% \"%5%\"%1%") % commandQuote % idpQueryPath % "GeneGroup" % mainProteinColumns % inputFilepath).str();
    cout << endl << command << endl;
    unit_assert(0 < testCommand(command));
    unit_assert(!bfs::exists(outputFilepath));


    proteinGroupColumns += "Gene", "GeneGroup";

    try
    {
        IDPicker::Embedder::embedGeneMetadata(inputFilepath);
        IDPicker::Embedder::embedGeneMetadata(quantifiedInputFilepath);
    }
    catch (exception& e)
    {
        throw runtime_error(string("[testIdpQuery] error embedding gene metadata: ") + e.what());
    }

    // run through all the group modes with all columns after embedding gene metadata
    for(const string& groupColumn : proteinGroupColumns)
    {
        command = (format("%1%\"%2%\" %3% %4% \"%5%\"%1%") % commandQuote % idpQueryPath % groupColumn % mainProteinColumns % inputFilepath).str();
        unit_assert_operator_equal(0, testCommand(command));
        unit_assert(bfs::exists(outputFilepath));
        bfs::rename(outputFilepath, testDataPath / ("merged-no-quantitation-" + groupColumn + ".tsv"));
        {
            ifstream outputFile((testDataPath / ("merged-no-quantitation-" + groupColumn + ".tsv")).string().c_str());
            string line;
            getlinePortable(outputFile, line);
            unit_assert_operator_equal(expectedProteinHeader, line);
            // TODO: figure out how to test the output of the second line
            getlinePortable(outputFile, line);
            unit_assert_operator_equal(std::count(expectedProteinHeader.begin(), expectedProteinHeader.end(), '\t'), std::count(line.begin(), line.end(), '\t'));
        }

        // asking for summary quantitation columns without quantitation embedded will not be an error, but also won't add any columns
        command = (format("%1%\"%2%\" %3% %4%,TMT2plex \"%5%\"%1%") % commandQuote % idpQueryPath % groupColumn % mainProteinColumns % inputFilepath).str();
        unit_assert_operator_equal(0, testCommand(command));
        unit_assert(bfs::exists(outputFilepath));
        bfs::rename(outputFilepath, testDataPath / ("merged-empty-TMT2plex-" + groupColumn + ".tsv"));
        {
            ifstream outputFile((testDataPath / ("merged-empty-TMT2plex-" + groupColumn + ".tsv")).string().c_str());
            string line;
            getlinePortable(outputFile, line);
            //unit_assert_operator_equal(expectedProteinHeader + "\tTMT", line);
            // TODO: figure out how to test the output of the second line
            getlinePortable(outputFile, line);
            unit_assert_operator_equal(std::count(expectedProteinHeader.begin(), expectedProteinHeader.end(), '\t'), std::count(line.begin(), line.end(), '\t'));
        }

        // asking for pivot quantitation columns without quantitation embedded will be an error
        command = (format("%1%\"%2%\" %3% %4%,PivotITRAQBySource \"%5%\"%1%") % commandQuote % idpQueryPath % groupColumn % mainProteinColumns % inputFilepath).str();
        unit_assert_operator_equal(1, testCommand(command));
        unit_assert(!bfs::exists(outputFilepath));
        //bfs::rename(outputFilepath, testDataPath / ("merged-empty-iTRAQ8plex-" + groupColumn + ".tsv"));

        command = (format("%1%\"%2%\" %3% %4%,iTRAQ8plex,PivotITRAQBySource \"%5%\"%1%") % commandQuote % idpQueryPath % groupColumn % mainProteinColumns % quantifiedInputFilepath).str();
        unit_assert_operator_equal(0, testCommand(command));
        unit_assert(bfs::exists(quantifiedOutputFilepath));
        bfs::rename(quantifiedOutputFilepath, testDataPath / ("merged-quantified-iTRAQ8plex-" + groupColumn + ".tsv"));
        {
            ifstream outputFile((testDataPath / ("merged-quantified-iTRAQ8plex-" + groupColumn + ".tsv")).string().c_str());
            string line;
            getlinePortable(outputFile, line);
            //unit_assert_operator_equal(expectedProteinHeader + "\tTMT", line);
            // TODO: figure out how to test the output of the second line
            getlinePortable(outputFile, line);
            unit_assert_operator_equal(std::count(expectedProteinHeader.begin(), expectedProteinHeader.end(), '\t') + 24, std::count(line.begin(), line.end(), '\t'));
        }
    }

    vector<string> peptideGroupColumns;
    peptideGroupColumns += "Peptide", "PeptideGroup", "DistinctMatch";

    string mainPeptideColumns = " Sequence,MonoisotopicMass,MolecularWeight,ProteinAccessions,GeneIds,GeneGroups,DistinctPeptides,DistinctMatches,FilteredSpectra,IsDecoy,Cluster,ProteinGroups"
                                ",Length,ProteinDescriptions,TaxonomyIds,GeneNames,GeneFamilies,Chromosomes,GeneDescriptions,PeptideGroup,Instances,Modifications,Charges,ProteinCount,ProteinGroupCount,DistinctMatches,FilteredSpectra";
    string expectedPeptideHeader = bal::trim_copy(bal::replace_all_copy(mainPeptideColumns, ",", "\t"));

    // run through all the peptide group modes with all columns after embedding gene metadata
    for(const string& groupColumn : peptideGroupColumns)
    {
        command = (format("%1%\"%2%\" %3% %4% \"%5%\"%1%") % commandQuote % idpQueryPath % groupColumn % mainPeptideColumns % inputFilepath).str();
        unit_assert_operator_equal(0, testCommand(command));
        unit_assert(bfs::exists(outputFilepath));
        bfs::rename(outputFilepath, testDataPath / ("merged-no-quantitation-" + groupColumn + ".tsv"));

        command = (format("%1%\"%2%\" %3% %4%,iTRAQ8plex \"%5%\"%1%") % commandQuote % idpQueryPath % groupColumn % mainPeptideColumns % inputFilepath).str();
        unit_assert_operator_equal(0, testCommand(command));
        unit_assert(bfs::exists(outputFilepath));
        bfs::rename(outputFilepath, testDataPath / ("merged-empty-iTRAQ8plex-" + groupColumn + ".tsv"));
        {
            ifstream outputFile((testDataPath / ("merged-empty-iTRAQ8plex-" + groupColumn + ".tsv")).string().c_str());
            string line;
            getlinePortable(outputFile, line);
            unit_assert_operator_equal(expectedPeptideHeader, line);
            // TODO: figure out how to test the output of the second line
            getlinePortable(outputFile, line);
            unit_assert_operator_equal(std::count(expectedPeptideHeader.begin(), expectedPeptideHeader.end(), '\t'), std::count(line.begin(), line.end(), '\t'));
        }

        command = (format("%1%\"%2%\" %3% %4%,iTRAQ8plex,PivotITRAQBySource \"%5%\"%1%") % commandQuote % idpQueryPath % groupColumn % mainPeptideColumns % quantifiedInputFilepath).str();
        unit_assert_operator_equal(0, testCommand(command));
        unit_assert(bfs::exists(quantifiedOutputFilepath));
        bfs::rename(quantifiedOutputFilepath, testDataPath / ("merged-quantified-iTRAQ8plex-" + groupColumn + ".tsv"));
    }

    vector<string> modificationGroupColumns;
    modificationGroupColumns += "Modification", "DeltaMass", "ModifiedSite", "ProteinSite";

    string mainModificationColumns = " ProteinSite,ModifiedSite,MonoDeltaMass,AvgDeltaMass,ProteinOffsets,PeptideSequences,DistinctPeptides,DistinctMatches,FilteredSpectra";
    string expectedModificationHeader = bal::trim_copy(bal::replace_all_copy(mainModificationColumns, ",", "\t"));

    // run through all the modification group modes with all columns after embedding gene metadata
    for(const string& groupColumn : modificationGroupColumns)
    {
        command = (format("%1%\"%2%\" %3% %4% \"%5%\"%1%") % commandQuote % idpQueryPath % groupColumn % mainModificationColumns % inputFilepath).str();
        unit_assert_operator_equal(0, testCommand(command));
        unit_assert(bfs::exists(outputFilepath));
        bfs::rename(outputFilepath, testDataPath / ("merged-no-quantitation-" + groupColumn + ".tsv"));

        command = (format("%1%\"%2%\" %3% %4%,iTRAQ8plex \"%5%\"%1%") % commandQuote % idpQueryPath % groupColumn % mainModificationColumns % inputFilepath).str();
        unit_assert_operator_equal(0, testCommand(command));
        unit_assert(bfs::exists(outputFilepath));
        bfs::rename(outputFilepath, testDataPath / ("merged-empty-iTRAQ8plex-" + groupColumn + ".tsv"));
        {
            ifstream outputFile((testDataPath / ("merged-empty-iTRAQ8plex-" + groupColumn + ".tsv")).string().c_str());
            string line;
            getlinePortable(outputFile, line);
            unit_assert_operator_equal(expectedModificationHeader, line);
            // TODO: figure out how to test the output of the second line
            getlinePortable(outputFile, line);
            unit_assert_operator_equal(std::count(expectedModificationHeader.begin(), expectedModificationHeader.end(), '\t'), std::count(line.begin(), line.end(), '\t'));
        }

        command = (format("%1%\"%2%\" %3% %4%,iTRAQ8plex,PivotITRAQBySource \"%5%\"%1%") % commandQuote % idpQueryPath % groupColumn % mainModificationColumns % quantifiedInputFilepath).str();
        unit_assert_operator_equal(0, testCommand(command));
        unit_assert(bfs::exists(quantifiedOutputFilepath));
        bfs::rename(quantifiedOutputFilepath, testDataPath / ("merged-quantified-iTRAQ8plex-" + groupColumn + ".tsv"));
    }

    // test that all quantitation methods are supported by idpQuery
    {
        vector<bfs::path> idFiles;
        pwiz::util::expand_pathmask(testDataPath / "*.mzid", idFiles);
        vector<bfs::path> idpDbFiles;
        for (const bfs::path& idFile : idFiles)
            idpDbFiles.push_back(bfs::path(idFile).replace_extension(".idpDB"));
        for (const auto& quantitationMethod : boost::make_iterator_range(IDPicker::QuantitationMethod::begin(), IDPicker::QuantitationMethod::end()))
        {
            if (quantitationMethod == IDPicker::QuantitationMethod::None || quantitationMethod == IDPicker::QuantitationMethod::LabelFree)
                continue;

            cout << quantitationMethod << endl;
            {
                // embed selected QuantitationMethod
                command = (format("%1%\"%2%\" \"%3%\" -DecoyPrefix \"\" -OverwriteExistingFiles 1 -QuantitationMethod %4% -ReporterIonMzTolerance 0.003mz%1%")
                    % commandQuote % idpQonvertPath % bal::join(idFiles | boost::adaptors::transformed(path_stringer()), "\" \"") % quantitationMethod.str()).str();
                unit_assert_operator_equal(0, testCommand(command));

                sqlite3pp::database db(idpDbFiles[0].string());
                unit_assert(sqlite3pp::query(db, "SELECT COUNT(*) FROM SpectrumSource WHERE QuantitationMethod > 0").begin()->get<int>(0) > 0);
                unit_assert(sqlite3pp::query(db, "SELECT COUNT(*) FROM SpectrumQuantitation WHERE TMT_ReporterIonIntensities IS NOT NULL OR iTRAQ_ReporterIonIntensities IS NOT NULL").begin()->get<int>(0) > 0);


                // create isobaric mapping for selected QuantitationMethod
                string isobaricSampleMappingFilepath = (testDataPath / "sample_mapping.txt").string();
                ofstream isobaricSampleMappingFile(isobaricSampleMappingFilepath.c_str());
                vector<string> samples;
                isobaricSampleMappingFile << "/\tChannel1";
                for (size_t i=1; i < IDPicker::Embedder::channelsByQuantitationMethod(quantitationMethod); ++i)
                    isobaricSampleMappingFile << ",Channel" << i;
                isobaricSampleMappingFile << "\n";
                isobaricSampleMappingFile.close();

                // assemble for filters
                command = (format("%1%\"%2%\" \"%3%\" -IsobaricSampleMapping \"%4%\" %1%")
                    % commandQuote % idpAssemblePath % idpDbFiles[0].string() % isobaricSampleMappingFilepath).str();
                unit_assert_operator_equal(0, testCommand(command));

                // test idpQuery
                string quantifiedInputFilepath = idpDbFiles[0].string();
                string quantifiedOutputFilepath = bfs::path(idpDbFiles[0]).replace_extension(".tsv").string();
                command = (format("%1%\"%2%\" ProteinGroup %4%,%3% \"%5%\"%1%") % commandQuote % idpQueryPath % quantitationMethod.str() % mainProteinColumns % quantifiedInputFilepath).str();
                unit_assert_operator_equal(0, testCommand(command));
                unit_assert(bfs::exists(quantifiedOutputFilepath));
                bfs::rename(quantifiedOutputFilepath, testDataPath / ("merged-quantified-" + string(quantitationMethod.str()) + "-ProteinGroup.tsv"));
            }
        }
    }

    // test isobaric sample mapping
    {
        string quantifiedInputFilepath = (testDataPath / "merged-TMT10plex-withIsobaricSampleMapping.idpDB").string();
        string quantifiedOutputFilepath = (testDataPath / "merged-TMT10plex-withIsobaricSampleMapping.tsv").string();
        string expectedIsobaricSampleMappingHeader = "Accession\t201203-TMT2\t201203-TMT3\t201203-TMT5\t201203-TMT7\t201203-TMT8\t201203-TMT9\t201203-TMT10\t201208-TMT1\t201208-TMT2\t201208-TMT3\t201208-TMT5\t201208-TMT7\t201208-TMT8\t201208-TMT9";

        command = (format("%1%\"%2%\" ProteinGroup Accession,PivotTMTByGroup \"%3%\"%1%") % commandQuote % idpQueryPath % quantifiedInputFilepath).str();
        unit_assert_operator_equal(0, testCommand(command));
        unit_assert(bfs::exists(quantifiedOutputFilepath));
        {
            ifstream outputFile(quantifiedOutputFilepath.c_str());
            string line;
            getlinePortable(outputFile, line);
            // /201203 Empty,201203-TMT2,201203-TMT3,Empty,201203-TMT5,Reference,201203-TMT7,201203-TMT8,201203-TMT9,201203-TMT10
            // /201208 201208-TMT1,201208-TMT2,201208-TMT3,Empty,201208-TMT5,Reference,201208-TMT7,201208-TMT8,201208-TMT9,Empty
            unit_assert_operator_equal(expectedIsobaricSampleMappingHeader, line);
            // TODO: figure out how to test the output of the second line
            getlinePortable(outputFile, line);
            unit_assert_operator_equal(std::count(expectedIsobaricSampleMappingHeader.begin(), expectedIsobaricSampleMappingHeader.end(), '\t'), std::count(line.begin(), line.end(), '\t'));
        }
    }
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

#ifdef WIN32
        string exeExtension = ".exe";
#else
        string exeExtension = "";
#endif

    try
    {
        // change to test executable's directory so that embedGeneMetadata can find gene2protein.db3
        bfs::current_path(bfs::path(argv[0]).parent_path());

        vector<string> args(argv+1, argv + argc);

        size_t idpQonvertArg = findOneFilename("idpQonvert" + exeExtension, args);
        string idpQonvertPath = args[idpQonvertArg];
        args.erase(args.begin() + idpQonvertArg);

        size_t idpAssembleArg = findOneFilename("idpAssemble" + exeExtension, args);
        string idpAssemblePath = args[idpAssembleArg];
        args.erase(args.begin() + idpAssembleArg);

        size_t idpQueryArg = findOneFilename("idpQuery" + exeExtension, args);
        string idpQueryPath = args[idpQueryArg];
        args.erase(args.begin() + idpQueryArg);

#if defined(WIN32) && defined(_DEBUG)
        try { args.erase(args.begin() + findOneFilename("idpQonvert.pdb", args)); } catch (runtime_error&) {}
        try { args.erase(args.begin() + findOneFilename("idpAssemble.pdb", args)); } catch (runtime_error&) {}
        try { args.erase(args.begin() + findOneFilename("idpQuery.pdb", args)); } catch (runtime_error&) {}
#endif

        // the rest of the arguments should be directories
        for(const string& arg : args)
        {
            if (arg[0] == '-')
                continue;
            if (!bfs::is_directory(arg))
                throw runtime_error("expected a path to test files, got \"" + arg + "\"");

            // copy the TestData files to a temporary directory so that the test data directory isn't littered with test-related files
            bfs::path inputTestDataPath(arg);
            bfs::path outputTestDataPath = bfs::path(argv[0]).parent_path() / "CommandlineTest.data";

            vector<bfs::path> testDataFiles;
            expand_pathmask(inputTestDataPath / "*.*", testDataFiles);
            bfs::create_directory(outputTestDataPath);
            for(const bfs::path& filepath : testDataFiles)
                if (!bal::starts_with(filepath.filename().string(), ".")) // don't try to copy .svn directory
                    bfs::copy_file(filepath, outputTestDataPath / filepath.filename(), bfs::copy_option::overwrite_if_exists);

            testIdpQonvert(idpQonvertPath, outputTestDataPath.string());
            testIdpAssemble(idpQonvertPath, idpAssemblePath, outputTestDataPath.string());
            testIdpQuery(idpQonvertPath, idpAssemblePath, idpQueryPath, outputTestDataPath.string());
        }
    }
    catch (exception& e)
    {
        TEST_FAILED(string("[CommandlineTest] ") + e.what())
    }
    catch (...)
    {
        TEST_FAILED("Caught unknown exception.")
    }

    TEST_EPILOG
}
