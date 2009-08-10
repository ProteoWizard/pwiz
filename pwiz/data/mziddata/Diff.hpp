//
// Diff.hpp
//
//
// Original author: Robert Burke <robetr.burke@proteowizard.org>
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics
//   University of Southern California, Los Angeles, California  90033
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


#ifndef _MSIDDATA_DIFF_HPP_
#define _MSIDDATA_DIFF_HPP_

#include "pwiz/utility/misc/Export.hpp"
#include "MzIdentML.hpp"
#include "TextWriter.hpp"

#include <string>

namespace pwiz {
namespace mziddata {

struct PWIZ_API_DECL DiffConfig 
{
    /// precision with which two doubles are compared
    double precision;

    DiffConfig()
    :   precision(1e-6)
    {}
};


//
// diff implementation declarations
//


namespace diff_impl {


PWIZ_API_DECL
void diff(const std::string& a,
          const std::string& b,
          std::string& a_b,
          std::string& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const CV& a,
          const CV& b,
          CV& a_b,
          CV& b_a,
          const DiffConfig& config);

void diff(const CVParam& a, 
          const CVParam& b, 
          CVParam& a_b, 
          CVParam& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const ParamContainer& a,
          const ParamContainer& b,
          ParamContainer& a_b,
          ParamContainer& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const FragmentArray& a,
          const FragmentArray& b,
          FragmentArray& a_b,
          FragmentArray& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const IonType& a,
          const IonType& b,
          IonType& a_b,
          IonType& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Material& a,
          const Material& b,
          Material& a_b,
          Material& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Measure& a,
          const Measure& b,
          Measure& a_b,
          Measure& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const ModParam& a,
          const ModParam& b,
          ModParam& a_b,
          ModParam& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const PeptideEvidence& a,
          const PeptideEvidence& b,
          PeptideEvidence& a_b,
          PeptideEvidence& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const ProteinAmbiguityGroup& a,
          const ProteinAmbiguityGroup& b,
          ProteinAmbiguityGroup& a_b,
          ProteinAmbiguityGroup& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const ProteinDetectionHypothesis& a,
          const ProteinDetectionHypothesis& b,
          ProteinDetectionHypothesis& a_b,
          ProteinDetectionHypothesis& b_a,
          const DiffConfig& config);


PWIZ_API_DECL
void diff(const DataCollection& a,
          const DataCollection& b,
          DataCollection& a_b,
          DataCollection& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const SpectrumIdentificationList& a,
          const SpectrumIdentificationList& b,
          SpectrumIdentificationList& a_b,
          SpectrumIdentificationList& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const ProteinDetectionList& a,
          const ProteinDetectionList& b,
          ProteinDetectionList& a_b,
          ProteinDetectionList& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const AnalysisData& a,
          const AnalysisData& b,
          AnalysisData& a_b,
          AnalysisData& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const SearchDatabase& a,
          const SearchDatabase& b,
          SearchDatabase& a_b,
          SearchDatabase& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const SpectraData& a,
          const SpectraData& b,
          SpectraData& a_b,
          SpectraData& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const SourceFile& a,
          const SourceFile& b,
          SourceFile& a_b,
          SourceFile& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Inputs& a,
          const Inputs& b,
          Inputs& a_b,
          Inputs& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Enzyme& a,
          const Enzyme& b,
          Enzyme& a_b,
          Enzyme& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Enzymes& a,
          const Enzymes& b,
          Enzymes& a_b,
          Enzymes& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const MassTable& a,
          const MassTable& b,
          MassTable& a_b,
          MassTable& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Residue& a,
          const Residue& b,
          Residue& a_b,
          Residue& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const AmbiguousResidue& a,
          const AmbiguousResidue& b,
          AmbiguousResidue& a_b,
          AmbiguousResidue& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Filter& a,
          const Filter& b,
          Filter& a_b,
          Filter& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const SpectrumIdentificationProtocol& a,
          const SpectrumIdentificationProtocol& b,
          SpectrumIdentificationProtocol& a_b,
          SpectrumIdentificationProtocol& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const ProteinDetectionProtocol& a,
          const ProteinDetectionProtocol& b,
          ProteinDetectionProtocol& a_b,
          ProteinDetectionProtocol& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const AnalysisProtocolCollection& a,
          const AnalysisProtocolCollection& b,
          AnalysisProtocolCollection& a_b,
          AnalysisProtocolCollection& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Contact& a,
          const Contact& b,
          Contact& a_b,
          Contact& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Affiliations& a,
          const Affiliations& b,
          Affiliations& a_b,
          Affiliations& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Person& a,
          const Person& b,
          Person& a_b,
          Person& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Organization& a,
          const Organization& b,
          Organization& a_b,
          Organization& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const BibliographicReference& a,
          const BibliographicReference& b,
          BibliographicReference& a_b,
          BibliographicReference& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const ProteinDetection& a,
          const ProteinDetection& b,
          ProteinDetection& a_b,
          ProteinDetection& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const SpectrumIdentification& a,
          const SpectrumIdentification& b,
          SpectrumIdentification& a_b,
          SpectrumIdentification& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const AnalysisCollection& a,
          const AnalysisCollection& b,
          AnalysisCollection& a_b,
          AnalysisCollection& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const DBSequence& a,
          const DBSequence& b,
          DBSequence& a_b,
          DBSequence& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Modification& a,
          const Modification& b,
          Modification& a_b,
          Modification& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const SubstitutionModification& a,
          const SubstitutionModification& b,
          SubstitutionModification& a_b,
          SubstitutionModification& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Peptide& a,
          const Peptide& b,
          Peptide& a_b,
          Peptide& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const SequenceCollection& a,
          const SequenceCollection& b,
          SequenceCollection& a_b,
          SequenceCollection& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Sample::subSample& a,
          const Sample::subSample& b,
          Sample::subSample& a_b,
          Sample::subSample& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Sample& a,
          const Sample& b,
          Sample& a_b,
          Sample& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const SearchModification& a,
          const SearchModification& b,
          SearchModification& a_b,
          SearchModification& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const SpectrumIdentificationItem& a,
          const SpectrumIdentificationItem& b,
          SpectrumIdentificationItem& a_b,
          SpectrumIdentificationItem& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const SpectrumIdentificationResult& a,
          const SpectrumIdentificationResult& b,
          SpectrumIdentificationResult& a_b,
          SpectrumIdentificationResult& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const AnalysisSampleCollection& a,
          const AnalysisSampleCollection& b,
          AnalysisSampleCollection& a_b,
          AnalysisSampleCollection& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Provider& a,
          const Provider& b,
          Provider& a_b,
          Provider& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const ContactRole& a,
          const ContactRole& b,
          ContactRole& a_b,
          ContactRole& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const AnalysisSoftware& a,
          const AnalysisSoftware& b,
          AnalysisSoftware& a_b,
          AnalysisSoftware& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const MzIdentML& a,
          const MzIdentML& b,
          MzIdentML& a_b,
          MzIdentML& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const IdentifiableType& a,
          const IdentifiableType& b,
          IdentifiableType& a_b,
          IdentifiableType& b_a,
          const DiffConfig& config);

} // namespace diff_impl

///     
/// Calculate diffs of objects in the MSData structure hierarchy.
///
/// A diff between two objects a and b calculates the set differences
/// a\b and b\a.
///
/// The Diff struct acts as a functor, but also stores the 
/// results of the diff calculation.  
///
/// The bool conversion operator is provided to indicate whether 
/// the two objects are different (either a\b or b\a is non-empty).
///
/// object_type requirements:
///   object_type a;
///   a.empty();
///   diff(const object_type& a, const object_type& b, object_type& a_b, object_type& b_a);
///
template <typename object_type>
struct Diff
{
    Diff(const DiffConfig& config = DiffConfig())
    :   config_(config)
    {}

    Diff(const object_type& a,
               const object_type& b,
               const DiffConfig& config = DiffConfig())
    :   config_(config)
    {
        
        diff_impl::diff(a, b, a_b, b_a, config_);
    }

    object_type a_b;
    object_type b_a;

    /// conversion to bool, with same semantics as *nix diff command:
    ///  true == different
    ///  false == not different
    operator bool() {return !(a_b.empty() && b_a.empty());}

    Diff& operator()(const object_type& a,
                           const object_type& b)
    {
        
        diff_impl::diff(a, b, a_b, b_a, config_);
        return *this;
    }

    private:
    DiffConfig config_;
};


///
/// stream insertion of Diff results
///

template <typename object_type>
std::ostream& operator<<(std::ostream& os, const Diff<object_type>& diff)
{
    TextWriter write(os, 1);

    if (!diff.a_b.empty())
    {            
        os << "+\n";
        write(diff.a_b);
    }

    if (!diff.b_a.empty())
    {            
        os << "-\n";
        write(diff.b_a);
    }

    return os;
}

PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const Diff<MzIdentML>& diff);

} // namespace mziddata
} // namespace pwiz

#endif // _MSIDDATA_DIFF_HPP_
