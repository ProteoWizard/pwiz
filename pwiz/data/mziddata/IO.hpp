//
// IO.hpp
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

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ContactPtr cp);
PWIZ_API_DECL void read(std::istream& writer, ContactPtr cp);

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const pwiz::CV& CV);
PWIZ_API_DECL void read(std::istream& is, pwiz::CV& CV);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SearchModification& sm);
PWIZ_API_DECL void read(std::istream& is, SearchModification& sm);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Filter& filter);
PWIZ_API_DECL void read(std::istream& is, Filter& filter);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SpectrumIdentificationProtocolPtr sip);
PWIZ_API_DECL void read(std::istream& is, SpectrumIdentificationProtocolPtr sip);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ProteinDetectionProtocolPtr pdp);
PWIZ_API_DECL void read(std::istream& is, ProteinDetectionProtocolPtr pdp);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const AnalysisProtocolCollection& apc);
PWIZ_API_DECL void read(std::istream& is, AnalysisProtocolCollection& apc);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const BibliographicReference& bibliographicReference);
PWIZ_API_DECL void read(std::istream& is, BibliographicReference& bibliographicReference);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const DataCollection& dataCollection);
PWIZ_API_DECL void read(std::istream& is, DataCollection& dataCollection);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const AnalysisSoftware& analysisSoftware);
PWIZ_API_DECL void read(std::istream& is, AnalysisSoftware& analysisSoftware);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const DBSequencePtr dbSequence);
PWIZ_API_DECL void read(std::istream& is, DBSequencePtr dbSequence);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const PeptidePtr peptide);
PWIZ_API_DECL void read(std::istream& is, PeptidePtr peptide);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Modification& mod);
PWIZ_API_DECL void read(std::istream& is, Modification& mod);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SubstitutionModification& sm);
PWIZ_API_DECL void read(std::istream& is, SubstitutionModification& sm);


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const MzIdentML& mzidDataPtr);
PWIZ_API_DECL void read(std::istream& is, MzIdentML& mziddata);


} // namespace IO

} // namespace pwiz 
} // namespace mziddata 

#endif //  _MZIDDATA_IO_HPP_
