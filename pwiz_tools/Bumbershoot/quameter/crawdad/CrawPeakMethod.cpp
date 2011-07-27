/*
 * Original author: Greg Finney <gfinney .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
#include "CrawPeakMethod.h"

namespace crawpeaks {

void CrawPeakMethod::init() {
  this->set_sd(4.0f);
  saved_weights = false;
  mean_cutoff   = false;
  minimum_level = 0.0f;
  min_len       = 3;
  switch_len    = 2;
  bound_meth = GAUSS_2D_BOUND;
  peak_location_meth = MAXIMUM_PEAK;
  chrom_smooth_method = SAVITZKY_GOLAY_SMOOTHER;
  extend_peak_to_lower_bound = false;
  extend_to_zero_crossing = false;
  extend_allowed_asymmetry = 1.0f;
  fraction_to_valley = 0.0f;
  background_estimation_method = PEAK_BOUNDARY_ESTIMATE;
  this->one_peak_slope_merge_constraint = 0.0f;
  this->mean_slope_merge_constraint = 0.0f;
  this->exclude_extension_overlaps_by_peakrt = false;
  this->ratchet_back_to_frac_maxval = -1.0f;
  this->extend_from_peak_rt = false;
  merge_peaks_list_based = false;
  extend_peak_set = false;
};

#ifdef HAVE_CRAWDAD
void CrawPeakMethod::process_opts( crawusage::optset & opts ) {
  std::string opt_val;
  //TODO ER ADD CMD LINE FOR PEAK MERGING
  if ( opts.get_val(std::string("merge_peaks_list_based"),opt_val) ) {
     this->merge_peaks_list_based = true;
  }
  if ( opts.get_val(std::string("one_peak_slope_merge"),opt_val ) ) {
      this->one_peak_slope_merge_constraint = atof(opt_val.c_str());
  }
  if ( opts.get_val(std::string("mean_slope_merge"),opt_val ) ) {
     this->mean_slope_merge_constraint = atof(opt_val.c_str());
  }
  if ( opts.get_val(std::string("crawpeak_sd"),opt_val) ) {
    this->set_sd(atof(opt_val.c_str()));
  }
  if ( opts.get_val(std::string("crawpeak_fwhm"),opt_val)) {
    this->set_fwhm(atof(opt_val.c_str()));
  }
  if ( opts.get_val(std::string("crawpeak_min_level"),opt_val)) {
    this->minimum_level = atof(opt_val.c_str());
  }
  if ( opts.get_val(std::string("crawpeak_min_len"),opt_val)) {
    this->min_len = atoi(opt_val.c_str());
  }
  if ( opts.get_val(std::string("crawpeak_peak_method"),opt_val ) ) {
    if ( opt_val == std::string("gauss_2d_peak") || opt_val == std::string("2d_peak") ) {
      this->peak_location_meth = GAUSS_2D_PEAK;
    }
    else if ( opt_val == std::string("centroid_peak") ) {
      this->peak_location_meth = CENTROID_PEAK;
    }
    else if ( opt_val == std::string("max_peak") || opt_val == std::string("maximum_peak")) {
      this->peak_location_meth = MAXIMUM_PEAK;
    }
    else {
      std::string err = std::string("invalid option passed: ") + opt_val;
      throw(err.c_str());
    }
  }
  if ( opts.get_val(std::string("crawpeak_switch_len"),opt_val ) ) {
    this->switch_len = atoi(opt_val.c_str());
  }
  if ( opts.get_val(std::string("extend_peak_to_lower_bound"),opt_val ) ) {
     this->extend_peak_to_lower_bound = true;
     this->extend_allowed_asymmetry = atof(opt_val.c_str());
  }
  if ( opts.get_val(std::string("extend_peak_to_zero_crossing"),opt_val) ) {
     this->extend_to_zero_crossing = true;
     this->fraction_to_valley = atof(opt_val.c_str());
  }
  
  if ( opts.get_val(std::string("peak_background_estimate"),opt_val ) ) {
      if ( opt_val == std::string("normal_boundary") ) {
         this->background_estimation_method = PEAK_BOUNDARY_ESTIMATE;
      }
      else if ( opt_val == std::string("lower_boundary") ) {
          this->background_estimation_method = LOWER_BOUNDARY;
      }
      else if ( opt_val == std::string("mean_boundary") ) {
          this->background_estimation_method = MEAN_BOUNDARY;
      }
      else if ( opt_val == std::string("fixed_background") ) {
          this->background_estimation_method = FIXED_BACKGROUND;
      }
  }
 
  if ( opts.get_val(std::string("smoother_type"),opt_val ) ) {  
      if ( opt_val == std::string("savitzky-golay") ) {
         this->chrom_smooth_method = SAVITZKY_GOLAY_SMOOTHER;
      }
      else if ( opt_val == std::string("gaussian") ) {
         this->chrom_smooth_method = GAUSS_SMOOTHER;
      }
      else {
          std::cerr << "I recognize smoothers : savitzky-golay, gaussian, but not: " << opt_val << std::endl;
          exit(1);
      }
     
  }
 
  if ( opts.has_key(std::string("exclude_extension_overlaps_by_peakrt" ) ) ) {
      this->exclude_extension_overlaps_by_peakrt = true;
  }
  if ( opts.get_val(std::string("ratchet_back_to_frac_maxval"), opt_val ) ) {
      this->ratchet_back_to_frac_maxval = atof(opt_val.c_str());
  }
  if ( opts.has_key(std::string("extend_from_peak_rt")) ) {
      this->extend_from_peak_rt = true;
  }
  if ( opts.has_key(std::string("extend_peak_set")) ) {
      this->extend_peak_set = true;
  }
  if ( opts.has_key(std::string("set_extension_start_peak")) ) {
      this->set_extension_start_peak();
  }
 
  //if ( opts.get_val(std::string("

}

#endif

void CrawPeakMethod::set_default_peak_opts() {
  this->init();
}

void CrawPeakMethod::set_extension_start_peak() {
  this->extend_peak_set = true;
  this->extend_from_peak_rt = true;
  this->exclude_extension_overlaps_by_peakrt = true;
  //this->ratchet_back_to_frac_maxval = 0.01f;
  this->ratchet_back_to_frac_maxval = 0.05f; // erynes BUGBUG just a test
}
void CrawPeakMethod::set_extension_start_bounds() {
  set_extension_start_peak();
  this->extend_from_peak_rt = false;
}
  
}

