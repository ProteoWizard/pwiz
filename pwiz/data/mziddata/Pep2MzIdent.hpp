//
// Pep2MzIdent.hpp
//

#ifndef _PEP2MZIDENT_HPP_
#define _PEP2MZIDENT_HPP_

#include "MzIdentML.hpp"
#include "pwiz/data/misc/MinimumPepXML.hpp"
#include "pwiz/utility/misc/Export.hpp"

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
    
    void translateMetadata();
    void translateSpectrumQueries();
    
    MSMSPipelineAnalysis _mspa;
    MzIdentMLPtr _result;
    bool _translated;

};

} // namespace mziddata
} // namespace pwiz


#endif
