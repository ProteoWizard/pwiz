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
/// WarpFunctionTest.cpp
///

#include "WarpFunction.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "boost/lexical_cast.hpp"
#include <cstring>

using namespace std;
using namespace pwiz;
using namespace pwiz::eharmony;
using namespace pwiz::util;

ostream* os_ = 0;
const double epsilon = 2 * numeric_limits<double>::epsilon();

vector<pair<double,double> > initializeColinearAnchors()
{
    pair<double,double> a(0,0);
    pair<double,double> b(1,1);
    pair<double,double> c(2,2);

    vector<pair<double,double> > anchors;
    anchors.push_back(a);
    anchors.push_back(b);
    anchors.push_back(c);

    return anchors;

}

vector<pair<double,double> > initializeNonColinearAnchors()
{
    pair<double,double> a(0,0);
    pair<double,double> b(1,1);
    pair<double,double> c(2,4);

    vector<pair<double,double> > anchors;
    anchors.push_back(a);
    anchors.push_back(b);
    anchors.push_back(c);

    return anchors;

}

vector<double> initializeRtVals()
{
    vector<double> rtVals;
    rtVals.push_back(-1);
    rtVals.push_back(3);
    rtVals.push_back(1.5);
    rtVals.push_back(0.5);
    rtVals.push_back(7);

    return rtVals;

}

vector<pair<double,double> > initializeSplineAnchors()
{
  vector<pair<double,double> > splineAnchors;
  splineAnchors.push_back(make_pair(0,0));
  splineAnchors.push_back(make_pair(1,1));
  splineAnchors.push_back(make_pair(2,1.5));
  splineAnchors.push_back(make_pair(3,4));

  return splineAnchors;

}

void testColinearLinearWarp()
{
    if (os_) *os_ << "testColinearLinearWarp() ... \n\n";
 
    // test LinearWarp for preserving colinear points

    vector<pair<double,double> > anchors = initializeColinearAnchors();
    vector<double> rtVals = initializeRtVals();

    LinearWarpFunction linearWarp(anchors);
    vector<double> warpedRtVals;
    linearWarp(rtVals, warpedRtVals);
    
    unit_assert_equal(warpedRtVals.at(0), -1, epsilon);
    unit_assert_equal(warpedRtVals.at(1), 3, epsilon);
    unit_assert_equal(warpedRtVals.at(2), 1.5, epsilon);
    unit_assert_equal(warpedRtVals.at(3), 0.5, epsilon);
    

    if (os_)
        {
            *os_ << "testing LinearWarpFunction on colinear points... \n";
            *os_ << "(original value , warped value)\n";
            vector<double>::iterator rt_it = rtVals.begin();
            vector<double>::iterator warped_it = warpedRtVals.begin();
            for(; rt_it != rtVals.end(); ++rt_it, ++warped_it)
                {
                    *os_<< ("(" + boost::lexical_cast<string>(*rt_it) + ", " + boost::lexical_cast<string>(*warped_it) + ")" ).c_str() << endl;
                    
                }

            *os_ << "\n";

        }
}

void testNonColinearLinearWarp()
{
    if (os_) *os_ << "testNonColinearLinearWarp() ... \n\n";

    // test LinearWarp for non-colinear points
    
    vector<pair<double, double> > anchors = initializeNonColinearAnchors();
    vector<double> rtVals = initializeRtVals();
    rtVals.pop_back(); // don't need last elt - is for piecewise linear

    LinearWarpFunction ncLinearWarp(anchors);
    vector<double> warpedRtVals;
    ncLinearWarp(rtVals,warpedRtVals);


    unit_assert_equal(warpedRtVals.at(0), -7.0/3.0, epsilon);
    unit_assert_equal(warpedRtVals.at(1), 17.0/3.0, epsilon);
    unit_assert_equal(warpedRtVals.at(2), 8.0/3.0, epsilon);


    if (os_)
        {
            *os_ << "testing LinearWarpFunction on non-colinear points... \n";
            *os_ << "(original value , warped value)\n";
            vector<double>::iterator rt_it = rtVals.begin();
            vector<double>::iterator warped_it = warpedRtVals.begin();
            for(; rt_it != rtVals.end(); ++rt_it, ++warped_it)
                {
                    *os_<< ("(" + boost::lexical_cast<string>(*rt_it) + ", " + boost::lexical_cast<string>(*warped_it) + ")" ).c_str() << endl;

                }
            *os_ << "\n";

        }

}

void testPiecewiseLinearWarp()
{
    if (os_) *os_ << "testPiecewiseLinearWarp() ... \n\n";

    vector<pair<double, double> > anchors = initializeNonColinearAnchors();
    vector<double> rtVals = initializeRtVals();

    PiecewiseLinearWarpFunction piecewiseLinearWarp(anchors);
    vector<double> warpedRtVals;
    piecewiseLinearWarp(rtVals, warpedRtVals);

    unit_assert_equal(warpedRtVals.at(0), 0, epsilon);
    unit_assert_equal(warpedRtVals.at(1), 7, epsilon);
    unit_assert_equal(warpedRtVals.at(2), 2.5, epsilon);
    unit_assert_equal(warpedRtVals.at(3), 0.5, epsilon);

    if (os_)
        {
            *os_ << "testing PiecewiseLinearWarpFunction ... \n";
            *os_ << "(original value , warped value)\n";
            vector<double>::iterator rt_it = rtVals.begin();
            vector<double>::iterator warped_it = warpedRtVals.begin();
            for(; rt_it != rtVals.end(); ++rt_it, ++warped_it)
                {
                    *os_<< ("(" + boost::lexical_cast<string>(*rt_it) + ", " + boost::lexical_cast<string>(*warped_it) + ")" ).c_str() << endl;

                }

            *os_ << "\n";

        }

}

void testSplineWarp()
{
  vector<pair<double,double> > anchors = initializeSplineAnchors();
  SplineWarpFunction swf(anchors);
  
  vector<double> rtVals = initializeRtVals();
  vector<double> warpedRtVals;
  swf(rtVals, warpedRtVals);

  // coefficients:
  //  0, 0,3,-2;
  //  1,.125,2.1875,-1.3125;
  //  2,.5625,1.875,-1.4375;

  const double error = .001;

  unit_assert_equal(warpedRtVals.at(0), 0, error);
  unit_assert_equal(warpedRtVals.at(1), 4, error);
  unit_assert_equal(warpedRtVals.at(2), 1.2191162109375, error);
  unit_assert_equal(warpedRtVals.at(3), 1, error);
  unit_assert_equal(warpedRtVals.at(4),-395.25, error); // hey, i didn't say it was good.

}

int main(int argc, char* argv[])
{
    try
        {
            if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
            if (os_) *os_ << "WarpFunctionTest:\n\n";
            testColinearLinearWarp();
            testNonColinearLinearWarp();
            testPiecewiseLinearWarp();
            //	    testSplineWarp();

        }
    
    catch (exception& e)
        {
            cerr << e.what();
            return 1;

        }
    
    return 0;

}
