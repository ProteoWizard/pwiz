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

#ifndef _IDENTDATA_IO_HPP_
#define _IDENTDATA_IO_HPP_

#include "pwiz/utility/misc/Export.hpp"
#include "IdentData.hpp"
#include "pwiz/utility/minimxml/XMLWriter.hpp"
#include "pwiz/utility/misc/IterationListener.hpp"


namespace pwiz {
namespace identdata {

enum SchemaVersion { SchemaVersion_1_1 = 0, SchemaVersion_1_0 }; // 0 is the default

namespace IO {


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const CV& cv);
PWIZ_API_DECL void read(std::istream& is, CV& cv);
    

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const UserParam& userParam);
PWIZ_API_DECL void read(std::istream& is, UserParam& userParam);
    

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const CVParam& cv);
PWIZ_API_DECL void read(std::istream& is, CVParam& cv);
    

// Novel functions

// For testing purposes only
PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Identifiable& it);
PWIZ_API_DECL void read(std::istream& is, Identifiable& it);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const IdentifiableParamContainer& it);
PWIZ_API_DECL void read(std::istream& is, IdentifiableParamContainer& it);


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


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Sample& sample);
PWIZ_API_DECL void read(std::istream& writer, Sample& sample);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Person& cp);
PWIZ_API_DECL void read(std::istream& writer, Person& cp);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Organization& cp);
PWIZ_API_DECL void read(std::istream& writer, Organization& cp);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SearchModification& sm);
PWIZ_API_DECL void read(std::istream& is, SearchModification& sm);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Filter& filter);
PWIZ_API_DECL void read(std::istream& is, Filter& filter);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const TranslationTable& tt);
PWIZ_API_DECL void read(std::istream& is, TranslationTable& tt);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const DatabaseTranslation& dt);
PWIZ_API_DECL void read(std::istream& is, DatabaseTranslation& dt);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SpectrumIdentificationProtocol& sip);
PWIZ_API_DECL void read(std::istream& is, SpectrumIdentificationProtocol& sip);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ProteinDetectionProtocol& pdp);
PWIZ_API_DECL void read(std::istream& is, ProteinDetectionProtocol& pdp);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ProteinDetection& pd);
PWIZ_API_DECL void read(std::istream& is, ProteinDetection& pd);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const AnalysisCollection& ac);
PWIZ_API_DECL void read(std::istream& is, AnalysisCollection& ac);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Enzyme& ez);
PWIZ_API_DECL void read(std::istream& is, Enzyme& ez);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Enzymes& ez);
PWIZ_API_DECL void read(std::istream& is, Enzymes& ez);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Residue& r);
PWIZ_API_DECL void read(std::istream& is, Residue& r);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const AmbiguousResidue& ar);
PWIZ_API_DECL void read(std::istream& is, AmbiguousResidue& ar);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const MassTable& mt);
PWIZ_API_DECL void read(std::istream& is, MassTable& mt);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const AnalysisProtocolCollection& apc);
PWIZ_API_DECL void read(std::istream& is, AnalysisProtocolCollection& apc);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SpectraData& sd);
PWIZ_API_DECL void read(std::istream& is, SpectraData& sd);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SourceFile& sf);
PWIZ_API_DECL void read(std::istream& is, SourceFile& sf);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SearchDatabase& sd);
PWIZ_API_DECL void read(std::istream& is, SearchDatabase& sd);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Inputs& inputs);
PWIZ_API_DECL void read(std::istream& is, Inputs& inputs);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const FragmentArray& fa);
PWIZ_API_DECL void read(std::istream& is, FragmentArray& fa);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SpectrumIdentificationItem& sir);
PWIZ_API_DECL void read(std::istream& is, SpectrumIdentificationItem& sir);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const PeptideHypothesis& ph);
PWIZ_API_DECL void read(std::istream& is, PeptideHypothesis& ph);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ProteinDetectionHypothesis& pdh);
PWIZ_API_DECL void read(std::istream& is, ProteinDetectionHypothesis& pdh);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ProteinAmbiguityGroup& pag);
PWIZ_API_DECL void read(std::istream& is, ProteinAmbiguityGroup& pag);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SpectrumIdentificationList& sil,
                         const pwiz::util::IterationListenerRegistry* iterationListenerRegistry = 0);
PWIZ_API_DECL void read(std::istream& is, SpectrumIdentificationList& sil,
                        const pwiz::util::IterationListenerRegistry* iterationListenerRegistry = 0);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SpectrumIdentificationResult& sir);
PWIZ_API_DECL void read(std::istream& is, SpectrumIdentificationResult& sir);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ProteinDetectionList& pdl);
PWIZ_API_DECL void read(std::istream& is, ProteinDetectionList& pdl);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const AnalysisData& pdl,
                         const pwiz::util::IterationListenerRegistry* iterationListenerRegistry = 0);
PWIZ_API_DECL void read(std::istream& is, AnalysisData& pdl,
                        const pwiz::util::IterationListenerRegistry* iterationListenerRegistry = 0);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const BibliographicReference& bibliographicReference);
PWIZ_API_DECL void read(std::istream& is, BibliographicReference& bibliographicReference);


enum PWIZ_API_DECL AnalysisDataFlag {IgnoreAnalysisData, ReadAnalysisData, IgnoreProteinDetectionList};


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const DataCollection& dataCollection,
                         const pwiz::util::IterationListenerRegistry* iterationListenerRegistry = 0);
PWIZ_API_DECL void read(std::istream& is, DataCollection& dataCollection,
                        const pwiz::util::IterationListenerRegistry* iterationListenerRegistry = 0,
                        AnalysisDataFlag analysisDataFlag = ReadAnalysisData);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const AnalysisSoftware& analysisSoftware);
PWIZ_API_DECL void read(std::istream& is, AnalysisSoftware& analysisSoftware);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const DBSequence& dbSequence);
PWIZ_API_DECL void read(std::istream& is, DBSequence& dbSequence);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Peptide& peptide);
PWIZ_API_DECL void read(std::istream& is, Peptide& peptide);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const PeptideEvidence& pe);
PWIZ_API_DECL void read(std::istream& is, PeptideEvidence& pe);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Modification& mod);
PWIZ_API_DECL void read(std::istream& is, Modification& mod);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SubstitutionModification& sm);
PWIZ_API_DECL void read(std::istream& is, SubstitutionModification& sm);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SequenceCollection& sc,
                         const pwiz::util::IterationListenerRegistry* iterationListenerRegistry = 0);
PWIZ_API_DECL void read(std::istream& is, SequenceCollection& sc,
                        const pwiz::util::IterationListenerRegistry* iterationListenerRegistry = 0);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const AnalysisSampleCollection& asc);
PWIZ_API_DECL void read(std::istream& is, AnalysisSampleCollection& asc);


enum PWIZ_API_DECL SequenceCollectionFlag {IgnoreSequenceCollection, ReadSequenceCollection};


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const IdentData& identdata,
                         const pwiz::util::IterationListenerRegistry* iterationListenerRegistry = 0);
PWIZ_API_DECL void read(std::istream& is, IdentData& identdata,
                        const pwiz::util::IterationListenerRegistry* iterationListenerRegistry = 0,
                        SequenceCollectionFlag sequenceCollectionFlag = ReadSequenceCollection,
                        AnalysisDataFlag analysisDataFlag = ReadAnalysisData);


} // namespace IO

} // namespace pwiz 
} // namespace identdata 

#endif //  _IDENTDATA_IO_HPP_
