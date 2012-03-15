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
 * Definition of a few structs used for parsing results and spectrum
 * files.  Unlike the RefSpectrum class, allows different kinds of
 * identifiers for the spectra (for finding them in the files).  Also
 * separates peaks into two arrays for ease of inserting into the
 * library.  Also allows flexibility for parsing modifications
 * information.
 */

#include <string>
#include "boost/lexical_cast.hpp"

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

/**
 * \struct PSM
 * \brief A Peptide Spectrum Match (PSM) is a data type to hold
 * information for what will become a reference spectrum in the library. 
 * There are three ways if identifying a spectrum: by name, by scan
 * number or by index.  Most filetypes will use only one of the three.
 */
struct PSM{
  int charge;      ///< charge of the spectrum precursor
  std::string unmodSeq; ///< unmodified version of the sequence [A-Z]*
  std::string modifiedSeq; ///< apply mods to unmod seq as [+/- mass shift]
  std::vector<SeqMod> mods; ///< list of mods on the peptide seq
  int specKey;     ///< a key for identifying a spectrum
  int specIndex;   ///< the index of a spectrum in its file
  double score;    ///< score associated with this paring of spec and seq
  std::string specName; ///< the parentFileName attribute from the scanOrigin element

  PSM()
  : charge(0), specKey(-1), specIndex(-1), score(0) {};

  ~PSM(){ };

  void clear(){
    charge = 0;
    unmodSeq.clear();
    mods.clear();
    specKey = -1;
    specIndex = -1;
    score = 0;
    specName.clear();
  };

  std::string idAsString(){
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

