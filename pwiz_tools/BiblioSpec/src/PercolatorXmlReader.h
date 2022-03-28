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

#pragma once

/*
 * A program that reads through a Percolator v1.17 xml file and stores
 * the search results into library table.
 *
 * PercolatorXmlReader.h
 */

#ifndef PERCOLATOR_READER_H
#define PERCOLATOR_READER_H

#include "BuildParser.h"
#include "SQTreader.h"


class BlibMaker;

/* Percolator schema (at least the parts we want
 *
 * percolator_output
 *   psms
 *     psm (many)
 *        q_value
 *        peptide_seq
 */
namespace BiblioSpec { 

class PercolatorXmlReader : public BuildParser {

 public:

  PercolatorXmlReader(BlibBuilder& maker,
                      const char* filename,
                      const ProgressIndicator* parent_progress); 
  ~PercolatorXmlReader();

  bool parseFile(); // impelement BuildParser virtual function
  vector<PSM_SCORE_TYPE> getScoreTypes();
  virtual void startElement(const XML_Char* name, const XML_Char** attr);
  virtual void endElement(const XML_Char* name);
  virtual void characters(const XML_Char *s, int len);



 private:
  enum STATE {START_STATE, 
              ROOT_STATE, // between the percolator_output tags
              PSMS_STATE, // between the psms tags
              IGNORE_PSM_STATE, // between psm tags IF decoy==true or qval big
              QVALUE_STATE, // between the q_value tags
              PEPTIDES_STATE}; // between the peptides tag
  STATE currentState_;
  char* qvalueBuffer_;  // read characters between q_value tags into here
  char* qvalueBufferPosition_; // current position in the buffer
  map< string, vector<PSM*> > fileMap_; // vector of PSMs for each file
  double qvalueThreshold_;
  double staticMods_[MAX_MODS];
  double diffMods_[MAX_MODS];
  double masses_[128];  // amino acid masses

  void parseId(const XML_Char** attributes);
  void parseSequence(const XML_Char** attributes);
  void addCurPSM();
  void applyModifications(vector<PSM*>& psms, SQTreader& modsReader);

};

} // namespace

#endif







/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
