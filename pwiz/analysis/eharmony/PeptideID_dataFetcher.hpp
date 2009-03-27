///
/// PeptideID_dataFetcher.hpp
///

#ifndef _PEPTIDEID_DATAFETCHER_HPP_
#define _PEPTIDEID_DATAFETCHER_HPP_

#include "Feature_dataFetcher.hpp"
#include "pwiz/data/misc/MinimumPepXML.hpp"

#include <iostream>
#include <fstream>


using namespace pwiz;
using namespace pwiz::data::pepxml;

namespace pwiz{
namespace eharmony{

class PeptideID_dataFetcher
{

public:

    // default constructor
    PeptideID_dataFetcher() : _rtAdjusted(false) {}
    // istream constructor
    PeptideID_dataFetcher(std::istream& is);
    // from MSMSPipelineAnalysis object 
    PeptideID_dataFetcher(const MSMSPipelineAnalysis& mspa);
    // copy constructor
    PeptideID_dataFetcher(const PeptideID_dataFetcher& pidf) : _rtAdjusted(pidf.getRtAdjustedFlag()), _bin(pidf.getBin()) {}

    void update(const SpectrumQuery& sq);
    void erase(const SpectrumQuery& sq);
    std::vector<SpectrumQuery> getAllContents() const;

    std::vector<SpectrumQuery> getSpectrumQueries(double mz, double rt);
    Bin<SpectrumQuery> getBin() const { return _bin;}

    void setRtAdjustedFlag(const bool& flag) { _rtAdjusted = flag; }
    const bool& getRtAdjustedFlag() const { return _rtAdjusted; }

private:
    
    bool _rtAdjusted;
    Bin<SpectrumQuery> _bin;
    
};

} // namespace eharmony
} // namespace pwiz

#endif //_PEPTIDEID_DATAFETCHER_HPP_
