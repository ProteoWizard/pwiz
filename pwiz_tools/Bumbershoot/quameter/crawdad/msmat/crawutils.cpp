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
#include <algorithm>
#include <vector>
#include <assert.h>
#include <stdio.h>
#include <stdlib.h>
#include "crawutils.h"

//note that I do not yet have the cephes library for MSVC++
#ifndef _MSC_VER

/* 
#ifndef _NO_CEPHES
extern "C" {
#include "cephes_include.h"
};
#endif
*/


#endif



namespace crawutils {

  


  file_info::file_info() {
    ext_stripped = false;
    pref_stripped = false;
  }

  file_info::file_info( const file_info & rhs ) {
    ext_stripped = rhs.ext_stripped;
    pref_stripped = rhs.pref_stripped;
    dirpath = rhs.dirpath;
    filename = rhs.filename;
    original_filename = rhs.original_filename;
    this->original_dirpath = rhs.original_dirpath;
  }

  file_info::file_info(const char * fname) {
    ext_stripped = false;
    pref_stripped = false;
    char * last_pathsep = strrchr((char*)fname,pathsep);
    if ( last_pathsep == NULL ) {
      dirpath = std::string(".");
      filename = std::string(fname);
      original_filename = filename;
    }
    else {
      filename = std::string(last_pathsep+1);
      original_filename = filename;
      dirpath = std::string(fname,(last_pathsep - fname));
      original_dirpath = dirpath;
      std::cerr << "filename is " << filename << ", dirpath is:" << dirpath << std::endl;
    }
  }

    

  void file_info::strip_extension( char delim ) {
    if ( ! ext_stripped ) {
      std::string tmp_s("");
      uint last_ext_pos = filename.rfind(delim);
      if (  last_ext_pos != std::string::npos )  {
	tmp_s.append(filename,0,last_ext_pos);
	std::cerr << "stripped extension" << tmp_s << std::endl;
	filename = tmp_s;
	ext_stripped = true;
      }
    }
  }

  void file_info::strip_prefix ( char delim ) {
    size_t prefix_pos = filename.find(delim);
    if ( prefix_pos == std::string::npos || prefix_pos == filename.rfind(delim) ) {
      //no or only one dot, no need to strip
      pref_stripped = true;
    }
    if ( ! pref_stripped ) {
      std::string tmp_s("");
      tmp_s.append(filename, prefix_pos + 1, filename.size() );
      std::cerr << "stripped_prefix: " << tmp_s << std::endl;
      filename = tmp_s;
      pref_stripped = true;
    }
  }

  std::string file_info::get_prefix( char delim ) {
    size_t prefix_pos = filename.find(delim);
    if ( ! pref_stripped ) {
      std::string tmp_s("");
      tmp_s.append(filename, 0, prefix_pos);
      //std::cerr << "stripped_prefix: " << tmp_s << std::endl;
      return tmp_s;
    }
    else {
      return std::string("");
    }
  }

  void file_info::add_prefix ( const char * new_prefix , char delim) {
    std::string tmp_s("");
    tmp_s.append(new_prefix);
    tmp_s.push_back(delim);
    tmp_s.append(filename);
    filename = tmp_s;
    pref_stripped = false;
  }
  void file_info::change_prefix( const char * new_prefix , char delim) {
    strip_prefix(delim);
    add_prefix(new_prefix,delim);
  }
  void file_info::add_extension ( const char * extension , char delim) {
    filename.push_back(delim);
    filename.append(extension);
    ext_stripped = false;
  }
  std::string file_info::full_path() {
      std::string tmp_s = dirpath;
//convoluted logic since MSC c library does not seem to like opening files starting with '.\'
#ifdef _MSC_VER
      if ( dirpath == std::string(".") ) {
        tmp_s = filename;
      }
      else {
#endif 
    tmp_s.push_back(pathsep);
    tmp_s.append(filename);
#ifdef _MSC_VER
      }
#endif
    return tmp_s; 
  }	

std::string trim_whitespace ( const std::string & in_str ) {
   int start_ws = in_str.find_first_not_of(" \t\n\r");
   int stop_ws  = in_str.find_last_not_of(" \t\n\r");
   return in_str.substr(start_ws, stop_ws - start_ws + 1);
}


void make_idx_segments ( int num_mzs, int num_rts, int num_mzs_segments, int num_rts_segments,
                        std::vector<std::pair<int,int> > & mzs_segs,
                        std::vector<std::pair<int,int> > & rts_segs ) {

    int mz_seg_size      = num_mzs / (num_mzs_segments);
    int mz_seg_remainder = num_mzs - (mz_seg_size * (num_mzs_segments));
    int rt_seg_size      = num_rts / (num_rts_segments);
    int rt_seg_remainder = num_rts - (rt_seg_size * (num_rts_segments));
 


    int mz_lh = 0;
    int mz_rh;
    for ( int i = 0 ; i < num_mzs_segments ; i++ ) {
       mz_rh = mz_lh + (mz_seg_size-1);
       mzs_segs.push_back(std::pair<int,int>(mz_lh,mz_rh));
       mz_lh = mz_rh + 1;
    }
    if ( mz_seg_remainder > 0 ) {
        mzs_segs.push_back(std::pair<int,int>(mz_lh,mz_lh+mz_seg_remainder-1));
    }

    int rt_lh = 0;
    int rt_rh;
    for ( int i = 0 ; i < num_rts_segments ; i++ ) {
       rt_rh = rt_lh + (rt_seg_size-1);
       rts_segs.push_back(std::pair<int,int>(rt_lh,rt_rh));
       rt_lh = rt_rh + 1;
    }
    if ( rt_seg_remainder > 0 ) {
        rts_segs.push_back(std::pair<int,int>(rt_lh,rt_lh+rt_seg_remainder-1));
    }
    
}

  float calc_sqrsumf( const std::vector<float> & f) {
    float t = 0.0f;
    for ( uint i = 0 ; i < f.size() ; i++ ) {
      t += f[i] * f[i];
    }
    return t;
  }

  double calc_sqrsum ( const std::vector<float> & f) {
    double t = 0.0;
    for ( uint i = 0 ; i < f.size() ; i++ ) {
      t += (double)f[i] * (double)f[i];
			
    }
    return t;
  }

  float spectra_noise_est ( const std::vector<float> & s1, const std::vector<float> & s2, double s1_sqrsum , double s2_sqrsum, double resolution ) {
    if ( s1_sqrsum == SQRSUM_NULL || s2_sqrsum == SQRSUM_NULL ) {
      s1_sqrsum = calc_sqrsum(s1);
      s2_sqrsum = calc_sqrsum(s2);
    }
    if ( s1_sqrsum == 0 || s2_sqrsum == 0 ) {
      return 0.0f;
    }
    // c = (2*bin_size) / num_bins
    double c =  4 * resolution / s1.size();
    double denom = sqrt(s1_sqrsum) * sqrt(s2_sqrsum);
    double numerator = c * sum_vect(s1) * sum_vect(s2);
    return (float)(numerator / denom);
  
  } 


  float spectra_cosine_angle ( const std::vector<float> & s1, const std::vector<float> & s2, 
			       double s1_sqrsum, double s2_sqrsum ) {
    // SQRSUM NULL represents a dummy value for the sqrsum
    if ( s1_sqrsum == SQRSUM_NULL || s2_sqrsum == SQRSUM_NULL ) {
      return spectra_cosine_angle(s1,s2);
    }
    if ( s1_sqrsum == 0 || s2_sqrsum == 0 ) {
      return 0.0f;
    }
    double d = sqrt(s1_sqrsum) * sqrt(s2_sqrsum);
    if ( d == 0.0 )  {
      return 0.0f;
    }
    double n = mult_accum_vects(s1,s2);		
    return (float)(n / d);
  }

  /* TODO -- determine how to compare values when
     1. some bins should be ignored due to contaminants
     2. negative / zero intensity values
     3. assess data from noise values
     4. Speed -- pre-rank all spectra */
	
  void rankties( std::vector<float> & v ) {
    std::vector<int> tied_idxs(1);
    tied_idxs[0] = 0;
    int start_idx = -1;
    int stop_idx = -1;
    float last_seen = v[0];
    for ( int i = 1 ; i < (int)v.size() ; i++ ) {
      if ( v[i] != last_seen ) {
	if ( tied_idxs.size() > 1 ) {
	  float avg = (float)sum_vect(tied_idxs) / (float)tied_idxs.size();
	  for ( int t = 0 ; t < (int)tied_idxs.size() ; t++ ) {
	    v[tied_idxs[t]] = avg;
	  }
	}
	tied_idxs.clear();
      }
      tied_idxs.push_back(i);
      last_seen = v[i];
    }
  }

  float spectra_spearman_rank_corr ( const std::vector<float> & s1, const std::vector<float> & s2, 
				     double d1, double d2 ) {
    assert(s1.size() == s2.size());

    //note that we will modify comparisons by reducing them when one or the other of the intensities is zero
    std::vector< std::pair<float,int> > s1_sort(0);
    std::vector< std::pair<float,int> > s2_sort(0);
    int saved_idx = 0;
    for ( int i = 0 ; i < (int)s1.size();  i++ ) {
      if ( s1[i] <= 0.0f && s2[i] <= 0.0f ) {
	continue;
      }
      else {
	s1_sort.push_back(std::pair<float,int>(s1[i],saved_idx));
	s2_sort.push_back(std::pair<float,int>(s2[i],saved_idx));
	saved_idx++;
      }
    }
    int comparisons = saved_idx;
    if ( comparisons == 0 ) {
      return 0.0f;
    }
    //sort the indexes to bins by intensity
    std::sort(s1_sort.begin(),s1_sort.end());
    std::sort(s2_sort.begin(),s2_sort.end());
    //then we assign the rank to the bin index to determine the distances
    std::vector<int> rank_by_pos_s1(s1_sort.size());
    std::vector<int> rank_by_pos_s2(s1_sort.size());
    //number of elements is equal to saved_idx

    for ( int i = 0 ; i < (int)s1_sort.size() ; i++ ) {
      rank_by_pos_s1[s1_sort[i].second] = i;
      rank_by_pos_s2[s2_sort[i].second] = i;
    }

    double dist = 0.0;

    for ( int i = 0 ; i< (int)s1_sort.size() ; i++ ) {
      float t = (float)(rank_by_pos_s1[i] - rank_by_pos_s2[i]);
      dist += t * t;
    }
    float score = (float)(1.0f - ( 6 * dist ) / ( comparisons * ( comparisons * comparisons - 1 ) ));
    if ( score > 1.0f ) {
      std::cerr << "score: " << score << " comparisons " << comparisons << std::endl;
    }
    return score;
  }

  std::vector< std::pair<int,int> > fyates_shuffle_commands( int series_size ) {
  
    init_rand();
    std::vector< std::pair<int,int> > new_pair_vect;
    new_pair_vect.reserve(series_size);
    for ( int i = 0 ; i < series_size ; i++ ) {
      int j = (int)floor(get_rand() * i+1 );
      new_pair_vect.push_back( std::pair<int,int> ( i,j ) );
    }
    return new_pair_vect;
    
  }


  void rank_by_pos_ties ( const std::vector< std::pair< float, int > > & sorted_by_i , std::vector<float> & to_rank, bool avg_tie ) {
    assert(sorted_by_i.size() == to_rank.size());

    int i = 0;
    int tie_start = 0;
    int last_rank = -1;
    float last_val = sorted_by_i[0].first - 1.0f;
    bool in_tie = false;

    while ( true ) {
      if ( i >= (int)sorted_by_i.size() ) {
	break;
      }
      if ( ( i != (sorted_by_i.size() ) ) && ( sorted_by_i[i].first == last_val) ) {
	if ( ! in_tie ) {
	  in_tie = true;
	  tie_start = i;
	}
	else {
	  //continuing a tie
	}
      }
      else {
	if ( in_tie == false ) {
	  //we are not ending a tie
	  to_rank[sorted_by_i[i].second] = (float)i;
	}
	else {
	  //we are ending a tie
	  if ( avg_tie ) {
	    int rank_total = 0;
	    int tie_size = i - tie_start + 1;
	    int j = tie_start;
	    for ( ; j < i ; j++ ) {
	      rank_total += j;
	    }
	    float tied_rank = (float)rank_total / (float)tie_size;
	    for ( j = tie_start ;  j < i ; j++ ) {
	      to_rank[sorted_by_i[i].second] = tied_rank;
	    }
	  }
	  else {
	    //use the floor of the start of the ranks
	    float tie_rank_floor = (float)tie_start;
	    for ( int j = tie_start; j < i ; j++ ) {
	      to_rank[sorted_by_i[i].second] = tie_rank_floor;
	    }
	  }
	  //since we are not ending a tie..
	  last_val = sorted_by_i[i].first;
	  in_tie = false;
	}
      }
      i++; // make sure we always increment i
    }
  }
  


  void rank_by_pos_noties ( const std::vector< std::pair< float, int > > & sorted_by_i , std::vector<float> & to_rank ) {
    assert(sorted_by_i.size() == to_rank.size());

    int i = 0;


    while ( true ) {
      if ( i >= (int)sorted_by_i.size() ) {
	break;
      }
      to_rank[sorted_by_i[i].second] = (float)i;
      i++;
    } 
  }

  float spectra_spearman_rank_corr_2 ( const std::vector<float> & s1, const std::vector<float> & s2 , double d1, double d2) {
    assert(s1.size() == s2.size());

    //note that we will modify comparisons by reducing them when one or the other of the intensities is zero
    std::vector< std::pair<float,int> > s1_sort(0);
    std::vector< std::pair<float,int> > s2_sort(0);
    int saved_idx = 0;
    for ( int i = 0 ; i < (int)s1.size();  i++ ) {
      if ( s1[i] <= 0.0f || s2[i] <= 0.0f ) {
	continue;
      }
      else {
	s1_sort.push_back(std::pair<float,int>(s1[i],saved_idx));
	s2_sort.push_back(std::pair<float,int>(s2[i],saved_idx));
	saved_idx++;
      }
    }
   
    //sort the indexes to bins by intensity
    std::sort(s1_sort.begin(),s1_sort.end());
    std::sort(s2_sort.begin(),s2_sort.end());
    //then we assign the rank to the bin index to determine the distances
    std::vector<float> rank_by_pos_s1(s1_sort.size());
    std::vector<float> rank_by_pos_s2(s1_sort.size());
    //number of elements is equal to saved_idx

    rank_by_pos_ties( s1_sort, rank_by_pos_s1 );
    rank_by_pos_ties( s2_sort, rank_by_pos_s2 );
    
    float cc = spectra_corr_coef( rank_by_pos_s1, rank_by_pos_s2 );
    return cc;
  }

  float spectra_spearman_rank_corr_3 ( const std::vector<float> & s1, const std::vector<float> & s2, double d1, double d2 ) {
    assert(s1.size() == s2.size());

    //note that we will modify comparisons by reducing them when one or the other of the intensities is zero
    std::vector< std::pair<float,int> > s1_sort(0);
    std::vector< std::pair<float,int> > s2_sort(0);
    int saved_idx = 0;
    for ( int i = 0 ; i < (int)s1.size();  i++ ) {
      if ( s1[i] <= 0.0f || s2[i] <= 0.0f ) {
	continue;
      }
      else {
	s1_sort.push_back(std::pair<float,int>(s1[i],saved_idx));
	s2_sort.push_back(std::pair<float,int>(s2[i],saved_idx));
	saved_idx++;
      }
    }
   
    //sort the indexes to bins by intensity
    std::sort(s1_sort.begin(),s1_sort.end());
    std::sort(s2_sort.begin(),s2_sort.end());
    //then we assign the rank to the bin index to determine the distances
    std::vector<float> rank_by_pos_s1(s1_sort.size());
    std::vector<float> rank_by_pos_s2(s1_sort.size());
    //number of elements is equal to saved_idx

    rank_by_pos_noties( s1_sort, rank_by_pos_s1 );
    rank_by_pos_noties( s2_sort, rank_by_pos_s2 );
    
    float cc = spectra_corr_coef( rank_by_pos_s1, rank_by_pos_s2 );
    return cc;
  }

  float spectra_spearman_rank_corr_4 ( const std::vector<float> & s1, const std::vector<float> & s2, double d1, double d2 ) {
    assert(s1.size() == s2.size());

    //note that we will modify comparisons by reducing them when one or the other of the intensities is zero
    std::vector< std::pair<float,int> > s1_sort(0);
    std::vector< std::pair<float,int> > s2_sort(0);
    int saved_idx = 0;
    for ( int i = 0 ; i < (int)s1.size();  i++ ) {
      s1_sort.push_back(std::pair<float,int>(s1[i],saved_idx));
      s2_sort.push_back(std::pair<float,int>(s2[i],saved_idx));
      saved_idx++;
    }
   
    //sort the indexes to bins by intensity
    std::sort(s1_sort.begin(),s1_sort.end());
    std::sort(s2_sort.begin(),s2_sort.end());
    //then we assign the rank to the bin index to determine the distances
    std::vector<float> rank_by_pos_s1(s1_sort.size());
    std::vector<float> rank_by_pos_s2(s1_sort.size());
    //number of elements is equal to saved_idx

    rank_by_pos_ties( s1_sort, rank_by_pos_s1 );
    rank_by_pos_ties( s2_sort, rank_by_pos_s2 );
    
    /* remove sections of rank_by_pos_s1 , rank_by_pos_s2 where both
       entries are zero */

    std::vector<float> filt_s1(0);
    std::vector<float> filt_s2(0);
    

    float cc = spectra_corr_coef( rank_by_pos_s1, rank_by_pos_s2 );
    return cc;
  }


  float spectra_spearman_rank_corr_5 ( const std::vector<float> & s1, const std::vector<float> & s2, double d1, double d2 ) {
    assert(s1.size() == s2.size());

    //note that we will modify comparisons by reducing them when one or the other of the intensities is zero
    std::vector< std::pair<float,int> > s1_sort(0);
    std::vector< std::pair<float,int> > s2_sort(0);
    int saved_idx = 0;
    for ( int i = 0 ; i < (int)s1.size();  i++ ) {
      s1_sort.push_back(std::pair<float,int>(s1[i],saved_idx));
      s2_sort.push_back(std::pair<float,int>(s2[i],saved_idx));
      saved_idx++;
    }
   
    //sort the indexes to bins by intensity
    std::sort(s1_sort.begin(),s1_sort.end());
    std::sort(s2_sort.begin(),s2_sort.end());
    //then we assign the rank to the bin index to determine the distances
    std::vector<float> rank_by_pos_s1(s1_sort.size());
    std::vector<float> rank_by_pos_s2(s1_sort.size());
    //number of elements is equal to saved_idx

    rank_by_pos_noties( s1_sort, rank_by_pos_s1 );
    rank_by_pos_noties( s2_sort, rank_by_pos_s2 );
    
    /* remove sections of rank_by_pos_s1 , rank_by_pos_s2 where both
       entries are zero */

    std::vector<float> filt_s1(0);
    std::vector<float> filt_s2(0);
    

    float cc = spectra_corr_coef( rank_by_pos_s1, rank_by_pos_s2 );
    return cc;
  }


  float spectra_cosine_angle ( const std::vector<float> & s1, const std::vector<float> & s2) {
    double sv1_sqrsum, sv2_sqrsum;
    sv1_sqrsum = calc_sqrsum(s1);
    sv2_sqrsum = calc_sqrsum(s2);
    return spectra_cosine_angle(s1,s2,sv1_sqrsum,sv2_sqrsum);
  }
  float spectra_cosine_angle_squared ( const std::vector<float> & s1, const std::vector<float> & s2,
				       double d1, double d2) {
    float cos_angle = spectra_cosine_angle(s1,s2);
    return cos_angle * cos_angle;
  }
  float spectra_corr_coef ( const std::vector<float> & s1, const std::vector<float> & s2) {
    double sv1_sqrsum, sv2_sqrsum;
    sv1_sqrsum = calc_sqrsum(s1);
    sv2_sqrsum = calc_sqrsum(s2);
    return spectra_corr_coef(s1,s2,sv1_sqrsum,sv2_sqrsum);
  }
  /* correlation coefficient :
     cov(X,Y) / sigmaXsigmaY  -- 
     E(XY) - E(X)E(Y) / ( 
     

  */

  void filter_zero_values (const std::vector<float> & s1 , const std::vector<float> & s2,
			   std::vector<float> & out_s1, std::vector<float> & out_s2 ,
			   float min_val ) {
    if ( s1.size() != s2.size() ) {
      throw("spectra must be of the same size");
    }
    std::vector<float> s1_tmp; s1_tmp.reserve(s1.size());
    std::vector<float> s2_tmp; s2_tmp.reserve(s2.size());
    for ( int i = 0 ; i < (int)s1.size(); i++ ) {
      if ( s1[i] <= min_val || s2[i] <= min_val ) {
	continue;
      }
      else {
	s1_tmp.push_back(s1[i]);
	s2_tmp.push_back(s2[i]);
      }
    }
    out_s1.resize(s1_tmp.size());
    out_s2.resize(s2_tmp.size());
    std::copy(s1_tmp.begin(), s1_tmp.end(), out_s1.begin());
    std::copy(s2_tmp.begin(), s2_tmp.end(), out_s2.begin());
    
  }

  float spectra_corr_coef_nozeros ( const std::vector<float> & s1, const std::vector<float> & s2 , double s1_sqrsum, double s2_sqrsum) {
    
    std::vector<float> new_s1, new_s2;
    filter_zero_values ( s1, s2, new_s1, new_s2, 0.0f);
    if ( new_s1.size() == 0 ) {
      return 0.0f;
    }
    else {
      return spectra_corr_coef ( new_s1, new_s2 );
    }
  }

  float spectra_corr_coef ( const std::vector<float> & s1, const std::vector<float> & s2, 
			    double s1_sqrsum, double s2_sqrsum ) {
 	  
    /* calculate as the centered cosine angle */
    if ( s1_sqrsum == SQRSUM_NULL || s2_sqrsum == SQRSUM_NULL ) {
      return spectra_corr_coef(s1,s2);
    }
    if ( s1_sqrsum == 0 || s2_sqrsum == 0 ) {
      return 0.0f;
    }
    double meanS1 = sum_vect(s1) / s1.size();
    double meanS2 = sum_vect(s2) / s2.size();
    std::vector<float> ns1(s1);
    std::vector<float> ns2(s2);
    for ( int i = 0 ; i < (int)s1.size() ; i++ ) {
      ns1[i] = (float)(ns1[i] - meanS1);
      ns2[i] = (float)(ns2[i] - meanS2);
    }
    double s1_sqrsum_tmp = calc_sqrsum(ns1);
    double s2_sqrsum_tmp = calc_sqrsum(ns2);
    return spectra_cosine_angle( ns1, ns2, s1_sqrsum_tmp, s2_sqrsum_tmp);
  }

  float spectra_dot_product ( const std::vector<float> & s1, const std::vector<float> & s2 ) {
    double t = 0.0;
    assert(s1.size() == s2.size());
    for ( uint i = 0; i < s1.size() ; i++ ) {
      t += s1[i] * s2[i];
    }
    return (float)t;
  }

  double vector_magnitude ( const std::vector<float> & s ) {
     double t = 0.0;
     for ( int i = 0 ; i < (int)s.size() ; i++ ) {
        t += s[i] * s[i];
     }
     return sqrt(t);
  }

  float spectra_tic_geometric_distance ( const std::vector<float> & s1, const std::vector<float> & s2, double d1 , double ) {
    assert(s1.size() == s2.size());
    double total = 0.0;
    for ( int i = 0 ; i < (int)s1.size() ; i++ ) {
      double d = s1[i] - s2[i];
      total += d*d;
    }
    total = sqrt(total);
    /* total gives the distance (euclidean) between the two vectors --
       how can we normalize that such that it ranges between 0 and 1 */
    /* 1 could be the greater of the magnitudes of the two vectors? */
    double s1_mag = vector_magnitude(s1);
    double s2_mag = vector_magnitude(s2);
    double vect_max = std::max(s1_mag,s2_mag);
    if ( vect_max == 0.0 ) {
      return 0.0;
    }
    return (float)std::max(1.0-(total / vect_max),0.0);
  } 

  float spectra_tic_similarity ( const std::vector<float> & s1, const std::vector<float> & s2 ) {
    float s1_sum = sum_vect(s1);
    float s2_sum = sum_vect(s2);
    return 1 - ( ( std::max(s1_sum,s2_sum) - fabs(s1_sum - s2_sum) ) / std::max(s1_sum,s2_sum) );
  }

  void test_dot_product () {
    float _v1[5] = { 1.0f , 2.0f , 3.0f , 4.0f , 5.0f };
    float _v2[5] = { 1.0f , 3.0f , 2.0f , 5.0f, 4.0f };
    float _v3[5] = { 5.0f , 4.0f , 3.0f , 2.0f , 1.0f };
    std::vector<float> v1(_v1 ,_v1 + 5);
    std::vector<float> v2(_v2 ,_v2 + 5);
    std::vector<float> v3(_v3 ,_v3 + 5);
    std::cout << "v1 vs v2" << spectra_cosine_angle( v1,v2 ) << std::endl;        
    std::cout << "v2 vs v1" << spectra_cosine_angle( v2,v1 ) << std::endl;        
    std::cout << "v2 vs v3" << spectra_cosine_angle( v2,v3 ) << std::endl;
    std::cout << "v1 vs v3" << spectra_cosine_angle( v3,v1 ) << std::endl;
    std::cout << "raw" << std::endl;
    std::cout << "v1,v2" << spectra_cosine_angle( v1,v2,1.0,1.0) << std::endl;
    std::cout << "v2,v3" << spectra_cosine_angle( v2,v3,1.0,1.0) << std::endl; 
    std::cout << "v1,v3" << spectra_cosine_angle(v1,v3,1.0,1.0) << std::endl;
    std::cout << "dot product" << std::endl;
    std::cout << "v1,v2" << spectra_dot_product(v1,v2) << std::endl;
  }


  int test_spectra_cosine_angle () {
    std::vector<float> a(2);
    a[0] = 0;
    a[1] = 100;
    std::vector<float> b(2);
    b[0] = 100;
    b[1] = 0;
    std::cerr << "dot product(a,a) " << spectra_cosine_angle(a,a) << std::endl;
    std::cerr << "dot product(a,b)"  << spectra_cosine_angle(a,b) << std::endl;
    return 1;
  }


  /* TODO -- deal with reverse ordered */
  template<typename data_type>
  bool ordered_list(std::vector<data_type> & v ) {
    if ( v.size() == 1 ) { return true; }
    for ( int i = 1; i < v.size(); i++ ) {
      if(v[i] < v[i-1]) {
	return false;
      }
    }
    return true;
  }



  /*
    template <typename data_type>
    void  load_type( data_type * d, char * field_data, int data_len) {
    assert(data_len == sizeof(data_type));
    memcpy(d,field_data,data_len);
    };
  */

  template <typename data_type>
  void load_int_type( data_type * out_val, char * field_data, int data_len ) {
    /* copy field data to a buffer we can manipulate */
    char tmp_data[128];
    char ** ret_val;
    strncpy(tmp_data, field_data, data_len);
    tmp_data[data_len+1] = '\0';
    *out_val = (data_type)strtol(tmp_data,ret_val,0);
    if ( *ret_val == tmp_data ) {
      std::cerr << "error attempting to parse integer " << tmp_data << std::endl;
      exit(-1);
    }
  };
  /*
    template <typename data_type>
    void load_array( data_type * out_val, char * field_data, int char_len ) {
    //TODO -- deal with different endianness in the data
    memcpy((void*)out_val, (void*)field_data, char_len );	
    };


    template <typename data_type>
    void load_array( data_type * out_val , char * field_data, int char_len, int num_fields ) {
    assert(num_fields * sizeof(data_type) == char_len);
    load_array(out_val, field_data, char_len);
    };
  */
  template<typename data_type>
  data_type templ_abs( data_type v ) {
    if ( v < 0.0 ) {
      return ((data_type)(v * -1));
    }
    else {
      return( v);
    }
  };

  /*
    template <typename data_type > 
    vector<data_type>::iterator find_nearest( conststd::vector<data_type> & v, data_type key ) {
    //TODO -- check thatstd::vector is sorted
    vector<data_type>::iterator lb = lower_bound(v.begin(), v.end(), key);
    if ( lb == v.end() ) {
    return lb;
    }
    else {
    vector<data_type>::iterator rb = lb + 1;
    data_type lb_diff = templ_abs<data_type>( key - *lb);
    data_type rb_diff = templ_abs<data_type>( *rb - key);
    if ( lb_diff <= rb_diff ) {
    return lb;
    }	
    else {
    return rb;
    }
    }
    }
  */

  std::vector<float>::const_iterator find_nearest( const std::vector<float> & v, float key ) {
    //TODO -- check that std::vector is sorted
    float lb_diff, rb_diff;
    std::vector<float>::const_iterator rb, lb;
    lb = lower_bound(v.begin(), v.end(), key);
    if ( lb == v.end() ) {
      return lb;
    }
    if ( lb == v.begin() ) {
      return lb;
    }
    else {
      //since this gives us the position of where to insert a unit, we need to 
      //decrement it by one to match the item that the search is keyed off of
      lb--;
      rb = lb;
      rb++;
      lb_diff = templ_abs<float>( key - *lb);
      rb_diff = templ_abs<float>( *rb - key);

		
      if ( lb_diff <= rb_diff ) {
	return lb;
      }	
      else {
	return rb;
      }
    }
  }

  //vector<float>::iterator find_nearest( const std::vector<float> & v, float key );

  /*
    flt_set::iterator find_nearest( flt_set & s, float key) {
    flt_set::iterator lh = s.lower_bound(key);
    flt_set::iterator rh = s.upper_bound(key);
    assert( ! ( lh == s.end() && rh == s.end() ) );
    if ( lh == s.end() ) {
    return rh;
    }
    else if ( rh == s.end() ) {
    return lh;
    }
    else {
    if ( fabs(key-(*lh)) < fabs(key-(*rh)) ) {
    return lh;
    }
    }
    return rh;
    }

	 
    if ( mypnt == NULL ) {
    fprintf(stderr,"tried to free a NULL pointer\n");
    }	
    else {	
    free(mypnt);
    mypnt = NULL;
    }
    }
  */

  void * mymalloc(size_t size, size_t n) {
    void * ptr = malloc(size*n);
    if ( ptr == NULL) {
      fprintf(stderr,"could not allocate memory\n");
      exit(1);
    }
    return ptr;
  }

  double round_to_dbl(double n, double r) {
    n *= (1.0 / r);
    n = round(n);
    n *= r;
    return n;
  }

  // TODO -- adapt to take a stream
  void print_cmd_args(int argc, char ** argv ) {
    for ( int i = 0 ; i < argc ; i++) {
      std::cerr << argv[i] << " ";
		
    }
    std::cerr << std::endl;
  }



  float trapezoidal_linear_centroid ( std::vector<float> & v ) {
    assert(monotonic_vect(v));
    std::vector<float> cum_sum(v.size());
    cum_sum[0] = (float)0;
    for ( uint i = 1 ; i < v.size() ; i++ ) {
      float trap = v[i-1] - ( ( v[i-1] - v[i] ) / 2 );
      cum_sum[i] = cum_sum[i-1] + trap;
    }
    float half_val = cum_sum[v.size() - 1] / 2;
    //std::cerr << "blah" << std::endl;
	
    std::vector<float>::iterator lh,rh;
    /* TODO -- use of lower_bound here is wrong */
    lh = lower_bound(cum_sum.begin(), cum_sum.end(), half_val);
    rh = upper_bound(cum_sum.begin(), cum_sum.end(), half_val);
    assert((rh - lh) == 1 );
    float diff = *rh - *lh;
    assert( half_val >= *lh && *rh >= half_val);
    assert(diff >= 0);
    uint lh_idx = lh - cum_sum.begin();
    if ( diff == 0 ) {
      return lh_idx + 0.5f;
    }
    else {
      return lh_idx + (half_val - *lh) / diff;
    }
  }


  // This version assumes vector y holds floating-point "y" values
  // that are equally spaced w.r.t. the x-axis (i.e. constant delta x),
  // and are each >= 0.  (Exits on an empty vector or negative element.)
  //
  // The area of the trapezoid below the line joining (x1,y1) to (x2,y2),
  // where y1 and y2 are both >= 0 or both <= 0, is (x2-x1)*0.5*(y1+y2).
  int idx_of_centroid(const std::vector<float>& y)
  {
    if (y.empty())
      {
	std::cerr << "Error:  Called idx_of_centroid() on an empty vector." << std::endl;
	exit(1);
      }
    if (1 == y.size())
      return 0;

    std::vector<float> cumulative_area;
    cumulative_area.push_back(0.);
    int idx;
    for (idx = 1; idx < (int)y.size(); idx++)
      {
	if (y[idx] < 0.)
	  {
	    std::cerr << "Error:  idx_of_centroid() expected wholly nonnegative y-values; received "
		      << y[idx] << " in position " << idx << " of a vector." << std::endl;
	    exit(1);
	  }
	cumulative_area.push_back((float)(0.5*(y[idx-1] + y[idx]) + cumulative_area[idx-1]));
      }
    float half_total_auc((float)(0.5*cumulative_area[cumulative_area.size()-1]));
    idx = 1;
    while (cumulative_area[idx] < half_total_auc)
      idx++;
    if (cumulative_area.size() == idx)
      idx--; // for safety's sake, in case we can't rely upon two floats truly equalling one another
    if (cumulative_area[idx] - half_total_auc < half_total_auc - cumulative_area[idx-1])
      return idx;
    return idx - 1;
  }

  // This version assumes vector x holds the x values
  // and vector y holds the y values for a set of (x,y) points.
  // Each x value and each y value must be >= 0 (function exits when it detects otherwise).
  //
  // The area of the trapezoid below the line joining (x1,y1) to (x2,y2),
  // where y1 and y2 are both >= 0 or both <= 0, is (x2-x1)*0.5*(y1+y2).
  int idx_of_centroid(const std::vector<float>& x, const std::vector<float>& y)
  {
    if (y.size() != x.size())
      {
	std::cerr << "Error:  idx_of_centroid() received vectors of unequal size ("
		  << x.size() << ", " << y.size() << ")." << std::endl;
	exit(1);
      }
    if (x.empty())
      {
	std::cerr << "Error:  Called idx_of_centroid() on an empty vector." << std::endl;
	exit(1);
      }
    if (1 == x.size())
      return 0;

    std::vector<float> cumulative_area;
    cumulative_area.push_back(0.);
    int idx;
    for (idx = 1; idx < (int)y.size(); idx++)
      {
	if (y[idx] < 0. || x[idx] < 0.)
	  {
	    std::cerr << "Error:  idx_of_centroid() expected wholly nonnegative y-values; received "
		      << (y[idx] < 0 ? y[idx] : x[idx]) << " in position " << idx << " of a vector." << std::endl;
	    exit(1);
	  }
	cumulative_area.push_back((float)((x[idx] - x[idx-1])*0.5*(y[idx-1] + y[idx]) + cumulative_area[idx-1]));
      }
    float half_total_auc((float)(0.5*cumulative_area[cumulative_area.size()-1]));
    idx = 1;
    while (cumulative_area[idx] < half_total_auc)
      idx++;
    if (cumulative_area.size() == idx)
      idx--; // for safety's sake, in case we can't rely upon two floats truly equalling one another
    if (cumulative_area[idx] - half_total_auc < half_total_auc - cumulative_area[idx-1])
      return idx;
    return idx - 1;
  }




  /* strip_extension:
     strips the portion of filename to the right of the last delim character */
  

  /*
    1. strip file names into directory, file portions 
    2. munge on the file
    3. modify the directory if appropriate
    4. need an option to output data in the cwd or in another directory 
    
    Implementation  --
    0. convert char* to string
    1. pass in a string pair set of arguments
    a: split to two strings
  */

  



  char * strip_extension( const char * str , char * new_str , int strlen, char delim, char pathsep ) {
    char * delim_pos = strrchr((char*)str, delim);
    if ( delim_pos == NULL ) return NULL;
   
    uint len = delim_pos - str;
    strncpy( new_str, str, len);
    new_str[len] = '\0';
    return new_str;
  }



  std::vector<std::string> * split_string( std::string & s, char c ) {
    std::vector<std::string> * v = new std::vector<std::string>();
    std::vector<size_t> ps;
    int pos = 0;
    while ( true ) {
      pos = s.find(c, pos);
      if ( pos < 0 ) {
	break;
      }
      else {
	ps.push_back(pos);
      }
      pos++;
    }
    if ( ps.size() < 2 ) {
      v->push_back(s);
      return v;
    }
    else {
      v->push_back(s.substr(0,ps[0]));
      for ( uint i = 0 ; i < ps.size() - 1 ; i++ ) {
	v->push_back(s.substr(ps[i]+1, (ps[i+1] - 1  - ps[i])  ));
      }
      v->push_back(s.substr(ps[ps.size()-1]+1,s.length() - 1 - ps[ps.size() - 1] ) );
      return v;
    }
 
  }


  int read_string_array( std::vector<std::string> & in, const char * string_data, int data_length ) {
    if ( string_data[data_length-1] != '\0' ) {
      std::cerr << "chain of strings must terminate with '\0'" << std::endl;
      return -1;
    }
    int strbuf_size = 256;
    char * strbuf = (char*)malloc(strbuf_size);
    int data_idx = 0;
    int str_idx = 0;

    while ( data_idx < data_length ) {
      if ( str_idx >= strbuf_size ) {
	strbuf_size = strbuf_size * 2;
	strbuf = (char*)realloc((void*)strbuf,strbuf_size);
      }
      if ( string_data[data_idx] == '\0' ) {
	strbuf[str_idx] = '\0';
	in.push_back(std::string(strbuf));
	str_idx = 0;
	data_idx++;
      }
      else {
	strbuf[str_idx] = string_data[data_idx];
	str_idx++;
	data_idx++;
      }
    }
    free(strbuf);
    return 0;
  }

uint rand_in_range( uint low, uint high) {
 uint idx = (uint)(((double)rand()/(RAND_MAX+1)) * (high-low)) + low;
 return idx;
}

void init_rand() {
#ifdef _MSC_VER
srand((uint)time(NULL));
#else
srand48(time(NULL));
#endif

}
 
double get_rand() {
#ifdef _MSC_VER
return rand() / (double)RAND_MAX;
#else
return drand48();
#endif
}



  int tryptic_digester_proto ( std::string protein, std::vector<std::string> & peptides , int min_length ){
    const char * prot_str = protein.c_str();
    int pep_added = 0;
    std::vector<char> pepbuf;
    bool last_tryp = false;
		  
    for ( int i = 0 ; i < (int)protein.length() - 1 ; i++ ) {
      char this_res = prot_str[i];
      pepbuf.push_back(this_res);
      if ( ( this_res == 'K' || this_res == 'R' ) && prot_str[i+1] != 'P' ) {
			  
	if ( min_length < 0 || (int)pepbuf.size() >= min_length )  {
	  pepbuf.push_back('\0');
	  std::string pepstr(&pepbuf[0]);
	  pepbuf.clear();
	  peptides.push_back(pepstr);
	  pep_added++;
	}
      }
    }
    pepbuf.push_back(prot_str[protein.length() - 1]);
    if ( min_length < 0 || (int)pepbuf.size() >= min_length ) {
      pepbuf.push_back('\0');
      std::string pepstr(&pepbuf[0]);
      peptides.push_back(pepstr);     
      pep_added++;
    }
    return pep_added; 
  }


	
	
  std::string trim(std::string& s, const std::string & drop)
  {
    std::string r=s.erase(s.find_last_not_of(drop)+1);
    return r.erase(0,r.find_first_not_of(drop));
  }
	      

	      
  void tokenize_string(const std::string& str,
		       std::vector<std::string>& tokens,
		       const std::string& delimiters )
  {
    // Skip delimiters at beginning.
    std::string::size_type lastPos = str.find_first_not_of(delimiters, 0);
    //std::string::size_type lastPos = str.find_first_not_of('\0',0);
    // Find first "non-delimiter".
      //std::string::size_type pos     = str.find_first_of('\0',0);
    std::string::size_type pos     = str.find_first_of(delimiters, lastPos);
		    
    while (std::string::npos != pos || std::string::npos != lastPos)
      {
	// Found a token, add it to the vector.
	tokens.push_back(str.substr(lastPos, pos - lastPos));
	// Skip delimiters.  Note the "not_of"
	lastPos = str.find_first_not_of(delimiters, pos);
	// Find next "non-delimiter"
	pos = str.find_first_of(delimiters, lastPos);
      }
  }





  std::vector< TwoGroupsType >
      select_N_wo_replacement_twogrps ( IdxGroupType g1, IdxGroupType g2, int N ) {
          std::vector< TwoGroupsType > v = permute_two_groups ( g1, g2 );
          if ( N < (int)v.size() ) {
              throw("invalid");
          }
          std::random_shuffle( v.begin(), v.end() );
          v.resize(N);
          return v;
  }

  std::vector< TwoGroupsType >
      select_N_w_replacement_twogrps ( IdxGroupType g1, IdxGroupType g2, int N ) {
          std::vector< TwoGroupsType > v = permute_two_groups ( g1, g2 );
          if ( N < (int)v.size() ) {
              throw("invalid");
          }
          std::vector< TwoGroupsType > r(0);
          for ( int i = 0 ; i < N ; i++ ) {
              //somewhat lousy random number selection -- biased slightly towards 0
              uint idx = (uint)(((double)rand()/(RAND_MAX+1)) * v.size());
              r.push_back(v[idx]);
          }
          return v;
  }


std::vector< TwoGroupsType >
permute_two_groups ( IdxGroupType g1, IdxGroupType g2 ) {
   int g1_len = g1.size();
   int g2_len = g2.size();
  
   //vector for storing all selections of size(len(g1))
   std::vector< std::vector< int > > g1_permutes;   
   std::vector< int > all_idxs;
   all_idxs.insert(all_idxs.end(), g1.begin(), g1.end());
   all_idxs.insert(all_idxs.end(), g2.begin(), g2.end());
   IdxGroupType all_idxs_set(all_idxs.begin(), all_idxs.end());

   //std::set::const_iterator it = g1.begin();
   //while ( it != g1.end() ) {
   //   all_idxs.push_back(*it);
   //   it++;
   //}
   //it = g2.begin()
   //while ( it != g2.end() ) {
   //   all_idxs.push_back(*it);
   //   it++;
   //}
   std::vector< std::vector< int > > new_g1s = unique_selections ( all_idxs, g1_len );
   std::vector< TwoGroupsType > r(0);
   for ( int i = 0 ; i < (int)new_g1s.size(); i++ ) {
       IdxGroupType new_g1(new_g1s[i].begin(), new_g1s[i].end());
       IdxGroupType new_g2;
       std::insert_iterator< IdxGroupType > ii( new_g2, new_g2.begin());
       std::set_difference(all_idxs_set.begin(), all_idxs_set.end() ,
           new_g1.begin(), new_g1.end(), 
           ii);
       r.push_back ( TwoGroupsType( new_g1, new_g2 ) );
   }
   return r;
}
   
    
   




std::vector< std::vector< int > > unique_selections ( std::vector< int > vals , int N ) {
    if ( N == 0 ) {   
        throw("invalid");
    }
    else if ( N == 1 ) {
        std::vector< std::vector< int > > r(0);
        for ( int i = 0 ; i < (int)vals.size() ; i++ ) {
            std::vector<int> v (1,vals[i]);
            r.push_back(v);
        }
        return r;
    }
    else {
        std::vector< std::vector< int > > r(0);
        for ( int i = 0 ; i < (int)vals.size() ; i++ ) {
            std::vector< int > sub( vals.begin() + i + 1 , vals.end() );
            std::vector< std::vector< int > > u = unique_selections ( sub, N - 1 );
            for ( int u_idx = 0 ; u_idx < (int)u.size() ; u_idx++ ) {
                std::vector< int > seed(1,vals[i]);
                seed.insert(seed.end(), u[u_idx].begin(), u[u_idx].end() );               
                r.push_back(seed);
            }
        }
        return r;
    }
}


  float spectra_pair_maxlog10 ( const std::vector<float> & s1, const std::vector<float> & s2) {
    float my_max = max_vect(s1);
    float other_max = max_vect(s2);
    float both_max = std::max(my_max,other_max);
    float logMaxI;
				
    if ( both_max <= 0.0f ) {
      logMaxI = 0.0f;
    }
    else {
      logMaxI = log10f(both_max+1);
    }
    return logMaxI;
  }
  float spectra_pair_max ( const std::vector<float> & s1, const std::vector<float> & s2) {
    float my_max = max_vect(s1);
    float other_max = max_vect(s2);
    return std::max(my_max,other_max);
  }
  
  float spectra_pair_either_zero ( const std::vector<float> & s1, const std::vector<float> & s2) { 
    float s1_s2_prod = mult_accum_vects( s1, s2 );
    if ( s1_s2_prod == 0.0f ) {
      return 0.0f;
    }
    else 
      return 1.0f;
  }

  void spectra_sqrt ( std::vector<float> & s, void * p ) {
    int size = s.size();
    for ( int i = 0 ; i < size ; i++ ) {
      float intensity = s[i];
      s[i] = sqrtf(intensity);
    }
  }
  void spectra_log10 ( std::vector<float> & s, void * p){
    int size = s.size();
    for ( int i = 0 ; i < size ; i ++ ) {
      float intensity = s[i];
      if ( intensity <= 0.0f ) {
	s[i] = 0.0f;
      }
      else {
	s[i] = log10f(intensity);	
      }
    }
  }

  void spectra_filtby_I( std::vector<float> & s, void * p ) {
    float threshold = *(float*)p;
    for ( int i = 0; i < (int)s.size() ; i++ ) {
      if ( s[i] < threshold ) {
	s[i] = 0.0f;
      }
    }
  }

  ///zeros out all but the top N signals in a spectrum
  void spectra_topN ( std::vector<float> & s , void * p ) {
    int n = *(int*)p;
    int size = s.size();
    std::vector< std::pair<float,int> > sorted(s.size());
    for ( int i = 0 ; i < (int)s.size() ; i++ ) {
      sorted[i].second = i;
      sorted[i].first = s[i];
    }
    std::sort(sorted.begin(),sorted.end());
    std::fill(s.begin(),s.end(),0.0f);
    for ( int i = 0 ; i < n ; i++ ) {
      std::pair<float,int> & p = sorted[size - 1 - i];
      s[p.second] = p.first;
    }
  }
  ///converts a spectrum to the top N rank values, the rest are zero
  void spectra_topN_rank ( std::vector<float> & s , void * p ) {
    int size = s.size();
    int n = *(int*)p;
    std::vector< std::pair<float,int> > sorted(s.size());
    for ( int i = 0 ; i < (int)s.size() ; i++ ) {
      sorted[i].second = i;
      sorted[i].first = s[i];
    }
    //std::pair is sorted first by 'first' field, i.e intensity, from lowest to highest
    std::sort(sorted.begin(),sorted.end());
    std::fill(s.begin(),s.end(),0.0f);
    for ( int i = 0 ; i < n ; i++ ) {
      std::pair<float,int> & p = sorted[size - 1 - i];
      s[p.second] = (float)(n - i);
    }
  }

  /* reduces a set of pair ranges down to a unique set based upon overlaps. If the adjacent flag is set,
     then adjacent ranges are also joined together */
  std::vector<std::pair<int,int> > unique_ranges ( std::vector< std::pair<int, int> > pairs , bool adjacent) {
    std::sort(pairs.begin(), pairs.end());
    std::vector<std::pair< int,int > > out_pairs(0);
    for ( int lh_pair = 0 ; lh_pair < (int)pairs.size(); lh_pair++ ) {
      int np_start = pairs[lh_pair].first;
      int np_stop  = pairs[lh_pair].second;
      for ( int rh_cand_pair = lh_pair + 1; rh_cand_pair < (int)pairs.size() ; rh_cand_pair++ ) {
	int lh_test = pairs[rh_cand_pair].first;
	if ( adjacent ) { lh_test = lh_test - 1; }
	if ( lh_test <= np_stop) {
	  np_stop = pairs[rh_cand_pair].second;
	  lh_pair++;
	}
	else {
	  break;
	}
      }
      out_pairs.push_back(std::pair<int,int>(np_start,np_stop));
    }
    return out_pairs;
  }


} //END NAMESPACE crawutils

//passed by copy due to the sort

double crawstats::ttest_pvalue( double t_score, int df ) {
  //just in case your boost doesn't have ibeta below, we punt and return 1.0
  #ifdef OLD_BOOST
  return 1.0;
  #else
  return boost::math::ibeta(0.5 * df , 0.5 , df / ( df + t_score*t_score) );
  #endif
   //return myincbet(0.5 * df, 0.5, df / ( df + t_score*t_score ) );
}


int crawstats::feature_index_from_type(const std::pair<int,int>& thepair, int whichOne)
{
  if (1 == whichOne)
    return thepair.first;
  return thepair.second;
}

// "Storey" meaning a cubic spline fit was recommended in Storey and Tibshirani PNAS 2003.
// This continued-fraction algorithm was derived by erynes in 2009.
float crawstats::pi0_from_StoreySplineFit(const std::vector<std::pair<float, float> >& xydata)
{
  int n = -1 + static_cast<int>(xydata.size()); // (x_0,y_0), ..., (x_n,y_n)
  if (n < 1)
    throw("invalid input received by pi0_from_StoreySplineFit");

  float c(0.), v, term(0.), h(xydata[1].first - xydata[0].first);
  float v_coeff(6.0f/(h*h)); // avoid redundant recalculations of this
  float z_nMinus1;
  float x_n(xydata[n].first), x_nMinus1(xydata[n-1].first),
    y_n(xydata[n].second), y_nMinus1(xydata[n-1].second);

  for (int i = 1; i <= n - 1; i++)
    {
      v = (float)(v_coeff*(xydata[i+1].second - 2.*xydata[i].second + xydata[i-1].second));
      c = (float)(1./(4. - c)); // yes, a continued fraction!
      term = c*(v - term);
    }

  z_nMinus1 = term; // z_0 == 0 == z_n

  double result = z_nMinus1*(x_n - 1.)*(x_n - 1.)*(x_n - 1.)/(6.*h);
  result += y_n*(1. - x_nMinus1)/h;
  result += (y_nMinus1/h - h*z_nMinus1/6.)*(x_n - 1.);

  return (float)result;
}



/*
double crawstats::myincbet(double a, double b, double c) {
  //have not built cephes in VC++ yet
#ifdef _MSC_VER
  return 1.0;
#else 
#ifdef _NO_CEPHES
  return 1.0;
#else
  return incbet(a,b,c);
#endif
#endif


}
*/

//#define _GFUTILS_MAIN
#ifdef _CRAWUTILS_MAIN
int main(int argc, char ** argv) {
  using namespace crawutils;
  using namespace crawstats;
  const int a_size = 5;
  float s1f[a_size] = { 4.0f, 5.0f, 2.0f , 3.0f, 9.0f };
  float s2f[a_size] = { 2.0f, 6.0f , 1.0f, 2.0f, 6.0f };

  std::cout << "ready?" << std::endl;
  std::vector<float> s1(s1f,s1f+a_size);
  std::vector<float> s2(s2f,s2f+a_size);

  //std::cout << spectra_spearman_rank_corr(s1,s2) << std::endl;
  std::cout << mean(s1) << std::endl;
  std::cout << mean(s2) << std::endl;
  std::cout << var_w_mean(s1,mean(s1)) << std::endl;
  std::cout << var_w_mean(s2,mean(s2)) << std::endl;
  float pvalue,pscore;
  ttest_ind(s1,s2,pvalue,pscore);
  std::cout << pvalue << "\t" << pscore << std::endl;
  ttest_dep(s1,s2,pvalue,pscore);
  std::cout << "Testing the paired t-test:  p-value = "
	    << pvalue << ", t-statistic = " << pscore << std::endl;
  std::sort(s1.begin(),s1.end());
  std::sort(s2.begin(),s2.end());

  std::cout << "testing get_lh_idx : " << std::endl;
  std::cout << "vector: ";
  output_vector( std::cout , s1 );
  std::cout << std::endl;
  std::cout << "key:0 " << get_lh_idx(s1,0.0f) << std::endl;
  std::cout << "key:20 " << get_lh_idx(s1,20.0f) << std::endl;
  std::cout << "key:2.5" << get_lh_idx(s1,2.5f) << std::endl;
  std::cout << "key:3.0" << get_lh_idx(s1,3.0f) << std::endl;

  std::cout << "testing unique_selection" << std::endl;
  std::vector<int> uniq_sel(1,0);
  uniq_sel.push_back(1);
  uniq_sel.push_back(2);
  uniq_sel.push_back(3);
  uniq_sel.push_back(4);


  std::vector< std::vector< int > > v = unique_selections( uniq_sel, 3);
  for ( int i = 0 ; i < v.size() ; i++ ) {
      crawutils::output_vector( std::cout , v[i] );
      std::cout << std::endl;
  }


      
}

#endif


