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
#include "CrawPeak.H"
#include "CrawPeakMethod.H"
#include <math.h>
#include <iostream>


/* 
- How much of a case do we need to avoid where the 2D derivative crosses excessively quickly? What
    are the consequences of finding peaks in this fashion? 
- What parameters go into peak finding with this technqiue? Distribution size, which affects smoothing?.


- It should be a lot easier to find and define baseline using this technique. It should
  work perfectly fine to go and look at frequency distributions of baseline intensity

*/
namespace crawpeaks {


   float SlimCrawPeak::get_peak_to_bg() const {

       if ( bg_area <= 0.0f ) {
          return 99999.0f;
       }
      return peak_area / bg_area;
   }

   SlimCrawPeak::SlimCrawPeak( int start_idx, int stop_idx, int peak_idx, 
			     const std::vector<float> & raw ,std::vector<float> & scratch , int mz_idx  ) : 
    start_rt_idx(start_idx) , 
    stop_rt_idx(stop_idx) , 
    peak_rt_idx(peak_idx) , 
    mz_idx(mz_idx) 
   {
     init();
     
    // begin erynes BUGBUG DEBUG
    if (!raw.empty())
      raw_area = (float)crawutils::area_under_curve(raw, start_idx, stop_idx);
    // end erynes BUGBUG DEBUG

    //calculate_slope( raw );
    //get_sig_bg_areas( raw , scratch );       
   }

void SlimCrawPeak::init() {
    len = stop_rt_idx - start_rt_idx + 1;
    raw_area = bg_area = peak_area = bgslope = peak_height = raw_height = -1.0f;
    fwhm = -1;
    this->fwhm_calculated_ok = false;
  }

void CrawPeak::init() {
  stddev_mean_above_baseline = -1.0f;
  baseline_p_mean_crossing_cnt = 0;
}

#ifdef DEBUG
int SlimCrawPeak::delcnt = 0;
#endif

CrawPeak::CrawPeak(  int start_idx, int stop_idx, int peak_idx, 
		  const std::vector<float> & raw , std::vector<float> & scratch,  int mz_idx 
                 ) :
    SlimCrawPeak( start_idx, stop_idx, peak_idx, raw, scratch, mz_idx ) {
  init();     
  intensities.resize(this->len);
  background_vals.resize(this->len);
  extract_chrom_regions(raw, this->intensities);
  calc_baseline_stats();
}

void CrawPeak::calc_CV() {
   
}

void CrawPeak::calc_baseline_stats( )  {
/* take all intensity values, take median or mean value
   - then use this as a springboard to calculate either CV, or number of crossing times across this point
  
   1. take baseline , calculate for each point the height above baseline (0 minimum) , then based upon this
      extend the baseline up by such amount. This can then be used as baseline+(mean_above_baseline), 
      then maybe take number of points above that line 

   
*/
    //TODO these intermediate vectors are probably unnecessary -- let's see what removing them would do..?
    

    ///difference between intensities and background values
    std::vector<float> height_delta(intensities.size());
    ///background values plus median of height delta
    std::vector<float> baseline_plus_median(intensities.size());
    
    for ( int i = 0 ; i < (int)height_delta.size() ; i++ ) {
       height_delta[i] = intensities[i] - background_vals[i];
    }

    
    float height_median = (float)crawstats::median(height_delta);
    for ( int i = 0 ; i < (int)height_delta.size() ; i++ ) {
       baseline_plus_median[i] = background_vals[i] + height_median;
    }
    //TODO find an efficient means for detecting sign crossing
    for ( int i = 0 ; i < (int)intensities.size() - 1 ; i++ ) {
       //calculate the number of times signal crosses, or sits on.. the height_delta vector shown before
        if ( intensities[i] <= baseline_plus_median[i] && intensities[i+1] >=  baseline_plus_median[i+1] ) {
           baseline_p_mean_crossing_cnt += 1;
        }
        else if ( intensities[i] >= baseline_plus_median[i] && intensities[i+1] <= baseline_plus_median[i+1] ) {
           baseline_p_mean_crossing_cnt += 1;
        }
    }
    std::vector<float> baseline_median_vs_signal(intensities.size());
    for ( int i = 0 ; i < (int) intensities.size() ; i++ ) {
       baseline_median_vs_signal[i] = intensities[i] - baseline_plus_median[i];
    }
    mean_above_baseline        = (float)crawstats::mean(baseline_median_vs_signal);
    max_above_baseline         = (float)crawutils::max_vect(baseline_median_vs_signal);
    stddev_mean_above_baseline = (float)sqrt(crawstats::var_w_mean(baseline_median_vs_signal, mean_above_baseline));

}


  float CrawPeak::assymmetry_stab() const {
    int peak_len = this->len;
    int min_distance = std::min(stop_rt_idx - peak_rt_idx, peak_rt_idx - start_rt_idx);
    return (min_distance *1.0f / peak_len) * 2.0f;
  }


  void CrawPeak::extract_chrom_regions ( const std::vector<float> & chrom, std::vector<float> & target ) {
    for ( int i = 0 ; i < this->len ; i++ ) {
      target[i] = chrom[start_rt_idx+i];
    }
  }

  float SlimCrawPeak::height_norm_slope() {
     return (bgslope * len) / this->peak_height;
  }



  void SlimCrawPeak::get_sig_bg_areas ( const std::vector<float> & raw, std::vector<float> & bg_scratch ) {
      //std::vector<float> counted_intensities(intensities.size(),0.0f);
      //already set in 
      //raw_area = crawutils::area_under_curve( raw , start_rt_idx, stop_rt_idx );
      double total_bg = 0.0;
      float bg_val = raw[start_rt_idx];
      /* TODO -- trapezoidal area calculation in place, rather than
	 copied to a vector and passed to a function */
      float next_used_val, curr_used_val;
      curr_used_val = next_used_val = bg_val;

      //#define FAST_WAY
#ifdef FAST_WAY
      float peak_bg_val;
      int rel_peak_idx = get_rel_peak_idx();
      peak_bg_val = bg_val + rel_peak_idx * bgslope;
      for ( int i = 0 ; i < this->len - 1 ; i++ ) {
	total_bg += next_used_val;
	curr_used_val = next_used_val;
	bg_val += bgslope;
	if ( raw[start_rt_idx+i+1] < bg_val ) {
	  next_used_val = raw[start_rt_idx+i+1];
	}
	else {
	  next_used_val = bg_val;
	}
	total_bg += ( next_used_val - curr_used_val ) / 2.0;
      }
      peak_height = raw[peak_rt_idx] - peak_bg_val;
#else
      //bg_area = total_bg;
      bg_val = raw[start_rt_idx];
      for ( int i = 0 ; i < this->len ; i++ ) {
          if ( raw[start_rt_idx+i] < bg_val ) {
             bg_scratch[i] = raw[start_rt_idx+i];
          }
          else {
             bg_scratch[i] = bg_val;
          }
          bg_val += this->bgslope;
      }
      bg_area = (float) crawutils::area_under_curve(bg_scratch, 0, this->len - 1);
      raw_height = raw[peak_rt_idx];
      peak_height = raw[peak_rt_idx] - bg_scratch[get_rel_peak_idx()];
#endif
      //total_bg = bg_area;

#ifdef TEST_AREA
      //assert(abs(bg_area - total_bg) <= 0.1 );
      if ( abs(bg_area - total_bg) > 1 ) {
	std::cerr << "Error calculating bg_area vs total_bg: " << bg_area << " , " << total_bg << "len: " << this->len << std::endl;
	std::cerr << this->as_string() << std::endl;
	exit(1);
      }
#endif
      peak_area = raw_area - bg_area;

  }



  std::string SlimCrawPeak::as_string_header() const { 
    return std::string("mz\tstart_idx\tpeak_idx\tstop_idx\tfwhm");
  }
   std::string SlimCrawPeak::as_string() const {
       char tmpstr[256];
       sprintf(tmpstr,"%d\t%d\t%d\t%d\t%2.2f",mz_idx,start_rt_idx,peak_rt_idx,stop_rt_idx,fwhm);
       return std::string(tmpstr);
   }
   std::string SlimCrawPeak::as_string_long() const {
     return (as_string() + this->internal_as_string_long());
   }

   std::string SlimCrawPeak::internal_as_string_long() const {
       char tmpstr[1024];
       sprintf(tmpstr,"\t%3.3f\t%3.3f\t%d\t%3.3f",
	       peak_height,peak_area, len, get_peak_to_bg() );

       return std::string(tmpstr);
   }


   std::string SlimCrawPeak::as_string_long_header() const {
     return as_string_header() + std::string("\tpeak_height\tpeak_area\tlen\tpeak_bg_ratio");
   }
 
   std::string CrawPeak::as_string_header()  const {
       return SlimCrawPeak::as_string_header();
   }

  std::string CrawPeak::as_string_long_header() const {
    return as_string_header() + std::string("\tpeak_height\tpeak_area\tlen\tpeak_bg_ratio") + \
      std::string("\tmean_above_baseline\tstddev_mean_above_baseline\tmean_crossing") + \
      std::string("\tasymmetry");
  }

   std::string CrawPeak::as_string_long() const {
       std::string start = as_string();
       std::string tstr = this->internal_as_string_long();
       start += tstr;
       return start;
   }


   std::string CrawPeak::internal_as_string_long() const {
       char tmpstr[1024];


       sprintf(tmpstr,"\t%3.3f\t%3.3f\t%d\t%3.3f\t%3.3f\t%3.3f\t%d\t%3.3f",
	       peak_height,peak_area, len, get_peak_to_bg(),
	       mean_above_baseline, stddev_mean_above_baseline,
	       get_baseline_p_mean_crossing(),assymmetry_stab() );
       return std::string(tmpstr);
   }



/* ---- suppeaklocated ---- */

CrawPeakLocated::CrawPeakLocated( int lh_valley, int rh_valley,
			       int peak_loc, const std::vector<float> &chrom , std::vector<float> & scratch, int mz_idx ) : 
  CrawPeak( lh_valley, rh_valley, peak_loc, chrom, scratch, mz_idx ) {
}

};
