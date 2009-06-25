///
/// DataFetcherContainer.hpp
///

#ifndef _DATAFETCHERCONTAINER_HPP_
#define _DATAFETCHERCONTAINER_HPP_

#include "PeptideID_dataFetcher.hpp"
#include "Feature_dataFetcher.hpp"
#include "WarpFunction.hpp"
#include "boost/shared_ptr.hpp"

namespace pwiz{
namespace eharmony{

typedef boost::shared_ptr<PeptideID_dataFetcher> PidfPtr;
typedef boost::shared_ptr<Feature_dataFetcher> FdfPtr;

struct DataFetcherContainer
{
    DataFetcherContainer(const PidfPtr pidf_a = PidfPtr(new PeptideID_dataFetcher()), const PidfPtr pidf_b = PidfPtr(new PeptideID_dataFetcher()), const FdfPtr fdf_a = FdfPtr(new Feature_dataFetcher()), const FdfPtr fdf_b = FdfPtr(new Feature_dataFetcher()));

    void adjustRT(bool runA=true, bool runB=true); 
    void warpRT(const WarpFunctionEnum& wfe);

    PidfPtr _pidf_a;
    PidfPtr _pidf_b;
    
    FdfPtr _fdf_a;
    FdfPtr _fdf_b;
  
private:

    vector<pair<double, double> > _anchors;

    void getAnchors(const int& freq = 30, const double& tol = 100);

    pair<vector<double>, vector<double> > getPeptideRetentionTimes();
    pair<vector<double>, vector<double> > getFeatureRetentionTimes();

    void putPeptideRetentionTimes();
    void putFeatureRetentionTimes();

    // no copying
    DataFetcherContainer(DataFetcherContainer&);
    DataFetcherContainer operator=(DataFetcherContainer&);

};

} // namespace eharmony
} // namespace pwiz

#endif
