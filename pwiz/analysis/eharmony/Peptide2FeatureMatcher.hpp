///
/// Peptide2FeatureMatcher.hpp
///

#ifndef _PEPTIDE2FEATUREMATCHER_HPP_
#define _PEPTIDE2FEATUREMATCHER_HPP_

//my stuff
#include "DataFetcherContainer.hpp"
#include "SearchNeighborhoodCalculator.hpp"
#include "PeptideMatcher.hpp"
#include "pwiz/data/misc/MinimumPepXML.hpp"

// other pwiz stuff
#include "pwiz/utility/minimxml/SAXParser.hpp"

namespace pwiz{
namespace eharmony{

class Peptide2FeatureMatcher
{

public:

    Peptide2FeatureMatcher(){}
    Peptide2FeatureMatcher(PeptideID_dataFetcher& a, Feature_dataFetcher& b, const SearchNeighborhoodCalculator& snc); 
    Peptide2FeatureMatcher(PeptideID_dataFetcher& a, Feature_dataFetcher& b, const NormalDistributionSearch& snc);

    // what do we need to know to match?
    // we need to know the search neighborhoods as well as the data.

    std::vector<Match> getMatches() const { return _matches;}
    std::vector<Match> getMismatches() const { return _mismatches;}

    std::vector<Match> getTruePositives() const { return _truePositives;}
    std::vector<Match> getFalsePositives() const { return _falsePositives;}
    std::vector<Match> getTrueNegatives() const { return _trueNegatives;}
    std::vector<Match> getFalseNegatives() const { return _falseNegatives;}

private:

    std::vector<Match> _matches;
    std::vector<Match> _mismatches; // un-apt type name Match, but want to store all the info in the Match struct so we can look at why there was a missed match

    // ROC info
    std::vector<Match> _truePositives;
    std::vector<Match> _falsePositives;
    std::vector<Match> _trueNegatives;
    std::vector<Match> _falseNegatives;

};

} // namespace eharmony
} // namespace pwiz

#endif // _PEPTIDE2FEATUREMATCHER_HPP_
