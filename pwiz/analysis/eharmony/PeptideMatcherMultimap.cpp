///
/// PeptideMatcher.cpp
///

#include "PeptideMatcher.hpp"
#include <map>
#include <iostream>

using namespace std;
using namespace pwiz;
using namespace pwiz::match;


multimap<string, pair<mzrtPair, SpectrumQuery> > makeSequenceMap(mzrtSpectrumQueryMap m)
{
    multimap<string, pair<mzrtPair, SpectrumQuery> > result;

    mzrtSpectrumQueryMap::iterator it = m.begin();
    for(; it != m.end(); ++it)
        {
            result.insert(make_pair(it->second.searchResult.searchHit.peptide, *it));

        }
        
    return result;
}

PeptideMatcher::PeptideMatcher(PeptideID_dataFetcher& a, PeptideID_dataFetcher& b, Feature_dataFetcher& f_a, Feature_dataFetcher& f_b) : _a(a), _b(b)
{
    /*
    ofstream MS2s_ab("MS2s_ab.txt"); // A ^ B
    ofstream MS2s_a("MS2s_a.txt"); // A
    ofstream MS2s_b("MS2s_b.txt"); // B
    ofstream MS2s_a_b("MS2s_a_b.txt"); // A ^ (~B)
    ofstream MS2s_b_a("MS2s_b_a.txt"); // B ^ (~A)

    MS2s_ab << "sequence\tmz_a\trt_a\tmz_b\trt_b\n";
    MS2s_a << "sequence\tmz\trt\n";
    MS2s_b << "sequence\tmz\trt\n";
    MS2s_a_b << "sequence\tmz\trt\n";
    MS2s_b_a << "sequence\tmz\trt\n";

    */

    ofstream wrong("wrong.txt");
    ostringstream oss_wrong;
    XMLWriter wrongWriter(oss_wrong);

    mzrtFeatureMap fm_a = f_a.getData();
    mzrtFeatureMap fm_b = f_b.getData();
    
    _a.adjustRT(fm_a);
    _b.adjustRT(fm_b);
    
    mzrtSpectrumQueryMap a_data = _a.getData();
    mzrtSpectrumQueryMap b_data = _b.getData();

    multimap<string, pair<mzrtPair, SpectrumQuery> > a_seqs = makeSequenceMap(a_data);
    multimap<string, pair<mzrtPair, SpectrumQuery> > b_seqs = makeSequenceMap(b_data);

    multimap<string, pair<mzrtPair, SpectrumQuery> >::iterator a_seq_it = a_seqs.begin();

    // fill in a map keyed by peptide sequences for each

    while(a_seq_it != a_seqs.end())
        {
            pair<multimap<string, pair<mzrtPair, SpectrumQuery> >::iterator, multimap<string, pair<mzrtPair, SpectrumQuery> >::iterator > potentialMatchees = a_seqs.equal_range(a_seq_it->first);
            pair<multimap<string, pair<mzrtPair, SpectrumQuery> >::iterator, multimap<string, pair<mzrtPair, SpectrumQuery> >::iterator > potentialMatches = b_seqs.equal_range(a_seq_it->first);
            
            // match the closest one (retention time).  Only one match allowed since these are going to be anchors.
            multimap<string, pair<mzrtPair, SpectrumQuery> >::iterator matchee_it = potentialMatchees.first;
            pair<pair<mzrtPair,SpectrumQuery>, pair<mzrtPair,SpectrumQuery> > best;
            double bestScore = 10000000000;
            for(; matchee_it != potentialMatchees.second; ++matchee_it)
                {
                    multimap<string, pair<mzrtPair, SpectrumQuery> >::iterator match_it = potentialMatches.first;
                    for(; match_it != potentialMatches.second; ++match_it)
                        {
                            double score = fabs(match_it->second.second.retentionTimeSec - matchee_it->second.second.retentionTimeSec);
                            if (score < bestScore)
                                {
                                    best = pair<pair<mzrtPair,SpectrumQuery>, pair<mzrtPair,SpectrumQuery> >(matchee_it->second, match_it->second);
                                    bestScore = score;
                                }

                        }
                
                }

            _matches.push_back(best);
            (best.first.second).write(wrongWriter);
            (best.second.second).write(wrongWriter);
            ++a_seq_it;

        }

    wrong << oss_wrong.str();
    /*

            if (a_seq_it->second.second.searchResult.searchHit.peptide == b_seq_it->second.second.searchResult.searchHit.peptide)
            {                
                _matches.push_back(pair<pair<mzrtPair,SpectrumQuery>, pair<mzrtPair,SpectrumQuery> >(a_seq_it->second, b_seq_it->second));
                MS2s_ab << a_seq_it->second.second.searchResult.searchHit.peptide << "\t" << a_seq_it->second.first.first << "\t" << a_seq_it->second.first.second << "\t" << b_seq_it->second.first.first << "\t" << b_seq_it->second.first.second << "\n";
                MS2s_a << a_seq_it->second.second.searchResult.searchHit.peptide << "\t" << a_seq_it->second.first.first << "\t" << a_seq_it->second.first.second << "\n";
                MS2s_b << b_seq_it->second.second.searchResult.searchHit.peptide << "\t" << b_seq_it->second.first.first << "\t" << b_seq_it->second.first.second << "\n";

                ++a_seq_it;
                ++b_seq_it;
                
                
            }
                
            else if (a_seq_it->first < b_seq_it->first) 
                {
                    MS2s_a << a_seq_it->second.second.searchResult.searchHit.peptide << "\t" << a_seq_it->second.first.first << "\t" << a_seq_it->second.first.second << "\n";
                    MS2s_a_b << a_seq_it->second.second.searchResult.searchHit.peptide << "\t" << a_seq_it->second.first.first << "\t" << a_seq_it->second.first.second << "\n";
                    ++a_seq_it;

                }

            else 
                {
                    MS2s_b << b_seq_it->second.second.searchResult.searchHit.peptide << "\t" << b_seq_it->second.first.first << "\t" << b_seq_it->second.first.second << "\n";
                    MS2s_b_a << b_seq_it->second.second.searchResult.searchHit.peptide << "\t" << b_seq_it->second.first.first << "\t" << b_seq_it->second.first.second << "\n";
                    ++b_seq_it;
                    
                }
                
        }
    */
  
}

void PeptideMatcher::calculateDeltaRTDistribution()
{
    if (_matches.size() == 0)
        {
            cerr << "[PeptideMatcher::calculateDeltaRTDistribution] No matching MS/MS IDS found." << endl;
            return;
        }

    double meanSum = 0;
    vector<pair<pair<mzrtPair,SpectrumQuery>, pair<mzrtPair,SpectrumQuery> > >::iterator match_it = _matches.begin();
    for(; match_it != _matches.end(); ++match_it)
        {           
            meanSum += fabs(match_it->first.first.second - match_it->second.first.second);
           
        } 
    
    _meanDeltaRT = meanSum / _matches.size();

    double stdevSum = 0;
    vector<pair<pair<mzrtPair,SpectrumQuery>, pair<mzrtPair,SpectrumQuery> > >::iterator stdev_it = _matches.begin();
    for(; stdev_it != _matches.end(); ++stdev_it)
        {            
            stdevSum += (fabs(stdev_it->first.first.second - stdev_it->second.first.second) - _meanDeltaRT)*(fabs(stdev_it->first.first.second - stdev_it->second.first.second) - _meanDeltaRT);
         
        }

    _stdevDeltaRT = sqrt(stdevSum / _matches.size());
    return;

}


PeptideMatchContainer PeptideMatcher::getMatches() const
{
    return _matches;

}

pair<double,double> PeptideMatcher::getDeltaRTParams() const
{
    return pair<double,double>(_meanDeltaRT, _stdevDeltaRT);

}


