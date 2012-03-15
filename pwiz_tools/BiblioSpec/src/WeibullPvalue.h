/*
  Copyright (c) 2011, University of Washington
  All rights reserved.

  Redistribution and use in source and binary forms, with or without
  modification, are permitted provided that the following conditions
  are met:

    * Redistributions of source code must retain the above copyright
    notice, this list of conditions and the following disclaimer. 
    * Redistributions in binary form must reproduce the above
    copyright notice, this list of conditions and the following
    disclaimer in the documentation and/or other materials provided
    with the distribution. 
    * Neither the name of the <ORGANIZATION> nor the names of its
    contributors may be used to endorse or promote products derived
    from this software without specific prior written permission.

  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
  "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
  LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS
  FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE
  COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT,
  INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING,
  BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
  LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
  CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT
  LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN
  ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
  POSSIBILITY OF SUCH DAMAGE.
*/
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

using namespace std;
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

