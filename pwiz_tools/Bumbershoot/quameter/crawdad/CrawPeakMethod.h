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
#ifndef _SUPPEAKMETHOD_H
#define _SUPPEAKMETHOD_H


#ifdef HAVE_CRAWDAD
#include "CrawUsage.h"
#include <string>
#endif

namespace crawpeaks {

///Lists how peak boundaries were detected
enum PeakBoundMethod { GAUSS_2D_BOUND, GAUSS_2D_MIN_BOUND, GAUSS_2D_AVG_BOUND };

///Lists how the actual 'peak' location of the peak is determined
enum PeakPeakMethod { GAUSS_2D_PEAK, CENTROID_PEAK, MAXIMUM_PEAK };

///Background determination technique
enum BackgroundEstimationMethod { PEAK_BOUNDARY_ESTIMATE, LOWER_BOUNDARY, MEAN_BOUNDARY, FIXED_BACKGROUND };

enum ChromSmoothMethod { GAUSS_SMOOTHER, SAVITZKY_GOLAY_SMOOTHER };


const double FHWM_TO_SD_CONV = 2.35482;
class CrawPeakMethod {

private:

  float sd;

public: 
  float minimum_level;
  bool saved_weights;
  bool mean_cutoff;
  int min_len;
  int switch_len;
  
  PeakBoundMethod bound_meth;
  PeakPeakMethod  peak_location_meth;
  BackgroundEstimationMethod background_estimation_method;
  ChromSmoothMethod chrom_smooth_method;

  bool merge_peaks_list_based;
  bool extend_peak_to_lower_bound;
  bool extend_to_zero_crossing;
 
  bool background_at_lower_boundary;
  bool background_at_mean_boundary;
  bool background_at_mid_boundary;

  bool exclude_extension_overlaps_by_peakrt;
  bool extend_from_peak_rt;
  bool extend_peak_set;

  float extend_allowed_asymmetry;
  float fraction_to_valley;

  float one_peak_slope_merge_constraint;
  float mean_slope_merge_constraint;   
  ///decrease a peak limit back to fraction of the maximum value
  ///applies during peak extension
  float ratchet_back_to_frac_maxval;

  void init();
  CrawPeakMethod() {
    init();
  }
  virtual ~CrawPeakMethod() {
  };

  void set_sd ( float sd ) {
    this->sd = sd;
    this->saved_weights = false;
  }
  void set_fwhm ( float fwhm ) {
    this->set_sd(fwhm / (float)FHWM_TO_SD_CONV);
  }
  float get_sd () {
    return this->sd;
  }
  float get_fwhm () {
    return this->sd * (float)FHWM_TO_SD_CONV;
  }

  void set_extend_to_lower_bound ( float val ) {
     this->extend_peak_to_lower_bound = true;

  }
  
  void set_extend_to_zero_crossing ( float val ) {
  }

  void set_default_peak_opts();
  void set_extension_start_peak();
  void set_extension_start_bounds();
  

  #ifdef HAVE_CRAWDAD
  void process_opts ( crawusage::optset & opts );
  #endif

};

}

#endif
