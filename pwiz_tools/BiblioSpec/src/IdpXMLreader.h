/*
  Copyright (c) 2011, University of Washington
  All rights reserved.

  Redistribution and use in source and binary forms, with or without
  modification, are permitted provided that the following conditions
  are met:

    * Redistributions of source code must retain the above copyright
    notice, this list of conditions and the following disclaimer. 
    * Redistributions in binary form must reproduce the above
    copyright notice, this list of conditions and the following
    disclaimer in the documentation and/or other materials provided
    with the distribution. 
    * Neither the name of the <ORGANIZATION> nor the names of its
    contributors may be used to endorse or promote products derived
    from this software without specific prior written permission.

  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
  "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
  LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS
  FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE
  COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT,
  INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING,
  BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
  LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
  CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT
  LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN
  ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
  POSSIBILITY OF SUCH DAMAGE.
*/
#pragma once

/**
 * Header file for the IdpXMLreader class.  A class to parse the
 * output files from IDPicker, .idpXML files.
 */

#include <iostream>
#include <string>
#include <map>
#include <vector>
#include <sys/stat.h>
#include "BuildParser.h"
#include "Verbosity.h"

using namespace std;

/*
 * Schema for idpXML files
 *
 * level  element
 * 1      idPickerPeptides
 *   2      proteinIndex
 *     3      protein
 *   2      peptideIndex
 *     3      peptide
 *      4      locus
 *   2      spectraSources
 *     3      spectraSource
 *      4      processingEventList
 *      4      spectrum
 *        5      result
 *         6      id
 */

namespace BiblioSpec {

/**
 * Information for one peptide listed in the XML file
 */
struct PeptideEntry{
  int id;
  char* seq;
  double mass;

  PeptideEntry() 
  : id(-1), seq(NULL), mass(-1) {};

  PeptideEntry(const PeptideEntry& original)
  : id(original.id), mass(original.mass)
  {
    seq = new char[strlen(original.seq)];
    strcpy(seq, original.seq);  
  }

  ~PeptideEntry() {
    delete[] seq; 
  }
};

/**
 * Information for one spectrum listed in the XML file.  Includes
 * information from sub-elements result and id.
 */
// TODO: this is mostly redundant with PSM struct
struct SpectrumEntry{
  char* id_str;          // of the form "controller=0 scan=10437"
  int key;               // for mzXML the id attr, for mxML will be index attr
  int charge;
  double score;
  int pep_id;            // might not need to store this
  PeptideEntry* peptide; // multiple spec can point to same peptide
  vector<SeqMod> mods;   // list of mods on the peptide sequence
  char* modSeq;          // annotated sequence eg. AAA[+deltaMass]AAA

  SpectrumEntry()
  : id_str(NULL), key(-1), charge(-1), score(-1), pep_id(-1)
  {
    peptide = NULL;
    modSeq = NULL;
    // start out with small mods vector
    mods.resize(5);
    mods.clear();
  };

  SpectrumEntry(const SpectrumEntry& original)
  : id_str(NULL),
    key(original.key),
    charge(original.charge),
    score(original.score),
    pep_id(original.pep_id),
    peptide(original.peptide),
    mods(original.mods)
  {
    id_str = new char[strlen(original.id_str)+1];
    strcpy(id_str, original.id_str);
    modSeq = NULL; // to be deleted
  };

  ~SpectrumEntry()
  { delete[] id_str; 
    //delete peptide; // don't delete, other spec point to it
    delete modSeq;
  }
};

/**
 * Class for reading idpXML files, result files from IDPicker.
 * Inherits from SAXHandler.  Parses one XML file  and stores results
 * in three data structures: a vector of protein ids, a
 * hash table of peptides (keyed by id) and a hash table of spectra
 * with references to the peptides.  As the file is read, spectra with
 * peptides are added to library tables with the assistance of a member
 * BlibBuilder object.  The names of associated spectrum files (mzML)
 * are found in the spectraSources element of the idpXML file and are
 * expected to be in the same directory as the idpXML file.
 */
class IdpXMLreader : public BuildParser{

 public:
  IdpXMLreader(BlibBuilder& maker, 
               const char* idpFileName, 
               const ProgressIndicator* parent_progress);
  ~IdpXMLreader();

  bool parseFile();
  virtual void startElement(const XML_Char* name, const XML_Char** attr);
  virtual void endElement(const XML_Char* name);

 private:
  enum STATE { START_STATE, ROOT_STATE, PROT_STATE, PEP_STATE, SPEC_STATE,
               SPEC_RESULT_STATE };

  map<int, PeptideEntry*> peptides; // hash map of peptides stored by ID
  // TODO will need new mapkey.  string?
  //  map<char*, SpectrumEntry*> spectra; // hash map of spectra stored by ID
  map<int, SpectrumEntry*> spectra; // hash map of spectra stored by ID
  vector<int> proteins;             // protein ids of non-decoys
  STATE currentState;               // keep track of level in schema
  PeptideEntry* curPeptide;
  bool curPeptideIsDecoy;           // false until one locus is in proteins
  SpectrumEntry* curSpectrumEntry;    
  int curPepIdCount;               // number peptide ids for cur spec entry

  /**
   * Methods for storing information from the respective elements
   */
  void parseProtein(const XML_Char** attributes);
  void parsePeptide(const XML_Char** attributes);
  void setSpecFilename(const XML_Char** attributes);
  void parseSpectrum(const XML_Char** attributes);
  void parseResult(const XML_Char** attributes);
  void parseId(const XML_Char** attributes);
  void parseModifications(const XML_Char** attributes);
  void addPeptide();
  void addSpectrum();
};

} // namespace











/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
