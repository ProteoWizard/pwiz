//
// $Id$
//
//
// Original author: Chris Paulse <cpaulse <a.t> systemsbiology.org>
//
// Copyright 2009 Institute for Systems Biology, Seattle, WA
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//

#define PWIZ_SOURCE


#include "MS2Deisotoper.hpp"
#include "pwiz/analysis/peakdetect/PeakFamilyDetectorFT.hpp"
#include "pwiz/utility/misc/Std.hpp"


namespace pwiz {
namespace analysis {

using namespace std;
using namespace msdata;
using namespace util;
using namespace pwiz::data;
using namespace pwiz::data::peakdata;

using boost::shared_ptr;

namespace {

struct PWIZ_API_DECL FilterSpectrum
{
    FilterSpectrum(const MS2Deisotoper::Config& params_, 
                   const pwiz::msdata::SpectrumPtr spectrum_);
    ~FilterSpectrum()
    {
    }

    void DeIsotopeHiRes() { /* TODO: call peak family detector */ }
    void DeIsotopeLowRes();
    void DeIsotopePoisson();
    double getKLscore( const isoChain, bool );

    // data
    const MS2Deisotoper::Config params;

    const pwiz::msdata::SpectrumPtr spectrum;
    BinaryData<double>&            massList_;
    BinaryData<double>&            intensities_;
    double                          precursorMZ;
    int                             precursorCharge;
};

vector<pair<double, int> > GetPrecursors(const SpectrumPtr spectrum)
{
    vector<pair<double, int> > precursorList;

    for(Precursor& precursor : spectrum->precursors)
    {
        for(SelectedIon& selectedIon : precursor.selectedIons)
        {
            double mz = 0;
            int charge = 0;
            if (selectedIon.hasCVParam(MS_m_z))
            {
                mz = selectedIon.cvParam(MS_m_z).valueAs<double>();
            }
            else if (selectedIon.hasCVParam(MS_selected_ion_m_z))
            {
                mz = selectedIon.cvParam(MS_selected_ion_m_z).valueAs<double>();
            }

            if (selectedIon.hasCVParam(MS_charge_state))
            {
                charge = selectedIon.cvParam(MS_charge_state).valueAs<int>();
            }

            precursorList.push_back(pair<double, int>(mz, charge));
        }
    }

    return precursorList;
}

FilterSpectrum::FilterSpectrum(const MS2Deisotoper::Config& params_, 
                               const pwiz::msdata::SpectrumPtr spectrum_) 
                    : params(params_), 
                      spectrum(spectrum_), 
                      massList_(spectrum->getMZArray()->data), 
                      intensities_(spectrum->getIntensityArray()->data),
                      precursorMZ(0),
                      precursorCharge(0)
{
    if (massList_.size() < 1)
    {
        //TODO: log encounter with empty spectrum?
        return;
    }

    if ( params.poisson )
        DeIsotopePoisson( );
    else
        DeIsotopeLowRes();
}

static double PropogateNulls(double& arg1, double& arg2)
{
    if (arg1 < 0) // indicates null
        return arg1;
    else
        return arg2;
}

struct indexValuePair
{
    double val;
    size_t index;
};

// Override "less than" with "greater than" for stl::sort to output in descending order
bool operator < (const indexValuePair& lhs, const indexValuePair& rhs)
{
    return lhs.val > rhs.val;
}

void FilterSpectrum::DeIsotopeLowRes()
{
    vector<indexValuePair> indexValuePairs;

    size_t ix = 0;
    for(double& intens : intensities_)
    {
        indexValuePair p;
        p.index = ix++;
        p.val = intens;
        indexValuePairs.push_back(p);
    }

    sort(indexValuePairs.begin(), indexValuePairs.end());
    
    int curIxValPair = 0;
    for(indexValuePair& ix : indexValuePairs)
    {
        ++curIxValPair;

        if (intensities_[ix.index] >= 0)
        {
            if (params.hires)
            {
                size_t i = ix.index + 1;
                while (i < massList_.size() && massList_[i] - massList_[ix.index] < (2.0 + params.matchingTolerance.value))
                {
                    if (intensities_[i] < intensities_[ix.index])
                    {
                        intensities_[i] = -1.;
                    }
                    i++;
                }
            }
            else
            {
                for (size_t i = curIxValPair; i < indexValuePairs.size(); i++)
                {
                    double massDiff = massList_[indexValuePairs[i].index] - massList_[ix.index];
                    if (-massDiff < params.matchingTolerance.value && massDiff < (2.0 + params.matchingTolerance.value))
                    {
                        intensities_[indexValuePairs[i].index] = -1.;
                    }
                }
            }
        }
    }

    transform(intensities_.begin(), intensities_.end(), massList_.begin(), massList_.begin(), PropogateNulls);
    intensities_.erase(remove_if(intensities_.begin(), intensities_.end(), bind2nd(less<double>(), 0)), intensities_.end());
    massList_.erase(remove_if(massList_.begin(), massList_.end(), bind2nd(less<double>(), 0)), massList_.end());

    spectrum->defaultArrayLength = massList_.size();
}

// Poisson-based algorithm for deisotoping. Based on:
// Breen et al., Electrophoresis 2000, 21, 2243-2251.
// Bellew et al., Bioinformatics 2006, 22(15), 1902-1909.
void FilterSpectrum::DeIsotopePoisson( )
{

    // xPeak stores m/z; yPeak stores intensity
    int nPeaks = massList_.size();
    if ( nPeaks < 2 ) return;

    //std::cout << "Performing Poisson deisotoping!" << endl;

    double massNeutron = 1.00335; // massC13 - massC12
    double mzTolppm = 100.0;
    double mzTolparts = mzTolppm / 1000000.0;
    int maxCharge = params.maxCharge, minCharge = params.minCharge;
    int maxIsotopePeaks = 5;

    vector <isoChain> chains;

    // build chains of potential isotopes                
    for (int j=0; j<nPeaks; j++)
    {

        for (int k = j+1; k<nPeaks; k++)
        {

            double mzDiff = massList_[k] - massList_[j]; // guaranteed to give positive value
            double mzTol = mzTolparts * massList_[k];
            if (mzDiff > massNeutron + mzTol) break; // subsequent sets of peaks will have spacing that is too large

            double recip = massNeutron / mzDiff;
            int possibleCharge = int(recip + 0.50); 
            if ( possibleCharge > maxCharge || possibleCharge < minCharge ) continue; // Not going to generate chain extension

            if ( abs( massNeutron / double(possibleCharge) - mzDiff) < mzTol )
            {
            
                // We have a hit
                // Start by checking if this can be connected to previous chains
                for (int w=0, wend=chains.size(); w < wend; ++w)
                {

                    if ( possibleCharge != chains[w].charge ) continue;
                    int nPeaksInChain = chains[w].indexList.size();
                    if ( nPeaksInChain >= maxIsotopePeaks ) continue;
                                
                    int finalIndex = chains[w].indexList[chains[w].indexList.size()-1];
                    if ( j == finalIndex ) // connect it and save previous chain
                    {
                        chains.push_back(chains[w]); // push back copy of previous chain with current size
                        chains[w].indexList.push_back(k); // now extend the size of the chain
                    }

                }

                // Also create a new chain of length 2
                isoChain newChain;
                newChain.charge = possibleCharge;
                newChain.indexList.push_back(j);
                newChain.indexList.push_back(k); 
                chains.push_back(newChain);

            } // end if peak is within tolerance
            
        } // end for loop k

    } // end for loop j

    vector <double> KLcutoffs(5);
    vector <double> SSEcutoffs(5);
    KLcutoffs[0] = 0.025; KLcutoffs[1] = 0.05; KLcutoffs[2] = 0.1; KLcutoffs[3] = 0.2; KLcutoffs[4] = 0.3;
    SSEcutoffs[0] = 0.00005; SSEcutoffs[1] = 0.0001; SSEcutoffs[2] = 0.0003; SSEcutoffs[3] = 0.0006; SSEcutoffs[4] = 0.001;

    // score chains, store indices that we want to remove
    vector <bool> removePeak(nPeaks,false);
    vector <isoChain>::iterator it = chains.begin();
    while ( it != chains.end() )
    {
        double KLscore = getKLscore( *it, false );
        int length = it->indexList.size();

        // calculate average monoisotopic mass based on m/z positions in chain
        double average=0.0;
        for (int k=0; k < length; k++)
            average += massList_[it->indexList[k]] - double(k)*massNeutron/double(it->charge);
        average /= double(length);

        // calculate summed square error of m/z positions in chain relative to the average monoisotopic m/z
        double sse=0.0;
        for (int k=0; k < length; k++)
            sse += pow( average - (massList_[it->indexList[k]] - double(k)*massNeutron/double(it->charge)),2);

        int reducedLength = length <= 6 ? length-2 : 4;
        double thisKLcutoff = KLcutoffs[reducedLength];
        double thisSSEcutoff = SSEcutoffs[reducedLength];
        double monoIntensity = intensities_[it->indexList[0]];

        if ( KLscore < thisKLcutoff && sse < thisSSEcutoff && monoIntensity > 5.0 ) 
        {
            for (int i=1, iend=it->indexList.size(); i < iend; ++i) // do not remove first peak!
                removePeak[it->indexList[i]] = true;
        }
        
        ++it;
    }

    // remove peaks
    for (int i=nPeaks-1; i >= 0; --i)
    {
        if ( removePeak[i] )
        {
            massList_.erase( massList_.begin() + i ); // remove this peak
            intensities_.erase( intensities_.begin() + i ); // remove this peak
        }
    }

    spectrum->defaultArrayLength = massList_.size();

}
// end function DeIsotopePoisson


double FilterSpectrum::getKLscore( const isoChain chain, bool printDist )
{

    if ( massList_.size() != intensities_.size() )
         throw runtime_error("[MS2Deisotope FilterSpectrum::getKLscore] m/z and intensity vectors must be equal in size.");

    // First, convert from molecular weight of ion to Mstar, which is linear mapping 
    double lambda = 1.0 / 1800.0; // parameter for poisson model
    double Mstar = lambda * massList_[chain.indexList[0]] * double(chain.charge); // from msInspect paper
    double Mexp = exp( -Mstar );
                    
    double poissonSum = 0.0; // sum up all the poisson values to normalize
    double observedIntensitySum = 0.0; // initialize this sum
    double KLscore = 0.0;

    vector <double> poissonVals(chain.indexList.size(),0.0);

    // calculate poisson distribution and sum up the intensities for normalization
    for (int k=0,kend=chain.indexList.size(); k < kend ; ++k)
    {
        // probability of seeing isotope with k additional ions relative to monoisotope
        double poisson = Mexp * pow(Mstar,k);
        for (int w=k;w>1;w--) poisson /= double(w);
        poissonVals[k] = poisson; // store value

        // sums for normalization
        poissonSum += poisson;
        observedIntensitySum += intensities_[chain.indexList[k]];
    }
        
    // calculate the K-L score
    for (int k=0,kend=chain.indexList.size(); k < kend ; ++k)
    {
        poissonVals[k] /= poissonSum; // normalize these values to use in the K-L score
        double normObservedIntensity = intensities_[chain.indexList[k]] / observedIntensitySum;
        KLscore += normObservedIntensity * log10( normObservedIntensity / poissonVals[k]  );
        if ( printDist )
            std::cout << "mz: " << massList_[chain.indexList[k]] << " int: " << intensities_[chain.indexList[k]] << " model/obs. intensity: " << poissonVals[k] << "/" << normObservedIntensity << std::endl;
    }
    if (printDist) std::cout << std::endl;
        

    return KLscore;

}

} // namespace


void MS2Deisotoper::describe(ProcessingMethod& method) const
{
    //method.set(MS_ECD_ETD_Precursor_Mass_Filter);
    //method.userParams.push_back(UserParam("remove precursor", lexical_cast<string>(params.removePrecursor)));
    //method.userParams.push_back(UserParam("filter charge reduced precursors", lexical_cast<string>(params.removeReducedChargePrecursors)));
    //method.userParams.push_back(UserParam("remove neutral loss masses", lexical_cast<string>(params.neutralLossSpecies.size() > 0)));
    //method.userParams.push_back(UserParam("selective removal of precursors (most intense peak in tolerance window)", lexical_cast<string>(params.removeMostIntensePeakInWindow)));
    //method.userParams.push_back(UserParam("blanket removal of neutral loss masses", lexical_cast<string>(params.useBlanketFiltering)));
    //method.userParams.push_back(UserParam("matching tolerance", lexical_cast<string>(params.matchingTolerance)));
}

void MS2Deisotoper::operator () (const SpectrumPtr& spectrum) const
{
    if (spectrum->cvParam(MS_ms_level).valueAs<int>() > 1 &&
        spectrum->cvParam(MS_MSn_spectrum).empty() == false &&
        spectrum->precursors.empty() == false &&
        spectrum->precursors[0].empty() == false &&
        spectrum->precursors[0].selectedIons.empty() == false &&
        spectrum->precursors[0].selectedIons[0].empty() == false)
    {
        FilterSpectrum(params, spectrum);
    }
}

} // namespace analysis 
} // namespace pwiz
