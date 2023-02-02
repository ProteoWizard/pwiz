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
 * The IdpXMLreader class definition.  A class to parse the output
 * files of IDPicker.  Used in the building of libraries.  IDPicker is
 * a post-search processer whose outuput contains only those PSMs
 * deemed correct.  Therefore all results should be included in
 * library. 
 */

#include "IdpXMLreader.h"

namespace BiblioSpec {

IdpXMLreader::IdpXMLreader(BlibBuilder& maker,
                           const char* idpFileName,
                           const ProgressIndicator* const parent_progress) 
: BuildParser(maker, idpFileName, parent_progress),
    currentState(START_STATE),
    curPeptide(NULL),
    curSpectrumEntry(NULL),
    curPepIdCount(0)
{

    this->setFileName(idpFileName);// this is done for the saxhandler

}

IdpXMLreader::~IdpXMLreader() 
{
    delete curSpectrumEntry;
    delete curPeptide;
    map<int, PeptideEntry*>::iterator itPep = peptides.begin();
    while (itPep != peptides.end()) {
        delete itPep->second;
        itPep++;
    }
    peptides.clear();
    map<int, SpectrumEntry*>::iterator itSpec = spectra.begin();
    while (itSpec != spectra.end()) {
        delete itSpec->second;
        itSpec++;
    }
    spectra.clear();
}


// populates the vector of proteins,
// hash of peptides, and hash of spec
// via startElement and endElement methods.
// as spectrum source files are encountered,
// the spectra are added to tables
bool IdpXMLreader::parseFile() {
    return parse();
}

vector<PSM_SCORE_TYPE> IdpXMLreader::getScoreTypes() {
    return vector<PSM_SCORE_TYPE>(1, IDPICKER_FDR);
}

void IdpXMLreader::startElement(const XML_Char* name, 
                                const XML_Char** attributes) {

    // Six possible states, deal with new element accordingly
    switch(currentState) {
    case START_STATE:
        if(isElement("idPickerPeptides", name)) {
            currentState = ROOT_STATE;
        }
        // else error, missing idpicker element
        break;

    case ROOT_STATE:
        if(isElement("proteinIndex", name)) {
            currentState = PROT_STATE;

        } else if(isElement("peptideIndex", name)) {
            currentState = PEP_STATE;

        } else if(isElement("spectraSources", name)) {
            currentState = SPEC_STATE;
            //int numSpecFiles = atoi(getAttrValue("count", attributes));
            int numSpecFiles = getIntRequiredAttrValue("count", attributes);
            if( numSpecFiles > 1 )
                initSpecFileProgress(numSpecFiles);
        }
        break;

    case PROT_STATE:
        if(isElement("protein", name)) {
            parseProtein(attributes);
        }
        break;

    case PEP_STATE:
        if(isElement("peptide", name)) {
            parsePeptide(attributes);
        } else if(isElement("locus", name)) {

            //int locusId = atoi(getRequiredAttrValue("id", attributes));
            int locusId = getIntRequiredAttrValue("id", attributes);
            // if locus id in proteins, curPeptideIsDecoy = false
            // don't reset to true in case prior locus was real
            if( binary_search(proteins.begin(), proteins.end(), locusId) ) {
                curPeptideIsDecoy = false;
            }
        }
        break;

    case SPEC_STATE:
        if(isElement("spectraSource", name)) {
            setSpecFilename(attributes);

        } else if(isElement("spectrum", name)) {
            parseSpectrum(attributes);

        } else if(isElement("result", name)) {
            parseResult(attributes);
            currentState = SPEC_RESULT_STATE;
        }
        break;

    case SPEC_RESULT_STATE:
        if(isElement("result", name)) {
            currentState = SPEC_RESULT_STATE;

        } else if(isElement("id", name)) {
            parseId(attributes);
        }
        break;

    }
}

void IdpXMLreader::endElement(const XML_Char* name) {

    // Change the state
    switch(currentState) {

    case START_STATE:
        // end of document
        break;

    case ROOT_STATE:
        if(!isElement("idPickerPeptides", name)) {
            //cerr << "element end should be idpicker" << endl;
        }
        break;

    case PROT_STATE:
        if(isElement("proteinIndex", name)) {
            currentState = ROOT_STATE;
        }
        break;

    case PEP_STATE:
        if(isElement("peptideIndex", name)) {
            currentState = ROOT_STATE;
        } else if( isElement("peptide", name)) {
            addPeptide();
        }
        break;

    case SPEC_STATE:
        if(isElement("spectraSources", name)) {
            currentState = ROOT_STATE;

        } else if(isElement("spectraSource", name)) {
            // put these spec in the tables, empty the spectrum hash
            buildTables(IDPICKER_FDR);

        } else if(isElement("spectrum", name)) {
            addSpectrum();
        }
        break;


    case SPEC_RESULT_STATE:
        if(isElement("result", name)) {
            currentState = SPEC_STATE;
        }
        break;

    }// end switch

}

/**
 * Add the protein ID to the collection of protein if it is not a
 * decoy. Assumes current element is protein.
 */
void IdpXMLreader::parseProtein(const XML_Char** attributes) {


    //int isDecoy = atoi(getRequiredAttrValue("decoy", attributes));
    int isDecoy = getIntRequiredAttrValue("decoy", attributes);

    if( !isDecoy ) {
        //    int curProtId = atoi(getRequiredAttrValue("id", attributes));
        int curProtId = getIntRequiredAttrValue("id", attributes);
        proteins.push_back(curProtId);
        Verbosity::comment(V_DETAIL, "Parsing protein id %i", curProtId);
    } else {
        Verbosity::comment(V_DETAIL, "Parsing decoy protein");
    }
    // TODO: are proteins always in sorted order by id???
}

/**
 * Create a new PeptideEntry, populate its members with the values of
 * the current element in the saxhandler and add the entry to the hash
 * of peptides.  No check for redundancy.  Assumes that the current XML
 * element is a 'peptide' element.
 */
void IdpXMLreader::parsePeptide(const XML_Char** attributes) {

    // assert(this->curPeptide == NULL);
    curPeptide = new PeptideEntry();

    //  curPeptide->id = atoi(getRequiredAttrValue("id", attributes));
    curPeptide->id = getIntRequiredAttrValue("id", attributes);
    const char* tmp_seq = getRequiredAttrValue("sequence", attributes);
    curPeptide->seq = new char[strlen(tmp_seq) + 1];
    strcpy(curPeptide->seq, tmp_seq);
    //  curPeptide->mass = atof(getRequiredAttrValue("mass", attributes));
    curPeptide->mass = getDoubleRequiredAttrValue("mass", attributes);
    Verbosity::comment(V_DETAIL, "Parsing peptide %s.", tmp_seq);

}

/**
 * Once we reach the end of the peptide element, add it if at least
 * one of its proteins is in the collection of proteins.  Current
 * peptide entry and inclusion stored as part of the reader
 */
void IdpXMLreader::addPeptide() {

    if( ! this->curPeptideIsDecoy ) {

        peptides.insert( pair<int, PeptideEntry*>(curPeptide->id, curPeptide) );
        // TODO? faster with map<char,int>::iterator it; it=mymap.rbegin(); mymap.insert (it, pair<char,int>('b',300));

    } else {
        //cerr << "Peptide number " << curPeptide->id << " (" << curPeptide->seq 
        //     << ") is a decoy and will not be added." << endl;
        delete curPeptide;
    }

    this->curPeptideIsDecoy = true;
    this->curPeptide = NULL;
}

/**
 * Use the attribute 'name' from the spectraSource element to set the
 * current filename. Assumes the current element in the sax parser is
 * a spectraSource.  Checks for file and throws error if unreadable.
 */
void IdpXMLreader::setSpecFilename(const XML_Char** attributes) {

    const char* name = getAttrValue("name", attributes);
    vector<std::string> extensions;
    extensions.push_back(".mzML");
    extensions.push_back(".mzXML");
    setSpecFileName(name, extensions);

}

/**
 * Create a new SpectrumEntry, populate its members with the values of
 * the current element in the saxhandler (charge, key, id string).
 * Store in the readers member variable, curSpectrumElement as there
 * are more values to be added from other elements. Assumes that the
 * current XML element is a 'spectrum' element. 
 */
void IdpXMLreader::parseSpectrum(const XML_Char** attributes) {

    // check that there is not currently a spectrum entry being stored
    if(curSpectrumEntry != NULL) {
        throwParseError("Cannot parse spectrum %d.", curSpectrumEntry->key);
    }
    Verbosity::comment(V_DETAIL, "Parsing spectrum"); 
    curSpectrumEntry = new SpectrumEntry();

    curSpectrumEntry->charge = getIntRequiredAttrValue("z", attributes);

    const char* tmp_id = getAttrValue("id", attributes);
    // if there is no 'id' attribute, try 'scan' attribute
    if (strlen(tmp_id) == 0)
    {
        tmp_id = getAttrValue("scan", attributes);
        if (strlen(tmp_id) == 0)
            throwParseError("Spectrum tag contains neither id nor scan attribute.");
    }

    curSpectrumEntry->id_str = new char[strlen(tmp_id)+1];
    strcpy(curSpectrumEntry->id_str, tmp_id);

    // find the scan=
    const char* id_str = strrchr(tmp_id, '='); // find last = 
    if( id_str != NULL ) { 
        // confirm that scan preceeds it
        if( strncmp(id_str-4, "scan=", strlen("scan=")) != 0 ) {
            throwParseError("Cannot find scan in spectrum id '%s'", tmp_id);
        }
        id_str++;  // point to the int after =

    } else { // in some .idpXML files, the id value is just the scan number
        // try converting the id to an int
        if( atoi(tmp_id) == 0 ) { // error value
            throwParseError("The spectrum id '%s' cannot be parsed.", tmp_id);
        }
        // else tmp_id is an int, set id_str to tmp_id
        id_str = tmp_id;
    }

    curSpectrumEntry->key = atoi(id_str);

    Verbosity::comment(V_DETAIL, "Parsing spectrum id %i z %i", 
                       curSpectrumEntry->key, curSpectrumEntry->charge);
    // when we switch to mxML, use index as key
    //curSpectrumEntry->key = atoi(getAttrValue("index", attributes));
}

/**
 * Save the FDR from top-ranking results in the curSpectrumEntry.
 * Assumes that the current element in the saxhandler is a result.
 */
void IdpXMLreader::parseResult(const XML_Char** attributes) {

    //  int rank = atoi(getAttrValue("rank", attributes));
    int rank = getIntRequiredAttrValue("rank", attributes);
    //  double fdr = atof(getAttrValue("FDR", attributes));
    double fdr = getDoubleRequiredAttrValue("FDR", attributes);

    // check that there is already a spectrum in progress
    if( curSpectrumEntry == NULL ) {
        throwParseError("Result (rank %d FDR %f) could not be parsed. "
                        "No associated spectrum.", rank, fdr);
    }

    // add score for top-ranking result
    if( rank == 1 ) {
        curSpectrumEntry->score = fdr;
    }
}

/**
 * Save the peptide id and any modifications in the curSpectrumEntry.
 * If this is not the first id for a spectrum, return leaving
 * the SE unchanged.
 * Assumes that the current element in the sax handler is an id.
 */
void IdpXMLreader::parseId(const XML_Char** attributes) {

    int pep_id = getIntRequiredAttrValue("peptide", attributes);

    // check that there is a spectrum in progress
    if( curSpectrumEntry == NULL ) {
        throwParseError("No spectrum associated with peptide id %d.", pep_id);
    }

    // If the peptide for this spectrum is not in the map, assume it was a decoy
    if (peptides.find(pep_id) == peptides.end())
        return;

    curPepIdCount++;

    if( curPepIdCount > 1 ) {
        // copy the current, push current on the list, add this id to copy
        SpectrumEntry* copySE = new SpectrumEntry(*curSpectrumEntry);
        addSpectrum();
        curSpectrumEntry = copySE;
    }
  
    curSpectrumEntry->pep_id = pep_id;
  
    // get any mods
    parseModifications(attributes);

}

/**
 * Turn the mods string into entries in the mods vector of the current
 * SpectrumEntry.  Mods string of the form "int|c|n:float" with
 * multiple mods separated with a space.  The position c is converted
 * to the beginning of the sequence, 1, and n to the end (strlen) of
 * the sequence.
 */
void IdpXMLreader::parseModifications(const XML_Char** attributes) {
    const char* mods = getAttrValue("mods", attributes); 
    if( strlen(mods) == 0 ) {// mods vector stays empty
        return;
    }

    // get first, then find any others by searching for a space character
    SeqMod curMod;
    const char* space = strchr(mods, ' ');
    const char* next_mod = mods;

    do{

        // mods in form of [int|n|c]:float, check n or c first
        if( *next_mod == 'n' ) {
            curMod.position = 1;
            sscanf(next_mod, "n:%lf", &curMod.deltaMass);

        } else if( *next_mod == 'c' ) {
            // get peptide
            map<int, PeptideEntry*>::iterator pep_iter =  
                peptides.find(curSpectrumEntry->pep_id);
            curMod.position = strlen(pep_iter->second->seq);
            sscanf(next_mod, "c:%lf", &curMod.deltaMass);

        } else {
            sscanf(next_mod, "%i:%lf", &curMod.position, &curMod.deltaMass);
        }

        curSpectrumEntry->mods.push_back(curMod);
        space = strchr(next_mod, ' ');
        next_mod = space + 1;

    }while( space != NULL );
    

}


/**
 * \brief Add information for the current spectrum to the list of
 * PSMs.
 *
 * Create a PSM, set fields, add to vector.  Do not add the spectrum
 * if the peptide is a decoy.  Add even if there are duplicates.
 * BuildParser will weed those out before creating the library.
 */
void IdpXMLreader::addSpectrum() {

    // look for the associated peptide sequence
    map<int, PeptideEntry*>::iterator pep_iter =  
        peptides.find(curSpectrumEntry->pep_id);

    // new implementation...
    // do not add decoys (which were not added to the set of peptides)
    if( pep_iter == peptides.end() ) {
        /*
          cerr << "WARNING: Spetrum " << curSpectrumEntry->key
          << " is matched to a decoy sequence and will not be added"
          " to the library" << endl;
        */
    } else {
        curSpectrumEntry->peptide = pep_iter->second;

        curPSM_ = new PSM();
        curPSM_->charge = curSpectrumEntry->charge;
        curPSM_->score = curSpectrumEntry->score;
        curPSM_->specKey = curSpectrumEntry->key;
        curPSM_->unmodSeq.assign(curSpectrumEntry->peptide->seq);
        curPSM_->mods = curSpectrumEntry->mods;

        psms_.push_back(curPSM_);
        curPSM_ = NULL;
    }
    delete curSpectrumEntry;
    curSpectrumEntry = NULL;
    curPepIdCount = 0;
}

} // namespace

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
