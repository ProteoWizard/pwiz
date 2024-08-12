//
// $Id$
//
//
// Original author: Barbara Frewen <frewen@u.washington.edu>
//
// Copyright 2012 University of Washington - Seattle, WA 98195
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

/*
 * The main function for BlibBuild.  Uses a BlibBuilder and the given
 * input files (any mix of .sqt, .pep.xml, .idpXML, .blib) and creates
 * a new library of given name.  
 *
 * $ BlibBuild,v 1.0 2009/01/07 15:53:52 Ning Zhang Exp $
 */

#include "pwiz/utility/misc/Filesystem.hpp"
#include "CommandLine.h"
#include "BlibBuilder.h"
#include "AllBuildParsers.h"
#include <memory>

using namespace BiblioSpec;

class BlibHandler : public BuildParser {
public:
    BlibHandler(BlibBuilder& maker, const char* fileName, int fileIdx, const ProgressIndicator* progress)
        : BuildParser(maker, fileName, progress) {
            file_ = fileIdx;
        }
    ~BlibHandler() {}
    bool parseFile() {
        blibMaker_.transferLibrary(file_, parentProgress_);
        return true;
    }
    vector<PSM_SCORE_TYPE> getScoreTypes() {
        BiblioSpec::BlibMaker::verifyFileExists(getFileName());
        blibMaker_.openDb(getFileName().c_str());
        const char* zSql =
            "SELECT DISTINCT(ScoreTypes.scoreType) FROM RefSpectra "
            "JOIN ScoreTypes ON RefSpectra.scoreType = ScoreTypes.id";
        smart_stmt pStmt;
        int rc = sqlite3_prepare(blibMaker_.getDb(), zSql, -1, &pStmt, 0);
        blibMaker_.check_rc(rc, zSql);

        vector<PSM_SCORE_TYPE> scoreTypes;
        for (rc = sqlite3_step(pStmt); rc == SQLITE_ROW; rc = sqlite3_step(pStmt)) {
            PSM_SCORE_TYPE scoreType = stringToScoreType(boost::lexical_cast<string>(sqlite3_column_text(pStmt, 0)));
            if (scoreType != UNKNOWN_SCORE_TYPE) {
                scoreTypes.push_back(scoreType);
            }
        }
        if (rc != SQLITE_DONE) {
            Verbosity::error("Error reading score types: %s", sqlite3_errmsg(blibMaker_.getDb()));
        }
        if (scoreTypes.empty()) {
            scoreTypes.push_back(UNKNOWN_SCORE_TYPE);
        }
        return scoreTypes;
    }
private:
    int file_;
};

static void WriteErrorLines(string s, ostream& out = std::cout) {
    istringstream iss(s);
    string line;
    while (std::getline(iss, line)) {
        out << "ERROR: " << line << endl;
    }
}

static void WriteScoreTypes(vector<PSM_SCORE_TYPE> scoreTypes) {
    for (vector<PSM_SCORE_TYPE>::const_iterator i = scoreTypes.begin(); i != scoreTypes.end(); i++) {
        cout << scoreTypeToString(*i)
             << '\t' << scoreTypeToProbabilityTypeString(*i)
             << std::endl;
    }
}

int main(int argc, char* argv[])
{
    try {
#ifdef _MSC_VER
#ifdef _DEBUG
        // Add memory dump on exit
        _CrtSetDbgFlag(_CrtSetDbgFlag(_CRTDBG_REPORT_FLAG) | _CRTDBG_LEAK_CHECK_DF);
        //    _crtBreakAlloc = 624219;
#endif
#endif
        bnw::args utf8ArgWrapper(argc, argv); // modifies argv in-place with UTF-8 version on Windows
        pwiz::util::enable_utf8_path_operations();

        // Are we anticipating an error message?
        string expectedError="";
        for (int argfind = 1; argfind < argc - 2; argfind++)
        {
            if (!strcmp(argv[argfind], "-e"))
            {
                expectedError = argv[argfind + 1];
                // Remove from arglist before passing along
                for (int a = argfind; a < argc - 2; a++)
                {
                    argv[a] = argv[a + 2];
                }
                argc -= 2;
                break;
            }
        }

        // Verbosity::set_verbosity(V_ALL); // useful for debugging
        BlibBuilder builder;
        builder.parseCommandArgs(argc, argv);

        builder.init();

        vector<string> inFiles = builder.getInputFiles();

        // for progress of score files 
        ProgressIndicator progress(inFiles.size());
        const ProgressIndicator* progress_cptr = &progress; // read-only pointer

        bool success = true;
        bool foundExpectedError = false;

        // process each .sqt, .pepxml, .idpXML, .xtan.xml, .dat, .blib file
        for (size_t i = 0; i < inFiles.size(); i++) {
            string result_file = inFiles.at(i);
            vector<string> errors;

            try {
                builder.setCurFile(i);

                if (!builder.isScoreLookupMode()) {
                    Verbosity::comment(V_STATUS, "Reading results from %s.", result_file.c_str());
                    progress.increment();
                } else {
                    if (i > 0) {
                        cout << endl;
                    }
                    cout << result_file << endl;
                }

                std::shared_ptr<BuildParser> reader;
                if (has_extension(result_file, ".blib")) {
                    reader = std::make_shared<BlibHandler>(builder, result_file.c_str(), i, progress_cptr);
                } else if (has_extension(result_file, ".pep.xml") || has_extension(result_file, ".pepXML")) {
                    reader = std::make_shared<PepXMLreader>(builder, result_file.c_str(), progress_cptr);
                } else if (has_extension(result_file, ".sqt")) {
                    reader = std::make_shared<SQTreader>(builder, result_file.c_str(), progress_cptr);
                } else if (has_extension(result_file, ".perc.xml")) {
                    reader = std::make_shared<PercolatorXmlReader>(builder, result_file.c_str(), progress_cptr);
                } else if (has_extension(result_file, ".idpXML")) {
                    reader = std::make_shared<IdpXMLreader>(builder, result_file.c_str(), progress_cptr);
                } else if (has_extension(result_file, ".dat")) {
                    reader = std::make_shared<MascotResultsReader>(builder, result_file.c_str(), progress_cptr);
                } else if (has_extension(result_file, ".ssl")) {
                    reader = std::make_shared<SslReader>(builder, result_file.c_str(), progress_cptr);
                } else if (has_extension(result_file, ".xtan.xml")) {
                    reader = std::make_shared<TandemNativeParser>(builder, result_file.c_str(), progress_cptr);
                } else if (has_extension(result_file, ".group.xml")) {
                    reader = std::make_shared<ProteinPilotReader>(builder, result_file.c_str(), progress_cptr);
                } else if (has_extension(result_file, ".group")) {
                    // Allow getting score type of .group files.
                    if (!builder.isScoreLookupMode()) {
                        Verbosity::error(".group files must be converted to .group.xml files "
                                         "(e.g. with group2xml or GroupFileExtractor) before building a library.");
                        throw "Failed to parse " + result_file;
                    }
                    WriteScoreTypes(ProteinPilotReader::getScoreTypesHelper());
                    continue;
                } else if (has_extension(result_file, "pride.xml")) {
                    reader = std::make_shared<PrideXmlReader>(builder, result_file.c_str(), progress_cptr);
                } else if (has_extension(result_file, "msms.txt")) {
                    reader = std::make_shared<MaxQuantReader>(builder, result_file.c_str(), progress_cptr);
                } else if (has_extension(result_file, ".msf") || has_extension(result_file, ".pdResult")) {
                    reader = std::make_shared<MSFReader>(builder, result_file.c_str(), progress_cptr);
                } else if (has_extension(result_file, ".mzid") || has_extension(result_file, ".mzid.gz")) {
                    reader = std::make_shared<MzIdentMLReader>(builder, result_file.c_str(), progress_cptr);
                } else if (has_extension(result_file, "final_fragment.csv")) {
                    reader = std::make_shared<WatersMseReader>(builder, result_file.c_str(), progress_cptr);
                } else if (has_extension(result_file, ".proxl.xml")) {
                    reader = std::make_shared<ProxlXmlReader>(builder, result_file.c_str(), progress_cptr);
                } else if (has_extension(result_file, ".mlb")) {
                    reader = std::make_shared<ShimadzuMLBReader>(builder, result_file.c_str(), progress_cptr);
                } else if (has_extension(result_file, ".speclib")) {
                    reader = std::make_shared<DiaNNSpecLibReader>(builder, result_file.c_str(), progress_cptr);
                } else if (has_extension(result_file, ".tsv")) {
                    reader = TSVReader::create(builder, result_file.c_str(), progress_cptr);
                } else if (has_extension(result_file, ".osw")) {
                    reader = std::make_shared<OSWReader>(builder, result_file.c_str(), progress_cptr);
                } else if (has_extension(result_file, ".mzTab") || has_extension(result_file, "mztab.txt")) {
                    reader = std::make_shared<mzTabReader>(builder, result_file.c_str(), progress_cptr);
                } else if (has_extension(result_file, ".hk.bs.kro")) {
                    reader = std::make_shared <HardklorReader>(builder, result_file.c_str(), progress_cptr);
                } else {
                    // shouldn't get to here b/c cmd line parsing checks, but...
                    Verbosity::error("Unknown input file type '%s'.", result_file.c_str());
                    throw "Failed to parse " + result_file;
                }

                if (builder.isScoreLookupMode()) {
                    try {
                        WriteScoreTypes(reader->getScoreTypes());
                    } catch (...) {
                        WriteScoreTypes(vector<PSM_SCORE_TYPE>(1, UNKNOWN_SCORE_TYPE));
                        throw; // rethrow
                    }
                } else if (!reader->parseFile()) {
                    // in the unlikely event a reader returns false instead of throwing an error
                    throw "Failed to parse " + result_file;
                }
            } catch (BlibException& e) {
                errors.push_back(e.what());
                if (!e.hasFilename()) {
                    errors.push_back("reading file " + result_file);
                }
            } catch (std::exception& e) {
                errors.push_back(e.what());
                errors.push_back("reading file " + result_file);
            } catch (string s) { // in case a throwParseError is not caught
                errors.push_back(s);
                errors.push_back("reading file " + result_file);
            } catch (const char *str) {
                errors.push_back(str);
                errors.push_back("reading file " + result_file);
            } catch (...) {
                errors.push_back("Unknown ERROR");
                errors.push_back("Unknown error reading file " + result_file);
            }

            for (vector<string>::const_iterator error = errors.begin(); error != errors.end(); error++) {
                WriteErrorLines(*error, !builder.isScoreLookupMode() ? std::cerr : std::cout);

                if (!foundExpectedError && !expectedError.empty() && error->find(expectedError) != string::npos) {
                    foundExpectedError = true;
                }
            }

            if (!builder.isScoreLookupMode()) { // always have score lookup have zero exit code
                success = (success && errors.empty()) || foundExpectedError;
            }
        }

        if (foundExpectedError) {
            builder.undoActiveTransaction();
        } else if (!expectedError.empty()) {
            // We expected to catch a failure
            cerr << "FAILED: This negative test expected an error containing \"" << expectedError << "\"" << endl;
            success = false;
        }

        if (!builder.isScoreLookupMode()) {
            // check that library contains spectra
            if (builder.is_empty()) {
                builder.abort_current_library();
                if (success) {
                    Verbosity::error("No spectra were found for the new library.");
                }
            } else {
                builder.collapseSources();
                builder.commit();
            }
        }

        Verbosity::close_logfile();
        return !success;

    } catch(BlibException& e){
        WriteErrorLines(e.what());
    } catch(std::exception& e){
        WriteErrorLines(e.what());
    } catch (...) {
        WriteErrorLines("Unknown ERROR");
    }
    return 1;
}

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
