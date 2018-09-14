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

        int oldPrecision = file_.precision(mzPrecision_);

        // write S line
        file_ << "S\t" << id
              << "\t" << id
              << "\t" << fixed << spec.getMz()
              << endl;

        // write I line
        if (spec.getRetentionTime() > 0)
        {
            file_.precision(oldPrecision);
            file_.unsetf(std::ios_base::floatfield);
            file_ << "I\tRTime\t" << spec.getRetentionTime()
                  << endl;
        }

        // write Z line
        file_.precision(mzPrecision_);
        file_ << "Z\t" << spec.getCharge()
              << "\t" << fixed << getPeptideMass(modSeq, masses_ )
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
        file_.precision(oldPrecision);

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
















