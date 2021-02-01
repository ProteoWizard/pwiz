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
#ifdef _WIN23
#define _MATRIX_USE_STATIC_LIB
#endif

#include "pwiz/utility/misc/Filesystem.hpp"
#include "CommandLine.h"
#include "BlibBuilder.h"
#include "AllBuildParsers.h"

using namespace BiblioSpec;

static void WriteErrorLines(string s)
{
    istringstream iss(s);
    char buffer[4096];
    while(iss)
    {
        iss.getline(buffer, sizeof(buffer));
        cerr << "ERROR: " << buffer << endl;
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

        vector<char*> inFiles = builder.getInputFiles();

        // for progress of score files 
        ProgressIndicator progress(inFiles.size());
        const ProgressIndicator* progress_cptr = &progress; // read-only pointer

        bool success = true;
        string failureMessage;

        // process each .sqt, .pepxml, .idpXML, .xtan.xml, .dat, .blib file
        for(int i=0; i<(int)inFiles.size(); i++) {
            try{
            
                char* result_file = inFiles.at(i);
                builder.setCurFile(i);
            
                Verbosity::comment(V_STATUS, "Reading results from %s.", 
                                   result_file);
                progress.increment();
            
                if(has_extension(result_file, ".pep.xml") || 
                   has_extension(result_file, ".pepXML")) {
                    PepXMLreader tmpXMLreader(builder, 
                                              result_file,
                                              progress_cptr);
                    success = tmpXMLreader.parseFile();
                } else if(has_extension(result_file, ".sqt")) {
                
                    SQTreader tmpSQTreader(builder, result_file, progress_cptr);
                    success = tmpSQTreader.parseFile();
                
                } else if(has_extension(result_file, ".perc.xml")) {
                    PercolatorXmlReader PercolatorXmlReader(builder, result_file, 
                                                            progress_cptr);
                    success = PercolatorXmlReader.parseFile();
                } else if (has_extension(result_file, ".blib")) {
                    builder.transferLibrary(i, progress_cptr);
                } else if (has_extension(result_file, ".idpXML")) {
                
                    IdpXMLreader tmpXMLreader(builder, result_file, progress_cptr);
                    success = tmpXMLreader.parseFile();
                
                } else if (has_extension(result_file, ".dat")) {
                    MascotResultsReader tmpMascotReader(builder, result_file, 
                                                        progress_cptr);
                    success = tmpMascotReader.parseFile();
                
                } else if (has_extension(result_file, ".ssl")) {
                    SslReader tmpSslReader(builder, result_file, progress_cptr);
                    success = tmpSslReader.parseFile(); 
                
                } else if (has_extension(result_file, ".xtan.xml")) {
                    TandemNativeParser tandemReader(builder, result_file, 
                                                    progress_cptr);
                    success = tandemReader.parseFile();
                
                } else if (has_extension(result_file, ".group.xml")) {
                    ProteinPilotReader pilotReader(builder, result_file, 
                                                   progress_cptr);
                    success = pilotReader.parseFile();
                
                } else if (has_extension(result_file, "pride.xml")) {
                    PrideXmlReader prideXmlReader(builder, result_file, progress_cptr);

                    success = prideXmlReader.parseFile();
                } else if (has_extension(result_file, "msms.txt")) {
                    MaxQuantReader maxQuantReader(builder, result_file, progress_cptr);
                
                    success = maxQuantReader.parseFile();
                } else if (has_extension(result_file, ".msf") ||
                           has_extension(result_file, ".pdResult")) {
                    MSFReader msfReader(builder, result_file, progress_cptr);

                    success = msfReader.parseFile();
                } else if (has_extension(result_file, ".mzid") ||
                           has_extension(result_file, ".mzid.gz")) {
                    MzIdentMLReader mzidReader(builder, result_file, progress_cptr);
                    success = mzidReader.parseFile();

                } else if (has_extension(result_file, "final_fragment.csv")) {
                    WatersMseReader mseReader(builder, result_file, progress_cptr);
                    success = mseReader.parseFile();
                } else if (has_extension(result_file, ".proxl.xml")) {
                    ProxlXmlReader proxlReader(builder, result_file, progress_cptr);
                    success = proxlReader.parseFile();
                } else if (has_extension(result_file, ".mlb")) {
                    ShimadzuMLBReader mlbReader(builder, result_file, progress_cptr);
                    success = mlbReader.parseFile();
                } else if (has_extension(result_file, ".speclib")) {
                    DiaNNSpecLibReader diannReader(builder, result_file, progress_cptr);
                    success = diannReader.parseFile();
                } else if (has_extension(result_file, ".tsv")) {
                    auto tsvReader = TSVReader::create(builder, result_file, progress_cptr);
                    success = tsvReader->parseFile();
                } else if (has_extension(result_file, ".osw")) {
                    OSWReader oswReader(builder, result_file, progress_cptr);
                    success = oswReader.parseFile();
                } else if (has_extension(result_file, ".mzTab") ||
                           has_extension(result_file, "mztab.txt")) {
                    mzTabReader mzTabReader(builder, result_file, progress_cptr);
                    success = mzTabReader.parseFile();
                } else {
                    // shouldn't get to here b/c cmd line parsing checks, but...
                    Verbosity::error("Unknown input file type '%s'.", result_file);
                }
            
                if( !success ){ // in the unlikely event a reader returns false instead of throwing an error
                    string errorMsg = "Failed to parse ";
                    errorMsg += result_file;
                    throw errorMsg;
                }
            } catch(BlibException& e){
                failureMessage = e.what();
                WriteErrorLines(e.what());
                if( ! e.hasFilename() ){
                    cerr << "ERROR: reading file " << inFiles.at(i) << endl;
                }
                success = false;
            } catch(std::exception& e){
                failureMessage = e.what();
                WriteErrorLines(e.what());
                cerr << "ERROR: reading file " << inFiles.at(i) << endl;
                success = false;
            } catch(string s){ // in case a throwParseError is not caught
                failureMessage = s;
                cerr << "ERROR: " << s << endl;
                cerr << "ERROR: reading file " << inFiles.at(i) << endl;
                success = false;
            } catch (const char *str){
                failureMessage = str;
                cerr << "ERROR: " << str << endl;
                cerr << "ERROR: reading file " << inFiles.at(i) << endl;
                success = false;
            } catch (...){
                failureMessage = "Unknown ERROR";
                cerr << "ERROR: Unknown error reading file " << inFiles.at(i) << endl;
                success = false;
            }
        }

        if( ! success ){
            if (expectedError != "" && failureMessage.find(expectedError) != string::npos)
                success = true; // We actually expected a failure, this is a negative test
            // try saving the library
            builder.undoActiveTransaction();
        }
        else if (expectedError != "")
        {
            // We expected to catch a failure
            cerr << "FAILED: This negative test expected an error containing \"" << expectedError << "\"" << endl;
            success = false;
        }

        // check that library contains spectra
        if( builder.is_empty() ){
            builder.abort_current_library();
            if ( success ){
                Verbosity::error("No spectra were found for the new library.");
            }
        } else {
            builder.collapseSources();
            builder.commit();
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
