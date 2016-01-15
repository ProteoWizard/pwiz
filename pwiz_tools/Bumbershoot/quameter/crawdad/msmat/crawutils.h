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
#ifndef __GFUTILS__
#define __GFUTILS__


#include <string>
#include <stdlib.h>
#include <stdio.h>
#include <math.h>
#include <assert.h>
#include <time.h>
#include <set>
#include <map>
#include <list>
#include <vector>
#include <algorithm>
#include <numeric>
#include <limits>
#include <iostream>
#include <fstream>
#ifndef OLD_BOOST
#include <boost/math/special_functions/beta.hpp>
#endif

#ifdef _MSC_VER
#define roundf round
#define ceilf ceil
#define floorf floor

//disable warnings about unsafe libc code in VC++2005
#ifndef _CRT_SECURE_NO_WARNINGS // Add this in preprocessor settings instead
#define _CRT_SECURE_NO_WARNINGS
#endif
#endif

/* obviously the world is not divided into vc++ and other platforms/compilers, but oh well */
/* define file path seperator */
#ifdef _MSC_VER
#define PATHSEP '\\'
#else 
#define PATHSEP '/'
#endif

//#define ER_DEBUG


/* define FSEEK, FTELL 64-bit offset compatible macros  --
    arguments are :
    int FSEEK( FILE * h , off_t pos, int pos_type )
    off_t FTELL( FILE * h )
 */

//todo test these macros...
#ifdef _MSC_VER
#define FSEEK(h,p,offset_flag) _fseeki64(h,p,offset_flag)
#define FTELL(h) _ftelli64(h)
typedef __int64 off64_t; //off_t is only 32-bit on 32-bit windows
#else

#ifndef _LARGEFILE_SOURCE
#error "need to define _LARGEFILE_SOURCE!!"
#endif    /* end _LARGEFILE_SOURCE */

#define FSEEK(h,p,offset_flag) fseeko(h,p,offset_flag)
#define FTELL(h) ftello(h)
//TODO -- I should check during the build if off_t really is 
//-- No need to. Posix OS already define off64_t properly
//typedef off_t off64_t; //if I compile with _LARGEFILE_SOURCE and _FILE_OFFSET_BITS=64, off_t is 64-bit

#endif /* end _MSC_VER */

#ifdef _MSC_VER   
#include <direct.h>
#define GETCWD _getcwd
#else
#include <unistd.h>
#define GETCWD getcwd
#endif


#define ALLZERO_SPECTRA_COMPARISON -2.0f;

typedef float flt_map_type;
typedef unsigned int uint;
typedef std::set<flt_map_type> flt_set;
typedef std::map<flt_map_type, flt_map_type> flt_map;
typedef std::map<flt_map_type, int> flt_idx_map;
typedef std::set<int> IdxGroupType;
typedef std::pair< IdxGroupType, IdxGroupType > TwoGroupsType;


//Macro for general large file support - RAMP toolkit style
#ifndef _LARGEFILE_SOURCE // use MSFT API for 64 bit file pointers
typedef fpos_t f_off;
#include <io.h>
#endif 



///Struct for comparing two floats using < operator

struct ltflt
{
  bool operator() ( float s1, float s2) const 
  {
    return ( s1 < s2 );
  }
};


namespace crawutils { 
const static double SQRSUM_NULL = -1.0;

///summarizes the dimensions of a 2D array

uint rand_in_range( uint low, uint high);

void make_idx_segments ( int num_mzs, int num_rts, int num_mzs_segments, int num_rts_segments,
                        std::vector<std::pair<int,int> > & mzs_segs,
                        std::vector<std::pair<int,int> > & rts_segs );

///Utility class for directory name and filename information of a file

/**
file_info summarizes the original filename and directory of an MSMAT file, and contains utility functions to strip and change the
MSMAT file prefix. It deals with 'extensions' and 'prefixes'.
extension - typically the '.msmat' at the end of the filename
prefix - an initial group of characters separated by a '.', when there are at least three groups. i.e. in foo.R1.msmat,
foo is the prefix
*/ 

class file_info {
   public :
  std::string dirpath;
  std::string filename;
  std::string original_filename;
  std::string original_dirpath;
  static const char pathsep = PATHSEP;
  bool ext_stripped;
  bool pref_stripped;

  file_info();
  file_info(const char * fname) ;
  file_info(const file_info & rhs);
    
  ///strips last .[word] extension from the filename
  void strip_extension(  char delim = '.' );

  ///strips the prefix from the filename
  void strip_prefix ( char delim = '.' ) ;
 
  ///returns the filename prefix 
  std::string get_prefix ( char delim = '.');

  ///adds a prefix - note that this will add, rather than replace
  void add_prefix ( const char * new_prefix ,char delim = '.');
  ///changes the existing prefix
  void change_prefix( const char * new_prefix, char delim = '.');
  ///adds an extension to a file
  void add_extension ( const char * extension, char delim = '.' );
  ///returns the full path to the file
  std::string full_path();



};

  const std::string DEFAULT_MSMAT_SET_LABEL("NO_MSMAT_SET_NAME");
  const std::string DEFAULT_MSMAT_LABEL("NO_MSMAT_NAME");

/* TODO -- deal with reverse ordered */


///checks if a vector is sorted
template<typename data_type>
bool ordered_list( std::vector<data_type> & v );
/*	
template<typename data_type>
data_type sum_list( data_type * d, size_t  l );

template<typename data_type>
data_type max_list( data_type * d, size_t l );
*/

std::string trim_whitespace ( const std::string & in_str );
double calc_sqrsum ( const std::vector<float> & f);
/// TODO ??
void rankties( std::vector<float> & v );
float spectra_pair_maxlog10 ( const std::vector<float> & s1, const std::vector<float> & s2) ;
  float spectra_pair_max( const std::vector<float> & s1, const std::vector<float> & s2);
  float spectra_pair_either_zero ( const std::vector<float> & s1, const std::vector<float> & s2);
  //inline as it is a trivial function, but it is used as a function pointer elsewhere along with it's SpectraCompFunc brethren 
  //see SpectraCompareConfig.h
inline float spectra_pair_uniform_weight ( const std::vector<float> & s1, const std::vector<float> & s2) { return 1.0f; }

  float spectra_noise_est ( const std::vector<float> & s1, const std::vector<float> & s2, double s1_sqrsum , double s2_sqrsum , double resolution );
float spectra_cosine_angle ( const std::vector<float> & s1, const std::vector<float> & s2, 
								double s1_sqrsum, double s2_sqrsum);
float spectra_cosine_angle ( const std::vector<float> & s1, const std::vector<float> & s2);
float spectra_cosine_angle_squared ( const std::vector<float> & s1, const std::vector<float> & s2, double d1, double d2);
  float spectra_dps_nz ( const std::vector<float> & s1, const std::vector<float> & s2, double d1, double d2 );
  float spectra_cc_nz ( const std::vector<float> & s1, const std::vector<float> & s2, double d1, double d2 );

float spectra_dot_product ( const std::vector<float> & s1, const std::vector<float> & s2, double d1, double d2);
float spectra_tic_manhattan_similarity ( const std::vector<float> & s1, const std::vector<float> & s2, double d1, double d2 );
  float spectra_corr_coef_nozeros ( const std::vector<float> & s1, const std::vector<float> & s2 , double d1, double d2);
  float spectra_corr_coef ( const std::vector<float> & s1, const std::vector<float> & s2);
float spectra_corr_coef ( const std::vector<float> & s1, const std::vector<float> & s2, 
                          double s1_sqrsum, double s2_sqrsum);

  void filter_zero_values (const std::vector<float> & s1 , const std::vector<float> & s2,
			   std::vector<float> & out_s1, std::vector<float> & out_s2 ,
			   float min_val = 0.0f );
  void rank_by_pos_ties ( const std::vector< std::pair< float, int > > & sorted_by_i , std::vector<float> & to_rank, bool avg_tie = true );
  void rank_by_pos_noties ( const std::vector< std::pair< float, int > > & sorted_by_i , std::vector<float> & to_rank );

  float spectra_spearman_rank_corr ( const std::vector<float> & s1, const std::vector<float> & s2, double d1, double d2 );
  float spectra_spearman_rank_corr_2 ( const std::vector<float> & s1, const std::vector<float> & s2, double d1, double d2 );
  float spectra_spearman_rank_corr_3 ( const std::vector<float> & s1, const std::vector<float> & s2, double d1, double d2 );
  float spectra_spearman_rank_corr_4 ( const std::vector<float> & s1, const std::vector<float> & s2, double d1, double d2 );
  float spectra_spearman_rank_corr_5 ( const std::vector<float> & s1, const std::vector<float> & s2, double d1, double d2 );

  ///sqroot the spectra before use
  void spectra_sqrt ( std::vector<float> & s, void * params );
  ///log10 the spectrum before use
  void spectra_log10 ( std::vector<float> & s, void * params);
  ///kepp only the topN intensities in the spectrum
  void spectra_topN ( std::vector<float> & s , void * params );
  ///convert the spectrum to intensity rank, keep top N
  void spectra_topN_rank ( std::vector<float> & s , void * params );
  ///filter the spectrum by intensity, retain only peaks above threshold N
  void spectra_filtby_I ( std::vector<float> & s, void * params );

std::vector<float>::const_iterator find_nearest( const std::vector<float> & v, float key );

double vector_magnitude ( const std::vector<float> & s );
float spectra_tic_geometric_distance ( const std::vector<float> & s1, const std::vector<float> & s2, double d1 = 0.0 , double d2 = 0.0 );
float trapezoidal_linear_centroid ( std::vector<float> & v );

int idx_of_centroid(const std::vector<float>& y);
int idx_of_centroid(const std::vector<float>& x, const std::vector<float>& y);

//flt_map::iterator find_nearest( flt_map & s, float key);

void myfree( void * mypnt);
	

void * mymalloc(size_t size, size_t n);

void print_cmd_args(int argc, char ** argv );
char * strip_extension( const char * str , char * new_str , int strlen, char delim = '.', char path_delim = PATHSEP);
char * strip_prefix ( const char * str, char *  new_str , int strlen ,
                    char delim = '.' , char path_delim= PATHSEP );



void test_dot_product();
int read_string_array( std::vector<std::string> & in, const char * string_data, int data_length );

 


int tryptic_digester_proto ( std::string protein, std::vector<std::string> & peptides , int min_length = -1 );

std::string trim(std::string& s, const std::string & drop = " ");

///boring old string tokenizer
/** splits a std::string into std::string tokens , using newline or space as a delimiter
 \param str input string
 \param tokens filled with the tokens
 \param delimiters - individual characaters used to separate tokens (default " \n") */
 
void tokenize_string(
                     const std::string& str,
                     std::vector<std::string>& tokens,
                     const std::string& delimiters = " \n");
///creates all permutations, shuffles, and takes the first N instances. see permute_two_groups.
 std::vector< TwoGroupsType >
      select_N_wo_replacement_twogrps ( IdxGroupType g1, IdxGroupType g2, int N );

///creates all permuations, shuffles, and takes N random instances. see permute_two_groups.
 std::vector< TwoGroupsType >
      select_N_w_replacement_twogrps( IdxGroupType g1, IdxGroupType g2,  int N );

///creates all unique permutations of two groups of indices
std::vector< TwoGroupsType >
permute_two_groups ( IdxGroupType g1, IdxGroupType g2 );

std::vector< std::vector< int > > unique_selections ( std::vector< int > vals , int N );

 


template <typename T> 
void extend_vector_set ( std::vector<T> & out_vec,  std::vector< std::vector<T> > & in_vecs ) {
    int total_size = 0 ;
    for ( int i = 0; i < in_vecs.size() ; i++ ) {
       total_size += in_vecs[i].size();
    }
    out_vec.clear();
    out_vec.reserve(total_size);
    for ( int i = 0 ; i < in_vecs.size(); i++ ) {
        for ( int in_vec_idx = 0 ; in_vec_idx < in_vecs[i].size(); in_vec_idx++ ) {
            out_vec.push_back(in_vecs[i][in_vec_idx]);
        }
    }
}

template <typename T> 
double area_under_curve ( const std::vector<T> & vals, int start_idx, int stop_idx ) {
   double sum = 0.0;
   for ( int i = start_idx ; i <= stop_idx - 1 ; i++ ) {
      sum += vals[i]; //rectangle to right of current value
      sum += ( vals[i+1] - vals[i] ) / 2.0;
   }
   return sum;
}
template <typename T>
double area_under_curve ( const std::vector<T> & vals ) {
   return area_under_curve(vals, 0, vals.size() - 1);
}


///reverses a vector in-place
template<typename data_type>
void reverse_vect( std::vector<data_type> & v ) {
  uint last = v.size() - 1;
  data_type t;
  for ( uint i = 0 ; i < v.size() / 2 ; i++ ) {
    t = v[i];
    v[i] = v[last-i];
    v[last-i] = t;
  }
}

///sum of a vector
template<typename data_type>
data_type sum_vect( const std::vector<data_type> & v ) {
	data_type t = (data_type)0.0;
	for ( size_t i = 0 ; i < v.size() ; i++ ) {
		t += v[i];
	}
	return t;
}

///sum of an array, size is passed in
template<typename data_type>
data_type sum_list( data_type * d, size_t  l ) {
  data_type t = 0.0;
  for ( size_t i = 0 ; i < l  ; i++ ) {
    t += d[i];
  }
  return t;
};

///mean of an stl container
template<class cont, class value_type> 
value_type average_stl_cont( cont & a ) {
	value_type sum = std::accumulate(a.begin(), a.end(), (value_type)0);
    return sum / a.size();
}


  /*
def shuffle_list(l) :
    '''Does an in-place Fisher-Yates shuffle of a sequence
    Described in some detail at
    http://www.nist.gov/dads/HTML/fisherYatesShuffle.html

    exchange each element in an array l of size n,
    starting at l[n-1], going down to 0, let the current index be i
    pick a random index from 0 to i to swap with i.

    Notes on randomization:
    This uses pythons builtin random library, which uses a Wichmann-Hill generator
    According to the documentation:
    "While of much higher quality than the rand() function supplied by most C libraries,
    the theoretical properties are much the same as for a single linear congruential generator
    of large modulus. It is not suitable for all purposes, and is completely unsuitable for
    cryptographic purposes."
    So, it should be fine for shuffling sequence databases
    The seed is automatically created from the system time,
    and the generator has a period of ~ 7e12.
    '''
    #i ranges from the last index to zero, decrementing by 1
    for i in range(len(l)-1,0,-1) :
        #j (swap index) goes from 0 to i

        j = int(random.random() * (i+1))
        #swap indices i,j
        (l[i],l[j]) = (l[j],l[i])
    return l




  */

///fyates_shuffle -- shuffles a vector
template<typename data_type>
std::vector<data_type> fyates_shuffle( const std::vector<data_type> & v ) {
  
  srand48(time(NULL));
 

  std::vector<data_type> new_vect(v.begin(), v.end());
  data_type swap;
  for ( int i = 0 ; i < v.size() ; i++ ) {
    int j = (int)floor((drand48() * i+1));
    swap = new_vect[i];
    new_vect[i] = new_vect[j];
    new_vect[j] = swap;
  }
  
  return new_vect;
    
}

std::vector< std::pair<int,int> > fyates_shuffle_commands( int series_size );
  template<typename data_type>
  std::vector< data_type> apply_fyates_shuffle (const std::vector<data_type> & v, std::vector< std::pair<int, int> > & cmds ) {
    data_type swap;
    std::vector<data_type> out_v(v.begin(), v.end());
    for ( int i = 0 ; i < cmds.size() ; i++ ) {
      swap = out_v[ cmds[i].first ];
      out_v[ cmds[i].first ] = out_v[ cmds[i].second ];
      out_v[ cmds[i].second ] = out_v[cmds[i].first ];
    }
    return out_v;
  }



  void init_rand();
  double get_rand();
  
  template<typename data_type>
  void fyates_shuffle( std::vector<data_type> & v ) {
    init_rand();
 

    data_type swap;
    for ( int i = 0 ; i < v.size() ; i++ ) {
      int j = (int)floor((get_rand() * i+1));
      swap = v[i];
      v[i] = v[j];
      v[j] = swap;
    }
  }



///returns a vector being the product of two other vectors
template<typename data_type>
std::vector<data_type> vect_mult ( const std::vector<data_type> & a , const std::vector<data_type> & b) {
	assert(a.size() == b.size());
	std::vector<data_type> t(a.size());
	for ( int i = 0 ; i < a.size() ; i++ ) {
		t[i] = a[i] * b[i];
	}
	return t;
}

///returns a vector<double> being the product of two other datatypes
template<typename data_type>
std::vector<double> vect_multd( std::vector<data_type> & a, std::vector<data_type> & b) {
	assert(a.size() == b.size());
	std::vector<double> t(a.size());
	for ( uint i = 0 ; i < a.size() ; i++ ) {
		t[i] = a[i] * b[i];
	}
	return t;
}

///returns the summed product of two vectors
template <typename data_type>
data_type mult_accum_vects( const std::vector<data_type> & a, const std::vector<data_type> & b ) {
    assert(a.size() == b.size());
    uint lim = a.size();
    data_type accum = (data_type)0.0;
	for ( uint i = 0 ; i < lim ; i++ ) {
       accum += a[i] * b[i];
	}
    return accum;
}

///returns the maximum value of a vector
template<typename data_type>
data_type max_vect( const std::vector<data_type> & d ) {
  data_type max = (data_type)0.0;
  for ( size_t  i = 0; i < d.size(); i++ ) {
    if ( d[i] > max ) {
      max = d[i];
    }
  }
  return max;
}
///minimum value of a vector
template<typename data_type>
data_type min_vect( const std::vector<data_type> & d ) {
  data_type min = (data_type)1e30;
  for ( size_t  i = 0; i < d.size(); i++ ) {
    if ( d[i] < min ) {
      min = d[i];
    }
  }
  return min;
}


template<typename data_type>
data_type max_list( data_type * d, size_t l ) {
  data_type max = 0.0;
  for ( size_t i = 0 ; i < l ; i++ ) {
    if ( d[i] > max ) {
      max = d[i];
    }
  }
  return max;
};

///returns a std::pair<int, data_type> of the index and value of the first instance of the maximum
template<typename data_type>
std::pair<int , data_type> max_pair( const std::vector<data_type> & v ) {
   int idx = max_idx_vect_bound(v,0,v.size());
   return std::pair<int, data_type>(idx,v[idx]);
}


template<typename T>
bool Comp_PairFloatOther_LessThan(const std::pair<float,T>& a, const std::pair<float,T>& b)
{
  return a.first < b.first;
}

template<typename data_type>
uint max_idx_vect( std::vector<data_type> & v ) {
  return max_idx_vect_bound(v,0,v.size());
};

///returns maximum index within bounds [ start, stop ) (not inclusive of stop)
template<typename data_type>
uint max_idx_vect_bound( std::vector<data_type> & v , uint start, uint stop ) {
  uint max_idx = start;
  data_type max = (data_type)0;
  for ( uint i = start; i < stop ; i++ ) {
    if ( max < v[i] ) {
      max = v[i];
      max_idx = i;
    }
  }
  return max_idx;
};

///returns index of minimum values within bounds [ start, stop ) (not inclusive of stop)
template<typename data_type>
uint min_idx_vect_bound( std::vector<data_type> & v , uint start, uint stop ) {
  uint min_idx = start;
  data_type min = (data_type)0;
  for ( uint i = start; i < stop ; i++ ) {
    if ( min > v[i] ) {
      min = v[i];
      min_idx = i;
    }
  }
  return min_idx;
};


///crude deserializer for arrays. calls memcpy to load binary data from a char * to a pointer
/** \param d - array into which data will be loaded
    \param field_data - const char * with serialized data
    \param data_len - len of data in bytes
*/
template <typename data_type>
void  load_type(data_type * d, const char * field_data, int data_len) {
	//assert(data_len == sizeof(data_type));
		memcpy(d,field_data,data_len);
}

///crude serializer for arrays. writes an array to an ostream as bytes
template <typename data_type>
  void write_type(data_type * d, int data_len, std::ostream & o ) {
  const char * data_as_charstr = (const char*)d;
  o.write(data_as_charstr, data_len * sizeof(data_type));
}
	
template <typename data_type>
void load_int_type( data_type * out_val, char * field_data, int data_len );
	
template <typename data_type>
void load_array( data_type * out_val, const char * field_data, int char_len ) {
	//TODO -- deal with different endianness in the data
	memcpy((void*)out_val, (void*)field_data, char_len );	
};


template <typename data_type>
void load_array( data_type * out_val , const char * field_data, int char_len, int num_fields ) {
	//assert(num_fields * sizeof(data_type) == char_len);
	load_array(out_val, field_data, char_len);
};


	
template<typename data_type>
data_type templ_abs( data_type v );

/* probably could be more numerically accurate */
double round_to_dbl(double n, double r);


///are all values in the vector unique
template<typename T>
bool unique_vect ( std::vector<T> & v) {
	for ( uint i = 0 ; i < v.size() - 1 ; i++ ) {
		if ( v[i] == v[i+1] ) {
			return false;
		}
	}
	return true;
}

///is the vector monotonically increasing?				
template<typename T> 
bool monotonic_vect ( std::vector<T> & v) {
	for ( uint i = 0 ; i < v.size() - 1 ; i++ ) {
		if ( v[i+1] < v[i] ) 
			return false;
	}
	return true;
}





template <typename T>
double sqrsum(std::vector<T> & a );
//TODO -- calculate sqrsum...

//template< typename data_type>
//uint get_lh_idx (std::vector<data_type> & v, data_type lookup );

//uint get_lh_idx (std::vector<float> & v, float lookup );
//uint get_lh_idx (std::vector<int> & v, int lookup );


/** get_lh_idx --
   finds the index(lower_bound) where an element would
   be inserted into a vector -- this should give the left-hand side
   of the lower bound for an element to be inserted into a list  -

   Note -- this interprets lower_bound correctly unlike some other items of the code...

*/
template< typename T>
uint get_lh_idx (std::vector<T> & v, T lookup ) {
    assert(monotonic_vect(v));
    typename std::vector<T>::iterator begin = v.begin();
    typename std::vector<T>::iterator end = v.end();
	typename std::vector<T>::iterator lh = lower_bound(begin , end, lookup);
    if ( lh == end ) {
        //std::cerr << "after end" << std::endl;
        return v.size() - 1;
    }
    if ( lookup == *lh ) { 
      return lh - begin;
    }
    else if ( lh == begin ) { return 0 ; }
    else { return ( lh - 1 - begin ) ; }
}

std::vector<std::string> * split_string( std::string & s, char c );



template <typename T>
void output_vector ( std::ostream & o, std::vector<T> & v, char delim=' ' ) {
    //o << '[';
    if ( v.size() > 0 ) {
	for ( uint i = 0; i < v.size() - 1; i++ ) {
         o << v[i] << delim;
	}
    o << v[v.size()-1];
    }
};

///creates a range from lh to rh with jump 
template <typename T>
void create_range ( T lh, T rh, T jump, std::vector<T> & output ) {
    T p = lh;
    while ( p < rh ) {
      output.push_back(p);
      p += jump;
    }
};


///given a vector of value
template <typename T>
int retain_vector ( const std::vector<int> & idxs,  const std::vector<T> & vals, std::vector<T> & ret )  {
	 std::vector<int> idxs_c(idxs);
	 std::sort(idxs_c.begin(), idxs_c.end());
     
	 std::list<T> keep_vals(0);
	 for ( uint i = 0 ; i < vals.size() ; i++ ) {
          keep_vals.push_back(vals[i]);
	 }
     typename std::list<T >::iterator ki = keep_vals.begin();
     uint i_idx = 0;


     int retained = 0;
	 while ( true ) {
		 if ( i_idx >= idxs_c.size()  ) {
             break;
		 }
		 while ( idxs[i_idx] != *ki ) {
			 if ( ki == keep_vals.end() ) {
      
                 break;
			 }
             typename std::list< T >::iterator d = ki;
             ki++;
             keep_vals.erase(d);
		 }
         retained++;
		 if ( ki == keep_vals.end() ) {
           break;
		 }
         ki++;
         i_idx++;
	 }
     ret.resize(keep_vals.size());
     typename std::list<T>::iterator k = keep_vals.begin();
	 for ( uint i = 0 ; i < keep_vals.size() ; i++ ) {
          ret[i] = *k;
          k++;
	 }

     return retained;
};



///mean over a 2D vector of vectors
template <typename T>
void average_mat_2d ( const std::vector< std::vector<T> > & data, std::vector<T> & avg ) {
   assert(data[0].size() == avg.size());
   //iterate over each member of data (i) 
   for ( uint avg_idx = 0 ; avg_idx < avg.size() ; avg_idx++ ) {
       T s = (T)0;
       for ( uint data_idx = 0; data_idx < data.size() ; data_idx++ ) {
          s += data[data_idx][avg_idx];
       }
       avg[avg_idx] = s / data.size();
   }
}


template <typename T>
void allocate_2D_vector ( size_t d1, size_t d2 , std::vector< std::vector<T> > & mod_vect ) {
  mod_vect.resize(d1);
  for ( uint i = 0; i < d1 ; i++ ) {
    mod_vect[i].resize(d2);
  }  
}

std::vector<std::pair<int,int> > unique_ranges ( std::vector< std::pair<int, int> > pairs, bool adjacent = false );

template <typename T>
void mydelete ( T* & in_arg ) {
    if ( in_arg != NULL ) {
       delete in_arg;
       in_arg = NULL;
    }
}

};

namespace crawstats {




template <typename T>
    double median ( const std::vector<T> & v ) {
         
        if (v.size() == 0) {
            throw("madonna mia! cannot take median of an empty vector");
        }
        else if ( v.size() == 1 ) {
           return v[0];
        }
        std::vector<T> v_c = v;
        //sort to derive median value
        
        std::sort(v_c.begin(), v_c.end());

        if ( v_c.size() % 2 == 1 ) {
          return (double)(v_c[(v_c.size() - 1) / 2]);
        }
        //even
        else {
          int rh_mid = v_c.size() / 2;
          return (v_c[rh_mid] + v_c[rh_mid-1]) / 2.0;
        }
    }

///sum of squares
template < typename T >
double ss( std::vector<T> & v ) {
  double t = 0.0;
  for ( int i = 0 ; i < v.size() ; i++ ) {
       t += v[i] * v[i];
  }
  return t;
 
}

template<typename T>
double mean( const std::vector<T> & v ) {
  double t= 0.0;
  for ( int i = 0 ; i < (int)v.size() ; i++ ) {
       t += v[i];
  }
  return t / v.size();
}

///given a vector and its mean , returns the variance
template <typename T>
double var_w_mean( const std::vector<T> & v, double m ) {
  double rt = 0.0;
  for ( int i = 0 ; i < (int)v.size(); i++ ) {
    T dev = (T)(v[i] - m);
    double dev2 = static_cast<double>(dev);
    rt = rt + (dev2 * dev2);
  }
  return rt / ( v.size() - 1 );
}


///yet another mean of a vector
template<typename T>
double vector_avg( const std::vector<T> & d ) {
   double total = 0.0;
   for ( size_t i = 0 ; i < d.size(); i ++ ) {
      total += d[i];
   }
   return total / d.size();
}


/* three point differentiation -- for equally spaced points */
/* TODO -- refer to source for these formulas */
template <typename T>
T three_point_d_v1 ( const std::vector<T> & x , const std::vector<T> & y, int idx ) {
    if ( idx == 0 ) {
       return (T)0;
    }
    else if ( idx == x.size() - 1 ) {
       return (T)0;
    }
    else { 
       return (y[idx+1] - y[idx-1]) / ( x[idx-1] + x[idx+1] );
    }
}


template <typename T>
T three_point_d_v2 ( const std::vector<T> & x , const std::vector<T> & y, int idx ) {
    if ( idx >= y.size() - 2 ) {
       return ( -3 * y[idx] + 4 * y[idx-1] - y[idx-2] ) / -2.0;
    }  
    else {
        return ( -3 * y[idx] + 4 * y[idx+1] - y[idx+2] ) / 2.0;
    }
}

template <typename T>
T five_point_d_stencil ( const std::vector<T> & x, const std::vector<T> & y, int idx ) {
    if ( idx <= 2 || idx >= x.size() - 2) {
       return three_point_d_v2(x,y,idx);
    }
    else {
       double n = (-1 * y[idx+2] + 8*y[idx+1] - 8 * y[idx-1] + y[idx-2] )   / 12;
    }
}


/* NOT IMPLEMENTED
template <typename T>
void uniform_rand_dist ( std::vector<T> & v ) {
}

template <typename T>
void gauss_rand_dist ( std::vector<T> & v ) {
}
*/

///returns the p-value given a T-statsitca and degrees of freedom.

///Note that if the incomplete beta integral is not available (windows for now), this returns -1



double ttest_pvalue( double t_score, int df ); 


///given two vectors, returns the t-statistic for dependent, a.k.a. paired samples (dependent t-test)
template<typename T>
float tvalue_dep(const std::vector<T>& a, const std::vector<T>& b)
{
  if (a.size() != b.size())
    {
      std::string errmsg = "Attempted to perform a paired t-test on samples of unequal size.";
      std::cerr << errmsg << std::endl;
      throw(errmsg);
    }
    std::vector<T> W(a.size());
    for ( size_t i = 0 ; i < a.size(); i++ ) {
        W[i] = a[i] - b[i];
    }

    double W_mean = vector_avg(W);
    double W_stddev = sqrt(var_w_mean(W,W_mean));
    float retval;

    if ( W_stddev == 0.0)
      {
#ifdef ER_DEBUG
	if (W_mean != 0)
	  {
	    std::cerr << "W_stddev == 0.0; a = (" << a[0];
	    for (int j = 1; j < a.size(); j++)
	      std::cerr << ", " << a[j];
	    std::cerr << "), b = (" << b[0];
	    for (int j = 1; j < b.size(); j++)
	      std::cerr << ", " << b[j];
	    std:: cerr << "), W = (" << W[0];
	    for (int j = 1; j < W.size(); j++)
	      std::cerr << ", " << W[j];
	    std::cerr << "), W_mean = " << W_mean << "." << std::endl;
	  }
#endif // ER_DEBUG

      return std::numeric_limits<float>::max();
      }
    //return static_cast<float>(W_mean / ( W_stddev / sqrt((double)a.size()) ));
    retval = static_cast<float>(W_mean / ( W_stddev / sqrt((double)a.size()) ));
#ifdef ER_DEBUG
    if (fabs(retval) > 1.0e+30 || !(retval <= 0 || retval >= 0))//|| fabs(retval) == 1)
      {
	std::cerr << "t = " << retval << ", W_stddev = " << W_stddev << ", a = (" << a[0];
	for (int j = 1; j < a.size(); j++)
	  std::cerr << ", " << a[j];
	std::cerr << "), b = (" << b[0];
	for (int j = 1; j < b.size(); j++)
	  std::cerr << ", " << b[j];
	std:: cerr << "), W = (" << W[0];
	for (int j = 1; j < W.size(); j++)
	  std::cerr << ", " << W[j];
	std::cerr << "), W_mean = " << W_mean << "." << std::endl;
      }
#endif // ER_DEBUG
    return retval;
}

///given two vectors, returns the pvalue and t-statistic for dependent, a.k.a. paired samples (dependent t-test)
template<typename T>
void ttest_dep ( const std::vector<T> & a, const std::vector<T> & b, float & pval, float & pscore ) {
  float t = tvalue_dep(a,b); // value of the t-statistic
  int df = a.size() -  1; // degrees of freedom
  pval = (std::numeric_limits<float>::max() == t) ? 0.0f : ttest_pvalue(t, df);
  pscore = t;
}


///given two vectors, returns the t-statistic (independent two-sample t-test assuming equal expected variance and unequal sample sizes)
template<typename T>
float tvalue_ind(const std::vector<T>& a, const std::vector<T>& b)
{
   double m1,m2,v1,v2;
   int n1(a.size()),n2(b.size());
   int df;
   double svar,t;

   df = n1 + n2 - 2;
   if (df <= 0)
     {
       std::cerr << "Invalid arguments to independent t-test (requires at least two numbers in one class)." << std::cerr;
       throw("Invalid arguments to independent t-test (requires at least two numbers in one class).");
     }
   m1 = vector_avg(a);
   m2 = vector_avg(b);
   v1 = var_w_mean(a,m1);
   v2 = var_w_mean(b,m2);
   svar = ( (n1-1) * v1 + (n2-1) * v2 ) / df; // unbiased estimator of the common variance between the two groups
   if (svar == 0.0)
     return std::numeric_limits<float>::max();
   return static_cast<float>((m1-m2) / sqrt( svar * ( 1.0 / n1 + 1.0 / n2 ) ));
}

///given two vectors, returns the pvalue and t-statistic (independent two-sample t-test assuming equal expected variance and unequal sample sizes)
template<typename T>
void ttest_ind ( const std::vector<T> & a, const std::vector<T> & b , float & pval, float & pscore )
{
  int df = a.size() + b.size() - 2;
  float t = tvalue_ind(a,b);
  pval = (std::numeric_limits<float>::max() == t) ? 0.0f : ttest_pvalue(t, df);
  pscore = t;
}


float pi0_from_StoreySplineFit(const std::vector<std::pair<float, float> >& xydata);
int feature_index_from_type(const std::pair<int,int>& thepair, int whichOne);

template<typename T>
void put_FDR_value(const float& fdr, std::vector<T>& vec, const int& idx)
{
  vec[idx].FDR = fdr;
}

template<typename T1, typename T2>
void FDRsFromPvals(const std::vector<std::pair<float, T1> >& pvalsForFeatures, std::vector<T2>& features)
{
  std::pair<float,float> xyPoint;
  std::vector<std::pair<float,float> > xyData;
  float final_pi0, FDR, prevFDR;
  const int N(pvalsForFeatures.size()); // number of features
  const int NUMTRIALS(95); // as in Storey's paper

  for (float lambda = .01*1; lambda <= .01*NUMTRIALS; lambda += .01*1)
    {
      int numPValsGreaterThanLambda(0), i(N-1);
      while (i >= 0 && pvalsForFeatures[i].first > lambda)
	{
	  numPValsGreaterThanLambda++;
	  i--;
	}
      xyPoint.first = lambda;
      xyPoint.second = 1.*numPValsGreaterThanLambda/(N*(1.-lambda));
      xyData.push_back(xyPoint);
    }
  final_pi0 = pi0_from_StoreySplineFit(xyData);
  // begin erynes DEBUG
  std::cerr << "final_pi0 = " << final_pi0 << std::endl;
  // end erynes DEBUG

  prevFDR = FDR = final_pi0 * pvalsForFeatures[N-1].first;
  put_FDR_value(FDR, features[feature_index_from_type(pvalsForFeatures[N-1].second,1)],
		feature_index_from_type(pvalsForFeatures[N-1].second,2));

  for (int i = N-2; i >= 0; i--)
    {
      prevFDR = FDR = std::min(static_cast<float>(final_pi0*N*pvalsForFeatures[i].first/(1.+i)), prevFDR);
      put_FDR_value(FDR, features[feature_index_from_type(pvalsForFeatures[i].second,1)],
		    feature_index_from_type(pvalsForFeatures[i].second,2));
    }
}


template <typename T>
void norm_to_unit_sum ( std::vector<T> & v ) {
   T total = (T)0.0;
   for ( int i = 0 ; i < v.size() ; i++ ) {
      total += v[i];
   }
   if ( total != (T)0.0) {
       for ( int i = 0 ; i < v.size() ; i++ ) {
           v[i] /= total;
       }
   }
}

template <typename T>

void norm_to_unit_sqrsum( std::vector<T> & v ) {
   T total=(T)0.0;
   for ( int i = 0 ; i < v.size() ; i++ ) {
      total += v[i] * v[i];
   }
   for ( int i = 0 ; i < v.size() ; i++ ) {
      v[i] / total;
   }
}

  //for some reason there was trouble making this inline w/ gcc3.4
  //  /* inline */ double myincbet(double, double, double);

}

#endif

