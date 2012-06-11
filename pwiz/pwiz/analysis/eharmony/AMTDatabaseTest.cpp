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

///
/// AMTDatabaseTest.cpp
///
#include "AMTDatabase.hpp"
#include "AMTContainer.hpp"
#include "pwiz/utility/proteome/Ion.hpp"
#include "pwiz/utility/misc/unit.hpp"

using namespace pwiz;
using namespace eharmony;
using namespace pwiz::utility;
using namespace pwiz::proteome;
using namespace pwiz::util;

typedef IslandizedDatabase::Gaussian Gaussian;

boost::shared_ptr<SpectrumQuery> makeSpectrumQuery(const double& mass, const int& charge, const double& rt, const string& sequence, const double& score = 0.95)
{
    boost::shared_ptr<SpectrumQuery> result(new SpectrumQuery());
    result->precursorNeutralMass = mass;
    result->assumedCharge = charge;
    result->retentionTimeSec = rt;
    result->searchResult.searchHit.peptide = sequence;
    result->searchResult.searchHit.analysisResult.peptideProphetResult.probability = score;

    return result;

}

boost::shared_ptr<AMTContainer> makeAMTContainer()
{
    boost::shared_ptr<AMTContainer> result(new AMTContainer());

    boost::shared_ptr<SpectrumQuery> rag_a = makeSpectrumQuery(3,3,3,"rag");    
    boost::shared_ptr<SpectrumQuery> rag_c = makeSpectrumQuery(3,3,3,"rag");
    boost::shared_ptr<SpectrumQuery> rag_b = makeSpectrumQuery(2,4,0.5,"rag");
    boost::shared_ptr<SpectrumQuery> mallick_a = makeSpectrumQuery(5,1,7,"mallick");
    boost::shared_ptr<SpectrumQuery> mallick_b = makeSpectrumQuery(15,3,7,"mallick");
    
    vector<boost::shared_ptr<SpectrumQuery> > sqs;
    sqs.clear();
    sqs.push_back(rag_a);    
    sqs.push_back(rag_c);
    sqs.push_back(rag_b);
    sqs.push_back(mallick_a);
    sqs.push_back(mallick_b);

    PidfPtr pidf(new PeptideID_dataFetcher(sqs));
    result->_pidf = pidf;
    result->rtAdjusted = true; // don't worry about adjusting RT here
    result->_sqs = sqs;

    return result;

}

void write(const Gaussian& gaussian)
{
    cout << gaussian.mu.first << endl;
    cout << gaussian.mu.second << endl;
    cout << gaussian.sigma.first << endl;
    cout << gaussian.sigma.second << endl;

    return;
}

void test()
{
    boost::shared_ptr<AMTContainer> amtc = makeAMTContainer();
    IslandizedDatabase id(amtc);

    // make the Island that we expect to find
    IslandizedDatabase::Island island;
    island.id = "rag";
    
    IslandizedDatabase::Gaussian gaussian(make_pair(make_pair(1.00727, 3), make_pair(.0009, 90)));
    IslandizedDatabase::Gaussian gaussian_2(make_pair(make_pair(2.00727, 3), make_pair(.0009, 90)));
    island.gaussians.push_back(gaussian);
    island.gaussians.push_back(gaussian_2);
    
    //    write(gaussian);
    //    write(gaussian_2);
    //    write(*(id.islands.begin())->gaussians.begin());

    // create IslandizedDatabase
    // test existence of the islands and correctness of their mu and sigmas

}

int main()
{
    test();
    return 0;
}
