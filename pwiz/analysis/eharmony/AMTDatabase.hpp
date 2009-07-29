///
/// AMTDatabase.hpp
///

#ifndef _AMTDATABASE_HPP_
#define _AMTDATABASE_HPP_

#include "AMTContainer.hpp"

namespace pwiz{
namespace eharmony{

using namespace pwiz::data::peakdata;

struct AMTDatabase
{
    AMTDatabase(const AMTContainer& amtContainer);

    std::vector<SpectrumQuery> query(DfcPtr dfc, const WarpFunctionEnum& wfe, NormalDistributionSearch& nds, MSMSPipelineAnalysis& mspa_in, string outputDir="./amtdb_query");
    
    PidfPtr _peptides;
    
};

} // namespace eharmony
} // namespace pwiz

#endif // _AMTDATABASE_HPP_
