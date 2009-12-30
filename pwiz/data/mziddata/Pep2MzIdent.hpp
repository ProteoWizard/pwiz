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
#include "pwiz/data/common/CVTranslator.hpp"
#include "pwiz/utility/misc/Export.hpp"

#include <vector>


namespace pwiz{
namespace mziddata{

using namespace pwiz::data::pepxml;
typedef boost::shared_ptr<MzIdentML> MzIdentMLPtr;

/// Translates data from a MinimumPepXML object into a MzIdentML
/// object tree when a translation is known. The MzIdentML object
/// initializes a cvList with the "MS" and "UO" elements.
///
/// The translate method is used to copy data. The clear method resets
/// the MzIdentML object, null's out the MSMSPipelineAnalysis.
class PWIZ_API_DECL Pep2MzIdent
{

public:
    /// Initialized the member variables. Also adds the "MS" and "UO"
    /// CV's to the cvList.
    Pep2MzIdent(const MSMSPipelineAnalysis& mspa,
                MzIdentMLPtr result = MzIdentMLPtr(new MzIdentML()));

    /// Resets the member variables.
    void clear();

    /// Translates all known tags in the pepXML object tree into the
    /// MzIdentML object tree. The resulting MzIdentMLPtr is returned.
    MzIdentMLPtr translate();

    /// Clears fields and then sets the _mspa field to the address of
    /// mspa.
    void setMspa(const MSMSPipelineAnalysis& mspa);
    
    /// Returns the MzIdentMLPtr object. If a translation has not been
    /// done, or if clear has been called, then an empty MzIdentML
    /// will be return in the MzIdentMLPtr object.
    MzIdentMLPtr getMzIdentML() const
    {
        return mzid;
    }
    
private:

    /// Translates pepXML data needed for the mzIdentML tag.
    void translateRoot();

    /// Copies the data in the enzyme tag into the mzIdentML tree. 
    void translateEnzyme(const SampleEnzyme& sampleEnzyme, MzIdentMLPtr result);

    /// Copies the data in an individual search tag into the mzIdentML tree.
    void translateSearch(const SearchSummaryPtr searchSummary, MzIdentMLPtr result);
    void translateQueries(const SpectrumQueryPtr query, MzIdentMLPtr result);

    /// Translates parameter tags into mzIdentML tree elements. 
    void earlyMetadata();

    /// Translates parameter tags into mzIdentML tree
    /// elements. Parameters that require a child tree to be populated
    /// before being processed go here. 
    void lateMetadata();

    /// Translates spectrum_query data into a spectrum identification
    /// list subtree.
    void translateSpectrumQuery(SpectrumIdentificationListPtr result,
                                const SpectrumQueryPtr sq);

    /// Checks a parameter for data that can be processed without
    /// additional data.
    void earlyParameters(ParameterPtr param, MzIdentMLPtr mzid);

    /// Checks a parameter for unprocessed data with a known mzIdentML
    /// destination.
    void lateParameters(ParameterPtr param, MzIdentMLPtr mzid);

    /// Creates a Peptide element for the search_hit element's peptide
    /// attribute.
    const std::string addPeptide(const SearchHitPtr sq, MzIdentMLPtr& x);

    /// Adds a modification element to peptides that match the
    /// aminoacid_modification.
    void addModifications(const std::vector<AminoAcidModification>& mods,
                          PeptidePtr peptide, MzIdentMLPtr result);

    // Adds any additional elements needed after all other data has
    // been processed.
    void addFinalElements();

    // Returns the CVID for a name or description. If CVTranslator
    // return CVID_Unknown, the a guess is made against common names
    // found in pepXML.
    CVID getCVID(const std::string& name);

    /// Translates the search_score with the given name into a CVParam
    /// object using getParamForSearchScore.
    CVParam translateSearchScore(const std::string& name,
                                 const std::vector<SearchScorePtr>& searchScore);

    /// Creates a CVParam from a SearchScorePtr object.
    CVParam getParamForSearchScore(const SearchScorePtr searchScore);

    // Returns the CVID for a name or description. If CVTranslator
    // return CVID_Unknown, the a guess is made against names
    // found in the search_score name attribute.
    CVID cvidFromSearchScore(const std::string& name);

    // old member variables
    const MSMSPipelineAnalysis* _mspa;
    MzIdentMLPtr mzid;
    bool _translated;

    // recursor flags.
    bool precursorMonoisotopic;
    bool fragmentMonoisotopic;

    // Handy state variables 
    struct Indices;
    boost::shared_ptr<Indices> indices;

    std::vector< std::pair<std::string, PeptidePtr> > seqPeptidePairs;
    
    pwiz::data::CVTranslator translator;

    const std::vector<AminoAcidModification>* aminoAcidModifications;
};

} // namespace mziddata
} // namespace pwiz


#endif
