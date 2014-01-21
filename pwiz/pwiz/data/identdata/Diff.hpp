//
// $Id$
//
//
// Original author: Robert Burke <robert.burke@proteowizard.org>
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
#include "IdentData.hpp"

namespace pwiz { namespace identdata { struct DiffConfig; } }


namespace pwiz {
namespace data {
namespace diff_impl {

using namespace identdata;

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
void diff(const Measure& a,
          const Measure& b,
          Measure& a_b,
          Measure& b_a,
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
void diff(const PeptideHypothesis& a,
          const PeptideHypothesis& b,
          PeptideHypothesis& a_b,
          PeptideHypothesis& b_a,
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
void diff(const SpectrumIdentificationListPtr a,
          const SpectrumIdentificationListPtr b,
          SpectrumIdentificationListPtr a_b,
          SpectrumIdentificationListPtr b_a,
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
void diff(const DatabaseTranslation& a,
          const DatabaseTranslation& b,
          DatabaseTranslation& a_b,
          DatabaseTranslation& b_a,
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
void diff(const IdentData& a,
          const IdentData& b,
          IdentData& a_b,
          IdentData& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Identifiable& a,
          const Identifiable& b,
          Identifiable& a_b,
          Identifiable& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const IdentifiableParamContainer& a,
          const IdentifiableParamContainer& b,
          IdentifiableParamContainer& a_b,
          IdentifiableParamContainer& b_a,
          const DiffConfig& config);

} // namespace diff_impl
} // namespace data
} // namespace pwiz



// this include must come after the above declarations or GCC won't see them
#include "pwiz/data/common/diff_std.hpp"


namespace pwiz {
namespace identdata {

struct PWIZ_API_DECL DiffConfig : public pwiz::data::BaseDiffConfig
{
    DiffConfig()
        :   BaseDiffConfig(1.2e-6) // Hack to make the maxdiff work
    {}
};


PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const data::Diff<IdentData, DiffConfig>& diff);

} // namespace identdata
} // namespace pwiz

#endif // _MSIDDATA_DIFF_HPP_
