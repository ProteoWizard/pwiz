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
#ifndef _SUPPEAK_H
#define _SUPPEAK_H


#include "CrawPeakMethod.H"
#include "filters/GaussSmoother.H"
#include <vector>
#include <iostream>
#include <string>

#ifdef _MSC_VER
#define USE_COUNTED_PTR
#endif

#ifdef USE_COUNTED_PTR
#include "counted_ptr.h"
#else
#include <boost/shared_ptr.hpp>
#endif

#include <memory>


typedef float mz_type;
using namespace std;
//class CrawPeak;
//class SlimCrawPeak;



/*! we use a shared_ptr so that consumers and producers don't need to worry about keeping track of
the original object 
*/

namespace crawpeaks {

class SlimCrawPeak {
public :



  #ifdef DEBUG
  static int delcnt;
  #endif
  int peak_rt_idx, start_rt_idx, stop_rt_idx, max_rt_idx;
  int mz_idx;
  int len;
  float fwhm;
  bool fwhm_calculated_ok;
  float bg_area;
  float raw_area; // total area under the curve, including background
  float peak_area;  
  float bgslope;
  ///cutoff level for extending past the peak

  ///maximum height, calculated above background
  float peak_height;
  float raw_height;

  //CrawPeakMethod sup_method;
  virtual void init();
  
  virtual std::string as_string() const;
  virtual std::string as_string_long() const;
  virtual std::string as_string_long_header() const;
  virtual std::string as_string_header() const;
  virtual std::string internal_as_string_long() const;

  //virtual void glom_peak ( const SlimCrawPeak * p );
 
  float get_peak_to_bg() const;

  //void calc_peak_height();
  
  ///internal method for calculating signal and background areas
  void get_sig_bg_areas ( const std::vector<float> & raw , std::vector<float> & scratch);

  SlimCrawPeak() {
    init();
  }

  float height_norm_slope();

  inline int get_rel_peak_idx() const {
     return peak_rt_idx - start_rt_idx;
  }

  void calc_fwhm();


  SlimCrawPeak(  int start_idx, int stop_idx, int peak_idx, const std::vector<float> & raw , std::vector<float> & scratch, int mz_idx = -1 );
  ~SlimCrawPeak() {
    #ifdef DEBUG
    delcnt++;
    #endif
  }
};



///A simple structure for peaks
class CrawPeak : public SlimCrawPeak {
public:
  ///index into the list of mzs to which this peak belongs


  ///indices to peak_rt, start_rt, stop_rt, rt of max intensity

  
  ///observed intensity values
   std::vector<float>  intensities;
  ///background intensity values 
   std::vector<float>  background_vals;
  ///area below bg area

  ///slope between peak ends

  ///difference between signal and the median above baseline
  float mean_above_baseline;
  float max_above_baseline;
  ///standard dev. of the difference between 
  float stddev_mean_above_baseline;

  //float sharpness;
  //int mean_crossing_cnt;
  int baseline_p_mean_crossing_cnt;
  ///internal method to calculate stats on 

  ///braindead constructor for now
  CrawPeak() {
    //sup_method = method;
    start_rt_idx = stop_rt_idx = peak_rt_idx = mz_idx = -1;
    init();
  }

  ///constructor with peak location. start, stop, and peak index locations
 /* CrawPeak( CrawPeakMethod method, int start_idx, int stop_idx, int peak_idx, int mz_idx ) :
    SlimCrawPeak(method, start_idx, stop_idx, peak_idx, mz_idx) {
    init();     
  }
*/

  ///Constructor taking start,stop,peak,mz indices, and a vector of intensities
  CrawPeak(  int start_idx, int stop_idx, int peak_idx, 
	   const std::vector<float> & raw, std::vector<float> & scratch ,int mz_idx = -1 );

  virtual void init();


  //TODO -- split this into a simpler approach
  void calc_baseline_stats() ;

  ///sharpness == area / len
  float get_area_sharpness() const {
    return peak_area / len;
  }
  ///height / len
  float get_height_sharpness() const {
    return peak_height / len;
  }
  
  int get_baseline_p_mean_crossing() const {
    return baseline_p_mean_crossing_cnt;
  }



  void calc_CV();

  
  ///extracts data corresponding to the peak's co-ordinates from a float vector to a target vector
  void extract_chrom_regions ( const std::vector<float> & chrom, std::vector<float> & target );

  ///calculates slope of the background level as estimated from peak boundaries

  
  ///calculates nearness of peak location to peak edges
  float assymmetry_stab() const;
   

  ///returns peak index as a measure of scans from the leftmost boundary

  ///internal method for calclating peak height
  
  virtual std::string as_string_header() const;

  std::string as_string_long_header() const;
  std::string as_string_long() const;
  std::string internal_as_string_long() const;

  ///returns peak to background ratio

};

class CrawPeakLocated : public CrawPeak {
public :
  float mz,rt_start,rt_stop,rt_peak;
  CrawPeakLocated(  int lh_valley, int rh_valley, int peak_loc,
		  const std::vector<float> & chrom, std::vector<float> & scratch_chrom, int mz_idx );
  virtual void init() {
    mz = rt_start = rt_stop = rt_peak = -1.0f;
    CrawPeak::init();
  }
  void set_rt_mz( float mz, float rt_start, float rt_peak, float rt_stop ) {
    this->mz = mz;
    this->rt_peak  = rt_peak;
    this->rt_start = rt_start;
    this->rt_stop = rt_stop;
  }
  virtual std::string as_string() const { 
    char tmpstr[256];
    sprintf(tmpstr,"%4.4f\t%3.3f\t%3.3f\t%3.3f\t%d\t%d\t%d",mz,rt_start,rt_peak,rt_stop,start_rt_idx,peak_rt_idx,stop_rt_idx);
    return std::string(tmpstr);
  }
  virtual std::string as_string_header() const {
    return std::string("mz\tstart_rt\tpeak_rt\tstop_rt\tstart_idx\tpeak_idx\tstop_idx");
  }
};

typedef float mz_type;
typedef float int_type;


/*! -- given a chromatogram, finds a series of 'CrawPeaks'
*/

#ifdef USE_COUNTED_PTR
typedef counted_ptr<CrawPeak> CrawPeakPtr;
typedef counted_ptr<SlimCrawPeak> SlimCrawPeakPtr;
#else
typedef boost::shared_ptr<CrawPeak> CrawPeakPtr;
typedef boost::shared_ptr<SlimCrawPeak> SlimCrawPeakPtr;
#endif

struct CompSlimCrawPeakPtrByHeight {
    bool operator() ( const SlimCrawPeakPtr & lh,const SlimCrawPeakPtr & rh ) {
        return lh->peak_height < rh->peak_height;
    }
};

struct CompSlimCrawPeakPtrByArea {
    bool operator() ( const SlimCrawPeakPtr & lh, const SlimCrawPeakPtr & rh ) {
        return lh->peak_area < rh->peak_area;
    }

};

struct CompSlimCrawPeakPtrByStartRTIdx {
    bool operator() ( const SlimCrawPeakPtr & lh, const SlimCrawPeakPtr & rh ) {
        return lh->start_rt_idx < rh->start_rt_idx;
    }

};
struct CompSlimCrawPeakPtrByPeakRTIdx {
    bool operator() ( const SlimCrawPeakPtr & lh, const SlimCrawPeakPtr & rh ) {
        return lh->peak_rt_idx < rh->peak_rt_idx;
    }

};

};

#endif

  
  
