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
    float driftTime; // precursor ion mobility
    float ccs; // collisional cross section
    double retentionTime; // in minutes
    double mz;
    int numPeaks;
    double* mzs;
    float* intensities;
    float* productDriftTimes; // In Waters machines, product ions have kinetic energy added after the drift tube and thus slightly faster time than the precursor from there to the detector.
    
    SpecData():
    id(0), driftTime(0), ccs(0), retentionTime(0), mz(0), numPeaks(-1){
        mzs = NULL;
        intensities = NULL;
        productDriftTimes = NULL;
    };

    ~SpecData(){
        delete [] mzs;
        delete [] intensities;
        delete [] productDriftTimes;
    };

    SpecData& operator=(SpecData& rhs){
        id = rhs.id;
        driftTime = rhs.driftTime;
        ccs = rhs.ccs;
        retentionTime = rhs.retentionTime;
        mz = rhs.mz;
        numPeaks = rhs.numPeaks;

        // clear any existing peaks
        delete [] mzs;
        delete [] intensities;
        delete [] productDriftTimes;
        mzs = NULL;
        intensities = NULL;
        productDriftTimes = NULL;

        if( numPeaks){
            mzs = new double[numPeaks];
            intensities = new float[numPeaks];
            productDriftTimes = ( (rhs.productDriftTimes == NULL) ? NULL : new float[numPeaks] );
            for(int i=0; i<numPeaks; i++){
                mzs[i] = rhs.mzs[i];
                intensities[i] = rhs.intensities[i];
                if (rhs.productDriftTimes != NULL)
                    productDriftTimes[i] = rhs.productDriftTimes[i];
            }   
        }
        return *this;
    }

    // In Waters machines, product ions have kinetic energy added after the drift tube and thus fly slightly faster than the precursor from there to the detector.
    double getDriftTimeHighEnergyOffsetMsec() const
    {
        if (productDriftTimes != NULL)
        {
            double sum = 0;
            for (int i=0; i<numPeaks; i++)
                sum += productDriftTimes[i];
            if (sum > 0)
                return (sum/numPeaks)-driftTime;
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
















