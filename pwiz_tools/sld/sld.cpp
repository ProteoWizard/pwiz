//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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


#include "sld.hpp"
#include <iostream>
#include <iomanip>
#include <fstream>
#include <stdexcept>


using namespace std;


namespace pwiz {
namespace sld {


char check_short_size[((int)sizeof(short)-2)==0];


template <typename value_type>
value_type readValue(istream& is)
{
    value_type result = 0;
    is.read((char*)&result, sizeof(result));
    return result;
}


string readString(istream& is)
{
    // read length-encoded unicode string

    // 32-bit length
    int length = readValue<int>(is);
    if (length > 1024) throw runtime_error("[readString()] String too long?");

    string result;
    result.resize(length);

    // unicode string
    for (int i=0; i<length; i++)
    {
        short c;
        is.read((char*)&c, sizeof(short));
        result[i] = (char)c;
    }

    return result;
}


string readString0(istream& is)
{
    // read zero-terminated unicode string
    
    string result;

    while (short s=readValue<short>(is))
        result.push_back((char)s);

    return result; 
}


Record readRecord(istream& is)
{
    Record result;

    result.one = readValue<int>(is);
    result.sampleType = readValue<int>(is);
    result.unknown1 = readValue<int>(is);
    result.unknown2 = readValue<int>(is);
    result.unknown3 = readValue<int>(is);
    result.injectionVolume = readValue<double>(is);
    result.sampleWeight = readValue<double>(is);
    result.sampleVolume = readValue<double>(is);
    result.istdCorrectionAmount = readValue<double>(is);
    result.dilFactor = readValue<double>(is);
    result.unknown4 = readValue<int>(is);

    for (int i=0; i<Record::StringCount; i++)
        result.strings[i] = readString(is);

    return result;
}


Record::Record()
:   one(0), sampleType(0),
    unknown1(0), unknown2(0), unknown3(0),
    injectionVolume(0), sampleWeight(0), sampleVolume(0), 
    istdCorrectionAmount(0), dilFactor(0), unknown4(0),
    strings(StringCount)
{}


const char* stringLabelText_[Record::StringCount] = 
{
    "Sample Name",
    "Sample ID",
    "Comment",
    "L1",
    "L2",
    "L3",
    "L4",
    "L5",
    "Instrument Method",
    "Process Method",
    "File Name",
    "Path",
    "Position",
    "Unknown",
    "Unknown",
    "Unknown",
    "Unknown"
};


string stringLabelText(int index, const vector<string>* userLabels = 0)
{
    if (index<0 || index>=Record::StringCount)
        throw runtime_error("[sld::stringLabelText()] Bad index.");

    string result = stringLabelText_[index];

    // special handling of user-defined labels
    const int indexUserLabelsBegin = 3;
    const int indexUserLabelsEnd = indexUserLabelsBegin + File::userLabelCount_;
    if (userLabels && 
        (int)userLabels->size()==File::userLabelCount_ &&
        index >= indexUserLabelsBegin &&
        index < indexUserLabelsEnd)
        result += " " + userLabels->at(index-indexUserLabelsBegin);

    return result;
}


const char* sampleTypeText(int sampleType)
{
    switch (sampleType)
    {
        case 0:
            return "Unknown";
        case 1:
            return "Blank";
        case 2:
            return "QC";
        case 5:
            return "Std Bracket";
        default:
            return "I don't know";
    }
}


const char* labelSampleType_ = "Sample Type";
const char* labelInjectionVolume_ = "Inj Vol";
const char* labelSampleWeight_ = "Sample Wt";
const char* labelSampleVolume_ = "Sample Vol";
const char* labelISTDCorrectionAmount_ = "ISTD Amt";
const char* labelDilFactor_ = "Dil Factor";


void Record::writeText(ostream& os, const vector<string>* userLabels) const
{
    os << "one: " << one << endl;
    os << labelSampleType_ << ": " << sampleTypeText(sampleType) << endl;
    os << "unknown1: " << unknown1 << endl;
    os << "unknown2: " << unknown2 << endl;
    os << "unknown3: " << unknown3 << endl;
    os << labelInjectionVolume_ << ": " << injectionVolume << endl;
    os << labelSampleWeight_ << ": " << sampleWeight << endl;
    os << labelSampleVolume_ << ": " << sampleVolume << endl;
    os << labelISTDCorrectionAmount_ << ": " << istdCorrectionAmount << endl;
    os << labelDilFactor_ << ": " << dilFactor << endl;
    os << "unknown4: " << unknown4 << endl;

    for (int i=0; i<Record::StringCount; i++)
        os << stringLabelText(i, userLabels) << ": " << strings[i] << endl; 
}


void Record::writeCSVLabels(ostream& os, const vector<string>* userLabels)
{
    os << labelSampleType_ << ","
       << stringLabelText(Filename) << "," 
       << stringLabelText(SampleID) << ","
       << stringLabelText(Path) << ","
       << stringLabelText(InstrumentMethod) << ","
       << stringLabelText(ProcessMethod) << ","
       << stringLabelText(Position) << ","
       << labelInjectionVolume_ << ","
       << "Level,"
       << stringLabelText(Comment) << ","
       << labelDilFactor_ << ","
       << stringLabelText(L1, userLabels) << "," 
       << stringLabelText(L2, userLabels) << "," 
       << stringLabelText(L3, userLabels) << "," 
       << stringLabelText(L4, userLabels) << "," 
       << stringLabelText(L5, userLabels) << "," 
       << labelISTDCorrectionAmount_ << ","
       << labelSampleVolume_ << ","
       << labelSampleWeight_ << ","
       << stringLabelText(SampleName) << ","
       << "Calibration File,"
       << endl;
}


void Record::writeCSV(std::ostream& os) const
{
    os << fixed << setprecision(3) 
       << sampleTypeText(sampleType) << ","
       << strings[Filename] << "," 
       << strings[SampleID] << ","
       << strings[Path] << ","
       << strings[InstrumentMethod] << ","
       << strings[ProcessMethod] << ","
       << strings[Position] << ","
       << injectionVolume << ","
       << "," // Level
       << strings[Comment] << ","
       << dilFactor << ","
       << strings[L1] << ","
       << strings[L2] << ","
       << strings[L3] << ","
       << strings[L4] << ","
       << strings[L5] << ","
       << istdCorrectionAmount << ","
       << sampleVolume << ","
       << sampleWeight << ","
       << strings[SampleName] << ","
       << "," // Calibration File
       << endl; 
}


enum XcaliburVersion
{
    XcaliburVersion_1_4,
    XcaliburVersion_2_0,
    XcaliburVersion_2_0_5,
    XcaliburVersion_2_2,
    XcaliburVersion_2_5_5
};


XcaliburVersion interpretVersion(short versionNumber)
{
    // TODO 
    cout << hex << versionNumber << endl;
    
    switch (versionNumber)
    {
        case 0x39:
            return XcaliburVersion_1_4;
        case 0x3e:
            return XcaliburVersion_2_0;
        case 0x3f:
            return XcaliburVersion_2_0_5;
        case 0x40:
            return XcaliburVersion_2_5_5;
        case 0x42:
            return XcaliburVersion_2_2;
        default:
            throw runtime_error("Invalid version number.  Tell Darren.");
    }
}


File::File(const string& filename)
:   userLabels(userLabelCount_)
{
    ifstream is(filename.c_str(), ios::binary);
    if (!is)
        throw runtime_error("Unable to read file " + filename);

    short magic = readValue<short>(is);
    if (magic != (short)0xa101)
        throw runtime_error("Bad format: Magic number not found!");

    string finnigan = readString0(is);
    if (finnigan != "Finnigan")
        throw runtime_error("Bad format: Finnigan not found!");

    is.seekg(36);
    short versionNumber = readValue<short>(is); 
    XcaliburVersion version = interpretVersion(versionNumber);

    const int headSize = 1452;
    is.seekg(headSize);

    string unknown1 = readString(is);
    for (int i=0; i<userLabelCount_; i++)
        userLabels[i] = readString(is);
    string unknown2 = readString(is);

    if (version >= XcaliburVersion_2_0)
        is.seekg(60, ios::cur);

    int recordCount = readValue<int>(is); 
    readValue<int>(is); // unknown int value

    for (int i=0; i<recordCount; i++)
    {
        records.push_back(readRecord(is));
        if (version >= XcaliburVersion_2_0)
        {
            // now advance past 15 fields we don't care about
            // (only the 14th field appears to contain data now, but maybe in the future others will?)
            for (int field=0; field<15; field++)
            {
                readString(is);
            }
        }
    }
}


void File::writeText(ostream& os) const
{
    for (int i=0; i<File::userLabelCount_; i++)
        os << "User Label " << i << ": " << userLabels[i] << endl; 

    os << "Record count: " << records.size() << "\n\n"; 

    for (unsigned int i=0; i<records.size(); i++)
    {
        os << "Record #" << i+1 << endl;
        records[i].writeText(os, &userLabels);
        os << endl;
    }
}


void File::writeCSV(ostream& os) const
{
    Record::writeCSVLabels(os, &userLabels);

    for (vector<Record>::const_iterator it=records.begin(); it!=records.end(); ++it)
        it->writeCSV(os);
}


} // namespace sld
} // namespace pwiz


