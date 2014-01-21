//
// Original Author: Parag Mallick
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics 
//   Cedars Sinai Medical Center, Los Angeles, California  90048
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


#include "digest.hpp"

double Digest::computeMass(string peptide){
  double mass  = 0;
  if (peptide.length () > 0) {
    mass = massMap_['o']+ 2 * massMap_['h']; //simply compute normal, not [H+]
    //yes - I should be doing all this with iterators - but I'll do that in the next version
    for(size_t i=1;i<peptide.length()-1;i++){
      if(isalpha(peptide[i])){
        mass += massMap_[peptide[i]];
      }
      else{
        mass += massMap_['X'];
      }
    }
  }
  return mass;
}

void Digest::createPeptides(){
  std::transform(sequence_.begin(), sequence_.end(), sequence_.begin(), 
                 (int(*)(int)) toupper);
  
  size_t length = sequence_.length();
  if(sequence_[length-1] != '*'){
    sequence_.push_back('*');
    // WCH: added to reflect the true length 
    length++;
  }

  string peptide;
  for(size_t start=0;start<length;start++){
    if((start == 0) || 
       (sequence_[start] == 'K' && sequence_[start+1] != 'P') || 
       (sequence_[start] == 'R' && sequence_[start+1] != 'P')) {
      //WCH: Original code: don't know why double is used
      //double numMisCleavages = -1;
      int numMisCleavages = 0;  // WCH
      for(size_t end=start+1;((end<length) && (((int)(end-start)) < config_._maxSeqLength));end++){
        if((sequence_[end] == 'K') || (sequence_[end] == 'R') || (sequence_[end] == '*')){
          if(end < length){
            if(sequence_[end+1] != 'P'){
              peptide = sequence_.substr(start,((int)(end-start))+2);
            } else {
              //WCH: this should not be considered as a cleavage
              //     continue to find the next cleavage
              continue;
            }
          }
          else{
            peptide = sequence_.substr(start,((int)(end-start))+2);
          }
          double mass;
          if (start == 0) {
            // WCH: we should include N-term residue!
            mass = computeMass(string(" ").append(peptide));
          } else {
            mass = computeMass(peptide);
          }
          if((mass > config_._minMass) && 
             (mass<config_._maxMass) &&
             (((int)(end-start)) > config_._minSeqLength)
             ){
	    //	    cout<<peptide<<endl;
            peptides_.push_back(peptide);
            massVector_.push_back(mass);
          }
          numMisCleavages++;
          // WCH: shifted from outer loop as it is more efficient
          //      to check number of cleavage after we have updated it
	  // PM: OK - nice change.
          if(numMisCleavages > config_._numAllowedMisCleavages){
            break;
          }
        }
      } /*for(size_t end=start;((end<length) && (((int)(end-start)) < config_._maxSeqLength));end++){*/

    }
  }
}

void Digest::initMassMap(){
  
  bool useAvgMass = false;

  if (useAvgMass == true) /*avg masses*/
    {
      massMap_['h']=  1.00794;  /* hydrogen */
      massMap_['o']= 15.9994;   /* oxygen */
      massMap_['c']= 12.0107;   /* carbon */
      massMap_['n']= 14.00674;  /* nitrogen */
      massMap_['p']= 30.973761; /* phosporus */
      massMap_['s']= 32.066;    /* sulphur */

      massMap_['G']= 57.05192;
      massMap_['A']= 71.07880;
      massMap_['S']= 87.07820;
      massMap_['P']= 97.11668;
      massMap_['V']= 99.13256;
      massMap_['T']=101.10508;
      massMap_['C']=103.13880;
      massMap_['L']=113.15944;
      massMap_['I']=113.15944;
      massMap_['X']=113.15944;
      massMap_['N']=114.10384;
      massMap_['O']=114.14720;
      massMap_['B']=114.59622;
      massMap_['D']=115.08860;
      massMap_['Q']=128.13072;
      massMap_['K']=128.17408;
      massMap_['Z']=128.62310;
      massMap_['E']=129.11548;
      massMap_['M']=131.19256;
      massMap_['H']=137.14108;
      massMap_['F']=147.17656;
      massMap_['R']=156.18748;
      massMap_['Y']=163.17596;
      massMap_['W']=186.21320;
    }
  else /* monoisotopic masses */
    {
      massMap_['h']=  1.0078250;
      massMap_['o']= 15.9949146;
      massMap_['c']= 12.0000000;
      massMap_['n']= 14.0030740;
      massMap_['p']= 30.9737633;
      massMap_['s']= 31.9720718;

      massMap_['G']= 57.0214636;
      massMap_['A']= 71.0371136;
      massMap_['S']= 87.0320282;
      massMap_['P']= 97.0527636;
      massMap_['V']= 99.0684136;
      massMap_['T']=101.0476782;
      massMap_['C']=103.0091854;
      massMap_['L']=113.0840636;
      massMap_['I']=113.0840636;
      massMap_['X']=113.0840636;
      massMap_['N']=114.0429272;
      massMap_['O']=114.0793126;
      massMap_['B']=114.5349350;
      massMap_['D']=115.0269428;
      massMap_['Q']=128.0585772;
      massMap_['A']= 71.0371136;
      massMap_['S']= 87.0320282;
      massMap_['P']= 97.0527636;
      massMap_['V']= 99.0684136;
      massMap_['T']=101.0476782;
      massMap_['C']=103.0091854;
      massMap_['L']=113.0840636;
      massMap_['I']=113.0840636;
      massMap_['N']=114.0429272;
      massMap_['O']=114.0793126;
      massMap_['B']=114.5349350;
      massMap_['D']=115.0269428;
      massMap_['Q']=128.0585772;
      massMap_['K']=128.0949626;
      massMap_['Z']=128.5505850;
      massMap_['E']=129.0425928;
      massMap_['M']=131.0404854;
      massMap_['H']=137.0589116;
      massMap_['F']=147.0684136;
      massMap_['R']=156.1011106;
      massMap_['Y']=163.0633282;
      massMap_['W']=186.0793126;
    }
}
