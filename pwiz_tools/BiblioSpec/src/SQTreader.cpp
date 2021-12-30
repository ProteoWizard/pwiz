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
 * Class definition for SQTreader, a class for parsing sqt files
 */

#include "SQTreader.h"
#include "BlibMaker.h"
#include "AminoAcidMasses.h"

namespace bxp = boost::xpressive;

namespace BiblioSpec {

SQTreader::SQTreader(BlibBuilder& maker, 
                     const char* sqtfile, 
                     const ProgressIndicator* parent_progress)
: BuildParser(maker, sqtfile, parent_progress), percolated(false)
{
    // initialize mod arrays
    for(int i=0; i<MAX_MODS; i++) {
        staticMods[i]=0;
        diffMods[i] = 0;
    }
    AminoAcidMasses::initializeMass(masses_, 0);
    sqtVersion = new SequestVersion("2.7");
    cometModRegex = bxp::sregex::compile("^H[[:blank:]]+"    //H followed by at least some white space
                                         "CometParams[[:blank:]]+" //CometParams followed by at least somewhite space 
                                         "add_(?P<aa>[A-Z])_"       //pull out the amino acid from add_<aa>_
                                         "[^\\s]+"                  //some non-white space after add_<aa>_ (ex add_C_Cysteine)
                                         "[[:blank:]]*=[[:blank:]]*"           //equal sign, potentially preceeded and followed by white space
                                         "(?P<modMass>(\\d*[.])?\\d+)"//modification mass as a floating point number
                                         "[[:blank:]]*$");          //potential blank space, end of the line
}

SQTreader::~SQTreader()
{
    if(file.is_open())
        file.close();
    if(sqtVersion != NULL)
        delete sqtVersion;
}


/**
 * Open sqt file for reading.  Read in header and leave filepointer at
 * beginning of first record.
 */
void SQTreader::openRead(bool warnIfNotPercolated)
{
    if(file.is_open())
        file.close();

    file.open(getFileName().c_str());

    if(!file.is_open()) {
        throw BlibException(true, "Couldn't open '%s'.", getFileName().c_str());
    }

    //read through header to get the modifications
    string buffer;
    char thisLine[1024];
    char mods[512];

    bxp::smatch match;
    string::const_iterator start, end;

    while(file.peek() == 'H') {

        getline(file,buffer);
        start = buffer.begin();
        end = buffer.end();
        if(sqtVersion->generatorName() == "Sequest" && buffer.find("SQTGeneratorVersion") != string::npos) {
            size_t numPos = buffer.find_first_of("0123456789");
            if (numPos == string::npos)
            {
                throw new BlibException(false, "Could not get the SEQUEST "
                    "version from this line in the header: %s.", 
                     buffer.c_str());
            }

            string versionStr = buffer.substr(numPos, 
                                              buffer.length() - numPos);
            if (!sqtVersion->trySetVersion(versionStr))
            {
                throw new BlibException(false, "Could not get the SEQUEST "
                    "version from this line in the header: %s.", 
                     buffer.c_str());
            }
        }

        if(buffer.find("CometVersion") != string::npos)
        {
            if (sqtVersion != NULL)
                delete sqtVersion;
            sqtVersion = new CometVersion();
            size_t numPos = buffer.find_first_of("0123456789");
            if (numPos == string::npos)
            {
                throw new BlibException(false, "Could not get the Comet "
                    "version from this line in the header: %s.", 
                     buffer.c_str());
            }
            string versionStr = buffer.substr(numPos,
                                              buffer.length() - numPos);
            if (!sqtVersion->trySetVersion(versionStr))
            {
                throw new BlibException(false, "Could not get the Comet "
                    "version from this line in the header: %s.", 
                     buffer.c_str());
            }
        }

        if(buffer.find("StaticMod") != string::npos) {
            strcpy(thisLine, buffer.c_str());
            sscanf(thisLine, "%*c %*s %s", mods);
            float modValue;
            char modLetter;
            sscanf(mods, "%c%*c%f", &modLetter, &modValue);
      
            // if version is > 2.7, subtract modValue from residue mass
            // if no version number found, die
            if( sqtVersion->generatorName() == "Comet" ||
                    (sqtVersion->generatorName() == "Sequest" && *sqtVersion > SequestVersion("2.7"))){
                string residue(1, modLetter);
                float residueMass = static_cast<float>(getPeptideMass(residue, masses_));
                modValue = modValue - residueMass;
            } 
            staticMods[(int)modLetter]=modValue;
        }

        if(bxp::regex_search(start, end, match, cometModRegex))
        {
            // the comet params values report static modificiations to amino acids
            // and are redundant to values reported in the StaticMod entry in the header
            // The comet params entries are preferable to use because they report the mass of the modification.
            // Static mod reports amino acid mass + modification, and we don't necessarily know if 
            // the amino acid mass is average or monoisotopic
            if (match["aa"].length() != 1 || match["modMass"].length() <=0)
            {
                throw new BlibException(false, "Could not extract the staticMod "
                        "from this line in the header: %s",buffer.c_str());
            }
            char modLetter = match["aa"].str()[0];
            float modValue = atof(match["modMass"].str().c_str());
            staticMods[(int)modLetter]=modValue;
        }
    
        if( buffer.find(" DiffMod") != string::npos || 
            buffer.find("	DiffMod") != string::npos) {
            size_t posEquals = buffer.find("=");
            if( posEquals == string::npos ){ 
                throw new BlibException(false, "Unexpected static mod format: "
                                        "%s", buffer.c_str());
            }
            char modSymbol = buffer[posEquals - 1];
            double modValue = atof(buffer.c_str() + posEquals +1);

            diffMods[(int)modSymbol] = modValue;
        }

        if( buffer.find("Percolator") != string::npos) {
            percolated = true;
        }
    }// next line
  
    if( warnIfNotPercolated && percolated == false ){
        Verbosity::status("File was not processed by Percolator. "
                          "Filtering on xcorr.");
    }

}

/**
 * \brief Implementation of BuildParser virtual function.  Reads the
 * SQT file and populates the vector of PSMs with the information.
 */
bool SQTreader::parseFile() {
    openRead(true);

    vector<std::string> extensions;
    extensions.push_back(".ms2");
    extensions.push_back(".cms2");
    extensions.push_back(".bms2");
    extensions.push_back(".pms2");
    string fileroot = getFileRoot(getFileName());
    setSpecFileName(fileroot.c_str(), extensions);

    //  parseFile_mine();
    extractPSMs();

    // close file
    if( file.is_open() ) {
        file.close();
    }
    return true;
}

vector<PSM_SCORE_TYPE> SQTreader::getScoreTypes() {
    openRead(false);
    return vector<PSM_SCORE_TYPE>(1, percolated ? PERCOLATOR_QVALUE : SEQUEST_XCORR);
}

/**
 * Read an sqt file beginning with the first S line. collect a
 * list of PSMs that pass score cutoff, populate the library tables
 * with spectrum information.
 */
void SQTreader::extractPSMs()
{
    string buffer;
    double scoreThreshold = getScoreThreshold(SQT);
    Verbosity::debug("Using Percolator q-value threshold %f",
                     scoreThreshold);
  
    while(!file.eof()) {
        if(!buffer.empty())
            buffer.clear();
        
        curPSM_ = new PSM();
        
        bool isEof = false;
        while (file.peek() != 'S') {
            if (file.eof()) {
                isEof = true;
                break;
            }
            getline(file, buffer);
        }
        if (isEof) {
            break;
        }
        getline(file,buffer); //the S line
        
        // read the S line to get spectrum scan number, charge, mass
        int scanNumber, charge;
        sscanf(buffer.c_str(), "%*c %d %*d %d %*d %*s %*lf %*f %*f %*d", 
               &scanNumber,&charge);
        curPSM_->charge = charge;
        curPSM_->specKey = scanNumber;
        
        if (file.peek() != 'M') {
            delete curPSM_;
            curPSM_ = NULL;
            continue;
        }
        buffer.clear();
        getline(file,buffer); //the first M line
        // read the first M line to get score and sequence
        double xcorr, qvalue;
        char wholePepSeq[200];
        sscanf(buffer.c_str(), "%*c %*d %*d %*f %*f %lf %lf %*d %*d %s %*c",
               &xcorr, &qvalue, wholePepSeq);
        if( percolated ){
            curPSM_->score = -1 * qvalue; // q-value negatated by percolator
        } else {
            curPSM_->score = xcorr;
        }

        if( curPSM_->score > scoreThreshold ) {// good matches score 0 to threshold
            delete curPSM_;
            curPSM_ = NULL;
            continue;
        }
        // get the unmodified seq and mods from the file's version of seq
        parseModifiedSeq(wholePepSeq, curPSM_->unmodSeq, curPSM_->mods);
        psms_.push_back(curPSM_);
        Verbosity::comment(V_DETAIL, "Saving PSM: scan %i, charge %i, "
                           "qvalue %.3g, seq %s.", curPSM_->specKey,
                           curPSM_->charge, curPSM_->score, 
                           curPSM_->unmodSeq.c_str());
        curPSM_ = NULL;
    }
   
    PSM_SCORE_TYPE score =  PERCOLATOR_QVALUE;
    if( ! percolated ){
        score = SEQUEST_XCORR;
    }
    buildTables(score);
    
}


/**
 * \brief convert the sequence as presented in the sqt file into an
 * unmodified sequence and a vector of mods.
 *
 * The sqt file format of sequence is flankingAA.SEQ.flankingAA where
 * any modified residue in SEQ is followed by a key character (@%^)
 * whose mass shift is defined in the header.  Any static mods are
 * also defined in the header.  If hasFlankingAA is false the given
 * modSeq does not include the flanking sequences.
 */
void SQTreader::parseModifiedSeq(const char* modSeq, 
                                 string& unmodSeq, 
                                 vector<SeqMod>& mods,
                                 bool hasFlankingAA)
{

    string modSymbols = "*^$@#%"; // all the symbols we may encounter
    int start_idx = 2;            // skip the 'X.' of X.SEQ.X
    if( hasFlankingAA == false ){
        start_idx = 0;
    }

    int modCount = 0;
    // scan sequence for mod symbols, stopping before the flanking aa
    for(int i = start_idx; modSeq[i] != '.' && modSeq[i] != '\0'; i++){

        if(modSymbols.find(modSeq[i]) < string::npos) { //char is a mod symbol

            SeqMod newmod;
            // position is 1-based, we are after the residue
            newmod.position = i - start_idx - modCount;
            newmod.deltaMass = diffMods[(int)modSeq[i]];// check for numDiffMods
            mods.push_back(newmod);
			modCount++;
        } else {
            unmodSeq += modSeq[i];
        }
    }

    // scan all residues for those with static mods
    for(unsigned int i=0; i < unmodSeq.size(); i++) {
        // look up residue in static mod mass array
        double modMass = staticMods[(int)unmodSeq[i]];
        if( modMass > 0 ) {
            SeqMod newmod;
            newmod.position = i + 1; // mods are 1-based in index
            newmod.deltaMass = modMass;
            mods.push_back(newmod);
        }
    }
}

} // namespace

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
