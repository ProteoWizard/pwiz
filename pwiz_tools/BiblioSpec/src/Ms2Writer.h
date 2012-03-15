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
 *  The Ms2Writer is a class for printing spectra in the .ms2 format
 */

#include "Verbosity.h"
#include "BlibUtils.h"
#include "SpecFileReader.h"
#include "RefSpectrum.h"
#include "AminoAcidMasses.h"
#include "boost/program_options.hpp"

namespace ops = boost::program_options;

namespace BiblioSpec {

class Ms2Writer {
 public:
    Ms2Writer(const ops::variables_map& options_table) :
    mzPrecision_(options_table["mz-precision"].as<int>()),
    intensityPrecision_(options_table["intensity-precision"].as<int>())
    {
        Verbosity::comment(BiblioSpec::V_DETAIL, 
                                       "Creating Ms2Writer.");
        AminoAcidMasses::initializeMass(masses_, 0);// average isotopic mass
    };

    ~Ms2Writer(){
        if( file_.is_open() ){
            file_.close();
        }
    };

    /** 
     * Open the filename given and prepare to write to the file.
     * Throw error if can't be opened.  Currently no check for file
     * type or overwriting.
     */ 
    void openFile(const char* filename){
        Verbosity::comment(BiblioSpec::V_DETAIL, 
                           "Ms2Writer preparing file '%s'.",
                           filename);
        filename_ = filename;

        file_.open(filename);

        if( ! file_.is_open() ){
            Verbosity::error("Cannot open ms2 file %s.", filename);
        }

        // write header
        time_t t = time(NULL);
        char* date = ctime(&t);
        file_ << "H\tCreationDate\t" << date 
              << "H\tExtractor\tBlibToMs2" << endl;
    };
    
    /**
     * Write the name of the library to the file.
     */
    void writeLibName(const char* libName){
        if( ! file_.is_open() ){
            Verbosity::error("Cannot write library name to un-open ms2 file.");
        }

        file_ << "H\tComment\tLibrary\t" << libName << endl;
    };

    /**
     * Write the given spectrum to file.
     */
    bool writeSpectrum(const RefSpectrum& spec){

        int id = spec.getLibSpecID();
        string modSeq = spec.getMods();

        // write S line
        file_ << "S\t" << id
              << "\t" << id
              << "\t" << spec.getMz()
              << endl;

        // write Z line
        file_ << "Z\t" << spec.getCharge()
              << "\t" << getPeptideMass(modSeq, masses_ )
              << endl;

        // write D line (seq)
        file_ << "D\tseq\t" << spec.getSeq() << endl;
        file_ << "D\tmodified seq\t" << modSeq << endl;

        // write peaks
        const vector<PEAK_T>& peaks = spec.getRawPeaks(); 
        for(int i = 0; i < (int)peaks.size(); i++){
            file_.precision(mzPrecision_);
            file_ << fixed << peaks[i].mz << "\t";
            file_.precision(intensityPrecision_);
            file_ << peaks[i].intensity << endl;
        }

        return true;
    };

 private:
    ofstream file_;
    string filename_;
    double masses_[128];
    int mzPrecision_;
    int intensityPrecision_;

};

} // namespace BiblioSpec

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
















