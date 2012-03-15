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
