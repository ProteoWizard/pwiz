//
// FeatureDetectorSimple.cpp
//
//
// Original author: Kate Hoff <Katherine.Hoff@cshs.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Cnter, Los Angeles, California  90048
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

#include "FeatureDetectorSimple.hpp"

#include <vector>
#include <map>
#include <iostream>

#include "boost/tuple/tuple_comparison.hpp"

#include "pwiz/data/msdata/Serializer_mzML.hpp"
#include "boost/iostreams/positioning.hpp"

#include "pwiz/data/msdata/SpectrumIterator.hpp"
#include "pwiz/data/msdata/SpectrumInfo.hpp"
#include "pwiz/analysis/passive/MSDataCache.hpp"


using namespace std;

using namespace pwiz::analysis;
using namespace pwiz::data;
using namespace pwiz::data::peakdata;
using namespace pwiz::msdata;

class  FeatureDetectorSimple::Impl
{
public:
    
    Impl(PeakFamilyDetector& pfd) : _pfd(pfd) {}

    void detect(const MSData& msd, vector<Feature>& result) const;
    void makeFeature(const PeakFamily& pf, Feature& result, size_t& id) const;
    void updatePeakel(const Peak& peak, const Peakel& pre, Peakel& post) const;
    void updateFeature(const PeakFamily& pf, const Feature& pre, Feature& post) const;

    
private:
    
    PeakFamilyDetector& _pfd;

};


FeatureDetectorSimple::FeatureDetectorSimple(PeakFamilyDetector& pfd) : _pfd(pfd), _pimpl(new Impl(pfd)){}
void FeatureDetectorSimple::detect(const MSData& msd, std::vector<Feature>& result) const 
{
    _pimpl->detect(msd,result);
}

// helper function to fill in Peak retentionTime attribute
struct SetRT
{

    SetRT(double rt) : _rt(rt) {}
    void operator()(Peak& peak)
    {
        peak.retentionTime = _rt;
    }

private:

    double _rt;

};

void FeatureDetectorSimple::Impl::makeFeature(const PeakFamily& pf, Feature& result, size_t& id) const
{
    result.uniqueID = id;
    result.mzMonoisotopic = pf.mzMonoisotopic;
    result.retentionTime = pf.peaks.begin()->retentionTime; 
    result.rtVariance = 0; // assumes all peaks in a peak family have the same rt
    result.charge = pf.charge;
    for(vector<Peak>::const_iterator peak_it = pf.peaks.begin(); peak_it != pf.peaks.end(); ++peak_it) 
    {
        // sum peak intensities for totalIntensity attribute
        result.totalIntensity += peak_it->intensity;
        
        // initialize peakels, one for each peak in the PeakFamily
        Peakel peakel;
        peakel.mz = peak_it->mz;
        peakel.retentionTime = peak_it->retentionTime;
        peakel.maxIntensity = peak_it->intensity;
        peakel.mzVariance = 0;
        peakel.peaks.push_back(*peak_it);
        
        result.peakels.push_back(peakel);
    }

    return;
}

void FeatureDetectorSimple::Impl::updatePeakel(const Peak& peak, const Peakel& pre, Peakel& post) const
{
    post.peaks = pre.peaks;

    if (peak.intensity > pre.maxIntensity) post.maxIntensity = peak.intensity;
    else post.maxIntensity = pre.maxIntensity;
    post.totalIntensity = pre.totalIntensity + peak.intensity;
    
    post.peaks.push_back(peak);

    // calculate mz and retentionTime for Peakel (currently both mean)
    post.mz = (pre.mz * pre.peaks.size() + peak.mz) / post.peaks.size();
    post.retentionTime = (pre.retentionTime * pre.peaks.size() + peak.retentionTime) / post.peaks.size();

    // calculate mzVariance 
    post.mzVariance = 0;
    for(vector<Peak>::iterator it = post.peaks.begin(); it != post.peaks.end(); ++it)
        {
            post.mzVariance += (it->mz - post.mz)*(it->mz - post.mz);
        }

    post.mzVariance = post.mzVariance / post.peaks.size();

    return;
}

void FeatureDetectorSimple::Impl::updateFeature(const PeakFamily& pf, const Feature& pre, Feature& post) const
{

    
    // copy unchanged attributes
    post.uniqueID = pre.uniqueID;
    post.mzMonoisotopic = pre.mzMonoisotopic;
    post.charge = pre.charge;

    // change those that need change
   
    post.totalIntensity = pre.totalIntensity;

    vector<Peakel>::const_iterator peakel_it = pre.peakels.begin();
    vector<Peak>::const_iterator peak_it = pf.peaks.begin();

    for(; peak_it != pf.peaks.end(); ++peakel_it, ++peak_it)
    { 
        // update totalIntensity
        post.totalIntensity += peak_it->intensity;
        
        // update Peakels 
        Peakel postPeakel;
        updatePeakel(*peak_it, *peakel_it, postPeakel);
        post.peakels.push_back(postPeakel);

    }

    post.retentionTime = 0;
    post.rtVariance = 0;

    // calculate retentionTime: currently mean across peakels
    vector<Peakel>::const_iterator mean_it = post.peakels.begin();
    for(; mean_it != post.peakels.end(); ++mean_it)
        {
            post.retentionTime += mean_it -> retentionTime;
        
        }

    post.retentionTime = post.retentionTime / post.peakels.size();
    
    // calculate rtVariance
    vector<Peakel>::const_iterator var_it = post.peakels.begin();
    for(; var_it != post.peakels.end(); ++var_it)
        {
            post.rtVariance += (var_it->retentionTime - post.retentionTime)*(var_it->retentionTime - post.retentionTime);
        
        }

    post.rtVariance = post.rtVariance / post.peakels.size(); 

    return;

}

typedef boost::tuple<double,int,int> FeatureKey;
bool operator==(const FeatureKey& a, const FeatureKey& b) 
{
    return (fabs(a.get<0>() - b.get<0>())) < 10 &&
                a.get<1>() == b.get<1>() &&
                a.get<2>() == b.get<2>();
}

void FeatureDetectorSimple::Impl::detect(const MSData& msd, vector<Feature>& detected) const 
{   

    // initialize buffer map

    typedef size_t FeatureID;
   
    map<FeatureKey, FeatureID> bufferMap;
    map<FeatureID, Feature> featureMap;
    size_t featureCount = 0;

    MSDataCache cache;
    cache.open(msd);

    for(size_t spectrum_index = 0; spectrum_index < cache.size(); spectrum_index ++ ) 
    {
        
        map<FeatureKey, FeatureID> updatedBufferMap;
        const SpectrumInfo info = cache.spectrumInfo(spectrum_index, true); //getBinaryData ? 
       

        // call peak family detector on each scan
      
        vector<MZIntensityPair> mzIntensityPairs = info.data;       

        vector<PeakFamily> result;
        _pfd.detect(mzIntensityPairs,result);
  
        // iterate thru peak families

        vector<PeakFamily>::iterator result_it = result.begin();
        double rt = info.retentionTime;
        
        for(; result_it != result.end(); ++result_it)
            {
                // get RT attribute
                for_each(result_it->peaks.begin(), result_it->peaks.end(), SetRT(rt));
              
                // make keys for search
                FeatureKey featureKey(floor(result_it->mzMonoisotopic*100)/100, result_it->charge, result_it->peaks.size());
                map<FeatureKey,FeatureID>::iterator location = bufferMap.find(featureKey);
                

                if (location == bufferMap.end())
                    {   

                        // feature doesn't exist, make new feature
                        Feature feature;
                        FeatureID featureID = ++featureCount;
                        makeFeature(*result_it, feature, featureID);

                        
                        featureMap.insert(pair<FeatureID, Feature>(featureID, feature));
                        updatedBufferMap.insert(pair<FeatureKey, FeatureID>(featureKey, featureID));
                                        

                    }

                else
                    {
                        // update existing feature
                        FeatureID featureID = location->second;
                        Feature feature = featureMap[featureID];
                        Feature updatedFeature;
                        
                        updateFeature(*result_it, feature, updatedFeature);

                        featureMap.erase(featureID);
                        featureMap.insert(pair<FeatureID, Feature>(featureID, updatedFeature));
                        
                        updatedBufferMap.insert(pair<FeatureKey, FeatureID>(featureKey, featureID));

                    }
                   
            }

        // old buffer map := new buffer map
        bufferMap.swap(updatedBufferMap);
    }
 
        
    map<FeatureID, Feature>::iterator the_it = featureMap.begin();
    for(; the_it != featureMap.end(); ++the_it)
        {
            if(the_it->second.peakels.begin()->peaks.size()>1) // for now only write features lasting > 1 scan
                {
                    detected.push_back(the_it->second);
                }
        }

    return;
}


/*

    //
    // output files and plotting tools
    // commented out for now, to be implemented in FeatureExporter
    //

 
    map<FeatureID, Feature>::iterator the_it = featureMap.begin();
    for(; the_it != featureMap.end(); ++the_it)
        {
  
        
            // .features file
            ofstream ofs_all((msd.id+".features").c_str(), ios::app);

            // files for plotting
            ofstream ofs_peak("peak_plot.txt", ios::app);
            ofstream ofs_pkl("peakel_plot.txt", ios::app);
            ofstream ofs_feat("feature_plot.txt", ios::app);

            

            if(the_it->second.peakels.begin()->peaks.size()>=1) // for now only write stuff found in > 1 scan NOT
                {
                    detected.push_back(the_it->second);
                    
                    // write features to .features file
                    ofs_all << the_it->second;
                    
                    /////////////////////////////////////////////////////////////
                    // write files suitable for gnuplot
                    // factor this out into a separate program that will read in a .features file and export a gnuplot file?
                    /////////////////////////////////////////////////////////////

                    // write to files for plotting:

                    // write all the feature data out
                    ofs_feat << the_it->second.mzMonoisotopic << "\t" << the_it->second.retentionTime << "\n";
                    
     
                    // write all the peakel peak data out

                    
                    vector<Peakel>::iterator peakel_it = the_it->second.peakels.begin();
                    for(; peakel_it != the_it->second.peakels.end(); ++peakel_it)
                        {
                            
                            vector<Peak>::iterator peak_it = peakel_it->peaks.begin();
                            for(; peak_it!= peakel_it->peaks.end(); ++peak_it)
                                {
                                    ofs_peak << peak_it->mz << "\t" << peak_it->retentionTime << "\n";
                                    ofs_pkl << peak_it->mz << "\t" << peak_it->retentionTime << "\n";
                                 }

                            ofs_pkl << "\n\n";
                        }
                
                }

        }

    return;

        
}
*/

    

