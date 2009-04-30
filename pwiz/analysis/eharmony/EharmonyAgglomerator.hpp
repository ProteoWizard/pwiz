///
/// EharmonyAgglomerator.hpp
///

#ifndef _EHARMONY_AGGLOMERATOR_HPP_
#define _EHARMONY_AGGLOMERATOR_HPP_

#include "pwiz/analysis/eharmony/Matcher.hpp"

namespace pwiz{
namespace eharmony{

struct AMTContainer
{
    Feature_dataFetcher _fdf;
    PeptideID_dataFetcher _pidf;
    Config _config;

    void merge(const AMTContainer& that);

    AMTContainer(){}

};

} // namespace eharmony
} // namespace pwiz



#endif
