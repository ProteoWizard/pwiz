//
// $Id$
//
//
// Original author: Barbara Frewen <frewen@u.washington.edu>
//
// Copyright 2012 University of Washington - Seattle, WA 98195
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

/*
 * WeibullPvalue.h
 * Header file for the WeibullPvalue class for estimating the
 * parameters of a weibull distribution from a set of given values and
 * then computing the p-value of a given score from the estimated
 * parameters.
 */

#pragma once

#include <algorithm>
#include <math.h>
#include "BlibUtils.h"
#include "Verbosity.h"
#include "boost/program_options.hpp"

namespace ops = boost::program_options;

namespace BiblioSpec {

class WeibullPvalue{

 private:
  double eta_;   ///< shape? distribution paramter
  double beta_;  ///< scale? distribution parameter
  double shift_; ///< linear shift distribution parameter
  double correlation_; ///< fit of parameterized dist to real data
  double* data_;       ///< data to fit
  int numDataPoints_;  ///< size of data array
  int numDataPointsToFit_; ///< number of points in the tail to fit
  double fractionToFit_;   ///< datapts * fraction = pts to fit
  double min_shift_;       ///< start with this shift value
  double max_shift_;       ///< end with this shift value
  double step_;            ///< step between shift values by this much
  double correlation_tolerance_; ///< stop when corr drops this much below best
  double BONFERRONI_CUT_OFF_P_;// = 0.0001f;
  double BONFERRONI_CUT_OFF_NP_;// = 0.01f;
  bool printAll_;          ///< write to sdtout params at all shift values

  bool fitThreeParamDistribution(); ///< find eta, beta, shift
  ///< find eta, beta for given shift
  bool fitTwoParamDistribution(double shift,
                               double& best_eta,
                               double& best_beta,
                               double& correlation ); 
                          
   

 public:
  WeibullPvalue();
  WeibullPvalue(const ops::variables_map& options_table);
  ~WeibullPvalue();
  bool estimateParams(const vector<double>& scores);
  double getEta() const; 
  double getBeta() const;
  double getShift() const;
  double getCorrelation() const;
  int getNumPointsFit() const;
  double getFractionFit() const;

  double computePvalue(double score) const;
  double bonferroniCorrectPvalue(double pvalue) const;
};

} // namespace

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */

