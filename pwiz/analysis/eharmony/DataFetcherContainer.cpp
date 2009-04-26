///
/// DataFetcherContainer.cpp
///

#include "DataFetcherContainer.hpp"
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
        double bestScore = 10000000000;       
        FeatureSequenced* feat = (FeatureSequenced*) NULL;

        vector<FeatureSequenced> adjacentContenders;
        featureBin.getAdjacentBinContents(peptideCoords, adjacentContenders);
        vector<FeatureSequenced>::iterator ac_it = adjacentContenders.begin();

        for(; ac_it != adjacentContenders.end(); ++ac_it)
            {
                if ( ac_it->feature.charge == sq.assumedCharge )
                    {
                        double mzDiff = (ac_it->feature.mzMonoisotopic - Ion::mz(sq.precursorNeutralMass,sq.assumedCharge));
                        double rtDiff = (ac_it->feature.retentionTime - sq.retentionTimeSec);
                        double score = sqrt(mzDiff*mzDiff + rtDiff*rtDiff);
                        if ( score < bestScore )
                            {
                                feat = &(*ac_it);
				
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
        cout << "Number of spectrum queries: " << spectrumQueries.size() << endl;
        vector<SpectrumQuery>::iterator sq_it = spectrumQueries.begin();

        for(; sq_it != spectrumQueries.end(); ++sq_it)
            {
                if ( counter % 100 == 0) cout << "Spectrum query:"  << counter << endl;

                FeatureSequenced fs;
                getBestMatch(*sq_it, fdf, fs);

                if (fs.feature.id.size() > 0) // f exists
                    {        
                        fdf.erase(fs);
                        fs.ms2 = sq_it->searchResult.searchHit.peptide;                      
                        fdf.update(fs);

                        pidf.erase(*sq_it);
                        sq_it->retentionTimeSec = fs.feature.retentionTime;
                        pidf.update(*sq_it);
                        
                    }

                counter +=1;

            }

    }

} // anonymous namespace

void DataFetcherContainer::adjustRT()
{
    //    bool changed = false;
    bool flag = _pidf_a.getRtAdjustedFlag();

    if (!flag)
        {
            //            changed = true;
            cout << "Matching MS2 peptides to their precursor features ... " << endl;
            executeAdjustRT(_pidf_a, _fdf_a);
            _pidf_a.setRtAdjustedFlag(true);
            _fdf_a.setMS2LabeledFlag(true);
            
        }
    
    bool b_flag = _pidf_b.getRtAdjustedFlag();
    if (!b_flag)
        {
            //changed = true;
            cout << "Matching MS2 peptides to their precursor features ... " << endl;
            executeAdjustRT(_pidf_b, _fdf_b);
            _pidf_b.setRtAdjustedFlag(true);
            _fdf_b.setMS2LabeledFlag(true);

        }

    //    _rtAdjusted = changed;

}
