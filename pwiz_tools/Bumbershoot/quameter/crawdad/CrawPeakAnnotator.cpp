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
#include "CrawPeakAnnotator.h"
#include "CrawPeakFinder.h"

namespace crawpeaks {

PeaksUsedOrNot::PeaksUsedOrNot() { 
         p= NULL;
         used = false;
	  }
PeaksUsedOrNot::PeaksUsedOrNot( SlimCrawPeakPtr * in_p ) {
	   p = in_p;
       used = false;
	  }
      float PeaksUsedOrNot::get_peak_rt() const {
          if ( p == NULL ) {
              return -1.0f;
          }
          else {
              return (float)(*p)->peak_rt_idx;
          }
      };
      float PeaksUsedOrNot::get_peak_height() const {
          if ( p == NULL ) {
              return -1.0f;
          }
          else {
              return (*p)->peak_height;
          }
      };
      float PeaksUsedOrNot::get_peak_raw_height() const {
          if ( p == NULL ) {
              return -1.0f;
          }
          else {
              return (*p)->raw_height;
          }
      };



peak_lists::peak_lists ( std::vector<SlimCrawPeakPtr> & h_peaks, std::vector<SlimCrawPeakPtr> & r_peaks ) {
           peaks_by_height = h_peaks; 
           peaks_by_rt = r_peaks;
        }
void peak_lists::resort_peaks() { 
    std::sort(peaks_by_height.begin(), peaks_by_height.end(), HeightCmp);
    std::sort(peaks_by_rt.begin(), peaks_by_rt.end(), StartRTCmp);
}

void peak_lists::clear_peaks_by_rts_its (  std::vector< std::vector<SlimCrawPeakPtr>::iterator > & rts_its ) {
/*
            for ( int i = 0 ; i < rts_its.size(); i++ ) {
                SlimCrawPeakPtr lp = *(rts_its.at(i));
                
                //SlimCrawPeakPtr  lp = *l;   
        
                std::vector<SlimCrawPeakPtr>::iterator height_lookup = 
                    std::lower_bound(peaks_by_height.begin(), 
                                     peaks_by_height.end(), lp, HeightCmp);
                peaks_by_height.erase(height_lookup);
                peaks_by_rt.erase(rts_its[i]);
            }
*/
}
        




void peak_lists::clear_peak ( SlimCrawPeakPtr & p ) {
  std::vector<SlimCrawPeakPtr>::iterator l = 
                std::lower_bound(peaks_by_height.begin(),
                                 peaks_by_height.end(), p,
                                 HeightCmp );
            if ( (*l)->peak_height != p->peak_height ) {
                throw("peak height must match");
            }
            peaks_by_height.erase(l);
  
            std::vector<SlimCrawPeakPtr>::iterator r = 
                std::lower_bound(peaks_by_rt.begin(),
                                 peaks_by_rt.end(), 
                                 p,
                                 StartRTCmp);
             if ( (*r)->start_rt_idx != p->start_rt_idx ) {
                throw("peak start_rt_idx must match");
            }
   
            peaks_by_rt.erase(r);
            
            
}

     PeakCoordSet::PeakCoordSet ( const std::vector<SlimCrawPeakPtr> & peaks ) {

        peak_start_idxs.resize(peaks.size());
        peak_peak_idxs.resize(peaks.size());
        peak_stop_idxs.resize(peaks.size());
        for ( int i = 0 ; i < (int)peaks.size() ; i++ ) {
          peak_start_idxs[i] = peaks[i]->start_rt_idx;
          peak_stop_idxs[i]  = peaks[i]->stop_rt_idx;
          peak_peak_idxs[i]  = peaks[i]->peak_rt_idx;
        }
    };
    ///returns the leftmost, and rightmost peak index which overlap with this peak
        std::pair<int,int> PeakCoordSet::find_overlap_bounds_by_peak_rt_idx ( SlimCrawPeakPtr & p ) {
        int rh_peak_idx = -1;
        
        int lh_peak_idx = crawutils::get_lh_idx(peak_peak_idxs,p->start_rt_idx) + 1;
        for ( rh_peak_idx = lh_peak_idx ; rh_peak_idx < (int)peak_peak_idxs.size() ; rh_peak_idx++ ) 
        {
            if ( p->stop_rt_idx < peak_peak_idxs[rh_peak_idx] ) {
              break;
            }
        }
        if ( rh_peak_idx == peak_peak_idxs.size() ) {
           rh_peak_idx--;
        }
        return std::pair<int,int>(lh_peak_idx,rh_peak_idx);

    }

CrawPeakAnnotator::CrawPeakAnnotator(void)
{
   init();
}

CrawPeakAnnotator::CrawPeakAnnotator(BaseCrawPeakFinder * pf) {
   this->pf = pf;
   init();
}
CrawPeakAnnotator::~CrawPeakAnnotator(void)
{
}


/*
void CrawPeakAnnotator::get_bg_subtracted_area ( SlimCrawPeak & peak, int start_idx, int stop_idx , float bgslope , float & bg_area, float & peak_area ) {
   double total_area;
   total_area = get_area(start_idx,stop_idx);
   int len = stop_idx - start_idx + 1;

   double bg_val = (*get_active_chrom())[start_idx];
      for ( int i = 0 ; i < len ; i++ ) {
          if ( (*(get_active_chrom()))[start_idx+i] < bg_val ) {
             bg_scratch[i] = (*get_active_chrom())[start_idx+i];
          }
          else {
             bg_scratch[i] = bg_val;
          }
          bg_val += bgslope;
      }
      bg_area = crawutils::area_under_curve( bg_scratch, 0, len - 1);
      peak_area = total_area - bg_area;
      
}
*/



int CrawPeakAnnotator::get_peakloc_in_range ( int start_idx, int stop_idx ) {
  if ( pf->method.peak_location_meth == GAUSS_2D_PEAK ) {
    return _get_peakloc_by_2d(start_idx,stop_idx);
  }
  else if ( pf->method.peak_location_meth == MAXIMUM_PEAK ) {
    return _get_peakloc_by_local_max(start_idx,stop_idx);
  }
  else if ( pf->method.peak_location_meth == CENTROID_PEAK ) {
    return _get_peakloc_by_centroid(start_idx,stop_idx);
  }
  else {
    throw("no legal peak location has been found");
  }
  
}


int CrawPeakAnnotator::_get_peakloc_by_2d ( int start_idx, int stop_idx ) {
  return crawutils::max_idx_vect_bound(pf->chrom_2d,start_idx,stop_idx);
}

int CrawPeakAnnotator::_get_peakloc_by_centroid ( int start_idx, int stop_idx ) {
   throw("not implemented");
}

int CrawPeakAnnotator::_get_peakloc_by_local_max ( int start_idx, int stop_idx ) {
  return crawutils::max_idx_vect_bound(*(get_active_chrom()),start_idx,stop_idx);
}

//ER - USE THIS TO JOIN PEAKS TOGETHER -
///adds the right-hand peak to the left-hand peak
///a version that returns a new peak would be useful too but that is low priority
void CrawPeakAnnotator::glom_peak ( SlimCrawPeak & changed, const SlimCrawPeak & to_glom ) {
    if ( to_glom.start_rt_idx > changed.start_rt_idx ) {
      changed.stop_rt_idx = to_glom.stop_rt_idx;
    }
    else {
      changed.start_rt_idx = to_glom.start_rt_idx;
    }
    changed.len = changed.stop_rt_idx - changed.start_rt_idx + 1;
    this->peak_tweak(changed, this->pf->method );
    this->set_peak_bg_subtracted_area(changed);
}





///merges peaks by:
///start with the heighest peak -
///find nearest peak
void CrawPeakAnnotator::merge_peaks_list_based ( std::vector<SlimCrawPeakPtr> & in_peaks, std::vector<SlimCrawPeakPtr> & out_peaks ) {

    if ( ! this->pf->method.merge_peaks_list_based ) {
        std::cerr << "not list-based merging"<< std::endl;
        out_peaks.resize(in_peaks.size());
        std::copy(in_peaks.begin(), in_peaks.end(), out_peaks.begin());
       return;
    }
        std::cerr << "list-based merging"<< std::endl;
     
    std::vector<SlimCrawPeakPtr> peaks_by_height(in_peaks.size()); 
    std::copy(in_peaks.begin(), in_peaks.end(), peaks_by_height.begin());
    //TODO -- switch to using priority queues - not enough time to test at the moment
    std::sort(in_peaks.begin(), in_peaks.end(), CompSlimCrawPeakPtrByStartRTIdx() );
    std::sort(peaks_by_height.begin(), peaks_by_height.end(), CompSlimCrawPeakPtrByHeight() );

    peak_lists plist_struct(peaks_by_height, in_peaks);
    //TODO -- sort out deleting peaks after they are merged...

    //delete the rts iterators
    std::vector<std::vector<SlimCrawPeakPtr>::iterator> height_its;
    std::vector<SlimCrawPeakPtr> peaks_to_clear;
    std::vector<std::vector<SlimCrawPeakPtr>::iterator> rts_its;

    while ( plist_struct.peaks_by_height.size() > 1 ) {
       
       SlimCrawPeakPtr & highest_p_ptr = plist_struct.peaks_by_height.back();
       std::vector<SlimCrawPeakPtr>::iterator lookup = 
                std::lower_bound(plist_struct.peaks_by_rt.begin(),
                plist_struct.peaks_by_rt.end(), 
                highest_p_ptr, CompSlimCrawPeakPtrByStartRTIdx() );
       if ( lookup == plist_struct.peaks_by_rt.end() ) {
         throw("should always find an exact RT peak match");
       }
       if ( ! ( (*lookup)->start_rt_idx == highest_p_ptr->start_rt_idx ) ) {
          throw("should have always find an exact match");
       }
       bool comp_lh, comp_rh;
       bool merge_lh, merge_rh;
       comp_lh = comp_rh = true;
       merge_lh = merge_rh = false;

       if ( lookup == plist_struct.peaks_by_rt.end() ) {
         throw("should always find an exact RT peak match");
       }

       if (  lookup == plist_struct.peaks_by_rt.begin() ) {
          comp_lh = false;
       }
       if ( lookup == (plist_struct.peaks_by_rt.end() - 1) ) {
          comp_rh = false;
       }
       ///you can merge with up to two adjacent peaks
       rts_its.push_back(lookup);
       peaks_to_clear.push_back(*lookup);
       if ( comp_lh ) {
           if ( this->susceptible_to_merge( *(lookup-1), *lookup ) ) {
              merge_lh = true;
              rts_its.push_back(lookup-1);
              peaks_to_clear.push_back(*(lookup-1));
           }
       }
       if ( comp_rh ) {
           if ( this->susceptible_to_merge( *lookup, *(lookup+1) ) ){
              merge_rh = true;
              rts_its.push_back(lookup+1);
              peaks_to_clear.push_back(*(lookup+1));
           }
       }
       if ( merge_lh ) {
          this->glom_peak( *(*lookup), *(*(lookup-1)) );
       }
       if ( merge_rh ) {
          this->glom_peak( *(*lookup), *(*(lookup+1)) );
       }
       //TODO inefficient..., we need to copy the peak after it is glommed, otherwise it is out of order in 
       //these cases...
       plist_struct.resort_peaks();
       //delete this peak from both vectors
       //delete each merged_peak from both vectors
       out_peaks.push_back(*lookup);
       //plist_struct.clear_peaks_by_rts_its( rts_its);
       for ( int i = 0 ; i < (int)peaks_to_clear.size() ; i++ ) {
         plist_struct.clear_peak(peaks_to_clear[i]);
       }

       rts_its.clear();
       peaks_to_clear.clear();


    }
    if ( plist_struct.peaks_by_height.size() ) {
       out_peaks.push_back(plist_struct.peaks_by_height[0]);
    }
    
}


bool CrawPeakAnnotator::susceptible_to_merge ( const SlimCrawPeakPtr & lhs, const SlimCrawPeakPtr & rhs ) {
  
    if ( abs(lhs->stop_rt_idx - rhs->start_rt_idx) > 1) {
        return false;
    }

    float max_height = std::max( lhs->peak_height , rhs->peak_height);
    float max_len    = (float)std::max( lhs->len, rhs->len );
    float one_peak_slope_constraint = (this->pf->method.one_peak_slope_merge_constraint * max_height) / max_len;
    //TODO calcs for mean_peak_slope_constraint in the line below do not make sense
    float mean_peak_slope_constraint = (this->pf->method.mean_slope_merge_constraint * max_height) / max_len;
    //opposite slopes are not a valid assumption, when merging two shoulders next to each other
 
    
  
    if ( (lhs->bgslope * rhs->bgslope) < 0 ){
       //at least one peak must have a high slope...
       if ( (one_peak_slope_constraint && ( fabs(lhs->bgslope) >= one_peak_slope_constraint || 
             fabs(rhs->bgslope) >= one_peak_slope_constraint ) ) ) {
          return true;
       }
       else if (  mean_peak_slope_constraint && 
           (  (  lhs->bgslope + rhs->bgslope ) / 2.0 ) >= mean_peak_slope_constraint )
           return true;
    }
    /* HACK BELOW - not configured with options   */

    else if ( lhs->raw_height < rhs->raw_height ) {
        if ( lhs->bgslope > 0 ) {
            if ( lhs->height_norm_slope() > 1 ) {
                return true;
            }
        }
    }
    else if ( lhs->raw_height > rhs->raw_height ) {
        if ( rhs->bgslope < 0 )
            if ( rhs->height_norm_slope() < 1 ) {
                return true;
            }
    }

    else {
       float peak_to_peak_slope = (rhs->peak_height - lhs->peak_height) / ( rhs->peak_rt_idx - lhs->peak_rt_idx);
       if ( rhs->height_norm_slope() > 1 ) {
           if ( peak_to_peak_slope * rhs->bgslope > 1 ) {
              return true;
           }
       }
       else if ( lhs->height_norm_slope() > 1 ) {
           if ( peak_to_peak_slope * lhs->bgslope > 1 ) {
              return true;
           }
       }
    }
    return false;
}

bool CrawPeakAnnotator::susceptible_to_merge_by_shape ( const SlimCrawPeakPtr & lhs, const SlimCrawPeakPtr & rhs ) {
   return false; //ie not done yet

}

void CrawPeakAnnotator::set_bg_scratch ( int start_idx, int stop_idx ) {
    int len = stop_idx - start_idx + 1;
    if ( this->pf->method.background_estimation_method == PEAK_BOUNDARY_ESTIMATE ) {
        float bgslope = calculate_slope(start_idx, stop_idx);
        double bg_val = get_active_chrom()->at(start_idx);
        for ( int i = 0 ; i < len ; i++ ) {
            if ( get_active_chrom()->at(start_idx+i) < bg_val ) {
                bg_scratch[i] = get_active_chrom()->at(start_idx+i);
            }
            else {
                bg_scratch[i] = (float)bg_val;
            }
            bg_val += bgslope;
        }
       
    }

    else if ( this->pf->method.background_estimation_method == LOWER_BOUNDARY ) {
        float lower_bound = std::min( get_active_chrom()->at(start_idx), get_active_chrom()->at(stop_idx) );
        for ( int i = 0 ; i < len ; i++ ) {
           bg_scratch[i] = lower_bound;
        }
    }
    else if ( this->pf->method.background_estimation_method == MEAN_BOUNDARY ) {
        float mean_bound = (get_active_chrom()->at(start_idx) + get_active_chrom()->at(stop_idx)) / 2;
        for ( int i = 0 ; i < len ; i++ )
           bg_scratch[i] = mean_bound;
    }

    else if ( this->pf->method.background_estimation_method == FIXED_BACKGROUND ) {
        if ( this->fixed_background == NULL ) {
            throw("must set a fixed background");
        }
        else {
            for ( int i = 0 ; i < len ; i++ ) {
               bg_scratch[i] = *fixed_background;
            }
        }
    }


    else {
        throw("unknown peak background estimate");
    }
}

/*! get_all_areas - calculate raw,background,and background-subtracted areas under a chromatogram, using
  techniques for 'CrawPeaks' as of 03/09. 
  Background (bg) is simply calculated as the area under the pointwise minimum of
    a) a line drawn from the raw values at start,stop or:
    b) the actual data (which may fall below the line in (a)
  Areas are calculated using trapezoids.
  
  /param start_idx - starting index to chromatogram
  /param stop_idx  - stop index to chromatogram
  /param peak_idx  - 'peak' index in chromatogram
  /param raw_area  - ref float for raw area under curve
  /param bg_area   - ref float for background area
  /param peak_area - ref float for raw - bg
  /param bg_sub_height - bg subtracted height
*/
void CrawPeakAnnotator::get_all_areas( int start_idx, int stop_idx, int peak_idx,
				      float & raw_area, float & bg_area , float & bg_sub_area , float & bg_sub_height, float & raw_height ) {

  raw_area = (float)get_area(start_idx,stop_idx);
  int len = stop_idx - start_idx + 1;
  if ( len > (int)bg_scratch.size() ) {
    bg_scratch.resize(len);
  }
  set_bg_scratch(start_idx, stop_idx);
  bg_sub_height = get_active_chrom()->at(peak_idx) - bg_scratch[peak_idx - start_idx];
  raw_height    = get_active_chrom()->at(peak_idx);
  bg_area = (float)crawutils::area_under_curve( bg_scratch, 0, len - 1);
  bg_sub_area = raw_area - bg_area;  
}

void CrawPeakAnnotator::set_peak_bg_subtracted_area ( SlimCrawPeak & peak ) {
  // float total_area;
  get_all_areas( peak.start_rt_idx, peak.stop_rt_idx, peak.peak_rt_idx,
		 peak.raw_area, peak.bg_area, peak.peak_area, peak.peak_height, peak.raw_height );

}

void CrawPeakAnnotator::calc_fwhm( SlimCrawPeak & peak ) {
    std::vector<float> * c = this->get_active_chrom();
    std::vector<float> & chrom = *c;
   float lh_height = chrom.at(peak.start_rt_idx);
   float rh_height = chrom.at(peak.stop_rt_idx);
   float height = peak.raw_height - std::min(lh_height,rh_height);
   float half_max = (float)(peak.raw_height - (height / 2.0));
   int lh_pt = - 1, rh_pt = -1;
   float lh_hm, rh_hm;
   for ( int i = peak.start_rt_idx ; i < peak.peak_rt_idx ; i++ ) {
       if ( chrom[i] <= half_max && chrom[i+1] >= half_max ) {
          lh_pt = i;
          break;
       }
   }
   for ( int i = peak.peak_rt_idx ; i < std::min(peak.stop_rt_idx, (int)(chrom.size() - 2)) ; i++ ) {
       if ( chrom[i] >= half_max && chrom[i+1] <= half_max ) {
         rh_pt = i;
         break;
       }
   }
   if ( lh_pt > -1 && rh_pt > -1 ) {
      peak.fwhm_calculated_ok = true;
   }
   else {
      peak.fwhm_calculated_ok = false;
   }

   if ( lh_pt == -1 ) {
       lh_hm = (float)peak.start_rt_idx;
   }
   else {
       float frac_delta = (half_max - chrom[lh_pt]) / (chrom[lh_pt+1] - chrom[lh_pt]);
       lh_hm = (float)lh_pt + frac_delta;      
   }
   if ( rh_pt == -1 ) {
       rh_hm = (float)peak.stop_rt_idx;
   }
   else {
       float frac_delta = (chrom[rh_pt] - half_max) / (chrom[rh_pt] - chrom[rh_pt+1]);
       rh_hm = (float)rh_pt + frac_delta;
   }
   peak.fwhm = rh_hm - lh_hm;
}

void CrawPeakAnnotator::refind_peak_peak( SlimCrawPeak & peak ) {
   peak.peak_rt_idx = get_peakloc_in_range( peak.start_rt_idx, peak.stop_rt_idx );
}

void CrawPeakAnnotator::reannotate_peak ( SlimCrawPeak & peak, int start_idx, int stop_idx ) {
  peak.start_rt_idx = start_idx;
  peak.stop_rt_idx  = stop_idx;
  peak.peak_rt_idx  = get_peakloc_in_range( start_idx,stop_idx );
  set_peak_bg_subtracted_area(peak);
  //update this to incorporate other functions
}

void CrawPeakAnnotator::peak_tweak ( SlimCrawPeak & peak, const CrawPeakMethod & m ) {
    if ( m.extend_to_zero_crossing ) {
       this->extend_to_zero_crossing( peak, m.fraction_to_valley );
    }
    else if ( m.extend_peak_to_lower_bound ) {
       this->extend_to_lower_boundary( peak, m.extend_allowed_asymmetry );
    }

}

void CrawPeakAnnotator::extend_to_zero_crossing ( SlimCrawPeak & peak , float perc_towards_valley) {
   ////advance until next chrom2d zero crossing from rh_boundary
 
   // if ( perc_towards_valley != 0.0f ) {
   //   throw("using this flag is not supported yet!");
   // }
 
   float c2d_intensity_at_rh_bound = this->pf->chrom_2d[peak.stop_rt_idx];
   float c2d_intensity_at_lh_bound = this->pf->chrom_2d[peak.start_rt_idx];   
   float rh_cutoff = c2d_intensity_at_rh_bound * perc_towards_valley;
   float lh_cutoff = c2d_intensity_at_lh_bound * perc_towards_valley;

   std::vector<int>::iterator f = std::lower_bound(this->pf->plus_crosses.begin(), this->pf->plus_crosses.end(), peak.stop_rt_idx );
   int plus_cross_to_right_idx  = f - this->pf->plus_crosses.begin();
   if ( plus_cross_to_right_idx == this->pf->plus_crosses.size() - 1 )  {
      //TODO -- extend out to last point?
   }
   else {
       plus_cross_to_right_idx++;
       for ( int i = peak.stop_rt_idx ; i <= this->pf->plus_crosses[plus_cross_to_right_idx] ; i++ ) {
           if ( this->pf->chrom_2d[i] < rh_cutoff ) {
              peak.stop_rt_idx = i-1;
           }
       }
   }

   int minus_cross_to_left_idx = std::lower_bound(this->pf->minus_crosses.begin(), this->pf->minus_crosses.end(), peak.start_rt_idx ) - this->pf->minus_crosses.begin();
   if ( minus_cross_to_left_idx == 0 ) {
      //TODO -- extend out to last point?
   }
   else {
       //plus_cross_to_left_idx--;
       for ( int i = peak.start_rt_idx ; i >= this->pf->minus_crosses[plus_cross_to_right_idx] ; i-- ) {
           if ( this->pf->chrom_2d[i] < lh_cutoff ) {
              peak.stop_rt_idx = i+1;
           }
       }
   }

}

void CrawPeakAnnotator::extend_to_lower_boundary ( SlimCrawPeak & peak , float allowed_asymmetry) {
   //1. find lower point
   //2. find farther end-point
   //3. extend higher point until either: lower point is reached,
 


   //get intensity at either edge...
   float intensity_at_lh_bound = this->pf->chrom[peak.start_rt_idx];
   float intensity_at_rh_bound = this->pf->chrom[peak.stop_rt_idx];

   ///fraction of the difference in intensity between the two peak edges to which the more intense one has to fall...
   float fraction_of_intensity_difference = 1.0f;
   
   int max_distance         = std::max( peak.peak_rt_idx - peak.start_rt_idx , peak.stop_rt_idx - peak.peak_rt_idx);
   int min_distance         = std::min( peak.peak_rt_idx - peak.start_rt_idx , peak.stop_rt_idx - peak.peak_rt_idx);
   int max_allowed_distance = (int)round(max_distance * allowed_asymmetry);
   int max_delta_from_edge  = max_allowed_distance - min_distance;
   
   if ( intensity_at_lh_bound < intensity_at_rh_bound ) {
      //extend bound rightwards until either max distance is reached, or
       int delta = 0; 
       for ( ; delta < max_delta_from_edge ; delta++ ) {
          int pos = peak.stop_rt_idx + delta;
          if ( this->pf->chrom[pos] <= intensity_at_lh_bound ) {
             break;
          }
       }
       peak.stop_rt_idx += delta; //changed peak boundary, what would I need to adjust now...?
   }

   if ( intensity_at_rh_bound < intensity_at_lh_bound ) {
      //extend bound leftwards until either max distance is reached, or
       int delta = 0; 
       for ( ; delta < max_delta_from_edge ; delta++ ) {
          int pos = peak.start_rt_idx - delta;
          if ( this->pf->chrom[pos] <= intensity_at_rh_bound ) {
             break;
          }
       }
       peak.start_rt_idx -= delta; //changed peak boundary, what would I need to adjust now...?
   }

   

}

void CrawPeakAnnotator::ratchet_back_to_frac_maxval( SlimCrawPeak & peak , float frac, float bg_level) {
    std::vector<float> * chrom = this->get_active_chrom();
    if ( frac > 1 ) { 
      throw("define the fraction ranging from 0 to 1");
    }
    float min_level = ( peak.raw_height - bg_level ) * frac;
    for (  int i = peak.start_rt_idx; i < peak.peak_rt_idx ;i++ ) {
        if ( chrom->at(i) >= min_level ) {
           peak.start_rt_idx = i;
           break;
        }
    }
    for (  int i = peak.stop_rt_idx; i > peak.peak_rt_idx ;i-- ) {
        if ( chrom->at(i) >= min_level ) {
           peak.stop_rt_idx = i;
           break;
        }
    }

}

void CrawPeakAnnotator::extend_to_1d_zero ( SlimCrawPeak & peak, bool start_at_peak ) {
  /*	if ( blip_len == - 1 ) {
      blip_len = this->pf->method.switch_len;
	}
  */
	int delta_to_lh, delta_to_rh;
    int lh_start, rh_start;
    if ( start_at_peak ) 
      lh_start = rh_start = peak.peak_rt_idx;
	else {
      lh_start = peak.start_rt_idx;
      rh_start = peak.stop_rt_idx;
	}

    //find crossing point in the 1st derivative
	std::vector<int> c1d_plus_crosses;
	std::vector<int> c1d_minus_crosses;

    this->pf->find_cross_points( this->pf->chrom_1d, c1d_plus_crosses , c1d_minus_crosses );

    /*ER BUGBUG
      PLACE WHERE PEAKS ARE EXTENDED USING 1st derivative.
      crossing points in chrom_1d are found above. 
    so you would need to limit the 'walk' out to the 1st derivative crossing point.
    The other option is to investigate what I was doing in the function below, local minimum.
    (perhaps walk out to the point where either the 1st deriv hits zero or the intensity falls below some factor of the peak
     intensity, like 0.05?.

    -- it may be best later on, after peak_tweak or whatever the heck it is is called, to trim peaks back, rather than rejecting them... hmmm..
*/
    //extend left hand
	if ( ! (lh_start < c1d_plus_crosses[0]) )  {
		std::vector<int>::const_iterator lh_it = std::lower_bound( c1d_plus_crosses.begin(), 
              c1d_plus_crosses.end(), lh_start);
        lh_it--;
		if ( lh_it == c1d_plus_crosses.end() ) {
		}
		int leftmost_c1d_plus_cross = *lh_it; 
        //since the cross is defined as going left-to-right, nudge to the other side
        leftmost_c1d_plus_cross++;
        delta_to_lh = lh_start - leftmost_c1d_plus_cross;
	}
	else {
        delta_to_lh = 0;
	}

#if 0    
    //extend right hand
	if ( ! (rh_start > c1d_plus_crosses.back() ) )  {
		std::vector<int>::const_iterator rh_it = std::lower_bound( c1d_plus_crosses.begin(), c1d_plus_crosses.end(), rh_start);
		if ( rh_it == c1d_plus_crosses.end() - 1 ) {
           
		}
		else {
           //rh_it++;
		}
		int rightmost_c1d_plus_cross = *(rh_it);
        delta_to_rh = rightmost_c1d_plus_cross - rh_start;
	}
	else {
        delta_to_rh = 0;
	}
#endif // 0
    //extend right hand
	if ( ! (rh_start > c1d_plus_crosses.back() ) )  {
		std::vector<int>::const_iterator rh_it = std::lower_bound( c1d_plus_crosses.begin(), c1d_plus_crosses.end(), rh_start);
		if ( rh_it == c1d_plus_crosses.end() - 1 ) {
           
		}
		else {
           //rh_it++;
		}
		int rightmost_c1d_plus_cross = *(rh_it);
        delta_to_rh = rightmost_c1d_plus_cross - rh_start;
	}
	else {
        delta_to_rh = 0;
	}



    //for now, extend boundaries by delta_to_lh, delta_to_rh - note that we can also use those to enforce some peak asymmetry
  
	if ( start_at_peak ) {
        peak.start_rt_idx = peak.peak_rt_idx - delta_to_lh;
        peak.stop_rt_idx  = peak.peak_rt_idx + delta_to_rh;
	}
	else {
		peak.start_rt_idx -= delta_to_lh;
		peak.stop_rt_idx  += delta_to_rh;
	}
    //this->reannotate_peak(peak,peak.start_rt_idx,peak.stop_rt_idx);

}
void CrawPeakAnnotator::extend_to_1d_zero_local_minimum( SlimCrawPeak & peak,  bool start_at_peak) {
	int delta_to_lh, delta_to_rh;
    int lh_start, rh_start;
    if ( start_at_peak ) 
      lh_start = rh_start = peak.peak_rt_idx;
	else {
      lh_start = peak.start_rt_idx;
      rh_start = peak.stop_rt_idx;
	}

    //find crossing point in the 1st derivative
	std::vector<int> c1d_plus_crosses;
	std::vector<int> c1d_minus_crosses;

    this->pf->find_cross_points( this->pf->chrom_1d, c1d_plus_crosses , c1d_minus_crosses );

    //extend left hand
   
    int peak_intensity = (int)this->active_chrom->at(peak.peak_rt_idx);
    int last_minimum_intensity = peak_intensity;
   
	if ( ! (lh_start < c1d_plus_crosses[0]) )  {
		std::vector<int>::const_iterator lh_it = std::lower_bound( c1d_plus_crosses.begin(), c1d_plus_crosses.end(), lh_start);
		if ( lh_it == c1d_plus_crosses.end() ) {
            throw("makes no sense");
		}
   
        //walk leftwards
		do {
			if ( lh_it == c1d_plus_crosses.begin() ) {
               break;
			}
            int this_valley_intensity = (int)this->active_chrom->at(*lh_it);
			if ( this_valley_intensity > last_minimum_intensity ) {
                 //the last valley was the local minimum
                 lh_it++;
                 break;
			}
            last_minimum_intensity = this_valley_intensity;
            lh_it--;
		} while ( lh_it != c1d_plus_crosses.begin() );

		int leftmost_c1d_plus_cross = *lh_it; 
        //since the cross is defined as going left-to-right, nudge to the other side
        leftmost_c1d_plus_cross++;
        delta_to_lh = lh_start - leftmost_c1d_plus_cross;
	}
	else {
        delta_to_lh = 0;
	}
    
    //extend right hand
	last_minimum_intensity = peak_intensity;

	if ( ! (rh_start > c1d_minus_crosses.back() ) )  {
		std::vector<int>::const_iterator rh_it = std::lower_bound( c1d_minus_crosses.begin(), c1d_minus_crosses.end(), rh_start);
		if ( rh_it == c1d_minus_crosses.end() - 1 ) {
           
		}

		do {
			if ( rh_it == c1d_minus_crosses.end() ) {
               rh_it--;
               break;
			}
            int this_valley_intensity = (int)this->active_chrom->at(*rh_it);
			if ( this_valley_intensity > last_minimum_intensity ) {
                 //the last valley was the local minimum
                 rh_it--;
                 break;
			}
            last_minimum_intensity = this_valley_intensity;
            rh_it++;
		} while ( rh_it != c1d_plus_crosses.end() );

		int rightmost_c1d_minus_cross = *(rh_it);
        delta_to_rh = rightmost_c1d_minus_cross - rh_start;
	}
	else {
        delta_to_rh = 0;
	}

    //for now, extend boundaries by delta_to_lh, delta_to_rh - note that we can also use those to enforce some peak asymmetry
  
	if ( start_at_peak ) {
        peak.start_rt_idx = peak.peak_rt_idx - delta_to_lh;
        peak.stop_rt_idx  = peak.peak_rt_idx + delta_to_rh;
	}
	else {
		peak.start_rt_idx -= delta_to_lh;
		peak.stop_rt_idx  += delta_to_rh;
	}
    //this->reannotate_peak(peak,peak.start_rt_idx,peak.stop_rt_idx);


}


void CrawPeakAnnotator::extend_peak_set ( const std::vector<SlimCrawPeakPtr> & in_peaks, std::vector<SlimCrawPeakPtr> & out_peaks, bool start_at_peak ) {

	if ( in_peaks.size() == 0  ) {
	   return;
	}

	std::vector<SlimCrawPeakPtr> peak_ptrs_by_rt(in_peaks.begin(), in_peaks.end());
	//std::cerr << peak_ptrs_by_rt.size() << std::endl;

	std::sort(peak_ptrs_by_rt.begin(),peak_ptrs_by_rt.end(), CompSlimCrawPeakPtrByPeakRTIdx() );
	std::vector<PeaksUsedOrNot*> peaks_by_rt;
	for ( int i = 0 ; i < (int)peak_ptrs_by_rt.size(); i++ ) {
        PeaksUsedOrNot * p = new PeaksUsedOrNot( &(peak_ptrs_by_rt[i]) );
	    peaks_by_rt.push_back(p);
	}
    std::vector<PeaksUsedOrNot*> peaks_by_I(peaks_by_rt.size());
    std::copy(peaks_by_rt.begin(), peaks_by_rt.end(), peaks_by_I.begin());
    std::sort(peaks_by_I.begin(), peaks_by_I.end(), CompPeaksUsedOrNotDumbPtrByPeakRawHeightIdx() );
    PeakCoordSet peak_coords(peak_ptrs_by_rt);
	std::vector<PeaksUsedOrNot*>::iterator intensity_iterator = peaks_by_I.end();
	do {
       intensity_iterator--;
	   if ( (*intensity_iterator)->used  ) {
          continue;
          //TODO check behavior of this in debugger, see if while condition is checked
	   }
	   else {
          //extend peak
          //iterator->SlimCrawPeakPtr->SlimCrawPeak. blech.
          this->extend_to_1d_zero( *(*(**intensity_iterator).p), this->pf->method.extend_from_peak_rt );
          if ( this->pf->method.ratchet_back_to_frac_maxval > -1 ) { 
              this->ratchet_back_to_frac_maxval( *(*(**intensity_iterator).p) , 
                 this->pf->method.ratchet_back_to_frac_maxval );
          }
          //exclude overlappers -- find peaks from my start to my end
          //TODO -- deal with overlappers... or convert to linear walk algorithm
          //safe, since push_back will copy the dereferenced SlimCrawPeakPtr
          if ( this->pf->method.exclude_extension_overlaps_by_peakrt ) {
              mark_overlaps_by_peak_rt( peaks_by_rt, peak_coords, *(*intensity_iterator));
          }

          out_peaks.push_back(*((**intensity_iterator).p));
          (**intensity_iterator).used = true;
          
	   }
	} while ( ! (intensity_iterator == peaks_by_I.begin() ) );


}

//assumes peaks are sorted by peakrt
void CrawPeakAnnotator::mark_overlaps_by_peak_rt( std::vector<PeaksUsedOrNot*> & peaks, PeakCoordSet & peak_set,PeaksUsedOrNot & p ) 
{
    //gawd so much de-referencing
    std::pair<int,int> overlap_bounds = peak_set.find_overlap_bounds_by_peak_rt_idx(*(p.p));
    for ( int i = overlap_bounds.first ; i <= overlap_bounds.second; i++ ) {
        peaks[i]->used = true;
    }
   
}



double CrawPeakAnnotator::get_raw_area( SlimCrawPeak & peak ) {
   double total_area = get_area(peak.start_rt_idx, peak.stop_rt_idx);
   return total_area;
}

double CrawPeakAnnotator::get_raw_area( int start_rt_idx, int stop_rt_idx ){
   return get_area( start_rt_idx, stop_rt_idx);
}

void CrawPeakAnnotator::set_peak_slope ( SlimCrawPeak  & peak ) {
   float slope = calculate_slope(peak.start_rt_idx , peak.stop_rt_idx);
   peak.bgslope = slope;
}

float CrawPeakAnnotator::calculate_slope( int start_rt_idx, int stop_rt_idx) {
    int len = this->calc_len(start_rt_idx, stop_rt_idx);
    float delta = get_active_chrom()->at(stop_rt_idx) - get_active_chrom()->at(start_rt_idx);
    float d = (float) (len - 1);
    return delta / d;
    
  }

double CrawPeakAnnotator::get_area( int start_rt_idx, int stop_rt_idx ) {
   return crawutils::area_under_curve( *(get_active_chrom()) , start_rt_idx, stop_rt_idx );
}

};
