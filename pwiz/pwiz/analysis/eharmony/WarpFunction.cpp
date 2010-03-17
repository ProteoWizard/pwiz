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
/// WarpFunction.cpp
///


#include "WarpFunction.hpp"
#include "pwiz/utility/math/Stats.hpp"
#include "pwiz/utility/math/Stats.cpp" //TODO: figure out why this is required to avoid the linker error

#include <iostream>
#include <fstream>

using namespace std;
using namespace pwiz::eharmony;

WarpFunction::WarpFunction(const vector<pair<double,double> >& anchors) : anchors_(anchors){}
void WarpFunction::operator()(vector<double>& rt_vals, vector<double>& warped_rt_vals)
{
  //copy rt_vals to warped_rt_vals
  warped_rt_vals.assign(rt_vals.begin(),rt_vals.end());
}

LinearWarpFunction::LinearWarpFunction(vector<pair<double,double> >& anchors) : WarpFunction(anchors)
{
  vector<pair<double,double> >::iterator anchor_it = anchors_.begin();
  double rt1MeanSum = 0;
  double rt2MeanSum = 0;

  for(; anchor_it != anchors_.end(); ++anchor_it)
    {
        rt1MeanSum += anchor_it->first;
        rt2MeanSum += anchor_it->second;

    }

  double rt1Mean = rt1MeanSum / anchors_.size();
  double rt2Mean = rt2MeanSum / anchors_.size();
 
  double numeratorSum = 0;
  double denominatorSum = 0;
  
  vector<pair<double,double> >::iterator ss_it = anchors_.begin();
  for(; ss_it != anchors_.end(); ++ss_it)
    {
        numeratorSum += ss_it->first*ss_it->second;
        denominatorSum += ss_it->first*ss_it->first;
     
    }
  
  double m = (numeratorSum - anchors_.size()*rt1Mean*rt2Mean)/(denominatorSum - anchors_.size()*rt1Mean*rt1Mean);
  double b = rt2Mean - m*rt1Mean;

  coefficients_.push_back(b);
  coefficients_.push_back(m);
 

}

void LinearWarpFunction::operator()(vector<double>& rt_vals, vector<double>& warped_rt_vals)
{
  vector<double>::iterator rt_it = rt_vals.begin();
  for(; rt_it != rt_vals.end(); ++rt_it)
    {
      double warped_val = coefficients_.at(0) + coefficients_.at(1)*(*rt_it);
      warped_rt_vals.push_back(warped_val);

    }

  return;
}

PiecewiseLinearWarpFunction::PiecewiseLinearWarpFunction(vector<pair<double,double> >& anchors) : WarpFunction(anchors)
{
  
  sort(anchors_.begin(), anchors_.end());
  vector<pair<double,double> >::iterator anchor_it = anchors_.begin();
  vector<pair<double,double> >::iterator anchor_it_plus = anchors_.begin() + 1;

  if (anchors_.size() == 0)
    {
      cerr << "[WarpFunction] Error: no anchor points selected.  Returning original retention times." << endl;
      anchors_.push_back(make_pair(10000,10000)); // everything gets the first pair of coefficients
      coefficients_.push_back(make_pair(0,1)); // y = 0 + 1x

    }
			    
  else
    {

      double m_0;
      if (anchors_.begin()->first == 0) m_0 = 0; // shouldn't worry about negative numbers.. if zero anchor for strange reason, first slope is default to zero so as to not break anything with a div by zero
      else m_0 = anchors_.begin()->second / anchors_.begin()->first;

      double b_0 = anchors_.begin()->second - m_0 * anchors_.begin()->first;

      pair<double,double> first_piece(b_0,m_0);
      coefficients_.push_back(first_piece);

      for(; anchor_it_plus != anchors_.end(); ++anchor_it,++anchor_it_plus)
	{    
	  double m = (anchor_it_plus->second - anchor_it->second)/(anchor_it_plus->first - anchor_it->first);
	  double b = anchor_it_plus->second - m * anchor_it_plus->first;

	  pair<double,double> piece_coefficients(b,m);
	  coefficients_.push_back(piece_coefficients);
     
	}

    }
}

bool first_less_than(pair<int,double> a, pair<int,double> b)
{
  return a.first < b.first;

}

bool second_less_than(pair<int,double> a, pair<int,double> b)
{
  return a.second < b.second;

}

void PiecewiseLinearWarpFunction::operator()(vector<double>& rt_vals, vector<double>& warped_rt_vals)
{
  vector<pair<int,double> > rt_table;
  vector<pair<int,double> > warped_rt_table;
  
  vector<double>::iterator rt_it = rt_vals.begin();
  size_t index = 0;

  for(; rt_it != rt_vals.end(); ++rt_it, ++index)
    {
      pair<int,double> table_entry(index,*rt_it);
      rt_table.push_back(table_entry);

    }
  
  sort(rt_table.begin(),rt_table.end(), second_less_than);
  sort(anchors_.begin(), anchors_.end());

  vector<pair<int,double> >::iterator rt_table_it = rt_table.begin();
  vector<pair<double,double> >::iterator anchor_it = anchors_.begin();
  vector<pair<double,double> >::iterator coeff_it = coefficients_.begin();

  if (anchors_.size() != coefficients_.size()) throw runtime_error("[WarpFunction] wrong size");
  for(; rt_table_it != rt_table.end() ; ++rt_table_it)  
    {      
      bool done = false;
      while (!done)
	{
              if(rt_table_it->second < anchor_it->first || anchor_it + 1 == anchors_.end())
                  {
                      double warped_rt = coeff_it->first + coeff_it->second * rt_table_it->second; 
                      pair<int,double> warped_table_entry(rt_table_it->first, warped_rt);
                      warped_rt_table.push_back(warped_table_entry);
                      done = true;

                  }

              else 
                  {
                      ++anchor_it;
                      ++coeff_it;

                  }

          }

    }


  sort(warped_rt_table.begin(),warped_rt_table.end(),first_less_than);
 
  vector<pair<int,double> >::iterator warped_rt_table_it = warped_rt_table.begin();
  for(;warped_rt_table_it != warped_rt_table.end(); ++warped_rt_table_it)

    {
        warped_rt_vals.push_back(warped_rt_table_it->second);

    }

}

double slackCoefficient = .5;

SplineWarpFunction::SplineWarpFunction(std::vector<pair<double,double> >& anchors) : WarpFunction(anchors)
{
  // calculate tangent constraints (cardinal spline)
  vector<double> m; 
  m.push_back(0); // 0 slope at beginning
  size_t index = 1;
  for(; index < anchors.size() - 1; ++index)
    {
      double p0 = anchors.at(index - 1).second;
      double p1 = anchors.at(index + 1).second;
      m.push_back((1-slackCoefficient)*(p1-p0)/2);

    }

  m.push_back(0); // 0 slopes at end

  // fill in _coefficients vector  
  index = 0;
  vector<pair<double,double> >::iterator it = anchors.begin();
  vector<pair<double,double> >::iterator it_plus = ++(anchors.begin());

  for(; it_plus != anchors.end(); ++it, ++it_plus, ++index)
    {
        double h = it_plus->second - it->second;
	double a0 = it->second;
	double a1 = h*m.at(index);
	double a2 = -3*it->second - 2*h*m.at(index) + 3*it_plus->second - h*m.at(index+1);
	double a3 = 2*it->second +h*m.at(index) - 2*it_plus->second + h*m.at(index+1);
	
	boost::tuple<double,double,double,double> pieceCoefficients(a0,a1,a2,a3);
	coefficients_.push_back(pieceCoefficients);
 
    }

  coefficients_.push_back(coefficients_.back());   
    
}

void SplineWarpFunction::operator()(std::vector<double>& rt_vals, std::vector<double>& warped_rt_vals)
{

  vector<pair<int,double> > rt_table;
  vector<pair<int,double> > warped_rt_table;

  vector<double>::iterator rt_it = rt_vals.begin();
  size_t index = 0;

  for(; rt_it != rt_vals.end(); ++rt_it, ++index)
    {
      pair<int,double> table_entry(index,*rt_it);
      rt_table.push_back(table_entry);

    }

  sort(rt_table.begin(),rt_table.end(), second_less_than);
  sort(anchors_.begin(), anchors_.end());

  vector<pair<int,double> >::iterator rt_table_it = rt_table.begin();
  vector<pair<double,double> >::iterator anchor_it = anchors_.begin();
  vector<boost::tuple<double,double,double,double> >::iterator coeff_it = coefficients_.begin();

  if (anchors_.size() != coefficients_.size()) throw runtime_error("[WarpFunction] wrong size");
  for(; rt_table_it != rt_table.end() ; ++rt_table_it)
    {
      bool done = false;
      while (!done)
        {
	  if(rt_table_it->second < anchor_it->first || anchor_it + 1 == anchors_.end())
	    {
	      double warped_rt = coeff_it->get<0>() + coeff_it->get<1>() * rt_table_it->second + coeff_it->get<2>() * rt_table_it->second * rt_table_it->second + coeff_it->get<3>() * rt_table_it->second * rt_table_it->second * rt_table_it->second;
	      pair<int,double> warped_table_entry(rt_table_it->first, warped_rt);
	      warped_rt_table.push_back(warped_table_entry);
	      done = true;

	    }

	  else
	    {
	      ++anchor_it;
	      ++coeff_it;

	    }

	}

    }

  sort(warped_rt_table.begin(),warped_rt_table.end(),first_less_than);
  vector<pair<int,double> >::iterator warped_rt_table_it = warped_rt_table.begin();
  for(;warped_rt_table_it != warped_rt_table.end(); ++warped_rt_table_it)

    {
      warped_rt_vals.push_back(warped_rt_table_it->second);

    }

}
