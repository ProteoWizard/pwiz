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

#include "WeibullPvalue.h"

namespace BiblioSpec {
/**
 * Default constructor.  Use correlation=-1 to indicate that the eta,
 * beta, and shift parameters have not bee estimated.  Set default
 * values for min/max shift, step and cutoffs.
 */
// eventually replace this with constructor with params and let
// defaults be set there
/*
WeibullPvalue::WeibullPvalue() :
    eta_(0),
    beta_(0),
    shift_(0),
    correlation_(-1),
    numDataPoints_(0),
    numDataPointsToFit_(0),
    fractionToFit_(0.5),
    min_shift_(-1),
    max_shift_(1),
    step_(0.001),
    BONFERRONI_CUT_OFF_P_(0.0001),
    BONFERRONI_CUT_OFF_NP_(0.01),
    printAll_(false)
{
    data_ = NULL;
}
*/
WeibullPvalue::WeibullPvalue(const ops::variables_map& options_table) :
    eta_(0),
    beta_(0),
    shift_(0),
    correlation_(-1),
    numDataPoints_(0),
    numDataPointsToFit_(0),
    fractionToFit_(options_table["fraction-to-fit"].as<double>()),
    min_shift_(-1),
    max_shift_(1),
    step_(0.001),
    correlation_tolerance_(options_table["correlation-tolerance"].as<double>()),
    BONFERRONI_CUT_OFF_P_(0.0001),
    BONFERRONI_CUT_OFF_NP_(0.01),
    printAll_(options_table["print-all-params"].as<bool>())
{
    data_ = NULL;
}

WeibullPvalue::~WeibullPvalue(){
    delete [] data_;
}

/**
 * Given the data, estimate the eta, beta, and shift parameters of a
 * Weibull distribution and compute the correlation between the given
 * data and the parameterized distribution.
 */
bool WeibullPvalue::estimateParams(const vector<double>& scores){
    // clean up from previous data
    eta_ = 0;
    beta_ = 0;
    shift_ = 0;
    correlation_ = -1;
    delete [] data_;
    
    // allocate an array for the data
    numDataPoints_ = scores.size();
    data_ = new double[numDataPoints_];
    
    // copy scores into the array
    for(int i=0; i < numDataPoints_; i++){
        data_[i] = scores.at(i);
    }
    // sort high to low
    sort(data_, data_ + numDataPoints_, doublesDescending);
    
    // best score is first, ignore it
    data_ += 1;
    numDataPoints_ -= 1;
    numDataPointsToFit_ = (int)(fractionToFit_ * numDataPoints_);
    
    // find parameters
    bool success = fitThreeParamDistribution();
    
    // have data_ point back to the beginning of the array and delete
    data_ -= 1;
    numDataPoints_ += 1;
    //delete [] data_;
    //data_ = NULL;
    
    return success;
}

/**
 * Find and set the eta, beta, and shift parameters.  Try each shift
 * between min and max in units of step.  Assumes that the data_ has
 * been allocated and is sorted in descending order.
 */
bool WeibullPvalue::fitThreeParamDistribution(){
    
    double cur_eta = 0.0;
    double cur_beta = 0.0;
    double cur_correlation = 0.0;
    double cur_shift;
    
    for (cur_shift = max_shift_; cur_shift > min_shift_ ; cur_shift -= step_) {
        
        fitTwoParamDistribution(cur_shift, cur_eta, cur_beta, cur_correlation);
        
        if( printAll_ ){
            cout << cur_shift << "\t" << cur_correlation << "\t"
                 << cur_eta << "\t"
                 << cur_beta << "\t"
                 << cur_shift << "\t"
                 << endl;
        }
        // update if this shift is better
        if (cur_correlation > correlation_) {
            eta_ = cur_eta;
            beta_ = cur_beta;
            shift_ = cur_shift;
            correlation_ = cur_correlation;
        } else if (cur_correlation < correlation_ - correlation_tolerance_){
            break;
        }
    } // next shift
    
    // could require a minimum correlation and return false if not met
    return true;
    
}

/**
 * Find the best eta and beta for the given shift.  Return via the
 * arguments eta, beta, and the corrleation between the parameterized
 * distribution and the data.
 */
bool WeibullPvalue::fitTwoParamDistribution(double shift,
                                            double& eta,
                                            double& beta,
                                            double& correlation){
    bool success = true;
    int numDataPointsToFitPostShift = numDataPointsToFit_;
    
    double* X = new double[numDataPoints_];
    
    // transform data into an array of values for fitting
    // shift (including only non-neg values) and take log
    int idx;
    for(idx=0; idx < numDataPointsToFit_; idx++) {
        double score = data_[idx] + shift; // move right by shift
        if (score <= 0.0) {
            numDataPointsToFitPostShift = idx;
            break;
        }
        X[idx] = log(score);
    }
    //cerr << "Fitting " << numDataPointsToFitPostShift << " data points" << endl;
    
    double* F_T = new double[numDataPoints_];
    for(idx=0; idx < numDataPointsToFitPostShift; idx++) {
        int reverse_idx = numDataPoints_ - idx;
        // magic numbers 0.3 and 0.4 are never changed
        F_T[idx] = (float)((reverse_idx - 0.3) / (numDataPoints_ + 0.4));
        
    }
    
    double* Y = new double[numDataPoints_];
    for(idx=0; idx < numDataPointsToFitPostShift; idx++) {
        Y[idx] = (double)log( -log(1.0 - F_T[idx]) );
    }
    
    int N = numDataPointsToFitPostShift; // rename for formula's sake
    double sum_Y  = 0.0;
    double sum_X  = 0.0;
    double sum_XY = 0.0;
    double sum_XX = 0.0;
    for(idx=0; idx < numDataPointsToFitPostShift; idx++) {
        sum_Y  += Y[idx];
        sum_X  += X[idx];
        sum_XX += X[idx] * X[idx];
        sum_XY += X[idx] * Y[idx];
    }
    //cerr <<"sum_Y "<<sum_Y<< endl;
    //cerr <<"sum_XX "<<sum_XX<< endl;
    //cerr <<"sum_XY "<<sum_XY<<endl; 
    
    double b_num    = sum_XY - (sum_X * sum_Y / N);
    double b_denom  = sum_XX - sum_X * sum_X / N;
    double b_hat    = b_num / b_denom;
    //cerr << "bnum " << b_num << " bdenom " << b_denom << " bhat " << b_hat << endl;
    
    double a_hat    = (sum_Y - b_hat * sum_X) / N;
    beta = b_hat;
    eta  = exp( - a_hat / beta );
    
    double c_num   = 0.0;
    double c_denom_X = 0.0;
    double c_denom_Y = 0.0;
    double mean_X = sum_X / N;
    double mean_Y = sum_Y / N;
    for (idx=0; idx < N; idx++) {
        double X_delta = X[idx] - mean_X;
        double Y_delta = Y[idx] - mean_Y;
        c_num += X_delta * Y_delta;
        c_denom_X += X_delta * X_delta;
        c_denom_Y += Y_delta * Y_delta;
    }
    double c_denom = sqrt(c_denom_X * c_denom_Y);
    //cerr << "cdenomx " << c_denom_X << " cdenomy " << c_denom_Y << " cdenom " << c_denom << endl;
    if (c_denom == 0.0) {
        correlation = 0.0; // min value
        eta = 0;
        beta = 0;
        success =  false;
    } else {
        correlation = c_num / c_denom;
    }

    delete [] F_T;
    delete [] Y;
    delete [] X;

    return success;
}

/**
 * Computes a p-value for the given score for a Weibull with the
 * parameters currently held by this object.
 */
// TODO check that there are valid params estimated
double WeibullPvalue::computePvalue(double score) const{

    double p_value = 1.0;
    if( (score + shift_) > 0 ){ // else weibull undefined
        p_value = exp( - pow( (score + shift_) / eta_, beta_));
    }
    return p_value;
}

double WeibullPvalue::bonferroniCorrectPvalue(double pvalue) const{
    
    double corrected_pvalue = 0;
    
    // numDataPoints_ has actually been the number of non-decoy
    // matches; find out what it should be...
    if( (pvalue > BONFERRONI_CUT_OFF_P_) || 
        (pvalue * numDataPoints_) > BONFERRONI_CUT_OFF_NP_) {
        corrected_pvalue = -log(1-pow((1-pvalue), numDataPoints_));
    }
    // else, use the approximation
    else {
        corrected_pvalue = -log(pvalue * numDataPoints_);
    }
    //cout << "raw pval: " << pvalue << " corrected: " << corrected_pvalue 
    //     << " unlogged raw " << exp(-1 * pvalue) 
    //     << " unlogged corrected " << exp(-1 * corrected_pvalue) << endl;
    return corrected_pvalue;
    
}

// getters
double WeibullPvalue::getEta() const {
    return eta_;
}

double WeibullPvalue::getBeta() const {
    return beta_;
}

double WeibullPvalue::getShift() const {
    return shift_;
}

double WeibullPvalue::getCorrelation() const {
    return correlation_;
}

int WeibullPvalue::getNumPointsFit() const{
    return numDataPointsToFit_;
}

double WeibullPvalue::getFractionFit() const{
    return fractionToFit_;
}

} // namespace

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
