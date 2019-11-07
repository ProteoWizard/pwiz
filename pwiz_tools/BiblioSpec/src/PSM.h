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
/**
 * Definition of a few structs used for parsing results and spectrum
 * files.  Unlike the RefSpectrum class, allows different kinds of
 * identifiers for the spectra (for finding them in the files).  Also
 * separates peaks into two arrays for ease of inserting into the
 * library.  Also allows flexibility for parsing modifications
 * information.
 */

#include <set>
#include <vector>
#include "pwiz/utility/misc/optimized_lexical_cast.hpp"
#include "SmallMolMetadata.h"
#include "BlibUtils.h" // For IONMOBILITY_TYPE enum

/**
 * \struct SeqMod
 * A pair containing the mass shift of a modification and the position
 * (1-based) in a spectrum.
 */
struct SeqMod{
  int position;
  double deltaMass;
 
  SeqMod() 
  : position(-1), deltaMass(0) {};

  SeqMod(const SeqMod& original)
  : position(original.position), deltaMass(original.deltaMass) {};

  SeqMod(int pos, double mass)
  : position(pos), deltaMass(mass) {};
};

struct Protein {
  std::string accession;
  Protein() {}
  Protein(const std::string& s): accession(s) {}
};

/**
 * \struct PSM
 * \brief A Peptide Spectrum Match (PSM) is a data type to hold
 * information for what will become a reference spectrum in the library. 
 * There are three ways if identifying a spectrum: by name, by scan
 * number or by index.  Most filetypes will use only one of the three.
 * N.B. as we extend Skyline to generalized small molecules, "PSM" is
 * a misnomer, like a lot of things in Skyline
 */
struct PSM{
  int charge;      ///< charge of the spectrum precursor
  std::string unmodSeq; ///< unmodified version of the sequence [A-Z]*
  std::string modifiedSeq; ///< apply mods to unmod seq as [+/- mass shift]
  std::vector<SeqMod> mods; ///< list of mods on the peptide seq
  int specKey;     ///< a key for identifying a spectrum
  int specIndex;   ///< the index of a spectrum in its file
  double score;    ///< score associated with this paring of spec and seq
  double ionMobility; ///< e.g. drift time, inverse reduced ion mobility, or compensation voltage
  BiblioSpec::IONMOBILITY_TYPE ionMobilityType;
  double ccs; // collisional cross section (ion mobility information)
  std::string specName; ///< the parentFileName attribute from the scanOrigin element
  std::set<const Protein*> proteins;

  // Small molecule stuff
  SmallMolMetadata smallMolMetadata;

  PSM()
  : charge(0), specKey(-1), specIndex(-1), score(0), ionMobility(0), ionMobilityType(BiblioSpec::IONMOBILITY_NONE), ccs(0) {};
  
  virtual ~PSM(){ };

  void clear(){
    charge = 0;
    unmodSeq.clear();
    modifiedSeq.clear();
    mods.clear();
    specKey = -1;
    specIndex = -1;
    score = 0;
    ionMobility = 0;
    ionMobilityType = BiblioSpec::IONMOBILITY_NONE;
    ccs = 0;
    specName.clear();
    smallMolMetadata.clear();
    proteins.clear();
  };

  bool IsCompleteEnough() const
  {
      return (specKey >= 0 || !specName.empty()) && 
          (smallMolMetadata.IsCompleteEnough() ||
           (charge != 0 && !unmodSeq.empty()));
  }

  std::string idAsString() const {
    std::string result;
        if( ! specName.empty() ){
            result = specName;
        } else if( specKey != -1 ){
            result =  boost::lexical_cast<std::string>(specKey);
        } else if( specIndex != -1 ){
            result = boost::lexical_cast<std::string>(specIndex);
        } 
        return result;
    };
};

struct PSMSpecKeySorter {
  bool operator() (const PSM* x, const PSM* y) { return x->specKey < y->specKey; }
};

struct PSMSpecIndexSorter {
  bool operator() (const PSM* x, const PSM* y) { return x->specIndex < y->specIndex; }
};
