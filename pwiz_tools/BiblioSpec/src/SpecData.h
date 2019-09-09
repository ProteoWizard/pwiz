//
// $Id: SpecFileReader.h 9985 2016-08-24 23:47:32Z pcbrefugee $
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

#include "Verbosity.h"
#include "PSM.h"

namespace BiblioSpec {

struct SpecData{
    int id;
    float ionMobility; // precursor ion mobility
    IONMOBILITY_TYPE ionMobilityType;
    float ccs; // collisional cross section
    double retentionTime; // in minutes
    double startTime;
    double endTime;
    double totalIonCurrent;
    double mz;
    int charge;
    int numPeaks;
    double* mzs;
    float* intensities;
    float* productIonMobilities; // In Waters machines, product ions have kinetic energy added after the drift tube and thus slightly faster time than the precursor from there to the detector.
    
    SpecData():
        id(0), ionMobility(0), ionMobilityType(IONMOBILITY_NONE), ccs(0), retentionTime(0), startTime(0), endTime(0), mz(0), charge(0), numPeaks(-1){
        mzs = NULL;
        intensities = NULL;
        productIonMobilities = NULL;
    };

    ~SpecData(){
        delete [] mzs;
        delete [] intensities;
        delete [] productIonMobilities;
    };

    SpecData& operator=(SpecData& rhs){
        id = rhs.id;
        ionMobility = rhs.ionMobility;
        ionMobilityType = rhs.ionMobilityType;
        ccs = rhs.ccs;
        retentionTime = rhs.retentionTime;
        startTime = rhs.startTime;
        endTime = rhs.endTime;
        mz = rhs.mz;
        charge = rhs.charge;
        numPeaks = rhs.numPeaks;

        // clear any existing peaks
        delete [] mzs;
        delete [] intensities;
        delete [] productIonMobilities;
        mzs = NULL;
        intensities = NULL;
        productIonMobilities = NULL;

        if( numPeaks){
            mzs = new double[numPeaks];
            intensities = new float[numPeaks];
            productIonMobilities = ( (rhs.productIonMobilities == NULL) ? NULL : new float[numPeaks] );
            for(int i=0; i<numPeaks; i++){
                mzs[i] = rhs.mzs[i];
                intensities[i] = rhs.intensities[i];
                if (rhs.productIonMobilities != NULL)
                    productIonMobilities[i] = rhs.productIonMobilities[i];
            }   
        }
        return *this;
    }

    // In Waters machines, product ions have kinetic energy added after the drift tube and thus fly slightly faster than the precursor from there to the detector.
    double getIonMobilityHighEnergyOffset() const
    {
        if (productIonMobilities != NULL)
        {
            double sum = 0;
            for (int i=0; i<numPeaks; i++)
                sum += productIonMobilities[i];
            if (sum > 0)
                return (sum/numPeaks)-ionMobility;
        }
        return 0;
    }
};

} // namespace

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
















