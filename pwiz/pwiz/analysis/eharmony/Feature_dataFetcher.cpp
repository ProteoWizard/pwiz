//
// $Id$
//
//
// Original author: Kate Hoff <katherine.hoff@proteowizard.org>
//
// Copyright 2009 Center for Applied Molecular Medicine
//   University of Southern California, Los Angeles, CA
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

#include "Feature_dataFetcher.hpp"
#include "pwiz/data/misc/PeakData.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::eharmony;
using namespace pwiz::data::peakdata;

namespace{

typedef pair<pair<double,double>, FeatureSequencedPtr> FeatBinPair;

void getCoordinates(const vector<FeaturePtr>& f, vector<FeatBinPair>& result)
{   
    vector<FeaturePtr>::const_iterator f_it = f.begin();
    for(; f_it != f.end(); ++f_it)
        {
            FeatureSequencedPtr fs(new FeatureSequenced());
            fs->feature = *f_it;

            FeatBinPair fb(make_pair(make_pair((*f_it)->mz, (*f_it)->retentionTime),fs));
            result.push_back(fb);

        }

}


void getCoordinates(const vector<FeatureSequencedPtr>& f, vector<FeatBinPair>& result)
{
  vector<FeatureSequencedPtr>::const_iterator f_it = f.begin();
  for(; f_it != f.end(); ++f_it)
    {
        result.push_back(make_pair(make_pair((*f_it)->feature->mz, (*f_it)->feature->retentionTime),*f_it));

    }

} 

} // anonymous namespace

Feature_dataFetcher::Feature_dataFetcher(istream& is)
{
    FeatureFile ff;
    ff.read(is);
    
    vector<FeatBinPair> features;
    getCoordinates(ff.features, features);

    _bin = Bin<FeatureSequenced>(features,.01, 1000); // be lenient for adjust rt? should be a config option

}
/*
Feature_dataFetcher::Feature_dataFetcher(vector<Feature>& f)
{
    vector<FeatBinPair> features;
    getCoordinates(f, features);
    _bin = Bin<FeatureSequenced>(features, .005, 1000); 

    }*/

Feature_dataFetcher::Feature_dataFetcher(const vector<FeaturePtr>& f)
{
    vector<FeatBinPair> features;
    getCoordinates(f, features);
    _bin = Bin<FeatureSequenced>(features, .005, 1000);

}

void Feature_dataFetcher::update(const FeatureSequenced& fs)
{
    double mz = fs.feature->mz;
    double rt = fs.feature->retentionTime;
    _bin.update(fs, make_pair(mz,rt));

}

void Feature_dataFetcher::erase(const FeatureSequenced& fs)
{
    double mz = fs.feature->mz;
    double rt = fs.feature->retentionTime;
    _bin.erase(fs, make_pair(mz,rt));

}

void Feature_dataFetcher::merge(const Feature_dataFetcher& that)
{
    Bin<FeatureSequenced> bin = that.getBin();
    vector<FeatureSequencedPtr> entries = bin.getAllContents();
    vector<FeatureSequencedPtr>::iterator it = entries.begin();
    for(; it != entries.end(); ++it) update(**it);
    
}

vector<FeatureSequencedPtr> Feature_dataFetcher::getFeatures(double mz, double rt) 
{
    pair<double,double> coords = make_pair(mz,rt);
    vector<FeatureSequencedPtr> features;
    _bin.getBinContents(coords,features);
    
    return features;

}

vector<FeatureSequencedPtr> Feature_dataFetcher::getAllContents() const
{
    return _bin.getAllContents();

}

bool Feature_dataFetcher::operator==(const Feature_dataFetcher& that)
{
    return getAllContents() == that.getAllContents(); // TODO: add mz tol and rt tol

}

bool Feature_dataFetcher::operator!=(const Feature_dataFetcher& that) 
{
    return !(*this == that);

}
