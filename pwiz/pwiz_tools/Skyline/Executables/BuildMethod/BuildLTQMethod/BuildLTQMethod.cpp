/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

// BuildLTQMethod.cpp
//   Builds a Thermo LTQ SRM instrument method from one or many
//   Skyline generated transition lists.

#define _WIN32_WINNT 0x0501     // Windows XP

#include <stdio.h>
#include <iostream>
#include <math.h>
#include "StringUtil.h"
#include "Verbosity.h"
#include "MethodBuilder.h"

// To create new versions of ltmethod.tlh|.tli, uncomment this line
// after installing the LTMethod DLL software.  Otherwise, you should
// be able to build without the DLL using the .tlh|.tli versions committed
// to this project.
// #define _IMPORT_PROCESSING_

#ifdef _IMPORT_PROCESSING_
#import "C:\\XCalibur\\system\\LTQ\\programs\\LTMethod.dll"
#else
#include "ltmethod.tlh"
//#include "ltmethod.tli" - change absolute path in the .tlh file
#endif
using namespace LTMETHODLib;

enum Fields
{
    precursor_mz,
    product_mz,
    collision_energy,
    start_time,
    stop_time,
    polarity,
    sequence,
    // CONSIDER: Use extended format like AB?
    protein,
    fragment,
    lib_rank,
    standard_type   // Optional
};

// Equation for low mass limit provided by Thermo
double FirstMass(double precursorMass, double activationQ)
{
    return ((int) (precursorMass * (activationQ/0.908))/5.0) * 5.0;
}

class BuildLTQMethod : public MethodBuilder
{
public:
    BuildLTQMethod();

    virtual void usage()
    {
        const char* usage =
            "Usage: BuildLTQMethod [options] <template file> [list file]*\n"
            "   Takes template LTQ SRM method file and a Skyline generated Thermo\n"
            "   scheduled transition list as inputs, to generate a new LTQ SRM method\n"
            "   file as output.\n";

        cerr << usage;

        MethodBuilder::usage();
    }

    virtual void createMethod(string templateMethod, string outputMethod,
        const vector<vector<string>>& tableTranList);

private:
    void replaceTransitionList(const vector<vector<string>>& tableTranList);
    void printMethod();

private:
    ILCQMethodPtr _methodPtr;
};

int main(int argc, char* argv[])
{
    BuildLTQMethod builder;
    builder.parseCommandArgs(argc, argv);
    builder.build();

    return 0;
}

BuildLTQMethod::BuildLTQMethod()
{
    if (FAILED(CoInitialize(NULL)))
        Verbosity::error("Failure during initialization.");

    try
    {
        ILCQMethodPtr methodPtr("LTMethod.LTMethod.1");
        _methodPtr = methodPtr;
    }
    catch (_com_error&)
    {
        Verbosity::error("Failure during initialization, LTQ method support may not be installed");
    }
}

void BuildLTQMethod::createMethod(string templateMethod, string outputMethod, const vector<vector<string>>& tableTranList)
{
    // Open the template
    try
    {
        // Template gets copied to output before this method is called,
        // because SaveAs() corrupts the output file.  Only Save() works
        // correctly.
        _bstr_t outputMethodW = str_to_wstr(outputMethod).c_str();

        _methodPtr->Open(outputMethodW);
    }
    catch (_com_error&)
    {
        Verbosity::error("Failure opening template method %s", templateMethod.c_str());
    }

    // Inject the new transition list into the template and save
    try
    {
        replaceTransitionList(tableTranList);

        // Save into existing to avoid losing information that Thermo
        // strips with SaveAs()
        _methodPtr->Save();
        _methodPtr->Close();
    }
    catch (_com_error&)
    {
        Verbosity::error("Failure creating new method from %s", templateMethod.c_str());
    }
}

void BuildLTQMethod::replaceTransitionList(const vector<vector<string>>& tableTranList)
{
    short numScans = _methodPtr->NumScanEvents;
    short scanType = _methodPtr->ScanType;
    short analyzerType = _methodPtr->Analyzer;

    double precursorMass = 0.0;
    short activationType = 0;
    double isolationWindow = 2.0;
    double normalizedCE = 35.0;
    float activationQ = 0.25f;
    double activationTime = 30.0;
    double productWindow = 2.0;

    if (numScans > 0 && scanType == 1 && analyzerType == 0)
    {
        if (_methodPtr->NumReactions > 0)
        {
            _methodPtr->GetReaction2(0, &precursorMass, &activationType, &isolationWindow,
                &normalizedCE, &activationQ, &activationTime);
        }

        if (_methodPtr->NumMassRanges > 0)
        {
            double startMass = 0.0, endMass = 0.0;
            _methodPtr->GetMassRange(0, &startMass, &endMass);
            double deltaMass = endMass - startMass;
            if (deltaMass > 0)
                productWindow = deltaMass;
        }
    }

    // TODO: Handle scheduling with segments
    short scanCount = 0;
    short rangeCount = 0;
    vector<vector<string>>::const_iterator it = tableTranList.begin();
    for (; it != tableTranList.end(); it++)
    {
        string value = it->at(precursor_mz);
        double precursorMassList = atof(value.c_str());
        if (precursorMassList == 0.0)
            Verbosity::error("Invalid precursor m/z %s", value);

        // Start a new scan, if precursor changes, or the maximum of 10 ranges
        // per scan is reached.
        if (precursorMassList != precursorMass || rangeCount >= 10)
        {
            scanCount++;
            _methodPtr->NumScanEvents = scanCount;
            _methodPtr->CurrentScanEvent = scanCount - 1;
            _methodPtr->ScanMode = 1;    // 0 = MS, ..., 9 = MS10
            _methodPtr->ScanType = 1;    // 0 = Full, 1 = SIM/SRM
            _methodPtr->NumReactions = 1;

            precursorMass = precursorMassList;

            _methodPtr->SetReaction2(0, precursorMass, activationType, isolationWindow,
                normalizedCE, activationQ, activationTime);

            rangeCount = 0;
        }

        value = it->at(product_mz);
        double productMass = atof(value.c_str());
        if (productMass == 0.0)
            Verbosity::error("Invalid product m/z %s", value);
        double startMass = productMass - productWindow/2;
        double endMass = productMass + productWindow/2;
        double firstMass = FirstMass(precursorMass, activationQ);
        if (firstMass > startMass)
        {
            Verbosity::error("Product start m/z %.6f less than low mass limit %.0f for %s - %s: %s, %s",
                startMass,
                firstMass,
                it->at(protein).c_str(),
                it->at(sequence).c_str(),
                it->at(precursor_mz).c_str(),
                it->at(product_mz).c_str());
        }
        
        // Because ILCQMethod sometimes changes this mysteriously
        _methodPtr->CurrentScanEvent = scanCount - 1;

        _methodPtr->NumMassRanges = ++rangeCount;
        short i = rangeCount - 1;

        double startMassLast = 0;
        double endMassLast = 0;

        for (; i > 0; i--)
        {
            double startMassOld = 0, endMassOld = 0;
            _methodPtr->GetMassRange(i - 1, &startMassOld, &endMassOld);
            // Copy mass ranges until one with lower start-mass is encountered
            // (insertion sort)
            if (startMassOld < startMass)
            {
                // Check the range before the new one to make sure its end is not
                // greater than the new range to be inserted.
                if (endMassOld >= startMass)
                {
                    Verbosity::error("Overlapping mass ranges %.3f-%.3f and %.3f-%.3f found for %s - %s: %s",
                        startMassOld, endMassOld, startMass, endMass,
                        it->at(protein).c_str(),
                        it->at(sequence).c_str(),
                        it->at(precursor_mz).c_str());
                }
                break;
            }

            _methodPtr->SetMassRange(i, startMassOld, endMassOld);

            startMassLast = startMassOld;
            endMassLast = endMassOld;
        }

        // Check the last mass that was greater than the new one to make sure
        // its start is greater than the end of the new range to be inserted.
        if (startMassLast != 0 && endMass >= startMassLast)
        {
            Verbosity::error("Overlapping mass ranges %.3f-%.3f and %.3f-%.3f found for %s - %s: %s",
                startMass, endMass, startMassLast, endMassLast,
                it->at(protein).c_str(),
                it->at(sequence).c_str(),
                it->at(precursor_mz).c_str());
        }

        // Insert the new mass range in its place
        _methodPtr->SetMassRange(i, startMass, endMass);

        if (Verbosity::get_verbosity() == V_DEBUG)
        {
            // If debugging errors, check method validity after every
            // transition is added, but otherwise this is way too slow.
            long valid = 0;
            try
            {
                _methodPtr->IsMethodValid(&valid);
            }
            catch (_com_error&)
            {
                valid = 0;
            }
            if (!valid)
            {
                printMethod();
                Verbosity::error("Failure adding the transition %s - %s: %s, %s",
                    it->at(protein).c_str(),
                    it->at(sequence).c_str(),
                    it->at(precursor_mz).c_str(),
                    it->at(product_mz).c_str());
            }
        }
    }

    _methodPtr->NumScanEvents = scanCount;
}

void BuildLTQMethod::printMethod()
{
    int numTrans = 0;
    for (short i = 0, numScans = _methodPtr->NumScanEvents; i < numScans; i++)
    {
        _methodPtr->CurrentScanEvent = i;
        for (short j = 0, numReactions = _methodPtr->NumReactions; j < numReactions; j++)
        {
            double precursorMass = 0;
            short activationType = 0;
            double isolationWindow = 2.0;
            double normalizedCE = 35.0;
            float activationQ = 0.25f;
            double activationTime = 30.0;

            _methodPtr->GetReaction2(j, &precursorMass, &activationType,  &isolationWindow,
                &normalizedCE, &activationQ, &activationTime);
            cerr << "mass = " << precursorMass
                << ", type = " << activationType
                << ", win = " << isolationWindow
                << ", ce = " << normalizedCE
                << ", q = " << activationQ
                << ", time = " << activationTime
                << endl;
        }

        for (short j = 0, numRanges = _methodPtr->NumMassRanges; j < numRanges; j++)
        {
            numTrans++;

            double mass1 = 0;
            double mass2 = 0;
            _methodPtr->GetMassRange(j, &mass1, &mass2);
            cerr << "    start-mass = " << mass1
                << ", end-mass = " << mass2
                << endl;
        }
    }
    cerr << "(" << _methodPtr->NumScanEvents << " precursors, " << numTrans << " transitions)" << endl;
    cerr << endl;
}
