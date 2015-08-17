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

#ifndef _SUPPEAKANNOTATOR_H
#define _SUPPEAKANNOTATOR_H

namespace crawpeaks {

class CrawPeakAnnotator;
class BaseCrawPeakFinder;
class CrawPeak;
class CrawPeakMethod;

}

#include <vector>
#include "crawutils.H"
#include "CrawPeak.H"

#include "CrawPeakMethod.H"
#include "CrawPeak.H"
#define BGSCRATCH_START_SIZE 512

namespace crawpeaks {

struct PeakCoordSet {
    std::vector<int> peak_start_idxs;
    std::vector<int> peak_peak_idxs;
    std::vector<int> peak_stop_idxs;
PeakCoordSet ( const std::vector<SlimCrawPeakPtr> & peaks ) ;
    std::pair<int,int> find_overlap_bounds_by_peak_rt_idx ( SlimCrawPeakPtr & p ) ;
   
};


	struct PeaksUsedOrNot {
      SlimCrawPeakPtr * p;
      bool used;
      	  PeaksUsedOrNot();
	  PeaksUsedOrNot( SlimCrawPeakPtr * in_p );
      float get_peak_rt() const;
      float get_peak_height() const ;
      float get_peak_raw_height() const ;

	};

    struct CompPeaksUsedOrNotDumbPtrByPeakRtIdx {
        bool operator() ( const PeaksUsedOrNot * lh,const PeaksUsedOrNot * rh ) {
            return lh->get_peak_rt() < rh->get_peak_rt();
        }
    };

  struct CompPeaksUsedOrNotDumbPtrByPeakHeightIdx {
        bool operator() ( const PeaksUsedOrNot * lh,const PeaksUsedOrNot * rh ) {
            return lh->get_peak_rt() < rh->get_peak_height();
        }
    };

  struct CompPeaksUsedOrNotDumbPtrByPeakRawHeightIdx {
        bool operator() ( const PeaksUsedOrNot * lh,const PeaksUsedOrNot * rh ) {
            return lh->get_peak_raw_height() < rh->get_peak_raw_height();
        }
    };

    class peak_lists {
         
    public:
    
        //typedef std::vector<SlimCrawPeakPtr> std::vector<SlimCrawPeakPtr>; 

        std::vector<SlimCrawPeakPtr> peaks_by_height;
        std::vector<SlimCrawPeakPtr> peaks_by_rt;
        CompSlimCrawPeakPtrByHeight HeightCmp;
        CompSlimCrawPeakPtrByStartRTIdx StartRTCmp;

        peak_lists ( std::vector<SlimCrawPeakPtr> & h_peaks, std::vector<SlimCrawPeakPtr> & r_peaks );
void resort_peaks();
        void clear_peaks_by_rts_its (  std::vector< std::vector<SlimCrawPeakPtr>::iterator > & rts_its ) ;

        




        void clear_peak ( SlimCrawPeakPtr & p );
    
    };


struct GaussPeakGenerator {
   GaussSmoother gs;
   std::vector<float> generate_peak ( int fwhm );
};

struct GaussPeakTailingGenerator {
    GaussSmoother gs;
    std::vector<float> generate_peak ( int fwhm );
};


class CrawPeakAnnotator
{
public:

    CrawPeakAnnotator(void);
    CrawPeakAnnotator(BaseCrawPeakFinder * pf);
    ~CrawPeakAnnotator(void);
  BaseCrawPeakFinder * pf;
  std::vector<float> * active_chrom;
  std::vector<float> bg_scratch;
  float * fixed_background;
  void set_active_chrom ( std::vector<float> * ac ) {
     active_chrom = ac;
  };
  inline std::vector<float> * get_active_chrom() {
     return active_chrom;
  };
  inline void init() {
     bg_scratch.resize(BGSCRATCH_START_SIZE);
     fixed_background = NULL;
  };
  //void get_sig_bg_areas ( const std::vector<float> & raw , std::vector<float> & scratch);
void set_peak_bg_subtracted_area ( SlimCrawPeak & peak );
void refind_peak_peak( SlimCrawPeak & peak );
  void reannotate_peak ( SlimCrawPeak & peak , int new_start_idx, int new_stop_idx );
void reannotate_peak_soft( SlimCrawPeak & peak, int start_idx, int stop_idx );
void peak_tweak ( SlimCrawPeak & peak, const CrawPeakMethod & m );

  /*! Calculation of peak, bg areas, bg_subtracted_peak_height for an arbitrary region using the suppeak algorithm as of
    03/09 */
  
  int get_peakloc_in_range ( int start_idx, int stop_idx );

void get_all_areas( int start_idx, int stop_idx, int peak_idx,
		    float & raw_area, float & bg_area , float & bg_sub_area , float & bg_sub_height, float & raw_height );

void extend_to_lower_boundary ( SlimCrawPeak & peak , float allowed_asymmetry = 1.0f);

void extend_to_zero_crossing ( SlimCrawPeak & peak , float fraction_to_valley );

void extend_to_1d_zero ( SlimCrawPeak & peak , bool start_at_peak = false);
void extend_to_1d_zero_local_minimum( SlimCrawPeak & peak,  bool start_at_peak = false);

void ratchet_back_to_frac_maxval( SlimCrawPeak & peak, float frac = 0.01f, float bg_level = 0.0f);

///this takes the set of peaks and extends them, based on potential shoulder peaks which have odd alignments
void extend_peak_set ( const std::vector<SlimCrawPeakPtr> & in_peaks, std::vector<SlimCrawPeakPtr> & out_peaks,  bool start_at_peak= false );

///based on the determination to merge peaks, join to_glom to changed
void glom_peak ( SlimCrawPeak & changed, const SlimCrawPeak & to_glom );


///merge peaks if lhs heuristics about slopes apply
bool susceptible_to_merge ( const SlimCrawPeakPtr & lhs, const SlimCrawPeakPtr & rhs );
///not implemented yet -- plan is to check the similarity of peaks by a correlation after normalizing for size
bool susceptible_to_merge_by_shape ( const SlimCrawPeakPtr & lhs, const SlimCrawPeakPtr & rhs );

///merge peaks based on adjacent peaks with similar slope patterns
void merge_peaks_list_based ( std::vector<SlimCrawPeakPtr> & in_peaks, std::vector<SlimCrawPeakPtr> & out_peaks );
///An unimplemented linear walk version of the above
void merge_peaks_walk_based ( std::vector<SlimCrawPeakPtr> & in_peaks, std::vector<SlimCrawPeakPtr> & out_peaks );

void mark_overlaps_by_peak_rt ( std::vector<PeaksUsedOrNot*> & peaks,
                                PeakCoordSet & peak_set,
                                PeaksUsedOrNot & p );

double get_raw_area( SlimCrawPeak & peak );
double get_raw_area( int start_rt_idx, int stop_rt_idx );


double get_area( int start_idx, int stop_idx );
double get_area_f( float start_idx, float stop_idx );
float calculate_slope(int start_rt_idx, int stop_rt_idx);
void set_peak_slope ( SlimCrawPeak & peak );
void set_bg_scratch ( int start_idx, int stop_idx );

void calc_fwhm( SlimCrawPeak & peak );

#if 0
  void set_peak_positions ( SlimCrawPeak & peak ) {

 //relate to peak locations, boundaries (should be one method)
    refind_peak_peak(peak);
    peak_tweak( peak, this->method );
  };
#endif // 0
  //summary method for methods relating to quantitating peaks.
  void peak_annotate ( SlimCrawPeak & peak ) {
    set_peak_slope(peak);
    //calls set_peak_areas onpeak member variables
    set_peak_bg_subtracted_area(peak);
    calc_fwhm(peak);
  };


private :
int calc_len ( int start_rt_idx, int stop_rt_idx ) {
  return stop_rt_idx - start_rt_idx + 1;
}

void calc_fwhm_extents(int start_idx, int stop_idx, int peak_idx, float raw_height,
    float & lh_hm, float & rh_hm, bool & calculated_ok);

  int _get_peakloc_by_2d ( int start_idx, int stop_idx );
  int _get_peakloc_by_centroid ( int start_idx, int stop_idx );
  int _get_peakloc_by_local_max ( int start_idx, int stop_idx );
 





};

};

#endif
