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
#include "CrawPeakFinder.h"
#include "CrawPeakMethod.h"
#include "CrawPeak.h"
#include <math.h>
#include <iostream>


namespace crawpeaks {

///is the data >= a threshold over a certain length 
/*!
@param c - the vector to search over
@param idx - start index to search
@param threshold - threshold value to compare againts
@param len - starting from idx, search len slots in c to check that
values are gte than ("greater than or equal to") the threshold
*/
bool BaseCrawPeakFinder::consistent_gte( const std::vector<float> & c , int idx,float threshold, int len ) {
  //find the maximum point to search through the data
    int rh_bound = std::min((int)c.size(),idx+len);
    for ( int i = idx ;  i < rh_bound ; i++ ) {
      if ( c[i] < threshold ) 
	return false;
    }
    return true;
}


///is the data lte a threshold over a certain length 
/*!
@param c - the vector to search over
@param idx - start index to search
@param threshold - threshold value to compare againts
@param len - starting from idx, search len slots in c to check that
values are lte than threshold
*/
bool BaseCrawPeakFinder::consistent_lte( const std::vector<float> & c , int idx, float threshold, int len ) {
    int rh_bound = std::min((int)c.size(),idx+len);
    for ( int i = idx ;  i < rh_bound ; i++ ) {
        if ( c[i] > threshold ) return false;
    }
    return true;
}

///finds points where a data series crosses a threshold value,
///populates plus_ and minus_ crosses
/*!
@param c - vector to search
@param threshold - threshold value to search for crossing 
@post - plus_crosses is populated with indices where the second
///derivative crosses the threshold line going upwards. Minus crosses
///takes the opposite value
*/

void BaseCrawPeakFinder::find_cross_points(std::vector<float> &c , std::vector<int> & plus_crosses_local,
          std::vector<int> & minus_crosses_local, float threshold  ) {


   // Preallocate enough space to keep from doing slow push_backs
   plus_crosses_local.resize(c.size());
   minus_crosses_local.resize(c.size());

   int iPlus = 0, iMinus = 0;
   for ( int i = 0 ; i < (int)c.size() - (method.switch_len - 1) ; i++ ) {
       if ( c[i] < threshold && this->consistent_gte( c, i+1, threshold, method.switch_len ) ) {
          plus_crosses_local[iPlus++] = i;
       }
       else if ( c[i] > threshold && this->consistent_lte( c , i+1, threshold, method.switch_len ) ) {
          minus_crosses_local[iMinus++] = i;
       }
   }

   plus_crosses_local.resize(iPlus);
   minus_crosses_local.resize(iMinus);

   #ifdef GFDEBUG
   std::vector<PlusMinusCross> pms(iPlus+iMinus);
   for ( int i = 0 ; i < iPlus ; i++  ) {
     pms[i] = PlusMinusCross(plus_crosses[i],'p');
   }
   for ( int i = 0 ; i < iMinus ; i++  ) {
     pms[i+iPlus] = PlusMinusCross(minus_crosses[i],'m');
   }
   std::sort(pms.begin(),pms.end());
   
   std::ofstream cp_o("cross_points.txt");
   for ( int i = 0 ; i < pms.size() ; i++ ) {
     cp_o << pms[i].idx << "\t" << pms[i].t << std::endl;
   }
   cp_o.close();
   #endif


}

BaseCrawPeakFinder::~BaseCrawPeakFinder () {
     if ( gs_0d != NULL )
       delete(gs_0d);
     if ( gs_1d != NULL )
       delete(gs_1d);
     if ( gs_2d != NULL )
       delete(gs_2d);
  }



void BaseCrawPeakFinder::find_cross_points(std::vector<float> &c , float threshold  ) {
	find_cross_points(c,this->plus_crosses, this->minus_crosses, threshold);
}
/*! Finds maxima and minima between crossing points of a signal as described in find_cross_points.
    sets 'peaks' and 'valleys' in the 2D signal in the peaks, valleys
    values

    TODO : this could be sped up

*/
class PVIndexType
{
public:
    PVIndexType()
    {
        index = -1;
        type = '\0';
    }

    int index;
    char type;

/*
 * a simple copy operation, using the = operator
 */
	PVIndexType& operator=(const PVIndexType &rhs)	{
		index = rhs.index;
		type = rhs.type;
		return *this;
	}
};

static bool lessThanIndex(const PVIndexType &l,const PVIndexType &r)
{
    return (l.index < r.index);
}


void BaseCrawPeakFinder::peaks_and_valleys_fast( std::vector<float> & c ) {

  
  assert( abs(int( minus_crosses.size() - plus_crosses.size() ) ) <= 1);
  
  peaks.reserve(plus_crosses.size());
  valleys.reserve(minus_crosses.size());
  
  if ( minus_crosses[0] < plus_crosses[0] ) {
    int min_idx = crawutils::min_idx_vect_bound( c, minus_crosses[0], plus_crosses[0] );
    valleys.push_back(min_idx);
    int mc_idx = 1;
    int pl_idx = 0;
    while ( true ) {
      int max_idx = crawutils::max_idx_vect_bound ( c, plus_crosses.at(pl_idx), minus_crosses.at(mc_idx) );
      peaks.push_back(max_idx);
      pl_idx++;
      int min_idx = crawutils::min_idx_vect_bound ( c, minus_crosses.at(mc_idx), plus_crosses.at(pl_idx) );
      valleys.push_back(min_idx);
      mc_idx++;
      
      if ( mc_idx >= (int)minus_crosses.size() - 2 ) {
	if ( pl_idx <= (int)plus_crosses.size() ) {
	  int max_idx = crawutils::max_idx_vect_bound ( c, plus_crosses.at(pl_idx), minus_crosses.at(mc_idx) );
	  peaks.push_back(max_idx);
	}
	break;
      }
    }

  }
  if ( plus_crosses[0] < minus_crosses[0] ) {
    int max_idx = crawutils::max_idx_vect_bound( c, plus_crosses[0], minus_crosses[0] );
    peaks.push_back(max_idx);
    int mc_idx = 0;
    int pl_idx = 1;
    while ( true ) {

      int min_idx = crawutils::min_idx_vect_bound ( c, minus_crosses.at(mc_idx), plus_crosses.at(pl_idx) );
      valleys.push_back(min_idx);
      mc_idx++;
      int max_idx = crawutils::max_idx_vect_bound ( c, plus_crosses.at(pl_idx), minus_crosses.at(mc_idx) );
      peaks.push_back(max_idx);
      pl_idx++;

      if ( pl_idx >= (int)plus_crosses.size() - 2 ) {
	if ( mc_idx <= (int)minus_crosses.size() ) {
	  int min_idx = crawutils::min_idx_vect_bound ( c, minus_crosses.at(mc_idx), plus_crosses.at(pl_idx) );
	  valleys.push_back(min_idx);
	}
	break;
      }

    }

  }
  //adding a valley at the end of the chrom
  valleys.push_back(c.size() - 1);
  
#ifdef GFDEBUG
    std::ofstream ps("peaks.tab");
    crawutils::output_vector(ps,peaks,'\n');
    ps.close();
    std::ofstream tr("valleys.tab");
    crawutils::output_vector(tr,valleys,'\n');
    tr.close();
#endif

  
}


void BaseCrawPeakFinder::peaks_and_valleys_brendanx( std::vector<float> & c ) {

    int iMinus = 0, lenMinus = (int)minus_crosses.size();
    int iPlus = 0, lenPlus = (int)plus_crosses.size();
    int i = 0;

    /* Merge sorted lists of plus and minus crosses */
    /* + and - always have to alternate, since we define in terms of crossing one line, although we have the rule that they can't pass
         over one gap point apart  */
    std::vector<PVIndexType> indexes(lenMinus + lenPlus);
    PVIndexType indexType;
    while (iMinus < lenMinus || iPlus < lenPlus) {
        if (iMinus >= lenMinus ||
                (iPlus < lenPlus && plus_crosses[iPlus] < minus_crosses[iMinus])) {
            // Ignore consecutive plus crosses (fix so this can be an assert)
            if (i != 0 && indexes[i - 1].type == 'p')
            {
                iPlus++;
                continue;
            }
            indexType.type = 'p';
            indexType.index = plus_crosses[iPlus++];
        }
        else {
            // Ignore consecutive minus crosses (fix so this can be an assert)
            if (i != 0 && indexes[i - 1].type == 'm')
            {
                iMinus++;
                continue;
            }
            indexType.type = 'm';
            indexType.index = minus_crosses[iMinus++];
        }
        indexes[i++] = indexType;
    }
    indexes.resize(i);

    valleys.resize(lenMinus + 2);
    int iValley = 0;
    peaks.resize(lenPlus);
    int iPeak = 0;

    //valley at the beginning of the chrom
    int lenIndexes = (int)indexes.size();
    if (lenIndexes == 0 || indexes[0].type == 'p')
        valleys[iValley++] = 0;
    if (lenIndexes > 0) {
    for ( int i = 0 ; i < lenIndexes - 1 ; i++ ) {
      char trans_type = indexes[i].type;

      if ( trans_type == 'p' ) {
          int max_idx = crawutils::max_idx_vect_bound( c, indexes[i].index, indexes[i+1].index );
          peaks[iPeak++] = max_idx;
          //find index of maximum value spanning from all_trans[idx] to all_trans[idx+1]
      }
      else if ( trans_type == 'm' ) {
          //find index of minimum value spanning from all_trans[idx] to all_trans[idx+1]
          int min_idx = crawutils::min_idx_vect_bound( c, indexes[i].index, indexes[i+1].index );
          valleys[iValley++] = min_idx;
      }
    }
    //adding a valley at the end of the chromatogram
    if (iValley == iPeak)
        valleys[iValley++] = c.size()-1;
    // Make sure the arrays are the right size
    valleys.resize(iValley);
    peaks.resize(iPeak);
  }
#ifdef GFDEBUG
    std::ofstream ps("peaks.tab");
    crawutils::output_vector(ps,peaks,'\n');
    ps.close();
    std::ofstream tr("valleys.tab");
    crawutils::output_vector(tr,valleys,'\n');
    tr.close();
#endif
}

void BaseCrawPeakFinder::peaks_and_valleys_slow( std::vector<float> & c ) {
    
    std::map<int,char  > all_trans;
    
    
    /* + and - always have to alternate, since we define in terms of crossing one line, although we have the rule that they can't pass
         over one gap point apart  */
    std::vector<int> keys;
    //valley at the beginning of the chrom
    valleys.push_back(0);
    for ( int i = 0 ; i < (int)minus_crosses.size() ; i++ ) {
      int k = minus_crosses[i];
      all_trans[k] = 'm';
      keys.push_back(k);
  
    }
    for ( int i = 0 ; i < (int)plus_crosses.size() ; i++ ) {
      int k = plus_crosses[i];
      all_trans[k] = 'p';
      keys.push_back(k);
    }

    if ( keys.size() > 0 ) {
    std::sort(keys.begin(),keys.end());
    for ( int i = 0 ; i < (int)keys.size() - 1 ; i++ ) {
      int idx = keys[i];
      char trans_type = all_trans[idx];

      if ( trans_type == 'p' ) {
          int max_idx = crawutils::max_idx_vect_bound( c, idx, keys[i+1] );
          peaks.push_back(max_idx);
          //find index of maximum value spanning from all_trans[idx] to all_trans[idx+1]
      }
      else if ( trans_type == 'm' ) {
          //find index of minimum value spanning from all_trans[idx] to all_trans[idx+1]
          int min_idx = crawutils::min_idx_vect_bound( c, idx, keys[i+1] );
          valleys.push_back(min_idx);
      }
    }
    //adding a valley at the end of the chromatogram
    valleys.push_back(c.size()-1);
  }

#ifdef GFDEBUG
    std::ofstream ps("peaks.tab");
    crawutils::output_vector(ps,peaks,'\n');
    ps.close();
    std::ofstream tr("valleys.tab");
    crawutils::output_vector(tr,valleys,'\n');
    tr.close();
#endif
    
    
}


void BaseCrawPeakFinder::get_2d_chrom( const std::vector<float> & in_chrom,
				       std::vector<float> & out_chrom ) {
  if ( ! this->method.saved_weights ) {
    set_weights();
  }
  gs_2d->smooth_vect( in_chrom, out_chrom);
}

void BaseCrawPeakFinder::get_2d_chrom( std::vector<float> & in_chrom ) {
  if ( ! this->method.saved_weights ) {
    set_weights();
  }
  gs_2d->smooth_vect( in_chrom );
}

void BaseCrawPeakFinder::get_1d_chrom( const std::vector<float> & in_chrom,
				       std::vector<float> & out_chrom ) {
  if ( ! this->method.saved_weights ) {
    set_weights();
  }
  gs_1d->smooth_vect( in_chrom, out_chrom);
}

void BaseCrawPeakFinder::get_0d_chrom( std::vector<float> & in_chrom ) {
  if ( ! this->method.saved_weights ) {
    set_weights();
  }
  gs_0d->smooth_vect ( in_chrom );
}
void BaseCrawPeakFinder::get_0d_chrom( const std::vector<float> & in_chrom,
				       std::vector<float> & out_chrom ) {
  if ( ! this->method.saved_weights ) {
    set_weights();
  }
  gs_0d->smooth_vect( in_chrom, out_chrom);
}

void BaseCrawPeakFinder::get_1d_chrom( std::vector<float> & in_chrom ) {
  if ( ! this->method.saved_weights ) {
    set_weights();
  }
  gs_1d->smooth_vect ( in_chrom );
}



void BaseCrawPeakFinder::set_weights_2d() {
  if ( gs_2d == NULL ) {
    this->init_smoothers();
  }
  gs_2d->set_gauss_weights( method.get_sd(), 2 );
  this->method.saved_weights = true;
  gs_2d->invert_weights();
  gs_2d->trim_weights_by_frac_max(0.005f);
}

void BaseCrawPeakFinder::set_weights_1d() {
  if ( gs_1d == NULL ) {
    this->init_smoothers();
  }
  gs_1d->set_gauss_weights( method.get_sd(), 1 );
  this->method.saved_weights = true;
  gs_1d->trim_weights_by_frac_max(0.005f);
}

void BaseCrawPeakFinder::set_weights_0d() {
  if ( gs_0d == NULL ) {
    this->init_smoothers();
  }
  gs_0d->set_gauss_weights( method.get_sd(), 0 );
  this->method.saved_weights = true;
  gs_0d->trim_weights_by_frac_max(0.005f);
}


void BaseCrawPeakFinder::call_peaks() {
  
    if ( this->gs_2d == NULL ) {
      this->init_smoothers();
    }

    if ( this->chrom.size() == 0 ) {
      throw("Come on! Set a chromatogram with set_chrom!");
    }
  if ( ! this->method.saved_weights ) {
    set_weights();
  }


#ifdef GFDEBUG
//   vector<float> w = gs.get_weights();
//   std::ofstream gst;
//   gst.open("gs_weights.tab");
//   crawutils::output_vector(gst,w);
//   gst.close();
#endif

  //std::vector<float> smooth_chrom = chrom;
  chrom_2d.resize(chrom.size());
  chrom_1d.resize(chrom.size());
  chrom_0d.resize(chrom.size());

  std::copy(chrom.begin(), chrom.end(), chrom_2d.begin());
  std::copy(chrom.begin(), chrom.end(), chrom_1d.begin());
  std::copy(chrom.begin(), chrom.end(), chrom_0d.begin());

  gs_2d->smooth_vect(chrom_2d);
  gs_1d->smooth_vect(chrom_1d);
  gs_0d->smooth_vect(chrom_0d);


  
  //  this->annotator.set_active_chrom(&chrom);

  #ifdef GFDEBUG
  std::ofstream chrom_2d_out("chrom_2d.tab");
  crawutils::output_vector(chrom_2d_out,chrom_2d,'\n');
  chrom_2d_out.close();
  std::ofstream chrom_1d_out("chrom_1d.tab");
  crawutils::output_vector(chrom_2d_out,chrom_2d,'\n');
  chrom_1d_out.close();
  #endif


  //find 2nd derivative zero crossing points
  this->find_cross_points(chrom_2d);
  //annotated peaks and valleys in the 2nd derivative
  this->peaks_and_valleys(chrom_2d);

  float mean_height_cutoff = 0.0f;
  if ( method.mean_cutoff ) {
     mean_height_cutoff = determine_mean_cutoff();
  }

  for ( int i = 0 ; i < (int)peaks.size() ; i++ ) {
    uint lh_valley, rh_valley;
    uint chrom2d_peak_loc = peaks[i];
    uint lh_valley_idx = crawutils::get_lh_idx(valleys,chrom2d_peak_loc);
    if ( valleys.size() == 1 ) {
      if ( valleys[0] > peaks[0] ) {
	lh_valley = 0;
	rh_valley = valleys[0];
      }
      else {
          lh_valley = valleys[0];
          rh_valley = chrom_2d.size() - 1;
      }
    }

    else if ( valleys[lh_valley_idx] > chrom2d_peak_loc ) {
      lh_valley = 0;

      rh_valley = 1;
    }
    else if ( lh_valley_idx >= valleys.size() - 1 ) {
      lh_valley = valleys[lh_valley_idx-1];
      rh_valley = chrom_2d.size() - 1;
    }
    else {
      lh_valley = valleys[lh_valley_idx];
      rh_valley = valleys[lh_valley_idx+1];
    }



    //now we expand from the peak locations to 
    //std::cerr<< "DEBUG: calling minimum_level" << std::endl;

    //note that this alters lh_valley, rh_valley
    delimit_by_minimum_level( /*ref*/ lh_valley, /*ref*/ rh_valley, chrom2d_peak_loc );
       
    //std::cerr<< "DEBUG: called minimum_level" << std::endl;
    //now we have lh,peak,rh
    if ( ! (method.mean_cutoff) || 
         ( this->annotator.get_active_chrom()->at(chrom2d_peak_loc) > mean_height_cutoff ) ) { 
      if ( passes_min_len( lh_valley, rh_valley ) )
	{
	  peak_voodoo(lh_valley,rh_valley,chrom2d_peak_loc);
	}
    }
  }
  if ( this->method.extend_peak_set ) {
     this->extend_peak_set();
  }
}

float BaseCrawPeakFinder::determine_mean_cutoff() {
     double t = 0.0;
     for ( int i = 0 ; i < (int)peaks.size() ; i++ ) {
        t += this->annotator.get_active_chrom()->at(peaks[i]);
     }
     return (float)(t / peaks.size());
  }


void CrawPeakFinder::peak_voodoo( int lh_valley, int rh_valley, int peak_loc ) {
  SlimCrawPeak * peak;
  //const
  if ( slim ) {
    peak = construct_slim_peak (lh_valley, rh_valley, peak_loc);
  }
  else {
    peak = construct_peak( lh_valley , rh_valley , peak_loc);
  }

  //relate to peak locations, boundaries (should be one method)
  annotator.refind_peak_peak(*peak);
  annotator.peak_tweak( *peak, this->method );
  
  //relate to intensities, areas, etc... (should be one method)
  annotator.set_peak_slope(*peak);
  annotator.set_peak_bg_subtracted_area ( *peak );
  annotator.calc_fwhm(*peak);

  SlimCrawPeakPtr shared_p(peak);
  sps.push_back(shared_p);
      
}

void StackCrawPeakFinder::peak_voodoo( int lh_valley, int rh_valley, int peak_loc ) {
     SlimCrawPeak peak( lh_valley, rh_valley, peak_loc, this->chrom, this->chrom_bg_scratch, this->mz_idx ) ;

     annotator.refind_peak_peak(peak);
     annotator.peak_tweak( peak, this->method );

     //peak.peak_height = 0.0f;
     //note this needs to altered if we do not use the standard of defining the peak area as
     //stretching from one end to the other
     annotator.set_peak_slope(peak);
     annotator.set_peak_bg_subtracted_area ( peak );
     annotator.calc_fwhm(peak);
     sps.push_back(peak);

}



CrawPeak * BaseCrawPeakFinder::construct_peak( int lh_valley , int rh_valley, int peak_loc ) {
  CrawPeak * p = new CrawPeak(  lh_valley, rh_valley, peak_loc, this->chrom, this->chrom_bg_scratch, this->mz_idx  );
  return p;
}

SlimCrawPeak * BaseCrawPeakFinder::construct_slim_peak( int lh_valley , int rh_valley, int peak_loc ) {
  SlimCrawPeak * p = new SlimCrawPeak(  lh_valley, rh_valley, peak_loc, this->chrom, this->chrom_bg_scratch, this->mz_idx );
  return p;
}


bool BaseCrawPeakFinder::passes_min_len ( int lh_valley, int rh_valley ) {
  return ( rh_valley - lh_valley + 1 ) >= this->method.min_len;
}

void BaseCrawPeakFinder::delimit_by_minimum_level( uint & lh_valley_idx , uint & rh_valley_idx ,
					      int peak_loc ) {
  for ( int lh = peak_loc ; lh > (int)lh_valley_idx ; lh-- ) {
    if ( this->chrom[lh] <= this->method.minimum_level ) {
      lh_valley_idx = lh;
      break;
    }
  }
  for ( int rh = peak_loc ; rh <= (int)rh_valley_idx ; rh++ ) {
    if ( this->chrom[rh] <= this->method.minimum_level ) {
      rh_valley_idx = rh;
      break;
    }
  }

}

void BaseCrawPeakFinder::init_smoothers() {
   gs_0d = new GaussSmoother();
   gs_1d = new GaussSmoother();
   gs_2d = new GaussSmoother();
}

void BaseCrawPeakFinder::set_chrom (const vector<float> & f, int mz_idx) {
  this->mz_idx = mz_idx;
  this->chrom.resize(f.size());
  this->chrom_bg_scratch.resize(f.size());
  std::copy(f.begin(), f.end(), chrom.begin());
  this->annotator.set_active_chrom(&chrom);
}

void BaseCrawPeakFinder::clear() {
  this->chrom.clear();
  this->chrom_2d.clear();
  this->chrom_1d.clear();
  this->mz_idx = -1;
  this->minus_crosses.clear();
  this->plus_crosses.clear();
  this->maxs.clear();
  this->mins.clear();
  this->peaks.clear();
  this->valleys.clear();
  this->clear_sps();
}

void BaseCrawPeakFinder::clear_sps() {
  this->sps.clear();
}

void BaseCrawPeakFinder::filter_sps_by_method( CrawPeakMethod * in_meth  ) {
  if ( in_meth == NULL ) {
    in_meth = &(this->method);
  }
  std::vector<stored_peak_t> tmp_peaks;
  tmp_peaks.reserve(sps.size());
  
  for ( int i = 0 ; i < (int)sps.size() ; i++ ) {
    /* use the annotator here to filter peaks more effectively */
  }
  

}



void CrawPeakFinder::extend_peak_set () { 

    std::vector<SlimCrawPeakPtr> extended_peaks;
    annotator.extend_peak_set(sps, extended_peaks);
    sps = extended_peaks;
}


  void StackCrawPeakFinder::extend_peak_set () { 

    throw("incredibly buggy code. Don't use this");

    std::vector<SlimCrawPeakPtr> extended_peaks;
    std::vector<SlimCrawPeakPtr> input_peaks;
    input_peaks.resize(sps.size());
    for ( int i = 0 ; i < (int)sps.size() ; i++ ) {
       *(input_peaks[i]) = sps[i];
    }
    annotator.extend_peak_set(input_peaks,extended_peaks);
    sps.clear();
    sps.reserve(extended_peaks.size());
    for ( int i = 0 ; i < (int)extended_peaks.size(); i++ ) {
      sps.push_back( *(extended_peaks[i]));
    }


  }

  void StackCrawPeakFinder::extend_peak_set_weird() {
    throw("don't go here");
    //std::vector<SlimCrawPeakPtr> extended_peaks;
    //annotator.extend_peak_set(sps, extended_peaks);
    //sps = extended_peaks;
  }
     

void CrawPeakFinder::clear_sps() {
   this->sps.clear();
}

void StackCrawPeakFinder::clear_sps() {
   this->sps.clear();
}

SlimCrawPeak * CrawPeakFinder::get_peak_ptr(int idx) {
  return this->sps[idx].get(); 
}

 SlimCrawPeak * StackCrawPeakFinder::get_peak_ptr(int idx) {
  /* ok, [] on vectors supposedly returns a reference to the value within, so this should 
     be semi-safe - although obviously you cannot count on this pointer being valid if the object falls off
     the stack */
  return &(this->sps[idx]); 
}
 void BaseCrawPeakFinder::report_peaks ( std::ostream & o ) {

  for ( int i = 0 ; i < (int)get_num_stored_peaks() ; i++ ) {
    if ( i == 0 ) {
      o << this->get_peak_ptr(0)->as_string_long_header() << std::endl;
    }
    std::string x = get_peak_ptr(i)->as_string_long();
    o << x << std::endl;
  }
}


int CrawPeakFinder::get_num_stored_peaks() {
  return sps.size();
}

int StackCrawPeakFinder::get_num_stored_peaks() {
  return sps.size();
}


void BaseCrawPeakFinder::init() {
  method = CrawPeakMethod();
  mz_idx = -1;
  slim = false;
  annotator = CrawPeakAnnotator(this);
  gs_0d = gs_1d = gs_2d = NULL;
}


/* -----  functions for suppeakfinder located ---- */


CrawPeakLocated * CrawPeakFinderLocated::construct_peak( int lh_valley , int rh_valley, int peak_loc ) {
  CrawPeakLocated * p = new CrawPeakLocated(  lh_valley, rh_valley, peak_loc, this->chrom, this->chrom_bg_scratch, this->mz_idx );
  p->set_rt_mz( current_mz, rts[lh_valley], rts[peak_loc], rts[rh_valley] );
  return p;
}


void CrawPeakFinderLocated::set_chrom (const vector<float> & f, int mz_idx) {
  this->mz_idx = mz_idx;
  this->current_mz = this->mzs[mz_idx];
  this->chrom.resize(f.size());
  this->chrom_2d.resize(f.size());
  this->chrom_1d.resize(f.size());
  std::copy(f.begin(), f.end(), chrom.begin());
}

}


