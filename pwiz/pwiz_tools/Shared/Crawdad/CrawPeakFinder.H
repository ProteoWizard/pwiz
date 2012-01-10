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
#ifndef _SUPPEAKFINDER_H
#define _SUPPEAKFINDER_H

namespace crawpeaks {

class CrawPeakAnnotator;
class CrawPeakFinder;
class CrawPeak;
class CrawPeakMethod;
};

#include "filters/GaussSmoother.H"
#include "CrawPeak.H"
#include "CrawPeakMethod.H"
#include "CrawPeakAnnotator.H"
#include <vector>
#include <iostream>
#include <string>
#ifdef _MSC_VER
#include "counted_ptr.h"
#else
#include <boost/shared_ptr.hpp>
#endif
#include <memory>


namespace crawpeaks {


class BaseCrawPeakFinder {
public :
  
  typedef SlimCrawPeakPtr stored_peak_t;


  
   struct PlusMinusCross {
     int idx;
     char t;
     
     PlusMinusCross ( int idx, char t ) : idx(idx), t(t) {};
     PlusMinusCross () { idx = 0 ; t = '\0'; };

     bool operator<(const PlusMinusCross & other) {
       return (this->idx < other.idx);
     };



   };


  ///smoothing object for convoluting with gaussian 2nd derivative
  virtual ~BaseCrawPeakFinder ();
  bool slim;
  CrawPeakMethod method;
  CrawPeakAnnotator annotator;
  
  GaussSmoother * gs_2d;
  GaussSmoother * gs_1d;
  GaussSmoother * gs_0d;
  
  ///chromatogram passed into object
  vector<float> chrom;
  ///second derivative of the passed-in chromatogram
  vector<float> chrom_2d;
  vector<float> chrom_1d;
  vector<float> chrom_0d;
  ///scratch vector for the background intensities
  vector<float> chrom_bg_scratch;

  ///peaks found on chromatogram
  vector<stored_peak_t> sps;
  int mz_idx;
  vector<int> minus_crosses;
  vector<int> plus_crosses;
  vector<int> maxs;
  vector<int> mins;
  vector<uint> peaks;
  vector<uint> valleys;



  ///standard deviation of gaussian peak used in distribution
  bool saved_weights;


  BaseCrawPeakFinder () {
    init();
  }

  BaseCrawPeakFinder( CrawPeakMethod m ) { 
    method = m;
    init();
  }
  

  bool can_smooth( const std::vector<float> & f ) {
    return true;
  }

  void set_slim ( bool slim = true ) {
    this->slim = slim;
  }

  virtual void set_chrom (const vector<float> & f, 
			  int mz_idx);

  void clear();

  void delimit_by_minimum_level( uint & lh_valley_idx ,
				 uint & rh_valley_idx ,
				 int peak_loc );

  void init_smoothers();

  bool passes_min_len ( int lh_valley,
			int rh_valley );
  
  void get_2d_chrom( const std::vector<float> & in_chrom,
		     std::vector<float> & out_chrom );
  void get_2d_chrom( std::vector<float> & in_chrom );
  void get_1d_chrom( const std::vector<float> & in_chrom,
		     std::vector<float> & out_chrom );
  void get_1d_chrom( std::vector<float> & in_chrom );
  void get_0d_chrom( const std::vector<float> & in_chrom,
		     std::vector<float> & out_chrom );
  void get_0d_chrom( std::vector<float> & in_chrom );

  void set_weights_2d();
  void set_weights_1d();
  void set_weights_0d();
  void set_weights() {
     set_weights_0d();
     set_weights_1d();
     set_weights_2d();
  }
  void call_peaks ();

  void report_peaks ( std::ostream & o = std::cout );

  ///finds points where the second derivative crosses zero - note that
  ///it must stay on the changed-to sign for at least two points
  void find_cross_points ( std::vector<float> & c, float threshold=0.0f);
  void find_cross_points(std::vector<float> &c , std::vector<int> & plus_crosses_local, std::vector<int> & plus_crosses_minus,  float threshold =0.0f );


  ///calls - peaks and edges
  void peaks_and_valleys_fast( std::vector<float> & c );
  void peaks_and_valleys_slow( std::vector<float> & c );
  void peaks_and_valleys_brendanx( std::vector<float> & c );
  void peaks_and_valleys( std::vector<float> & c ) {
    peaks_and_valleys_brendanx(c);
  }
 

  virtual void clear_sps();
  
  void filter_sps_by_method( CrawPeakMethod * in_meth = NULL );
  


  CrawPeak * construct_peak( int lh_valley , int rh_valley, int peak_loc );
  SlimCrawPeak * construct_slim_peak( int lh_valley , int rh_valley, int peak_loc );

  ///delimits the peak based upon the nearest lh and rh points that fall below a minimum level
  
  bool consistent_gte( const std::vector<float> & c , int idx,
		       float threshold=0.0f, int len=2);
  bool consistent_lte( const std::vector<float> & c , int idx,
		       float threshold=0.0f, int len=2);

  /*! get_area -- gets area under a range of points
    \@param start_idx
    \@param stop_idx -- note area is inclusive of this point
  */
  double get_bg_subtracted_area ( int start_idx, int stop_idx , float bgslope) ;
  double get_area ( int start_idx, int stop_idx );

  //stub for brendan's N-pass mean height determinator
  
  float determine_mean_cutoff();

  /* virtual methods */
  //making this a pure virtual class...
  virtual void peak_voodoo( int lh_valley, int rh_valley, int peak_loc ) = 0;
  virtual int get_num_stored_peaks() = 0;
  virtual SlimCrawPeak * get_peak_ptr ( int idx ) = 0;  
  virtual void extend_peak_set() = 0;
  
private : 
  void init();
  ///returns major, minor version numbers
  std::pair< int, int > get_version() {
    return std::pair<int, int>(0,2);
  }
   

};

class CrawPeakFinder : public BaseCrawPeakFinder {

public :

  vector<stored_peak_t> sps;
  virtual void peak_voodoo( int lh_valley, int rh_valley, int peak_loc );
  virtual void clear_sps();
  virtual int get_num_stored_peaks();
  virtual SlimCrawPeak * get_peak_ptr ( int idx );
  CrawPeakFinder () : BaseCrawPeakFinder() {};  
  CrawPeakFinder( CrawPeakMethod m ) : BaseCrawPeakFinder(m) {};
  //what a headache, this needs to be defined in each class due to sps vector being for
  //different types
  virtual void extend_peak_set();


};

class StackCrawPeakFinder : public BaseCrawPeakFinder {

public:
  typedef SlimCrawPeak stored_peak_t;
  vector<stored_peak_t> sps;
  virtual void peak_voodoo( int lh_valley, int rh_valley, int peak_loc );
  virtual void clear_sps();
  virtual int get_num_stored_peaks();
  virtual SlimCrawPeak * get_peak_ptr ( int idx );
  StackCrawPeakFinder () : BaseCrawPeakFinder() {};  
  StackCrawPeakFinder( CrawPeakMethod m ) : BaseCrawPeakFinder(m) {};
  //what a headache, this needs to be defined in each class due to sps vector being for
  //different types
  virtual void extend_peak_set();
  void extend_peak_set_weird ();

};

class CrawPeakFinderLocated : public CrawPeakFinder {
public:
  float current_mz;
  vector<float> rts;
  vector<mz_type> mzs;
  virtual CrawPeakLocated * construct_peak( int lh_valley , int rh_valley, int peak_loc );
  virtual void set_chrom (const vector<float> & f, int mz_idx);
  CrawPeakFinderLocated() {
  }
  CrawPeakFinderLocated( CrawPeakMethod m, const std::vector<float> & rts, 
      const std::vector<mz_type> & mzs ) : CrawPeakFinder(m) { 
    this->rts = rts;
    this->mzs = mzs;
  };
    

};

}
#endif
