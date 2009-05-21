///
/// DataFetcherContainer.cpp
///

#include "DataFetcherContainer.hpp"
#include "PeptideMatcher.hpp"
#include "pwiz/utility/proteome/Ion.hpp"

using namespace pwiz::eharmony;
using namespace pwiz::proteome;

DataFetcherContainer::DataFetcherContainer(const PeptideID_dataFetcher& pidf_a, const PeptideID_dataFetcher& pidf_b, const Feature_dataFetcher& fdf_a, const Feature_dataFetcher& fdf_b)
{
    _pidf_a = PeptideID_dataFetcher(pidf_a);
    _pidf_b = PeptideID_dataFetcher(pidf_b);
    _fdf_a = Feature_dataFetcher(fdf_a);
    _fdf_b = Feature_dataFetcher(fdf_b);

}

namespace{
    
    void getBestMatch(const SpectrumQuery& sq, const Feature_dataFetcher& fdf, FeatureSequenced& result)
    {
     
        Bin<FeatureSequenced> featureBin = fdf.getBin();
	
        pair<double,double> peptideCoords = make_pair(Ion::mz(sq.precursorNeutralMass, sq.assumedCharge), sq.retentionTimeSec);
        double bestScore = 1000000;       
        FeatureSequenced* feat = (FeatureSequenced*) NULL;

	
        vector<boost::shared_ptr<FeatureSequenced> > adjacentContenders;
        featureBin.getAdjacentBinContents(peptideCoords, adjacentContenders);
        vector<boost::shared_ptr<FeatureSequenced> >::iterator ac_it = adjacentContenders.begin();

        for(; ac_it != adjacentContenders.end(); ++ac_it)
            {
                if ( (*ac_it)->feature->charge == sq.assumedCharge )
                    {
                        double mzDiff = ((*ac_it)->feature->mzMonoisotopic - Ion::mz(sq.precursorNeutralMass,sq.assumedCharge));
                        double rtDiff = ((*ac_it)->feature->retentionTime - sq.retentionTimeSec);
                        double score = sqrt(mzDiff*mzDiff + rtDiff*rtDiff);
                        if ( score < bestScore )
                            {
                                feat = &(*(*ac_it));
				bestScore = score;
                            }

                    }

            }            

        if (feat) result = *feat;
        return;

    } 

    void executeAdjustRT(PeptideID_dataFetcher& pidf, Feature_dataFetcher& fdf)
    {
        int counter = 0;
        vector<SpectrumQuery> spectrumQueries = pidf.getAllContents();
        vector<SpectrumQuery>::iterator sq_it = spectrumQueries.begin();

        for(; sq_it != spectrumQueries.end(); ++sq_it)
            {
	  
	       if ( counter % 100 == 0) cout << "Spectrum query:"  << counter << endl;

		
                FeatureSequenced fs;
                getBestMatch(*sq_it, fdf, fs);
			     
                if (fs.feature->id.size() > 0) // f exists
                    {        
                        fdf.erase(fs);
                        fs.ms2 = sq_it->searchResult.searchHit.peptide;                      
                        fdf.update(fs);

                        pidf.erase(*sq_it);
                        sq_it->retentionTimeSec = fs.feature->retentionTime;
                        pidf.update(*sq_it);
                        
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
            _pidf_a.setRtAdjustedFlag(true);
            _fdf_a.setMS2LabeledFlag(true);
            
        }
    
    if (runB)
        {

            cout << "[eharmony] Matching MS2 peptides to their precursor features ... " << endl;
            executeAdjustRT(_pidf_b, _fdf_b);
            _pidf_b.setRtAdjustedFlag(true);
            _fdf_b.setMS2LabeledFlag(true);

        }

}

void DataFetcherContainer::warpRT(const WarpFunctionEnum& wfe) 
{
    // get anchors
 
     vector<pair<double,double> > anchors;
    PeptideMatcher pm(*this);
    PeptideMatchContainer matches = pm.getMatches();
    PeptideMatchContainer::iterator it = matches.begin();
    for(; it != matches.end(); ++it) anchors.push_back(make_pair(it->first.retentionTimeSec, it->second.retentionTimeSec));

  // get rt vals to be warped
  
 
    vector<double> rtUnadulterated;
    Bin<FeatureSequenced> bin = _fdf_b.getBin();
    vector<boost::shared_ptr<FeatureSequenced> > features = bin.getAllContents();
    
    vector<boost::shared_ptr<FeatureSequenced> >::iterator fs_it = features.begin();
    for(; fs_it != features.end(); ++fs_it)
      {
        rtUnadulterated.push_back((*fs_it)->feature->retentionTime);
        
      }


  // warp rt vals
 
    vector<double> rtAdulterated;
    switch (wfe)
      {
        case(Default) :
            {
              WarpFunction warpFunction(anchors);
              warpFunction(rtUnadulterated, rtAdulterated);

            } 

          break;

        case(Linear) :
            {
              LinearWarpFunction lfw(anchors);
              lfw(rtUnadulterated, rtAdulterated);

            }

        break;

        case(PiecewiseLinear) :
            {
               PiecewiseLinearWarpFunction plwf(anchors);
               plwf(rtUnadulterated, rtAdulterated);

            }

        break;

      }

  // put them back

    vector<double>::iterator rt_it = rtAdulterated.begin();
    fs_it = features.begin();
    for(; fs_it != features.end(); ++fs_it, ++rt_it)
      {
        (*fs_it)->feature->retentionTime = *rt_it;

      }

    Feature_dataFetcher _fdf_new(features);
    _fdf_b = _fdf_new;

}
