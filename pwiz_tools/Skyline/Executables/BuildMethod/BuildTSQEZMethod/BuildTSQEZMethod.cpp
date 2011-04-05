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

// BuildTSQEZMethod.cpp
//   Builds a Thermo TSQ EZ instrument method from one or many Skyline
//   generated transition lists.

#define _WIN32_WINNT 0x0501     // Windows XP

#include <stdio.h>
#include <iostream>
#include "StringUtil.h"
#include "Verbosity.h"
#include "MethodBuilder.h"

// To create new versions of tsqezmethod.tlh|.tli, uncomment this line
// after installing the TSQEZMethod DLL software.  Otherwise, you should
// be able to build without the DLL using the .tlh|.tli versions committed
// to this project.
// #define _IMPORT_PROCESSING_

#ifdef _IMPORT_PROCESSING_
#import "C:\\Program Files\\Thermo\\EZMethodXML2.1\\TSQEZMethod.dll"
#else
#include "tsqezmethod.tlh"
//#include "tsqezmethod.tli" - change absolute path in the .tlh file
#endif
using namespace TSQEZMethodLib;

#define EXPERIMENT_TYPE_NOT_SUPPORTED _HRESULT_TYPEDEF_(0x80002106)

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

class BuildTSQEZMethod : public MethodBuilder
{
public:
    BuildTSQEZMethod();

    virtual void usage()
    {
        const char* usage =
            "Usage: TsqEzMethBuild [options] <template method> [list file]*\n"
            "   Takes template TSQ EZ method file and a Skyline generated Thermo\n"
            "   scheduled transition list as inputs, to generate a new TSQ EZ method\n"
            "   file as output.\n";

        cerr << usage;

        MethodBuilder::usage();
    }

    virtual void createMethod(string templateMethod, string outputMethod,
        const vector<vector<string>>& tableTranList);

private:
    static string transitionsToXml(string xmlTemplate, const vector<vector<string>>& tableTranList);
    static string getConstantTag(string xmlTemplate, string defaultValue);

private:
    ITSQ_EZ_MethodPtr _methodPtr;
};

int main(int argc, char* argv[])
{
    BuildTSQEZMethod builder;
    builder.parseCommandArgs(argc, argv);
    builder.build();

    return 0;
}

BuildTSQEZMethod::BuildTSQEZMethod()
{
    if (FAILED(CoInitialize(NULL)))
        Verbosity::error("Failure during initialization.");

    try
    {
        ITSQ_EZ_MethodPtr methodPtr("TSQEZMethod.TSQ_EZ_Method");
        _methodPtr = methodPtr;
    }
    catch (_com_error&)
    {
        Verbosity::error("Failure during initialization, TSQ-EZ method support may not be installed. Method export for a TSQ should be performed on the TSQ instrument control computer, and requires TSQ version 2.3 or better.");
    }
}

void BuildTSQEZMethod::createMethod(string templateMethod, string outputMethod, const vector<vector<string>>& tableTranList)
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
    catch (_com_error& err)
    {
        if (err.Error() == EXPERIMENT_TYPE_NOT_SUPPORTED)
            Verbosity::error("Failure opening template method %s. Make sure the template is an EZ Method and not a Regular Method in the TSQ menu of the Instrument Setup application.", templateMethod.c_str());
        else
            Verbosity::error("Failure opening template method %s. Make sure the template is a valid TSQ EZ Method.", templateMethod.c_str());
    }

    // Get the template transition list
    _bstr_t xmlTranListW;
    try
    {
        _methodPtr->ExportMassListXMLStream(&xmlTranListW.GetBSTR());
    }
    catch (_com_error&)
    {
        // Assume there was none, in case of failure
        xmlTranListW = "";
    }

    // Create the new transition list
    string xmlTranList = transitionsToXml(wstr_to_str(xmlTranListW.GetBSTR()), tableTranList);
    xmlTranListW = str_to_wstr(xmlTranList).c_str();

    // Inject the new transition list into the template and save
    
    _bstr_t xmlErrorW;

    try
    {
        _methodPtr->ImportMassListXMLStream(xmlTranListW, &xmlErrorW.GetBSTR());
        _methodPtr->Save(&xmlErrorW.GetBSTR());
        _methodPtr->Close();
    }
    catch (_com_error&)
    {
        if (xmlErrorW.length() == 0)
            Verbosity::error("Failure creating new method from %s", templateMethod.c_str());
        else
            Verbosity::error(wstr_to_str(xmlErrorW.GetBSTR()).c_str());
    }
}

string BuildTSQEZMethod::transitionsToXml(string xmlTemplate, const vector<vector<string>>& tableTranList)
{
    string energyRamp = getConstantTag(xmlTemplate, "<EnergyRamp>1.00</EnergyRamp>\n");
    string scanTime = getConstantTag(xmlTemplate, "<ScanTime>1.00</ScanTime>\n");
    string tubeLens = getConstantTag(xmlTemplate, "<TubeLens>0</TubeLens>\n");
    string sLens = getConstantTag(xmlTemplate, "<S-Lens>0</S-Lens>\n");
    string polarity = getConstantTag(xmlTemplate, "<Polarity>1</Polarity>\n");
    string compensationVoltage = getConstantTag(xmlTemplate,
        "<CompensationVoltage>0.0</CompensationVoltage>\n");

    string xmlTranList = "<?xml version=\"1.0\" encoding=\"UTF-16\"?>\n"
                         "<TSQMassList>\n";

    vector<vector<string>>::const_iterator it = tableTranList.begin();
    for (; it != tableTranList.end(); it++)
    {
        xmlTranList += "<TSQListItem>\n";
        xmlTranList += "<ParentMass>" + it->at(precursor_mz) + "</ParentMass>\n";
        xmlTranList += "<ProductMass>" + it->at(product_mz) + "</ProductMass>\n";
        xmlTranList += "<CollisionEnergy>" + it->at(collision_energy) + "</CollisionEnergy>\n";
        xmlTranList += energyRamp;
        xmlTranList += scanTime;
        xmlTranList += "<StartTime>" + it->at(start_time) + "</StartTime>\n";
        xmlTranList += "<StopTime>" + it->at(stop_time) + "</StopTime>\n";
        xmlTranList += tubeLens;
        xmlTranList += sLens;
        xmlTranList += polarity;
        xmlTranList += compensationVoltage;
        xmlTranList += "<Name>" + it->at(sequence) + "</Name>\n";
        xmlTranList += "</TSQListItem>\n";
    }

    xmlTranList += "</TSQMassList>\n";
    return xmlTranList;
}

string BuildTSQEZMethod::getConstantTag(string xmlTemplate, string defaultValue)
{
    string startTag = defaultValue.substr(0, defaultValue.find('>') + 1);
    string endTag = startTag;
    endTag.insert(endTag.begin() + 1, '/');

    size_t startTagPos = xmlTemplate.find(startTag);
    size_t endTagPos = xmlTemplate.find(endTag);

    if (startTagPos == string::npos || endTagPos == string::npos)
        return defaultValue;

    size_t startValuePos = startTagPos + startTag.size();
    return startTag + xmlTemplate.substr(startValuePos, endTagPos - startValuePos) + endTag + "\n";
}
