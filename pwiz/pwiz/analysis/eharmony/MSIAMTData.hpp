//
// $Id$
//
//
// Original author: Kate Hoff <katherine.hoff@proteowizard.org>
//
// Copyright 2009 Center for Applied Molecular Medicine
//   University of Southern California, Los Angeles, CA
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

///
/// MSIAMTData.hpp
///

/// Not a verbatim reader/writer; mainly follows msinspect/amt schema but gets only the necessary elements, writing skipped element names to the console for monitoring , writes only elements necessary for eharmony

#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/utility/minimxml/XMLWriter.hpp"
#include "pwiz/utility/minimxml/SAXParser.hpp"
#include <vector>

namespace pwiz{
namespace eharmony{

using namespace pwiz::minimxml;

struct Observation
{
    double observedHydrophobicity;
    double peptideProphet;
    size_t runID;
    double timeInRun;
    size_t spectralCount;

    void read(std::istream& is);
    void write(XMLWriter& writer) const;

    Observation() : observedHydrophobicity(0), peptideProphet(0), runID(0), timeInRun(0), spectralCount(0){}

};

struct ModificationStateEntry
{
    std::string modifiedSequence;
    double modifiedMass;
    double medianObservedHydrophobicity;
    double medianPeptideProphet;
    
    std::vector<Observation> observations;

    void read(std::istream& is);
    void write(XMLWriter& writer) const;

    ModificationStateEntry() : modifiedSequence(""), modifiedMass(0), medianObservedHydrophobicity(0), medianPeptideProphet(0){}

};

struct PeptideEntry
{
    std::string peptideSequence;
    double calculatedHydrophobicity;
    double medianObservedHydrophobicity;
    double medianPeptideProphet;

    std::vector<ModificationStateEntry> modificationStateEntries;

    void read(std::istream& is);
    void write(XMLWriter& writer) const;

    PeptideEntry() : peptideSequence(""), calculatedHydrophobicity(0), medianObservedHydrophobicity(0), medianPeptideProphet(0){}

};

struct MSIAMTData // does not contain all elements of the <amt:amt_database ... > tag but serves as a container for reading in peptide entries
{
    std::vector<PeptideEntry> peptideEntries;
    
    void read(std::istream& is);
    void write(XMLWriter& writer) const;

    MSIAMTData(){}

};

} // eharmony
} // pwiz
