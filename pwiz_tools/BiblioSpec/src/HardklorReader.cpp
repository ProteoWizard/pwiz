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

/**
 * The HardklorReader parses the scan number, charge, mass and spectrum
 * file name from the .hk file and stores each record.  Records are
 * sorted and grouped by file.  Spectra are then retrieved from the
 * spectrum files.
 *
 * Similar to SSL reader, but has a bit of extra code to deal with the mix
 * of line types "P" and "S"
 *
 */



#include "HardklorReader.h"

namespace BiblioSpec {

    HardklorReader::HardklorReader(BlibBuilder& maker,
        const char* hkfilename,
        const ProgressIndicator* parent_progress)
        : SslReader(maker, hkfilename, parent_progress, "HardklorReader"), _currentRTINFO(), _currentSpecKey(0),
          _currentSpecIndex(0), _rtRanges()
    {
        hasHeader_ = false;
    }

    HardklorReader::~HardklorReader()
    {
    }

    void HardklorReader::setColumnsAndSeparators(DelimitedFileReader<sslPSM> &fileReader)
    {

        /*
        From https ://proteome.gs.washington.edu/software/hardklor/docs/hardklorresults.html

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
        NONSTANDARD ADDITION:
        Molecular Formula     Skyline's version of Hardklor adds this column for use in BiblioSpec. Note that this isn't the actual formula, mass is probably wrong but shows the isotope envelope shape 

        */

        // N.B. these must be added in column order (0,1,2,3,4,8) to function properly, since we don't call the header handling code which sorts them
        // (Since we aren't actually parsing a header, these column names are purely informational)
        fileReader.addRequiredColumn("hardklor line type", setIsSpectrumLine, 0); // Checks line type P vs S
        fileReader.addRequiredColumn("scan or mass", setScanNumberOrMeasuredMass, 1); // Column content varies by line type
        fileReader.addRequiredColumn("Charge or Retention Time", setRetentionTimeOrCharge, 2); // Column content varies by line type
        fileReader.addRequiredColumn("file or intensity", setFileOrPrecursorIntensity, 3); // Only used on "S" lines
        fileReader.addRequiredColumn("Base Isotope Peak", sslPSM::setPrecursorMzDeclared, 4); // Only used on "P" lines
        fileReader.addRequiredColumn("score", sslPSM::setScore, 8); // Only used on "P" lines
        fileReader.addRequiredColumn("molecular formula", setChemicalFormulaAndAdduct, 9); // Only used on "P" lines, and found only in files produced by special Skyline build of Hardklor

        // Hardklor filename entries may be in Windows path style, the default backslash escape character is trouble
        fileReader.defineSeparatorsNoEscape("\t", ""); // Hardklor files are tab delimited, don't use quotes, or escape characters

    }

    // Hardklor reader has to juggle two kinds of line, and preserve the state of one of them (the "S" lines)
    void HardklorReader::addDataLine(sslPSM& newPSM) {

        if (newPSM._currentLineIsSpectrumInfo)
        {
            // Values we pull from "S" lines
            _currentFilename = newPSM.filename;
            _currentRTINFO = newPSM.rtInfo;
            _currentSpecKey = newPSM.specKey;
            _currentSpecIndex = newPSM.specIndex;
            _currentSpecName = newPSM.specName;

        }
        else
        {
            // Values we pulled from "P" lines get merged with "S" values
            newPSM.filename = _currentFilename;
            newPSM.rtInfo = _currentRTINFO;
            newPSM.specKey = _currentSpecKey;
            newPSM.specIndex = _currentSpecIndex;
            newPSM.specName = _currentSpecName.empty() ? PRECURSOR_WITHOUT_MS2_SCAN : _currentSpecName;

            newPSM.scoreType = HARDKLOR_CORRELATION_SCORE;
            SslReader::addDataLine(newPSM);
        }
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
        if (success)
        {
            const RTINFO &rtInfo = _rtRanges[psm->specName];
            returnData.retentionTime = rtInfo.retentionTime;
            returnData.startTime = rtInfo.startTime;
            returnData.endTime = rtInfo.endTime;
        }
        return success;
    };

    // Hardklor will identify the same isotope envelope multiple times in a scan, and in sequential scans, so
    // try to remove all but the best fitting one (which is presumably also the RT peak)
    // N.B. Kronik does something like this, but can't take advantage of the isotope envelope information that our
    // customized Hardklor passes along
    void HardklorReader::removeDuplicates()
    {
double rt = -1;
bool yup = false;
        for (int index1 = 0; index1 < psms_.size(); ++index1)
        {
            sslPSM* psm1 = static_cast<sslPSM*>(psms_[index1]);
            if (psm1==NULL)
            {
                continue; // Already identified as part of a chromatogram
            }
yup = (psm1->smallMolMetadata.chemicalFormula == "H70C39N11O11");
#define MAGIC_NUMBER_PROCESSED_PEAK -2
#define MIN_PEAK_POINTS 8 // 8 points across the peak seems like not a lot to ask
            if (psm1->specIndex == MAGIC_NUMBER_PROCESSED_PEAK)
            {
                continue; // We've already identified this PSM as a chromatogram peak
            }
if (psm1->rtInfo.retentionTime < rt)
{
    printf("eek");
}
rt = psm1->rtInfo.retentionTime;
            const int charge = psm1->charge;
            const double tolerMz = .5 * (1.0 / fabs(charge)); // Used to choose the better scoring of two PSMs with identical but shifted isotope envelopes
            double precursorMzDeclared = psm1->smallMolMetadata.precursorMzDeclared;
            const string & chemicalFormula = psm1->smallMolMetadata.chemicalFormula;

            int specID1 = psm1->idAsInteger(); // Multiple PSMs can have common specID
            double rtStart = overrideRt_[specID1].retentionTime;
            double rtEnd = rtStart;
            int gap = 0; // Count of consecutive scans without a hit
            bool matched_PSM_this_scan = true;
            double score = psm1->score;
            double maxIntensity = psm1->precursorIntensity;
            int indexMaxIntensity = index1;
            vector<int> chromatogram{index1}; // List of following PSMs that match charge, formula, and approximate reported mass
            for (int index2 = index1; ++index2 < psms_.size();)
            {
                const sslPSM* psm2 = static_cast<sslPSM*>(psms_[index2]);
                if (psm2 == NULL)
                {
                    continue; // Already noted as non-peak in a different chromatogram
                }
                if (psm2->specIndex == MAGIC_NUMBER_PROCESSED_PEAK)
                {
                    continue; // We've already identified this PSM as a chromatogram peak
                }

                // See if we can find an occurrence of psm1 in the next several spectra, or a duplicate in this spectrum
                int specID2 = psm2->idAsInteger();
                if (specID1 != specID2)
                {
                    // We've moved on to another spectrum - did we match any PSMs in the one we're leaving?
                    specID1 = specID2; 
                    if (matched_PSM_this_scan)
                    {
                        matched_PSM_this_scan = false; // Reset for next scan
                        gap = 0;
                    }
                    else
                    {
                        gap++;
                    }
                    if (gap > MIN_PEAK_POINTS)
                    {
                        break; // Assume we've run off the end of the elution
                    }
                }
                if (charge == psm2->charge &&
                    fabs(precursorMzDeclared - psm2->smallMolMetadata.precursorMzDeclared) <= tolerMz &&
                    chemicalFormula == psm2->smallMolMetadata.chemicalFormula) // Formula is a marker for isotope envelope shape
                {
if (yup)
rt = psm1->rtInfo.retentionTime;
                    chromatogram.push_back(index2); // Similar enough - add to list of matches
                    matched_PSM_this_scan = true;
                    rtEnd = overrideRt_[specID2].retentionTime;
                    if (psm2->score > score)
                    {
                        precursorMzDeclared = psm2->smallMolMetadata.precursorMzDeclared;
                        score = psm2->score;
                    }
                    if (maxIntensity < psm2->precursorIntensity)
                    {
                        maxIntensity = psm2->precursorIntensity;
                        indexMaxIntensity = index2;
                    }
                }
            }

            // Does this PSM appear to be in any sort of elution? If not, discard
            if (chromatogram.size() < MIN_PEAK_POINTS)
            {
                delete(psm1);
                psms_[index1] = NULL;
                continue;
            }

            // We've identified a run of PSMs for the same ion, keep just the most intense one and note the time range
            for (int i = 0; i < chromatogram.size(); i++)
            {
                const int index2 = chromatogram[i];
                if (index2 != indexMaxIntensity)
                {
                    // Keeping only the most intense PSM, discard this
                    delete(psms_[index2]);
                    psms_[index2] = NULL;
                }
            }
            PSM* psmPeak = psms_[indexMaxIntensity];
            RTINFO peakRT;
            peakRT.retentionTime = overrideRt_[psmPeak->idAsInteger()].retentionTime;
            peakRT.startTime = rtStart;
            peakRT.endTime = rtEnd;
            _rtRanges[psmPeak->specName] = peakRT;
            psmPeak->specIndex = MAGIC_NUMBER_PROCESSED_PEAK; // Mark it as a peak so we don't revisit
        }
    }

} // namespace



/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
