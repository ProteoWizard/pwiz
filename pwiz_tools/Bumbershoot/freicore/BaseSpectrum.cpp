//
// $Id$
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
// The Original Code is the Bumbershoot core library.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//

#include "stdafx.h"
#include "BaseSpectrum.h"

using namespace freicore;

namespace freicore
{
    BaseSpectrum::BaseSpectrum()
        :    peakPreCount(0), peakCount(0), numFragmentChargeStates(0),
            mzOfPrecursor(0), mOfPrecursor(0), mOfUnadjustedPrecursor(0), retentionTime(0),
            processingTime(0), mzUpperBound(0), mzLowerBound(0), totalIonCurrent(0), totalPeakSpace(0)
    {}

    BaseSpectrum::BaseSpectrum( const BaseSpectrum& old )
    {
        id                        = old.id;
        nativeID                = old.nativeID;
        fileName                = old.fileName;

        peakPreCount            = old.peakPreCount;
        peakCount                = old.peakCount;

        mzOfPrecursor            = old.mzOfPrecursor;
        mOfPrecursor            = old.mOfPrecursor;
        mOfUnadjustedPrecursor    = old.mOfUnadjustedPrecursor;
        retentionTime            = old.retentionTime;
        processingTime            = old.processingTime;
        mzUpperBound            = old.mzUpperBound;
        mzLowerBound            = old.mzLowerBound;
        totalIonCurrent            = old.totalIonCurrent;
        totalPeakSpace            = old.totalPeakSpace;
    }

    BaseSpectrum::~BaseSpectrum()
    {}

    double BaseSpectrum::CalculateComplementMz( double mz, int z )
    {
        double chargedPrecursorMass = mOfPrecursor + ( id.charge * PROTON );
        double chargedFragmentMass = mz * z;
        double chargedComplementMass = chargedPrecursorMass - chargedFragmentMass;
        int complementCharge = id.charge - z;
        return chargedComplementMass / complementCharge;
    }

    size_t BaseSpectrum::size()
    {
        size_t mySize = sizeof( *this );
        mySize += fileName.length();
        return mySize;
    }
}
