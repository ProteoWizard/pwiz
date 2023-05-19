//
// $Id$
//
//
// Original author: Brian Pratt <bspratt @ proteinms.net>
//
// Copyright 2022 University of Washington - Seattle, WA 98195
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

#include "SslReader.h"

namespace BiblioSpec {


/**
 * The HardklorReader class to parse .hk.bs.kro files - the output of BullseyeSharp
 * postprocessing Hardklor files.
 *
 * It assumes the use of the special Skyline versions
 * of Hardklor and BullseyeSharp which add some extra information about averagine formulas.
 *
 */

class HardklorReader : public SslReader {
  public:
    HardklorReader(BlibBuilder& maker,
              const char* hkfilename,
              const ProgressIndicator* parent_progress);
    ~HardklorReader();

    virtual void setColumnsAndSeparators(DelimitedFileReader<sslPSM> &fileReader);
    virtual bool getSpectrum(PSM* psm, SPEC_ID_TYPE findBy, SpecData& returnData, bool getPeaks);
    virtual void addDataLine(sslPSM& data); // from DelimitedFileConsumer

private:

    static void setChemicalFormulaAndMassShift(sslPSM& psm, const std::string& value) {
        // Skyline's modified version of Hardklor supplies formula for isotope envelope, and
        // the offset that shifts it match the mass of the reported feature
        // e.g. "H21C14N4O4[+3.038518]" reported mass is 312.1948, formula mass is
        // 309.1563, and 309.1563+3.038518=312.1948
        // This allows Skyline to show the same isotope envelope that Hardklor was thinking of.
        psm.smallMolMetadata.chemicalFormula = value;
    }

    static void setChargeAndAdduct(sslPSM& psm, const std::string& value) {
        char buf[20];
        sslPSM::setCharge(psm, value);
        snprintf(buf, 20, "[M%+dH]", psm.charge);
        psm.smallMolMetadata.precursorAdduct = buf;
    }

    static void setNameAsMass(sslPSM& psm, const std::string& value) {
        psm.smallMolMetadata.moleculeName = "mass"+value;
    }
};

} // namespace

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
