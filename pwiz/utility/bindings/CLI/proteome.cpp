#include "proteome.hpp"

using System::String;

namespace b = pwiz::proteome;

namespace pwiz {
namespace CLI {
namespace proteome {

Peptide::Peptide(String^ sequence)
:   base_(new b::Peptide(ToStdString(sequence)))
{
}

String^ Peptide::sequence::get() {return gcnew String(base_->sequence().c_str());}

double Peptide::monoisotopicMass() {return base_->monoisotopicMass(0, true);}
double Peptide::monoisotopicMass(bool modified) {return base_->monoisotopicMass(0, modified);}
double Peptide::monoisotopicMass(int charge) {return base_->monoisotopicMass(charge, true);}
double Peptide::monoisotopicMass(bool modified, int charge) {return base_->monoisotopicMass(charge, modified);}

double Peptide::molecularWeight() {return base_->molecularWeight(0, true);}
double Peptide::molecularWeight(bool modified) {return base_->molecularWeight(0, modified);}
double Peptide::molecularWeight(int charge) {return base_->molecularWeight(charge, true);}
double Peptide::molecularWeight(bool modified, int charge) {return base_->molecularWeight(charge, modified);}

Fragmentation^ Peptide::fragmentation(bool monoisotopic, bool modified)
{
    return gcnew Fragmentation(this, monoisotopic, modified);
}

Fragmentation::Fragmentation(Peptide^ peptide,
                             bool monoisotopic,
                             bool modified)
:   base_(new b::Fragmentation(peptide->base_->fragmentation(monoisotopic, modified)))
{
}

double Fragmentation::a(int length, int charge) {return base_->a((size_t) length, (size_t) charge);}

double Fragmentation::b(int length, int charge) {return base_->b((size_t) length, (size_t) charge);}

double Fragmentation::c(int length, int charge) {return base_->c((size_t) length, (size_t) charge);}

double Fragmentation::x(int length, int charge) {return base_->x((size_t) length, (size_t) charge);}

double Fragmentation::y(int length, int charge) {return base_->y((size_t) length, (size_t) charge);}

double Fragmentation::z(int length, int charge) {return base_->z((size_t) length, (size_t) charge);}

} // namespace proteome
} // namespace CLI
} // namespace pwiz
