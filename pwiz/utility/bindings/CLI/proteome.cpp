#include "proteome.hpp"
#include "SharedCLI.hpp"
#include "utility/proteome/Chemistry.hpp"
#include "utility/proteome/Peptide.hpp"

using System::String;

namespace b = pwiz::proteome;

namespace pwiz {
namespace CLI {
namespace proteome {


double Chemistry::Proton::get() {return pwiz::proteome::Chemistry::Proton;}
double Chemistry::Neutron::get() {return pwiz::proteome::Chemistry::Neutron;}
double Chemistry::Electron::get() {return pwiz::proteome::Chemistry::Electron;}


ref class Peptide::Impl
{
    DEFINE_INTERNAL_BASE_CODE(Impl, pwiz::proteome::Peptide);

    Impl(String^ sequence)
        :   base_(new b::Peptide(ToStdString(sequence)))
    {
    }
};

Peptide::Peptide(String^ sequence)
:   impl_(gcnew Impl(sequence))
{
}

String^ Peptide::sequence::get() {return gcnew String(impl_->base_->sequence().c_str());}

double Peptide::monoisotopicMass() {return impl_->base_->monoisotopicMass(0, true);}
double Peptide::monoisotopicMass(bool modified) {return impl_->base_->monoisotopicMass(0, modified);}
double Peptide::monoisotopicMass(int charge) {return impl_->base_->monoisotopicMass(charge, true);}
double Peptide::monoisotopicMass(bool modified, int charge) {return impl_->base_->monoisotopicMass(charge, modified);}

double Peptide::molecularWeight() {return impl_->base_->molecularWeight(0, true);}
double Peptide::molecularWeight(bool modified) {return impl_->base_->molecularWeight(0, modified);}
double Peptide::molecularWeight(int charge) {return impl_->base_->molecularWeight(charge, true);}
double Peptide::molecularWeight(bool modified, int charge) {return impl_->base_->molecularWeight(charge, modified);}


ref class Fragmentation::Impl
{
    DEFINE_INTERNAL_BASE_CODE(Impl, pwiz::proteome::Fragmentation);

    Impl(Peptide^ peptide,
         bool monoisotopic,
         bool modified)
        :   base_(new b::Fragmentation(peptide->impl_->base_->fragmentation(monoisotopic, modified)))
    {
    }
};

Fragmentation^ Peptide::fragmentation(bool monoisotopic, bool modified)
{
    return gcnew Fragmentation(this, monoisotopic, modified);
}

Fragmentation::Fragmentation(Peptide^ peptide,
                             bool monoisotopic,
                             bool modified)
:   impl_(gcnew Impl(peptide, monoisotopic, modified))
{
}

double Fragmentation::a(int length, int charge) {return impl_->base_->a((size_t) length, (size_t) charge);}

double Fragmentation::b(int length, int charge) {return impl_->base_->b((size_t) length, (size_t) charge);}

double Fragmentation::c(int length, int charge) {return impl_->base_->c((size_t) length, (size_t) charge);}

double Fragmentation::x(int length, int charge) {return impl_->base_->x((size_t) length, (size_t) charge);}

double Fragmentation::y(int length, int charge) {return impl_->base_->y((size_t) length, (size_t) charge);}

double Fragmentation::z(int length, int charge) {return impl_->base_->z((size_t) length, (size_t) charge);}

} // namespace proteome
} // namespace CLI
} // namespace pwiz
