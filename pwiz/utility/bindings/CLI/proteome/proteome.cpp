//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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


#include "proteome.hpp"

using System::String;
using namespace System::Collections::Generic;
using namespace pwiz::CLI::chemistry;
namespace b = pwiz::proteome;

// HACK: this suppresses a "TypeRef without TypeDef" error apparently caused by
// nested pimpl classes, but Enumerator still works, so it should be innocuous
class pwiz::proteome::Digestion::const_iterator::Impl {};

namespace pwiz {
namespace CLI {
namespace proteome {


IMPLEMENT_STRING_PROPERTY_GET(AminoAcidInfo::Record, name);
IMPLEMENT_STRING_PROPERTY_GET(AminoAcidInfo::Record, abbreviation);
IMPLEMENT_SIMPLE_PRIMITIVE_PROPERTY_GET(System::Char, AminoAcidInfo::Record, symbol);
IMPLEMENT_REFERENCE_PROPERTY_GET(Formula, AminoAcidInfo::Record, residueFormula);
IMPLEMENT_REFERENCE_PROPERTY_GET(Formula, AminoAcidInfo::Record, formula);

AminoAcidInfo::Record^ AminoAcidInfo::record(AminoAcid aminoAcid)
{
    return gcnew AminoAcidInfo::Record(const_cast<b::AminoAcid::Info::Record*>(&b::AminoAcid::Info::record((b::AminoAcid::Type) aminoAcid)), Proton::Mass);
}

AminoAcidInfo::Record^ AminoAcidInfo::record(System::Char symbol)
{
    try
    {
        return gcnew AminoAcidInfo::Record(const_cast<b::AminoAcid::Info::Record*>(&b::AminoAcid::Info::record((char) symbol)), Proton::Mass);
    }
    CATCH_AND_FORWARD
}


Peptide::Peptide()
:   base_(new b::Peptide("")), owner_(nullptr)
{
}

Peptide::Peptide(String^ sequence)
:   base_(new b::Peptide(ToStdString(sequence))), owner_(nullptr)
{
}

Peptide::Peptide(String^ sequence, ModificationParsing mp)
:   base_(new b::Peptide(ToStdString(sequence), (b::ModificationParsing) mp)), owner_(nullptr)
{
}

Peptide::Peptide(String^ sequence, ModificationParsing mp, ModificationDelimiter md)
:   base_(new b::Peptide(ToStdString(sequence), (b::ModificationParsing) mp, (b::ModificationDelimiter) md)), owner_(nullptr)
{
}

String^ Peptide::sequence::get() {return gcnew String(base_->sequence().c_str());}

String^ Peptide::formula() {return gcnew String(base_->formula().formula().c_str());}
String^ Peptide::formula(bool modified) {return gcnew String(base_->formula(modified).formula().c_str());}

double Peptide::monoisotopicMass() {return base_->monoisotopicMass(0, true);}
double Peptide::monoisotopicMass(bool modified) {return base_->monoisotopicMass(0, modified);}
double Peptide::monoisotopicMass(int charge) {return base_->monoisotopicMass(charge, true);}
double Peptide::monoisotopicMass(bool modified, int charge) {return base_->monoisotopicMass(charge, modified);}

double Peptide::molecularWeight() {return base_->molecularWeight(0, true);}
double Peptide::molecularWeight(bool modified) {return base_->molecularWeight(0, modified);}
double Peptide::molecularWeight(int charge) {return base_->molecularWeight(charge, true);}
double Peptide::molecularWeight(bool modified, int charge) {return base_->molecularWeight(charge, modified);}

ModificationMap^ Peptide::modifications() {return gcnew ModificationMap(&base_->modifications(), this);}


Fragmentation^ Peptide::fragmentation(bool monoisotopic, bool modified)
{
    return gcnew Fragmentation(this, monoisotopic, modified);
}

Fragmentation::Fragmentation(Peptide^ peptide,
                             bool monoisotopic,
                             bool modified)
:   base_(new b::Fragmentation(peptide->base_->fragmentation(monoisotopic, modified))), owner_(nullptr)
{
    System::GC::KeepAlive(peptide);
}

double Fragmentation::a(int length, int charge) {return base_->a((size_t) length, (size_t) charge);}

double Fragmentation::b(int length, int charge) {return base_->b((size_t) length, (size_t) charge);}

double Fragmentation::c(int length, int charge) {return base_->c((size_t) length, (size_t) charge);}

double Fragmentation::x(int length, int charge) {return base_->x((size_t) length, (size_t) charge);}

double Fragmentation::y(int length, int charge) {return base_->y((size_t) length, (size_t) charge);}

double Fragmentation::z(int length, int charge) {return base_->z((size_t) length, (size_t) charge);}

double Fragmentation::zRadical(int length, int charge) {return base_->zRadical((size_t) length, (size_t) charge);}


Modification::Modification(String^ formula)
: base_(new b::Modification(ToStdString(formula)))
{owner_ = nullptr;}

Modification::Modification(double monoisotopicDeltaMass,
                           double averageDeltaMass)
: base_(new b::Modification(monoisotopicDeltaMass, averageDeltaMass))
{owner_ = nullptr;}

bool Modification::hasFormula() {return base_->hasFormula();}
String^ Modification::formula() {return gcnew String(base_->formula().formula().c_str());}
double Modification::monoisotopicDeltaMass() {return base_->monoisotopicDeltaMass();}
double Modification::averageDeltaMass() {return base_->averageDeltaMass();}


ModificationList::ModificationList()
: ModificationBaseList(new b::ModificationList())
{owner_ = nullptr; }

ModificationList::ModificationList(Modification^ mod)
: ModificationBaseList(new b::ModificationList(*mod->base_))
{owner_ = nullptr; }

double ModificationList::monoisotopicDeltaMass() {return base_->monoisotopicDeltaMass();}
double ModificationList::averageDeltaMass() {return base_->averageDeltaMass();}


ModificationMap::ModificationMap() {}

int ModificationMap::NTerminus() {return b::ModificationMap::NTerminus();}
int ModificationMap::CTerminus() {return b::ModificationMap::CTerminus();}


DigestedPeptide::DigestedPeptide(String^ sequence)
{
    Peptide::base_ = base_ = new b::DigestedPeptide(ToStdString(sequence));
    owner_ = nullptr;
}

DigestedPeptide::DigestedPeptide(String^ sequence, int offset, int missedCleavages, bool NTerminusIsSpecific, bool CTerminusIsSpecific)
{
    std::string sequenceNative = ToStdString(sequence);
    Peptide::base_ = base_ = new b::DigestedPeptide(sequenceNative.begin(), sequenceNative.end(), (size_t) offset, (size_t) missedCleavages, NTerminusIsSpecific, CTerminusIsSpecific);
    owner_ = nullptr;
}

int DigestedPeptide::offset() {return (int) base_->offset();}
int DigestedPeptide::missedCleavages() {return (int) base_->missedCleavages();}
int DigestedPeptide::specificTermini() {return (int) base_->specificTermini();}
bool DigestedPeptide::NTerminusIsSpecific() {return base_->NTerminusIsSpecific();}
bool DigestedPeptide::CTerminusIsSpecific() {return base_->CTerminusIsSpecific();}
String^ DigestedPeptide::NTerminusPrefix() {return ToSystemString(base_->NTerminusPrefix());}
String^ DigestedPeptide::CTerminusSuffix() {return ToSystemString(base_->CTerminusSuffix());}


Digestion::Config::Config()
{
    b::Digestion::Config defaultConfig;
    maximumMissedCleavages = defaultConfig.maximumMissedCleavages;
    minimumLength = defaultConfig.minimumLength;
    maximumLength = defaultConfig.maximumLength;
    minimumSpecificity = (Specificity) defaultConfig.minimumSpecificity;
}

Digestion::Config::Config(int maximumMissedCleavages,
                          int minimumLength,
                          int maximumLength,
                          Specificity minimumSpecificity)
{
    this->maximumMissedCleavages = maximumMissedCleavages;
    this->minimumLength = minimumLength;
    this->maximumLength = maximumLength;
    this->minimumSpecificity = minimumSpecificity;
}


Digestion::Digestion(Peptide^ peptide, CVID cleavageAgent)
{
    base_ = new b::Digestion(peptide->base(), (pwiz::cv::CVID) cleavageAgent);
    System::GC::KeepAlive(peptide);
}

Digestion::Digestion(Peptide^ peptide, CVID cleavageAgent, Config^ config)
{
    base_ = new b::Digestion(peptide->base(), (pwiz::cv::CVID) cleavageAgent,
                                 b::Digestion::Config(config->maximumMissedCleavages,
                                                      config->minimumLength,
                                                      config->maximumLength,
                                                      (b::Digestion::Specificity) config->minimumSpecificity));
    System::GC::KeepAlive(peptide);
    System::GC::KeepAlive(config);
}

Digestion::Digestion(Peptide^ peptide, IEnumerable<CVID>^ cleavageAgents)
{
    std::vector<pwiz::cv::CVID> cleavageAgentsNative;
    for each (CVID cvid in cleavageAgents)
        cleavageAgentsNative.push_back((pwiz::cv::CVID) cvid);

    base_ = new b::Digestion(peptide->base(), cleavageAgentsNative);
    System::GC::KeepAlive(peptide);
    System::GC::KeepAlive(cleavageAgents);
}

Digestion::Digestion(Peptide^ peptide, IEnumerable<CVID>^ cleavageAgents, Config^ config)
{
    std::vector<pwiz::cv::CVID> cleavageAgentsNative;
    for each (CVID cvid in cleavageAgents)
        cleavageAgentsNative.push_back((pwiz::cv::CVID) cvid);

    base_ = new b::Digestion(peptide->base(), cleavageAgentsNative,
                                 b::Digestion::Config(config->maximumMissedCleavages,
                                                      config->minimumLength,
                                                      config->maximumLength,
                                                      (b::Digestion::Specificity) config->minimumSpecificity));
    System::GC::KeepAlive(peptide);
    System::GC::KeepAlive(cleavageAgents);
    System::GC::KeepAlive(config);
}

Digestion::Digestion(Peptide^ peptide, System::String^ cleavageAgentRegex)
{
    base_ = new b::Digestion(peptide->base(), ToStdString(cleavageAgentRegex));
    System::GC::KeepAlive(peptide);
}

Digestion::Digestion(Peptide^ peptide, System::String^ cleavageAgentRegex, Config^ config)
{
    base_ = new b::Digestion(peptide->base(), ToStdString(cleavageAgentRegex),
                                 b::Digestion::Config(config->maximumMissedCleavages,
                                                      config->minimumLength,
                                                      config->maximumLength,
                                                      (b::Digestion::Specificity) config->minimumSpecificity));
    System::GC::KeepAlive(peptide);
    System::GC::KeepAlive(config);
}

CVID Digestion::getCleavageAgentByName(System::String^ agentName)
{
    return (CVID) b::Digestion::getCleavageAgentByName(ToStdString(agentName));
}

List<String^>^ Digestion::getCleavageAgentNames()
{
    List<String^>^ cleavageAgentNames = gcnew List<String^>();
    for(const std::string& s : b::Digestion::getCleavageAgentNames())
        cleavageAgentNames->Add(ToSystemString(s));
    return cleavageAgentNames;
}

String^ Digestion::getCleavageAgentRegex(CVID agentCvid)
{
    try
    {
        return ToSystemString(b::Digestion::getCleavageAgentRegex((pwiz::cv::CVID) agentCvid));
    }
    CATCH_AND_FORWARD
}

List<CVID>^ Digestion::getCleavageAgents()
{
    List<CVID>^ cleavageAgents = gcnew List<CVID>();
    for(const pwiz::cv::CVID& cvid : b::Digestion::getCleavageAgents())
        cleavageAgents->Add((CVID) cvid);
    return cleavageAgents;
}

IList<DigestedPeptide^>^ Digestion::find_all(Peptide^ peptide)
{
    List<DigestedPeptide^>^ instances = gcnew List<DigestedPeptide^>();

    std::vector<b::DigestedPeptide> nativeInstances = base().find_all(peptide->base());

    // make copies of the native DigestedPeptides because the vector is transient
    for (std::vector<b::DigestedPeptide>::const_iterator itr = nativeInstances.begin();
         itr != nativeInstances.end();
         ++itr)
        instances->Add(gcnew DigestedPeptide(new b::DigestedPeptide(*itr)));

    System::GC::KeepAlive(peptide);
    return instances;
}

IList<DigestedPeptide^>^ Digestion::find_all(String^ sequence)
{
    Peptide peptide(sequence);
    return find_all(%peptide);
}

DigestedPeptide^ Digestion::find_first(Peptide^ peptide)
{
    return find_first(peptide, 0);
}

DigestedPeptide^ Digestion::find_first(String^ sequence)
{
    return find_first(sequence, 0);
}

DigestedPeptide^ Digestion::find_first(Peptide^ peptide, int offsetHint)
{
    try
    {
        b::DigestedPeptide instance = base().find_first(peptide->base(), (size_t) offsetHint);
        System::GC::KeepAlive(peptide);

        // make a copy of the native DigestedPeptide because the instance is transient
        return gcnew DigestedPeptide(new b::DigestedPeptide(instance));
    }
    CATCH_AND_FORWARD
}

DigestedPeptide^ Digestion::find_first(String^ sequence, int offsetHint)
{
    try
    {
        DigestedPeptide^ instance = gcnew DigestedPeptide(new b::DigestedPeptide("K"));
        instance->base() = base().find_first(ToStdString(sequence), (size_t) offsetHint);
        return instance;
    }
    CATCH_AND_FORWARD
}


} // namespace proteome
} // namespace CLI
} // namespace pwiz
