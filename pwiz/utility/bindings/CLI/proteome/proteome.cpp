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

namespace b = pwiz::proteome;

namespace pwiz {
namespace CLI {
namespace proteome {


double Chemistry::Proton::get() {return pwiz::chemistry::Proton;}
double Chemistry::Neutron::get() {return pwiz::chemistry::Neutron;}
double Chemistry::Electron::get() {return pwiz::chemistry::Electron;}


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


} // namespace proteome
} // namespace CLI
} // namespace pwiz
