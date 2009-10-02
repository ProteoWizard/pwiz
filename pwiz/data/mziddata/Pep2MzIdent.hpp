//
// $Id$
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

    Pep2MzIdent(const MSMSPipelineAnalysis& mspa, MzIdentMLPtr result = MzIdentMLPtr(new MzIdentML()));    
    MzIdentMLPtr translate();

private:

    void translateRoot();
    void translateEnzyme(const SampleEnzyme& sampleEnzyme, MzIdentMLPtr result);
    void translateSearch(const std::vector<SearchSummaryPtr>& searchSummary, MzIdentMLPtr result);
    void translateQueries(const std::vector<SpectrumQuery>& queries, MzIdentMLPtr result);
    
    void translateMetadata();
    void translateSpectrumQueries();
    
    MSMSPipelineAnalysis _mspa;
    MzIdentMLPtr _result;
    bool _translated;

    pwiz::msdata::CVTranslator translator;
};

} // namespace mziddata
} // namespace pwiz


#endif
