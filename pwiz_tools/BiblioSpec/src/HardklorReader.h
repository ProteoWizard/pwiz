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
 * The HardklorReader class to parse .hk files.
 *
 * Similar to SSL reader, but has a bit of extra code to deal with the fact
 * that Hardklor files contain two kinds of lines:
 * (From https ://proteome.gs.washington.edu/software/hardklor/docs/hardklorresults.html)

    Spectrum Line Columns (starts with an "S")
    Scan Number	The number of the mass spectrum in the data file.
    Retention Time	Time(in minutes) of the scan event.
    File Name	File name for the scan event.
    Precursor Mass	If analyzing tandem mass spectra, the mass of the precursor ion(if known).
    Precursor Charge	If analyzing tandem mass spectra, the charge of the precursor ion(if known).
    Selected Precursor m/z	If analyzing tandem mass spectra, the m/z of the precursor ion.

    Peptide Line Columns (starts with a "P")
    Monoisotopic Mass	The zero charge, monoisotopic mass of the feature.  (BSP note: Not the theortical mass - it should be close though, depending on tolerances)
    Charge	The charge state of the feature.
    Intensity	The intensity value of the feature.The reported values are influenced by the parameters in the configuration file.
    Base Isotope Peak	The base isotope peak(tallest peak) of the feature's isotope distribution based on the theoretical model.
    Analysis Window	The m/z window that the feature was extracted from.
    (Deprecated)Formerly an indicator of localized signal - to - noise threshold.
    Modifications	Modifications on the peptide, if searched for.
    Correlation Score	The dot - product score of this feature to the theoretical model.
    NONSTANDARD ADDITION:
    Molecular Formula     Skyline's version of Hardklor adds this column for use in BiblioSpec
 */

class HardklorReader : public SslReader {
  public:
    HardklorReader(BlibBuilder& maker,
              const char* hkfilename,
              const ProgressIndicator* parent_progress);
    ~HardklorReader();

    virtual void setColumnsAndSeparators(DelimitedFileReader<sslPSM> &fileReader);
    virtual void addDataLine(sslPSM& newPSM);
    virtual void removeDuplicates(); // Special case for Hardklor - no peptide sequences, but lots of redundant IDs in sequential scans
    virtual bool getSpectrum(PSM* psm, SPEC_ID_TYPE findBy, SpecData& returnData, bool getPeaks);
    virtual bool keepAmbiguous();

private:

    map<string, RTINFO> _rtRanges; // Set of RT values after removeDuplicates()

    static void setIsSpectrumLine(sslPSM& psm, const std::string& value)
    {
        psm._currentLineIsSpectrumInfo = (value == "S");
    }

    static void setScanNumberOrMeasuredMass(sslPSM& psm, const std::string& value)
    {
        if (psm._currentLineIsSpectrumInfo)
        {
            sslPSM::setScanNumber(psm, value);
        }
        else
        {
            sslPSM::setMoleculeName(psm, std::string("mass") + value);
        }
    }

    static void setRetentionTimeOrCharge(sslPSM& psm, const std::string& value)
    {
        if (psm._currentLineIsSpectrumInfo)
        {
            sslPSM::setRetentionTime(psm, value);
        }
        else
        {
            sslPSM::setCharge(psm, value);
            sslPSM::setPrecursorAdduct(psm, std::string("[M+") + value + "H]");
        }
    }


    static void setFileOrPrecursorIntensity(sslPSM& psm, const std::string& value)
    {
        if (psm._currentLineIsSpectrumInfo)
        {
            sslPSM::setFile(psm, value);
        }
        else
        {
            sslPSM::setPrecursorIntensity(psm, value);
        }
    }

    static void setChemicalFormulaAndMassShift(sslPSM& psm, const std::string& value) {
        // Skyline's modified version of Hardklor supplies formula for isotope envelope, and
        // the offset that shifts it match the mass of the reported feature
        // e.g. "H21C14N4O4[+3.038518]" reported mass is 312.1948, formula mass is
        // 309.1563, and 309.1563+3.038518=312.1948
        // This allows Skyline to show the same isotope envelope that Hardklor was thinking of.
        psm.smallMolMetadata.chemicalFormula = value;
    }

    // Things we read from "S" lines that need to be combined with "P" line info
    string _currentFilename;
    RTINFO _currentRTINFO;
    int _currentSpecKey;
    int _currentSpecIndex;
    string _currentSpecName;
  };

} // namespace

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
