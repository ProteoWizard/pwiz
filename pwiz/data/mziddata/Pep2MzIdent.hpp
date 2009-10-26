//
// $Id$
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

#ifndef _PEP2MZIDENT_HPP_
#define _PEP2MZIDENT_HPP_

#include "MzIdentML.hpp"
#include "pwiz/data/misc/MinimumPepXML.hpp"
#include "pwiz/data/msdata/CVTranslator.hpp"
#include "pwiz/utility/misc/Export.hpp"

#include <vector>


namespace pwiz{
namespace mziddata{

using namespace pwiz::data::pepxml;
typedef boost::shared_ptr<MzIdentML> MzIdentMLPtr;

class PWIZ_API_DECL Pep2MzIdent
{

public:

    Pep2MzIdent(const MSMSPipelineAnalysis& mspa,
                MzIdentMLPtr result = MzIdentMLPtr(new MzIdentML()));    
    MzIdentMLPtr translate();

    MzIdentMLPtr getMzIdentML() const
    {
        return mzid;
    }
    
private:

    void translateRoot();
    void translateEnzyme(const SampleEnzyme& sampleEnzyme, MzIdentMLPtr result);
    void translateSearch(const SearchSummaryPtr searchSummary, MzIdentMLPtr result);
    void translateQueries(const SpectrumQueryPtr query, MzIdentMLPtr result);
    
    void translateMetadata();
    void translateSpectrumQuery(SpectrumIdentificationListPtr result,
                                const SpectrumQueryPtr sq);

    void processParameter(ParameterPtr param, MzIdentMLPtr mzid);
    
    void addModifications(const std::vector<AminoAcidModification>& mods,
                          PeptidePtr peptide, MzIdentMLPtr result);

    void addPeptide(const SpectrumQueryPtr sq, MzIdentMLPtr& x);

    void addFinalElements();

    MSMSPipelineAnalysis _mspa;
    MzIdentMLPtr mzid;
    bool _translated;

    bool precursorMonoisotopic;
    bool fragmentMonoisotopic;

    // Handy state variables 
    struct Indices;
    boost::shared_ptr<Indices> indices;

    std::vector< std::pair<std::string, PeptidePtr> > seqPeptidePairs;
    
    pwiz::msdata::CVTranslator translator;

    const std::vector<AminoAcidModification>* aminoAcidModifications;
};

} // namespace mziddata
} // namespace pwiz


#endif
