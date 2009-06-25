///
/// PeptideID_dataFetcher.hpp
///

#ifndef _PEPTIDEID_DATAFETCHER_HPP_
#define _PEPTIDEID_DATAFETCHER_HPP_

#include "Feature_dataFetcher.hpp"
#include "pwiz/data/misc/MinimumPepXML.hpp"
#include "boost/shared_ptr.hpp"

#include <iostream>
#include <fstream>


using namespace pwiz;
using namespace pwiz::data::pepxml;

namespace pwiz{
namespace eharmony{

class PeptideID_dataFetcher
{

public:


    PeptideID_dataFetcher() : _rtAdjusted(false) {}
    PeptideID_dataFetcher(std::istream& is);
    //    PeptideID_dataFetcher(const std::vector<SpectrumQuery>& sqs);
    PeptideID_dataFetcher(const std::vector<boost::shared_ptr<SpectrumQuery> >& sqs);
    PeptideID_dataFetcher(const MSMSPipelineAnalysis& mspa);

    void update(const SpectrumQuery& sq);
    void erase(const SpectrumQuery& sq);
    void merge(const PeptideID_dataFetcher& that);
    size_t size(){ return this->getAllContents().size();}

    // accessors
    std::vector<boost::shared_ptr<SpectrumQuery> > getAllContents() const;
    std::vector<boost::shared_ptr< SpectrumQuery> > getSpectrumQueries(double mz, double rt) ;
    Bin<SpectrumQuery> getBin() const { return _bin;}
    void setRtAdjustedFlag(const bool& flag) { _rtAdjusted = flag; }
    const bool& getRtAdjustedFlag() const { return _rtAdjusted; }

    bool operator==(const PeptideID_dataFetcher& that);
    bool operator!=(const PeptideID_dataFetcher& that);

    std::string id;

private:
    
    bool _rtAdjusted;
    Bin<SpectrumQuery> _bin;
    
    // no copying
    PeptideID_dataFetcher(PeptideID_dataFetcher&);
    PeptideID_dataFetcher operator=(PeptideID_dataFetcher&);

};

} // namespace eharmony
} // namespace pwiz

#endif //_PEPTIDEID_DATAFETCHER_HPP_
