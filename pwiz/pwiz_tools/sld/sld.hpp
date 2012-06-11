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


#ifndef _SLD_HPP_
#define _SLD_HPP_


#include <vector>
#include <string>
#include <iosfwd>


namespace pwiz {
namespace sld {


struct Record
{
    Record();

    // 64 byte header

    int one; // always 1?
    int sampleType;
    int unknown1;
    int unknown2;
    int unknown3;
    double injectionVolume;
    double sampleWeight;
    double sampleVolume;
    double istdCorrectionAmount;
    double dilFactor;
    int unknown4;

    // 17 strings

    enum StringIndex 
    {
        SampleName,
        SampleID,
        Comment,
        L1,
        L2,
        L3,
        L4,
        L5,
        InstrumentMethod,
        ProcessMethod,
        Filename,
        Path,
        Position,
        Unknown1,
        Unknown2,
        Unknown3,
        Unknown4,
        StringCount
    };

    std::vector<std::string> strings;

    // output functions

    void writeText(std::ostream& os, const std::vector<std::string>* userLabels = 0) const;
    static void writeCSVLabels(std::ostream& os, const std::vector<std::string>* userLabels = 0);
    void writeCSV(std::ostream& os) const;
};


struct File
{
    File(const std::string& filename);

    // user-defined labels
    static const int userLabelCount_ = 5;
    std::vector<std::string> userLabels;

    // records
    std::vector<Record> records;

    // output functions
    void writeText(std::ostream& os) const;
    void writeCSV(std::ostream& os) const;
};


} // namespace sld
} // namespace pwiz


#endif // _SLD_HPP_

