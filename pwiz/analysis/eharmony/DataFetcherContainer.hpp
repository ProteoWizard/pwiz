///
/// DataFetcherContainer.hpp
///

#ifndef _DATAFETCHERCONTAINER_HPP_
#define _DATAFETCHERCONTAINER_HPP_

#include "PeptideID_dataFetcher.hpp"
#include "Feature_dataFetcher.hpp"
#include "WarpFunction.hpp"

namespace pwiz{
namespace eharmony{

struct DataFetcherContainer
{
    DataFetcherContainer(){}
    DataFetcherContainer(const PeptideID_dataFetcher& pidf_a, const PeptideID_dataFetcher& pidf_b, const Feature_dataFetcher& fdf_a, const Feature_dataFetcher& fdf_b);

    void adjustRT(); // Adjusts RT for identified peptides to be that of the nearest feature with the same charge state; assigns that feature the sequence of the identified peptide

    void warpRT(const WarpFunctionEnum& wfe);

    PeptideID_dataFetcher _pidf_a;
    PeptideID_dataFetcher _pidf_b;
    
    Feature_dataFetcher _fdf_a;
    Feature_dataFetcher _fdf_b;


};

} // namespace eharmony
} // namespace pwiz

#endif
