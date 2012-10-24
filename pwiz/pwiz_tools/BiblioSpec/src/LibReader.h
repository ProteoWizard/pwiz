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

/****************************************************************
 *This class is to select all the library RefSpectrum given
 *an experiment precursorMZ and charge.
 *
 * $Id: LibReader.h,v 1.0 2008/10/24 09:53:52 Ning Zhang Exp $
 *
 ****************************************************************/

#ifndef LIBREADER_H
#define LIBREADER_H

#include <string>
#include <vector>
#include <deque>
#include <iostream>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include "sqlite3.h"
#include "zlib.h"
#include "RefSpectrum.h"
#include "Verbosity.h"

namespace BiblioSpec {

class LibReader{
 public:
  LibReader();
  LibReader(const char* libName);
  ~LibReader();

  int getSpecInMzRange(double minMz, double maxMz, int minPeaks,
                       vector<RefSpectrum*>& returnedSpectra );
  int getSpecInMzRange(double minMz, double maxMz, int minPeaks, 
                       deque<RefSpectrum*>& returnedSpectra );
  RefSpectrum getRefSpec(int libID); //given specific libSpecNumber, get a RefSpectrum
  bool getRefSpec(int libID, RefSpectrum& spec);
  vector<RefSpectrum> getRefSpecsInRange(int lowLibID, int highLibID);
  int getAllRefSpec(vector<RefSpectrum*>& spec);
  bool getNextSpectrum(RefSpectrum& spec);

  //setters and getters
  //  void setLibName(const char* libName);
  void setLowMZ(double lowMZ);
  void setHighMZ(double highMZ);
  void setCharge(int chg);
  void setLowChg(int lowChg);
  void setHighChg(int highChg);

  double getLowMZ();
  double getHighMZ();
  int getCharge();
  int getLowChg();
  int getHighChg();
  //  int getTotalCount();
  int countAllSpec();
  

  void initialize();
 protected:
  char  libraryName_[1024];
  sqlite3* db_;
  double expLowMZ_; //experimental spectral low_end precursorMZ
  double expHighMZ_; //experimental spectral high_end precursorMZ
  int expPreChg_;   //experimental charge if determined
  int expLowChg_;  //experimental charge range low end if not determined
  int expHighChg_;  //experimental charge range high end if not determined

  int totalCount_; //total RefSpectra in the mz range
  int curSpecId_;  // id of the next spectrum to get when getNextSpec called
  int maxSpecId_;  // biggest spec id in the library
  
  vector<PEAK_T> getUncompressedPeaks(int& numPeaks, int& mzLen, Byte* comprM,int& intensityLen, Byte* comprI);
  void setMaxLibId();
};

} // namespace

#endif        //end of LIBREADER_H

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
