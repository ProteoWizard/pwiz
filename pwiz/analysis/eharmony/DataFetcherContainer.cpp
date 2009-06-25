///
/// DataFetcherContainer.cpp
///

#include "DataFetcherContainer.hpp"
#include "PeptideMatcher.hpp"
#include "pwiz/utility/proteome/Peptide.hpp"
#include "pwiz/utility/proteome/Ion.hpp"
#include <iostream>
#include <fstream>

using namespace pwiz::eharmony;
using namespace pwiz::proteome;

DataFetcherContainer::DataFetcherContainer(const PidfPtr pidf_a, const PidfPtr pidf_b, const FdfPtr fdf_a, const FdfPtr fdf_b)
{
    _pidf_a = pidf_a;
    _pidf_b = pidf_b;
    _fdf_a = fdf_a;
    _fdf_b = fdf_b;

}

namespace{
    
    void getBestMatch(const SpectrumQuery& sq, const FdfPtr fdf, FeatureSequenced& result)
    {     
        Bin<FeatureSequenced> featureBin = fdf->getBin();	
        pair<double,double> peptideCoords = make_pair(Ion::mz(sq.precursorNeutralMass, sq.assumedCharge), sq.retentionTimeSec);

        double bestScore = 1000000;               
        vector<boost::shared_ptr< FeatureSequenced> > adjacentContenders;
        featureBin.getAdjacentBinContents(peptideCoords, adjacentContenders);
        vector<boost::shared_ptr< FeatureSequenced> >::iterator ac_it = adjacentContenders.begin();

        for(; ac_it != adjacentContenders.end(); ++ac_it)
            {
                if ( (*ac_it)->feature->charge == sq.assumedCharge )
                    {
                        double mzDiff = ((*ac_it)->feature->mz - Ion::mz(sq.precursorNeutralMass,sq.assumedCharge))/.005;
                        double rtDiff = ((*ac_it)->feature->retentionTime - sq.retentionTimeSec)/60;
                        double score = sqrt(mzDiff*mzDiff + rtDiff*rtDiff);
                        if ( score < bestScore )
                            {
                                result = **ac_it;     
                                bestScore = score;
                            }

                    }

            }            

        return;

    } 

    void executeAdjustRT(PidfPtr pidf, FdfPtr fdf)
    {
        int counter = 0;

        vector<boost::shared_ptr<SpectrumQuery> > spectrumQueries = pidf->getAllContents();      
        vector<boost::shared_ptr<SpectrumQuery> >::iterator sq_it = spectrumQueries.begin();

        for(; sq_it != spectrumQueries.end(); ++sq_it)
            {
	      
                if ( counter % 100 == 0) cout << "Spectrum query:"  << counter << endl;
           		
                FeatureSequenced fs;
                getBestMatch(*(*sq_it), fdf, fs);
			     
                if (fs.feature->id.size() > 0) // f exists
                    {        
                        fdf->erase(fs);
                        fs.ms2 = (*sq_it)->searchResult.searchHit.peptide;                      
                        Peptide peptide(fs.ms2);
                        fs.calculatedMass = peptide.monoisotopicMass(0, false);
                        fs.ppProb = (*sq_it)->searchResult.searchHit.analysisResult.peptideProphetResult.probability;
                        fs.peptideCount += 1;
                        fdf->update(fs);
                        
                        pidf->erase(*(*sq_it));                        
                        (*sq_it)->retentionTimeSec = fs.feature->retentionTime;
                        pidf->update(*(*sq_it));
                                                
                        
                    }

                counter +=1;

            }
	
    }

} // anonymous namespace

void DataFetcherContainer::adjustRT(bool runA, bool runB)
{
    if (runA)
        {
            cout << "[eharmony] Matching MS2 peptides to their precursor features ... " << endl;
            executeAdjustRT(_pidf_a, _fdf_a);
            _pidf_a->setRtAdjustedFlag(true);
            _fdf_a->setMS2LabeledFlag(true);            
                       
        }
    
    if (runB)
        {
            cout << "[eharmony] Matching MS2 peptides to their precursor features ... " << endl;
            executeAdjustRT(_pidf_b, _fdf_b);
            _pidf_b->setRtAdjustedFlag(true);
            _fdf_b->setMS2LabeledFlag(true);

        }

}

void DataFetcherContainer::getAnchors(const int& freq, const double& tol)
{
    _anchors.clear(); // erase any previous anchors

    PeptideMatcher pm(_pidf_a, _pidf_b);
    PeptideMatchContainer matches = pm.getMatches();

    size_t index = 0; 
    PeptideMatchContainer::iterator it = matches.begin();

    for( ; it != matches.end() ; ++index, ++it ) 
        {
            const double& rt_a = it->first->retentionTimeSec;
            const double& rt_b = it->second->retentionTimeSec;

            if (index % freq == 0 && fabs(rt_a - rt_b) < tol) _anchors.push_back(make_pair(rt_a, rt_b));

        }         

}

void DataFetcherContainer::warpRT(const WarpFunctionEnum& wfe) 
{    
    
}
