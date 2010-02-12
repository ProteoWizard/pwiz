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

#ifndef _MZIDDATA_IO_HPP_
#define  _MZIDDATA_IO_HPP_

#include "pwiz/utility/misc/Export.hpp"
#include "MzIdentML.hpp"
#include "pwiz/utility/minimxml/XMLWriter.hpp"


namespace pwiz {
namespace mziddata {

namespace IO {

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const CV& cv);
PWIZ_API_DECL void read(std::istream& is, CV& cv);
    

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const UserParam& userParam);
PWIZ_API_DECL void read(std::istream& is, UserParam& userParam);
    

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const CVParam& cv);
PWIZ_API_DECL void read(std::istream& is, CVParam& cv);
    

// Novel functions

// For testing purposes only
PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const IdentifiableType& it);
PWIZ_API_DECL void read(std::istream& is, IdentifiableType& it);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ContactRole& contactRole);
PWIZ_API_DECL void read(std::istream& writer, ContactRole& contactRole);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Provider& provider);
PWIZ_API_DECL void read(std::istream& writer, Provider& provider);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SpectrumIdentification& si);
PWIZ_API_DECL void read(std::istream& writer, SpectrumIdentification& si);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const IonType& ionType);
PWIZ_API_DECL void read(std::istream& writer, IonType& ionType);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Measure& measure);
PWIZ_API_DECL void read(std::istream& writer, Measure& measure);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Material& material);
PWIZ_API_DECL void read(std::istream& writer, Material& material);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SamplePtr sample);
PWIZ_API_DECL void read(std::istream& writer, SamplePtr sample);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Sample& sample);
PWIZ_API_DECL void read(std::istream& writer, Sample& sample);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Sample::subSample& subsample);
PWIZ_API_DECL void read(std::istream& writer, Sample::subSample& subsample);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ContactPtr cp);
PWIZ_API_DECL void read(std::istream& writer, ContactPtr cp);

/*
PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const OrganizationPtr cp);
PWIZ_API_DECL void read(std::istream& writer, OrganizationPtr cp);



PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const PersonPtr cp);
PWIZ_API_DECL void read(std::istream& writer, PersonPtr cp);
*/

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Person& cp);
PWIZ_API_DECL void read(std::istream& writer, Person& cp);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Organization& cp);
PWIZ_API_DECL void read(std::istream& writer, Organization& cp);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SearchModification& sm);
PWIZ_API_DECL void read(std::istream& is, SearchModification& sm);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Filter& filter);
PWIZ_API_DECL void read(std::istream& is, Filter& filter);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const TranslationTablePtr filter);
PWIZ_API_DECL void read(std::istream& is, TranslationTablePtr filter);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const TranslationTable& filter);
PWIZ_API_DECL void read(std::istream& is, TranslationTable& filter);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const DatabaseTranslationPtr filter);
PWIZ_API_DECL void read(std::istream& is, DatabaseTranslationPtr filter);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const DatabaseTranslation& filter);
PWIZ_API_DECL void read(std::istream& is, DatabaseTranslation& filter);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SpectrumIdentificationProtocolPtr sip);
PWIZ_API_DECL void read(std::istream& is, SpectrumIdentificationProtocolPtr sip);

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SpectrumIdentificationProtocol& si);
PWIZ_API_DECL void read(std::istream& is, SpectrumIdentificationProtocol& si);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ProteinDetectionProtocolPtr pdp);
PWIZ_API_DECL void read(std::istream& is, ProteinDetectionProtocolPtr pdp);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ProteinDetectionProtocol& pdp);
PWIZ_API_DECL void read(std::istream& is, ProteinDetectionProtocol& pdp);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ProteinDetection& pd);
PWIZ_API_DECL void read(std::istream& is, ProteinDetection& pd);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const AnalysisCollection& ac);
PWIZ_API_DECL void read(std::istream& is, AnalysisCollection& ac);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ModParam& mp);
PWIZ_API_DECL void read(std::istream& is, ModParam& mp);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Enzyme& ez);
PWIZ_API_DECL void read(std::istream& is, Enzyme& ez);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Enzymes& ez);
PWIZ_API_DECL void read(std::istream& is, Enzymes& ez);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ResiduePtr ez);
PWIZ_API_DECL void read(std::istream& is, ResiduePtr ez);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Residue& ez);
PWIZ_API_DECL void read(std::istream& is, Residue& ez);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const AmbiguousResiduePtr residue);
PWIZ_API_DECL void read(std::istream& is, AmbiguousResiduePtr residue);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const AmbiguousResidue& residue);
PWIZ_API_DECL void read(std::istream& is, AmbiguousResidue& residue);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const MassTable& mt);
PWIZ_API_DECL void read(std::istream& is, MassTable& mt);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const AnalysisProtocolCollection& apc);
PWIZ_API_DECL void read(std::istream& is, AnalysisProtocolCollection& apc);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SpectraData& sd);
PWIZ_API_DECL void read(std::istream& is, SpectraData& sd);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SpectraDataPtr sd);
PWIZ_API_DECL void read(std::istream& is, SpectraDataPtr sd);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SourceFile& sf);
PWIZ_API_DECL void read(std::istream& is, SourceFile& sf);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SearchDatabase& sd);
PWIZ_API_DECL void read(std::istream& is, SearchDatabase& sd);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SearchDatabasePtr sd);
PWIZ_API_DECL void read(std::istream& is, SearchDatabasePtr sd);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Inputs& inputs);
PWIZ_API_DECL void read(std::istream& is, Inputs& inputs);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const FragmentArrayPtr fa);
PWIZ_API_DECL void read(std::istream& is, FragmentArrayPtr fa);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const FragmentArray& fa);
PWIZ_API_DECL void read(std::istream& is, FragmentArray& fa);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SpectrumIdentificationItemPtr sir);
PWIZ_API_DECL void read(std::istream& is, SpectrumIdentificationItemPtr sir);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SpectrumIdentificationItem& sir);
PWIZ_API_DECL void read(std::istream& is, SpectrumIdentificationItem& sir);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SpectrumIdentificationResultPtr sir);
PWIZ_API_DECL void read(std::istream& is, SpectrumIdentificationResultPtr sir);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ProteinDetectionHypothesis& pdh);
PWIZ_API_DECL void read(std::istream& is, ProteinDetectionHypothesis& pdh);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ProteinAmbiguityGroup& pdh);
PWIZ_API_DECL void read(std::istream& is, ProteinAmbiguityGroup& pdh);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SpectrumIdentificationList& pdh);
PWIZ_API_DECL void read(std::istream& is, SpectrumIdentificationList& pdh);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SpectrumIdentificationListPtr pdh);
PWIZ_API_DECL void read(std::istream& is, SpectrumIdentificationListPtr pdh);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SpectrumIdentificationResult& sir);
PWIZ_API_DECL void read(std::istream& is, SpectrumIdentificationResult& sir);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ProteinDetectionList& pdl);
PWIZ_API_DECL void read(std::istream& is, ProteinDetectionList& pdl);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const AnalysisData& pdl);
PWIZ_API_DECL void read(std::istream& is, AnalysisData& pdl);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const BibliographicReference& bibliographicReference);
PWIZ_API_DECL void read(std::istream& is, BibliographicReference& bibliographicReference);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const DataCollection& dataCollection);
PWIZ_API_DECL void read(std::istream& is, DataCollection& dataCollection);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const AnalysisSoftwarePtr analysisSoftware);
PWIZ_API_DECL void read(std::istream& is, AnalysisSoftwarePtr analysisSoftware);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const AnalysisSoftware& analysisSoftware);
PWIZ_API_DECL void read(std::istream& is, AnalysisSoftware& analysisSoftware);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const DBSequencePtr dbSequence);
PWIZ_API_DECL void read(std::istream& is, DBSequencePtr dbSequence);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const DBSequence& dbSequence);
PWIZ_API_DECL void read(std::istream& is, DBSequence& dbSequence);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Peptide& peptide);
PWIZ_API_DECL void read(std::istream& is, Peptide& peptide);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const PeptidePtr peptide);
PWIZ_API_DECL void read(std::istream& is, PeptidePtr peptide);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const PeptideEvidencePtr pe);
PWIZ_API_DECL void read(std::istream& is, PeptideEvidencePtr pe);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const PeptideEvidence& pe);
PWIZ_API_DECL void read(std::istream& is, PeptideEvidence& pe);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ModificationPtr mod);
PWIZ_API_DECL void read(std::istream& is, ModificationPtr mod);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Modification& mod);
PWIZ_API_DECL void read(std::istream& is, Modification& mod);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SubstitutionModification& sm);
PWIZ_API_DECL void read(std::istream& is, SubstitutionModification& sm);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SequenceCollection& sc);
PWIZ_API_DECL void read(std::istream& is, SequenceCollection& sc);



PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const AnalysisSampleCollection& asc);
PWIZ_API_DECL void read(std::istream& is, AnalysisSampleCollection& asc);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const MzIdentML& mzidDataPtr);
PWIZ_API_DECL void read(std::istream& is, MzIdentML& mziddata);


} // namespace IO

} // namespace pwiz 
} // namespace mziddata 

#endif //  _MZIDDATA_IO_HPP_
