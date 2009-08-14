//
// $Id$
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

#include "pwiz/data/msdata/Serializer_mzML.hpp"
#include "boost/iostreams/positioning.hpp"
#include "pwiz/data/msdata/SpectrumIterator.hpp"
#include "pwiz/data/msdata/SpectrumInfo.hpp"
#include "pwiz/analysis/passive/MSDataCache.hpp"

#include "boost/tuple/tuple_comparison.hpp"

#include <vector>
#include <map>
#include <iostream>

namespace{

using namespace std;

using namespace pwiz::analysis;
using namespace pwiz::data;
using namespace pwiz::data::peakdata;
using namespace pwiz::msdata;
using boost::shared_ptr;


// helper function to fill in Peak id attribute (currently from scan number)
struct SetID
{
    SetID(int id) : _id(id) {}
    void operator()(Peak& peak)
    {
        peak.id = _id;
    }

    int _id;

};

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

// define keys for matching a PeakFamily to a Feature. FeatureKey captures all the attributes of a PeakFamily necessary to match it to an existing Feature, provided that the Feature and PeakFamily are contiguous in the retention time dimension.  FeatureKey is a tuple of the (mzMonoisotopic, charge, peaks.size()) attributes of a PeakFamily.

typedef boost::tuple<double,int,int> FeatureKey;

bool operator==(const FeatureKey& a, const FeatureKey& b) 
{
    return (fabs(a.get<0>() - b.get<0>())) < .001 && // accurate to 10e-3
                a.get<1>() == b.get<1>() &&
                a.get<2>() == b.get<2>();
}

} // anonymous namespace

using namespace std;

using namespace pwiz::analysis;
using namespace pwiz::data;
using namespace pwiz::data::peakdata;
using namespace pwiz::msdata;

class  FeatureDetectorSimple::Impl
{
public:
    
    Impl(shared_ptr<PeakFamilyDetector> pfd) : _pfd(pfd) {}

    typedef string FeatureID;

    void detect(const MSData& msd, FeatureField& result) const;
    FeaturePtr makeFeature(const PeakFamily& pf, FeatureID& id) const;
    void updatePeakel(const Peak& peak, Peakel& peakel) const;
    void updateFeature(const PeakFamily& pf, FeaturePtr feature) const;
    void getMetadata(FeatureField& result) const;
    
private:
    
    shared_ptr<PeakFamilyDetector> _pfd;
};


FeatureDetectorSimple::FeatureDetectorSimple(shared_ptr<PeakFamilyDetector> pfd) : _pimpl(new Impl(pfd)){}

void FeatureDetectorSimple::detect(const MSData& msd, FeatureField& result) const 
{
    _pimpl->detect(msd,result);
}

FeaturePtr FeatureDetectorSimple::Impl::makeFeature(const PeakFamily& pf, FeatureID& id) const
{
    FeaturePtr result(new Feature());
    result->id = id;
    result->charge = pf.charge;

    result->charge = pf.charge;
    for(vector<Peak>::const_iterator peak_it = pf.peaks.begin(); peak_it != pf.peaks.end(); ++peak_it) 
    {   
        // initialize peakels, one for each peak in the PeakFamily
        PeakelPtr peakel(new Peakel);
        updatePeakel(*peak_it, *peakel);
        result->peakels.push_back(peakel);
    }

    return result;
}

void FeatureDetectorSimple::Impl::updatePeakel(const Peak& peak, Peakel& peakel) const
{    
    peakel.peaks.push_back(peak);   
    return;
}

void FeatureDetectorSimple::Impl::updateFeature(const PeakFamily& pf, FeaturePtr feature) const
{   

    // iterate through peakels, adding new peaks and updating attributes
    vector<PeakelPtr>::iterator peakel_it = feature->peakels.begin();
    vector<Peak>::const_iterator peak_it = pf.peaks.begin();

    for(; peak_it != pf.peaks.end(); ++peakel_it, ++peak_it)
    {      
        // update appropriate peakel
        updatePeakel(*peak_it, **peakel_it);
    }

    return;
}

void FeatureDetectorSimple::Impl::getMetadata(FeatureField& detected) const
{
    FeatureField::iterator feat_it = detected.begin();
    for(; feat_it != detected.end(); ++feat_it)
        {
            (*feat_it)->calculateMetadata();
        }

    return;
}


// TODO Make this interpretable to a normal human

// FeatureDetectorSimple::Impl::detect uses two maps to find
// features. The first is rewritten with every scan,
// and is keyed by FeatureKeys mapping to FeatureIDs.  These 
// FeatureIDs correspond to features that
// will be eligible to be updated by PeakFamilys found in the next
// scan. The second map stores all detected features, keyed by these
// FeatureIDs, regardless of eligibility for update.  The first map is
// rolled over the scan set, updating the second as it goes.


void FeatureDetectorSimple::Impl::detect(const MSData& msd, FeatureField& detected) const 
{   
    // initialize buffer maps    
    map<FeatureKey, FeatureID> grandparentBuffer;
    map<FeatureKey, FeatureID> parentBuffer;

    map<FeatureID, FeaturePtr> featureMap;
    size_t featureCount = 0;

    MSDataCache cache;
    cache.open(msd);
   
    for(size_t spectrum_index = 0; spectrum_index < cache.size(); spectrum_index ++) 
    {
   
        map<FeatureKey, FeatureID> buffer;
        const SpectrumInfo info = cache.spectrumInfo(spectrum_index, true); //getBinaryData ? 
        // if info.PrecursorInfo ? 
        // call peak family detector on each scan
      
        vector<MZIntensityPair> mzIntensityPairs = info.data;       

        vector<PeakFamily> result;
                
        if (info.massAnalyzerType != MS_FT_ICR && info.massAnalyzerType != MS_orbitrap)
            {
               cerr << "Skipping non-FT scan number " << info.scanNumber << endl;
               continue;

            }
      
        _pfd->detect(mzIntensityPairs,result);

        // iterate thru peak families

        vector<PeakFamily>::iterator result_it = result.begin();
        double rt = info.retentionTime;
        int id = info.scanNumber;

        for(; result_it != result.end(); ++result_it)
            {
               
                // set ID attribute
                for_each(result_it->peaks.begin(), result_it->peaks.end(), SetID(id));

                // set RT attribute
                for_each(result_it->peaks.begin(), result_it->peaks.end(), SetRT(rt));
                
                // make keys for search
                FeatureKey featureKey(floor(result_it->mzMonoisotopic*100)/100, result_it->charge, result_it->peaks.size()); // no digits past 10e-3
                map<FeatureKey,FeatureID>::iterator grandparentLocation = grandparentBuffer.find(featureKey);
                map<FeatureKey,FeatureID>::iterator parentLocation = parentBuffer.find(featureKey);
                

                if ( parentLocation == parentBuffer.end() && grandparentLocation == grandparentBuffer.end())
                    {   
                       
                        // feature doesn't exist, make new feature
                        
                        FeatureID featureID = boost::lexical_cast<string>(++featureCount);
                        FeaturePtr feature(makeFeature(*result_it,featureID));
                       
                        featureMap.insert(pair<FeatureID, FeaturePtr>(featureID, feature));                               buffer.insert(pair<FeatureKey, FeatureID>(featureKey, featureID));
                                        

                    }

                else
                    {                      
                      // if parent location update from parent
                        FeatureID foundID;

                        if ( parentLocation != parentBuffer.end() ) foundID = parentLocation->second;
                        else foundID = grandparentLocation->second;
                        
                        // update existing feature
                        updateFeature(*result_it, featureMap[foundID]);
                        buffer.insert(pair<FeatureKey, FeatureID>(featureKey,foundID));

                    }
                   
            }

        // old buffer map := new buffer map
        //  bufferMap.swap(updatedBufferMap);

        grandparentBuffer.swap(parentBuffer);
        parentBuffer.swap(buffer);

    }
 
    // write output vector<Feature>
    map<FeatureID, FeaturePtr>::iterator the_it = featureMap.begin();
    for(; the_it != featureMap.end(); ++the_it)
        {
            if(the_it->second->peakels.front()->peaks.size()>1) // for now only write features lasting > 1 scan
                {
                    the_it->second->calculateMetadata();
                    detected.insert(the_it->second);
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

            

            if(the_it->second.peakels.begin()->peaks.size()>1) // for now only write stuff found in > 1 scan 
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
    

