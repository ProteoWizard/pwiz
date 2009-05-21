///
/// Match_dataFetcher.hpp
///

#ifndef _MATCH_DATAFETCHER_HPP_
#define _MATCH_DATAFETCHER_HPP_

#include "Bin.hpp"
#include "pwiz/data/misc/PeakData.hpp"
#include "pwiz/data/misc/MinimumPepXML.hpp"
#include "boost/shared_ptr.hpp"

#include<iostream>
#include<fstream>

using namespace pwiz::data::peakdata;
using namespace pwiz::data::pepxml;

namespace pwiz{
namespace eharmony{

class Match_dataFetcher
{

public:

    Match_dataFetcher(){}
    Match_dataFetcher(std::istream& is);
    Match_dataFetcher(const MatchData& md);
    Match_dataFetcher(const Match_dataFetcher& mdf) : _bin(mdf.getBin()) {}
    
    void update(const Match& m);
    void erase(const Match& m);
    void merge(const Match_dataFetcher& that);

    std::vector<Match> getAllContents() const; 
    std::vector<Match> getMatches(double mz, double rt);
    Bin<Match> getBin() const { return _bin; } 

    bool operator==(const Match_dataFetcher& that);
    bool operator!=(const Match_dataFetcher& that);
    
private:

    Bin<Match> _bin;
    
};

} // namespace eharmony
} // namespace pwiz


#endif //_MATCH_DATAFETCHER_HPP_
