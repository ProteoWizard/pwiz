//
// $Id$
//
//
// Original author: Brian Pratt <bspratt @ proteinms.net>
//
// Copyright 2023 University of Washington - Seattle, WA 98195
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

/**
 * The HardklorReader class to parse .hk.bs.kro files - the output of BullseyeSharp
 * postprocessing Hardklor files.
 *
 * It assumes the use of the special Skyline versions
 * of Hardklor and BullseyeSharp which add some extra information about averagine formulas.
 *
 * Similar to SSL reader, but tolerant of MS1-only data
 *
 */

#include "HardklorReader.h"

namespace BiblioSpec {

    HardklorReader::HardklorReader(BlibBuilder& maker,
        const char* hkfilename,
        const ProgressIndicator* parent_progress)
        : SslReader(maker, hkfilename, parent_progress, "HardklorReader")
    {
        hasHeader_ = true;
    }

    HardklorReader::~HardklorReader()
    {
    }

    void HardklorReader::setColumnsAndSeparators(DelimitedFileReader<sslPSM> &fileReader)
    {

        /*

        A .hk file that gets fed into BullseyeSharp looks like this:
         
        From https://proteome.gs.washington.edu/software/hardklor/docs/hardklorresults.html

        Spectrum Line Columns (starts with an "S")
        Scan Number	The number of the mass spectrum in the data file.
        Retention Time	Time(in minutes) of the scan event.
        File Name	File name for the scan event.
        Precursor Mass	If analyzing tandem mass spectra, the mass of the precursor ion(if known).
        Precursor Charge	If analyzing tandem mass spectra, the charge of the precursor ion(if known).
        Selected Precursor m / z	If analyzing tandem mass spectra, the m / z of the precursor ion.

        Peptide Line Columns (starts with a "P")
        Monoisotopic Mass	The zero charge, monoisotopic mass of the feature. (BSP note: Not the theortical mass - it should be close though, depending on tolerances)
        Charge	The charge state of the feature.
        Intensity	The intensity value of the feature.The reported values are influenced by the parameters in the configuration file.
        Base Isotope Peak	The base isotope peak(tallest peak) of the feature's isotope distribution based on the theoretical model.
        Analysis Window	The m / z window that the feature was extracted from.
        (Deprecated)Formerly an indicator of localized signal - to - noise threshold.
        Modifications	Modifications on the peptide, if searched for.
        Correlation Score	The dot - product score of this feature to the theoretical model.
        NONSTANDARD ADDITION FOR SKYLINE:
        Averagine Formula and mass offset    Skyline's version of Hardklor adds this column for use in BiblioSpec.

        In the BullseyeSharp output these are reduced to
        File, First Scan, Last Scan, Num of Scans, Charge, Monoisotopic Mass, Base Isotope Peak, Best Intensity, Summed Intensity, First RTime, Last RTime, Best RTime, Best Correlation, Modifications

        The special Skyline build adds
        Best Scan, Averagine
        
        */

        fileReader.addRequiredColumn("File", sslPSM::setFile); // N.B. the offical BullseyeSharp puts "NULL" in all this column's entries
        fileReader.addRequiredColumn("Charge", setChargeAndAdduct);
        fileReader.addRequiredColumn("Monoisotopic Mass", setNameAsMass);
        fileReader.addRequiredColumn("Base Isotope Peak", sslPSM::setPrecursorMzDeclared);
        fileReader.addRequiredColumn("Best Correlation", sslPSM::setScore);
        fileReader.addRequiredColumn("Best RTime", sslPSM::setRetentionTime);
        fileReader.addRequiredColumn("First RTime", sslPSM::setStartTime);
        fileReader.addRequiredColumn("Last RTime", sslPSM::setEndTime);

        // fileReader.addRequiredColumn("Best Scan", sslPSM::setScanNumber);  // Only in files produced by special Skyline build of Hardklor+Bullseye - not needed for now, MS1 only
        fileReader.addRequiredColumn("Averagine", setChemicalFormulaAndMassShift); // Only in files produced by special Skyline build of Hardklor+Bullseye

        // Hardklor filename entries may be in Windows path style, the default backslash escape character is trouble
        fileReader.defineSeparatorsNoEscape("\t", ""); // Hardklor files are tab delimited, don't use quotes, or escape characters

    }
    
    void HardklorReader::addDataLine(sslPSM& data) {
        data.setPrecursorOnly(); // Hardklor files are MS1 only for the moment
        data.scoreType = HARDKLOR_CORRELATION_SCORE;
        SslReader::addDataLine(data);
    }

    bool HardklorReader::getSpectrum(PSM* psm,
        SPEC_ID_TYPE findBy,
        SpecData& returnData,
        bool getPeaks)
    {
        bool isMS1 = psm->isPrecursorOnly();
        if (isMS1)
        {
            getPeaks = false;
            if (psm->specKey < 0 && findBy == SPEC_ID_TYPE::SCAN_NUM_ID)
            {
                findBy = NAME_ID; // Look up by constructed ID since there's no actual spectrum associated
            }
        }
        bool success = !getPeaks;
        if (getPeaks)
        {
            switch (findBy) {
            case NAME_ID:
                success = SslReader::getSpectrum(psm->specName, returnData, getPeaks);
                break;
            case SCAN_NUM_ID:
                success = SslReader::getSpectrum(psm->specKey, returnData, findBy, getPeaks);
                break;
            case INDEX_ID:
                success = SslReader::getSpectrum(psm->specIndex, returnData, findBy, getPeaks);
                break;
            }
        }
        return success;
    };
    
} // namespace



/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
