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

/**
 * Class for building a library from search result files and their
 * accomanying spectrum files and/or from existing libraries.  Extends
 * BlibMaker.
 *
 * $ BlibBuilder.cpp,v 1.0 2009/01/07 15:53:52 Ning Zhang Exp $
 */

#include "BlibBuilder.h"

using namespace std;

namespace BiblioSpec {

BlibBuilder::BlibBuilder():
level_compress(3), fileSizeThresholdForCaching(800000000)
{
    scoreThresholds[SQT] = 0.01;    // 1% FDR
    scoreThresholds[PEPXML] = 0.95; // peptide prophet probability
    scoreThresholds[IDPXML] = 0;    // use all results
    scoreThresholds[MASCOT] = 0.05; // expectation value
    scoreThresholds[TANDEM] = 0.1;  // expect score
    scoreThresholds[PROT_PILOT] = 0.95;  // 95% confidence
    scoreThresholds[SCAFFOLD] = 0.95;  // Scaffold: Peptide Probability
    scoreThresholds[MSE] = 6;       // Waters MSe peptide score
    scoreThresholds[OMSSA] = 0.00001; // Max OMSSA expect score
    scoreThresholds[PROT_PROSPECT] = 0.001; // expect score
}

BlibBuilder::~BlibBuilder()
{
}

void BlibBuilder::usage()
{
    const char* usage =
        "Usage: BlibBuild [options] <*.sqt|*.pep.xml|*.pepXML|*.blib|*.idpXML|*.dat|*.ssl|*.mzid|*.perc.xml|*final_fragment.csv>+ <library_name>\n"
        "   -o                Overwrite existing library. Default append.\n"
        "   -s                Result file names from stdin. e.g. ls *sqt | BlibBuild -s new.blib.\n"
        "   -q  <max score>   Maximum FDR for accepting results from Percolator (.sqt or .perc.xml) files. Default 0.01.\n"
        "   -p  <min score>   Minimum probability for accepting results from PeptideProphet (.pep.xml) files. Default 0.95.\n"
        "   -e  <max score>   Maximum expectation value for accepting results from Mascot (.dat) files. Default 0.05\n"
        "   -t  <max score>   Maximum expectation value for accepting results from X! Tandem (.xtan.xml) files. Default 0.1\n"
        "   -f  <min score>   Minimum Scaffold Peptide Probability for accepting results from .mzid files. Default 0.95\n"
        "   -w  <min score>   Minimum Waters MSe peptide score for accepting results from final_fragment.csv files. Default 6\n"
        "   -P  <min score>   Minimum ProteinPilot confidence level. Default 0.95 (or 95%)\n"
        "   -E  <max score>   Maximum expectation value for accepting results from OMSAA .pep.xml files. Default 0.00001\n"
        "   -R  <max score>   Maximum expectation value for accepting results from Protein Prospector .pep.xml files. Default 0.00001\n"
        "   -C  <file size>   Minimum file size required to use caching for .dat files.  Specifiy units as B,K,G or M.  Default 800M.\n"
        "   -v  <level>       Level of output to stderr (silent, error, status, warn).  Default status.\n"
        "   -L                Write status and warning messages to log file.\n"
        "   -m <size>         SQLite memory cache size in Megs. Default 250M.\n"
        "   -l <level>        ZLib compression level (0-?). Default 3.\n"
        "   -i <library_id>   LSID library ID. Default uses file name.\n"
        "   -a <authority>    LSID authority. Default proteome.gs.washington.edu.\n";

    cerr << usage << endl;
    exit(1);
}

double BlibBuilder::getScoreThreshold(BUILD_INPUT fileType) {
    return scoreThresholds[fileType];
}

int BlibBuilder::getLevelCompress() {
    return level_compress;
}

vector<char*> BlibBuilder::getInputFiles() {
    return input_files;
}

/**
 * Read the command line.  Use BlibMaker to parse options.  Get
 * filenames and store in input_files vector.
 * \returns The number of ??
 */
int BlibBuilder::parseCommandArgs(int argc, char* argv[])
{
    int i = BlibMaker::parseCommandArgs(argc, argv);
    argc--;               // Remove output library at the end

    int nInputs = argc - i;
    if(!isStdinput() && (nInputs < 1) ) {
        Verbosity::comment(V_ERROR,
                           "Missing input files (.sqt, .pep.xml/.pep.XML/.pepXML, .idpXML, .dat, .xtan.xml, .mzid, .perc.xml.)");
        usage();          // Nothing to add
    }

    if(isStdinput()) {
        string inFileName;

        while(cin) {
            getline(cin, inFileName);

            if(!inFileName.empty()) {
                char* name = new char[inFileName.size()+1];
                strcpy(name,inFileName.c_str());
                input_files.push_back(name);

            } else {
                break;
            }
        }

    } else {
        for (int j = i; j < argc; j++) {
            char* file_name = argv[j];
            //if (has_extension(file_name, ".blib"))
            //merge_libs[merge_count++] = file_name;
            if(has_extension(file_name,".blib") ||
               has_extension(file_name, ".pep.xml") ||
               has_extension(file_name, ".pep.XML") ||
               has_extension(file_name, ".pepXML") ||
               has_extension(file_name, ".sqt") ||
               has_extension(file_name, ".perc.xml") ||
               has_extension(file_name, ".dat") ||
               has_extension(file_name, ".xtan.xml") ||
               has_extension(file_name, ".idpXML") ||
               has_extension(file_name, ".group.xml") ||
               has_extension(file_name, ".mzid") ||
               has_extension(file_name, "final_fragment.csv") ||
               has_extension(file_name, ".ssl") ) {

                input_files.push_back(file_name);
            } else {
                Verbosity::error("Unsupported file type '%s'.  Must be .sqt, "
                                 ".pep.xml/.pep.XML/.pepXML, .idpXML, .dat, "
                                 ".xtan.xml, .ssl, .group.xml, .mzid, perc.xml,"
                                 " final_fragment.csv or .blib.", file_name);
            }
        }
    }

    return i;
}


void BlibBuilder::attachAll()
{
    // we are no longer attaching here, do it one file at a time
    /*
    for (int i = 0; i < (int)input_files.size(); i++) {
        if(has_extension(input_files.at(i), ".blib")) {
            sprintf(zSql, "ATTACH DATABASE '%s' as tmp%d", input_files.at(i), i);
            sql_stmt(zSql);
        }
    }
    */
}

int BlibBuilder::transferLibrary(int iLib,
                                 const ProgressIndicator* parentProgress)
{
    // Check to see if library exists
    struct stat fileStats;
    int gotStats = stat(input_files.at(iLib), &fileStats);
    if( gotStats != 0 ){
        throw BlibException(true, "Library file '%s' cannot be opened for "
                            "transfering", input_files.at(iLib));
    }

    // give the incomming library a name
    char schemaTmp[32];
    sprintf(schemaTmp, "tmp%d", iLib);

    // add it to our open libraries
    sprintf(zSql, "ATTACH DATABASE '%s' as %s",
            input_files.at(iLib), schemaTmp);
    sql_stmt(zSql);

    // does the incomming library have retentiontime, score, etc columns
    bool tmpHasAdditionalColumns =
        tableColumnExists(schemaTmp, "RefSpectra", "retentionTime");

    beginTransaction();

    string msg = "ERROR: Failed transfering spectra from ";
    msg += input_files.at(iLib);
    setMessage(msg.c_str());

    Verbosity::status("Transferring spectra from %s.",
                      base_name(input_files.at(iLib)).c_str());

    // first add all the spectrum source files from incomming library
    transferSpectrumFiles(schemaTmp);

    ProgressIndicator* progress =
        parentProgress->newNestedIndicator(getSpectrumCount(schemaTmp));

    sprintf(zSql, "SELECT id FROM %s.RefSpectra", schemaTmp);

    smart_stmt pStmt;
    int rc = sqlite3_prepare(getDb(), zSql, -1, &pStmt, 0);

    check_rc(rc, zSql);

    rc = sqlite3_step(pStmt);

    while(rc==SQLITE_ROW) {
        progress->increment();

        int spectraId = sqlite3_column_int(pStmt, 0);

        // Even if you are transfering from a non-redundant library
        // you only get credit for one spectrum in a redundant library
        transferSpectrum(schemaTmp, spectraId, 1, tmpHasAdditionalColumns);

        rc = sqlite3_step(pStmt);
    }

    endTransaction();
    int numberProcessed =  progress->processed();
    delete progress;
    return numberProcessed;
}

void BlibBuilder::commit()
{
    BlibMaker::commit();

    for (int i = 0; i < (int)input_files.size(); i++) {
        if(has_extension(input_files.at(i), ".blib")) {
            sprintf(zSql, "DETACH DATABASE tmp%d", i);
            sql_stmt(zSql);
        }
    }
}

int BlibBuilder::parseNextSwitch(int i, int argc, char* argv[])
{
    char* arg = argv[i];
    char switchName = arg[1];

    if (switchName == 'o')
        setOverwrite(true);
    else if(switchName == 's')
        setStdinput(true);
    else if (switchName == 'c' && ++i < argc) {
        double probability_cutoff = atof(argv[i]);
        scoreThresholds[PEPXML] = probability_cutoff;
        scoreThresholds[PROT_PILOT] = probability_cutoff;
        scoreThresholds[SQT] = 1 - probability_cutoff;
        scoreThresholds[MASCOT] = 1 - probability_cutoff;
        scoreThresholds[TANDEM] = 1 - probability_cutoff;
        scoreThresholds[SCAFFOLD] = probability_cutoff;
        scoreThresholds[OMSSA] = 1 - probability_cutoff;
        scoreThresholds[PROT_PROSPECT] = 1 - probability_cutoff;        
    } else if (switchName == 'q' && ++i < argc) {
        scoreThresholds[SQT] = atof(argv[i]);
    } else if (switchName == 'p' && ++i < argc) {
        scoreThresholds[PEPXML] = atof(argv[i]);
    } else if (switchName == 'P' && ++i < argc) {
        scoreThresholds[PROT_PILOT] = atof(argv[i]);
    } else if (switchName == 'E' && ++i < argc) {
        scoreThresholds[OMSSA] = atof(argv[i]);
    } else if (switchName == 'R' && ++i < argc) {
        scoreThresholds[PROT_PROSPECT] = atof(argv[i]);
    } else if (switchName == 'e' && ++i < argc) {
        scoreThresholds[MASCOT] = atof(argv[i]);
    } else if (switchName == 't' && ++i < argc) {
        scoreThresholds[TANDEM] = atof(argv[i]);
    } else if (switchName == 'f' && ++i < argc) {
        scoreThresholds[SCAFFOLD] = atof(argv[i]);
    } else if (switchName == 'w' && ++i < argc) {
        scoreThresholds[MSE] = atof(argv[i]);
    } else if (switchName == 'l' && ++i < argc) {
        level_compress = atoi(argv[i]);
    } else if (switchName == 'C' && ++i < argc) {
        int value = atoi(argv[i]);
        // get the last character for units
        const char* token = argv[i];
        char lastChar = *(token + strlen(token) -1);
        if( lastChar == 'B' || lastChar == 'b'){
          fileSizeThresholdForCaching = value;
        } else if( lastChar == 'K' || lastChar == 'k'){
          fileSizeThresholdForCaching = value * 1000;
        } else if( lastChar == 'M' || lastChar == 'm'){
          fileSizeThresholdForCaching = value * 1000000;
        } else if( lastChar == 'G' || lastChar == 'g' ){
          fileSizeThresholdForCaching = value * 1000000000;
        } else {
          Verbosity::error("File sizes must end in B, K, M or G. '%s' is invalid.", token);
        }
    } else if (switchName == 'v' && ++i < argc) {
        V_LEVEL v_level = Verbosity::string_to_level(argv[i]);
        Verbosity::set_verbosity(v_level);
    } else if (switchName == 'L') {
        Verbosity::open_logfile();
    } else {
        return BlibMaker::parseNextSwitch(i, argc, argv);
    }

    return min(argc, i + 1);
}

string base_name(const char* name)
{
    string baseName = name;
    size_t slash = baseName.find_last_of("/\\");
    if(slash != string::npos)
        baseName = baseName.substr(slash+1);
    return baseName;
}

bool has_extension(const char* name, const char* ext)
{
    return strcmp(name + strlen(name) - strlen(ext), ext) == 0;
}

/**
 * Call super classe's insertPeaks with our level of compression
 */
void BlibBuilder::insertPeaks(int spectraID,
                              int peaksCount,
                              double* pM,
                              float* pI) {
    BlibMaker::insertPeaks(spectraID, level_compress, peaksCount, pM, pI);

}

} // namespace

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
