///
/// AMTContainer.hpp
///

#ifndef _AMT_CONTAINER_HPP_
#define _AMT_CONTAINER_HPP_

#include "Matcher.hpp"
#include "Match_dataFetcher.hpp"
#include "Peptide2FeatureMatcher.hpp"
#include "PeptideMatcher.hpp"
#include "pwiz/utility/minimxml/XMLWriter.hpp"

namespace pwiz{
namespace eharmony{

struct AMTContainer
{
    Peptide2FeatureMatcher _p2fm;
    PeptideMatcher _pm;

    Feature_dataFetcher _fdf; // merged features from both runs
    PeptideID_dataFetcher _pidf; // merged ms2s from both runs
    Match_dataFetcher _mdf; // merged ms1.5s from both runs

    vector<SpectrumQuery> _sqs; // HACK TODO: FIX

    Config _config;

    void merge(const AMTContainer& that);

    void write(XMLWriter& writer) const;
    void read(istream& is);

    AMTContainer(){}

};


} // namespace eharmony
} // namespace pwiz



#endif // AMTContainer.hpp
