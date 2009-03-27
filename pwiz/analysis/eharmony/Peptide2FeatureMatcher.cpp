
///
/// Peptide2FeatureMatcher.cpp
///

#include "Peptide2FeatureMatcher.hpp"
#include "PeptideMatcher.hpp"
#include "pwiz/utility/proteome/Ion.hpp"
#include "pwiz/data/misc/MinimumPepXML.hpp"
#include <math.h>

using namespace std;
using namespace pwiz::eharmony;
using namespace pwiz::minimxml;
using namespace pwiz::data;
using namespace pwiz::data::peakdata;
using namespace pwiz::proteome;


namespace{

void getBestMatch(const SpectrumQuery& sq, Bin<Feature>& featureBin, Feature& feature)
{
    pair<double,double> peptideCoords = make_pair(Ion::mz(sq.precursorNeutralMass,sq.assumedCharge), sq.retentionTimeSec);

    double bestScore = 100000000;
    Feature* best_it = (Feature*) NULL;

    vector<Feature> adjacentContenders;
    featureBin.getAdjacentBinContents(peptideCoords, adjacentContenders);
    vector<Feature>::iterator ac_it = adjacentContenders.begin();
           
    for(; ac_it != adjacentContenders.end(); ++ac_it)
        {
            if ( ac_it->charge == sq.assumedCharge )
                {
                    double mzDiff = (ac_it->mzMonoisotopic - Ion::mz(sq.precursorNeutralMass,sq.assumedCharge));
                    double rtDiff = (ac_it->retentionTime - sq.retentionTimeSec);
                    double score = sqrt(mzDiff*mzDiff + rtDiff*rtDiff);

                    if ( score < bestScore )
                        {
                            ac_it->ms1_5 = sq.searchResult.searchHit.peptide;
                            best_it = &(*ac_it);
                            
                        }                  

                }

        }

    if (best_it) feature = *best_it;
    return;
        
}

} // anonymous namespace

Peptide2FeatureMatcher::Peptide2FeatureMatcher(PeptideID_dataFetcher& a, Feature_dataFetcher& b, const SearchNeighborhoodCalculator& snc)
{
    Bin<Feature> bin = b.getBin();
    bin.rebin(snc._mzTol, snc._rtTol);

    vector<SpectrumQuery> spectrumQueries = a.getAllContents();
    vector<SpectrumQuery>::iterator sq_it = spectrumQueries.begin();
    int counter = 0;
    for(; sq_it != spectrumQueries.end(); ++ sq_it)
        {
            if (counter % 100 == 0) cout << "Spectrum: " << counter << endl;
            
            Feature f;
            getBestMatch(*sq_it, bin, f);          

            if (f.ms1_5.size() > 0 && snc.close(*sq_it, f)) 
                {
                    Match match(*sq_it,f);
                    match.score = snc.score(*sq_it,f);

                    _matches.push_back(match); 
                    if (f.ms1_5 != f.ms2 && f.ms2.size() > 0)
                        {
                            _falsePositives.push_back(match);
                            
                        }
                    
                    if (f.ms1_5 == f.ms2 && f.ms2.size() > 0)
                        {
                         
                            _truePositives.push_back(match);

                        }

                }

            else 
                {           
                    size_t i = 2;
                    bool done = false;

                    while ( !done && i < 5)
                        {            
                            Feature g;
                            bin.rebin(i*snc._mzTol, i*snc._rtTol);
                            getBestMatch(*sq_it, bin, g);
                            if (g.ms1_5.size() != 0) 
                                {
                                    done = true;
                                    Match match(*sq_it, g);
                                    match.score = snc.score(*sq_it,g);

                                    _mismatches.push_back(match);

                                    if (g.ms1_5 == g.ms2 && g.ms2.size() > 0) _falseNegatives.push_back(match);
                                    if (g.ms1_5 != g.ms2 && g.ms2.size() > 0) _trueNegatives.push_back(match);
                                    
                                }

                            ++i;

                        }
                 
                    // if ( !done ) cerr << "[Peptide2FeatureMatcher] No feature within 4*search radius." << endl;

                }            

            counter += 1;

        } 
    

}





