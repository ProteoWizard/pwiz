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

using namespace std;

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

  void parseId(const XML_Char** attributes);
  void parseSequence(const XML_Char** attributes);
  void addCurPSM();
  void applyModifciations(vector<PSM*>& psms, SQTreader& modsReader);

};

} // namespace

#endif







/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
