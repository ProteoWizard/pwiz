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
#include "ChromSmoother.h"
#define SG_DALKE_DUMP 0


/* go through each point in the spectrum, which is fully contained in the scan filter retention time window
   1. calculate border area where scans are...
   2. go through from these border points, and infer which weights to use to calculate values.
   3. serious problem, with uneven spacing, will I have to normalize the weights on an ongoing basis?
*/

/*

0 1 2 3 4 5 6 7 8 9 

 0.4  3.2 5.4  



*/


void UnevenChromSmoother::smooth_vect(const std::vector<float> &raw_vec, 
          const std::vector<float> &RT, 
          std::vector<float> &out_vec) {
   assert(raw_vec.size() == RT.size());
   std::copy(raw_vec.begin(), raw_vec.end(), out_vec.begin());
   int safe_start_rt_idx, safe_stop_rt_idx;
   safe_start_rt_idx = safe_stop_rt_idx = -1;
   float rt_delt;
       
   for ( int i = 1 ; i < (int)RT.size() ; i++ ) {
      rt_delt = RT[i] - RT[0];
      if ( (this->rt_filter_half_size) <= rt_delt ) {
         safe_start_rt_idx = i;
         break;
      }
   }

   for ( int i = RT.size() - 1; i > safe_start_rt_idx ; i-- ) {
      rt_delt = RT.back() - RT[i];
      if ( (this->rt_filter_half_size) <= rt_delt ) {
         safe_stop_rt_idx = i;
         break;
      }
   }
   
   /* begin by going through the retention time points until we find all neighbors in an appropriate
      frame -- start at points that are larger than one-half window */

   //go through each point and find flanking 
   for ( int i = 0 ; i < safe_start_rt_idx ; i++ ) {
     out_vec[i] = raw_vec[i];
   }
   for ( int i = safe_stop_rt_idx + 1 ; i < (int)RT.size() ; i++ ) {
     out_vec[i] = raw_vec[i];
   }
   
   for ( int i = safe_start_rt_idx ; i <= safe_stop_rt_idx ; i++ ) {
       /* find retention time points that are plus and minus half rt len of this point */
       float RT_lh = RT[i] - (float)this->rt_filter_half_size;
       float RT_rh = RT[i] + (float)this->rt_filter_half_size;
       std::vector<float>::const_iterator lh_bound = std::lower_bound(RT.begin(), RT.end(), RT_lh);
       std::vector<float>::const_iterator rh_bound = std::lower_bound(RT.begin(), RT.end(), RT_rh);
       int lh_idx = lh_bound - RT.begin() + 1;
       int rh_idx = rh_bound - RT.begin();
       float t = 0.0f;
       for ( int j = lh_idx ; j <= rh_idx ; j++ ) {
           float rt_delta = RT[j] - RT[i] - (float)this->rt_filter_half_size;
           int weight_idx = (int)(rt_delta / this->rt_per_filter_pt);
           float weight = this->weights[weight_idx];
           t += weight * raw_vec[j];
       }
       /* TODO FIGURE OUT WTF TO DO WITH NORMALIZATION */
       out_vec[i] = t;
   }
}



void ChromSmoother::set_weight_size( int weight_size ) {
      weights.resize(weight_size,0.0f);
      half_window_size = weight_size / 2;
      window_size = weight_size;
}

void ChromSmoother::smooth_vect_discrete( const std::vector<float> & raw_vec, std::vector<float> & out_vec) {

	assert(raw_vec.size() == out_vec.size());
	if ( (int)raw_vec.size() <= half_window_size * 2 + 1) {
	  std::copy(raw_vec.begin(), raw_vec.end(), out_vec.begin());
	}
	else {
	  
	    for ( int i = 0 ; i < half_window_size ; i++ ) {
	      out_vec[i] = raw_vec[i]; 
	    }
	    for ( uint i = raw_vec.size() - half_window_size; i < raw_vec.size() ; i++) {
	      out_vec[i] = raw_vec[i];
	    }
	    /* we assume the weights are normalized */
	    for ( uint i = half_window_size ; i < raw_vec.size() - half_window_size ;
		  i++ ) {
	      float t = 0.0;
	      for ( int offset = 0 ; offset < window_size ;  offset++ ) {
		int raw_idx = i - half_window_size + offset;
		t += raw_vec[raw_idx] * weights[offset];
	      }
	      out_vec[i] = t;
	    }
	}
}

///Filters chromatogram by removing small spikes, where the signal is <= baseline over at least spike_len consecutive units.
///Other approaches might be : if in a window, at least M/N points are at 'baseline', then set all N points to 'baseline'.
///A bandpass filter may be a better approach. The motivation behind this function is that the matched filtration with
///gaussian or savitzky-golay may cause smoothing artifacts.


int ChromSmoother::spike_filter( const std::vector<float> & raw_vec, std::vector<float> & smooth_vect )  {
   assert(raw_vec.size() == smooth_vect.size());
   assert(baseline >= 0.0f);
   std::copy(raw_vec.begin(), raw_vec.end(), smooth_vect.begin());
   int num_spikes = 0;
   int spike_points = 0;
   for ( int i = 0 ; i < (int)raw_vec.size(); i++ ) {
       if ( raw_vec[i] > baseline ) {
          spike_points++;
       }
       else {
           if ( spike_points > 0 && spike_points <= spike_len ) {
	     float start_i = smooth_vect[i - spike_points - 1];
	     float stop_i =  smooth_vect[i];
	       if  ( start_i >= 0 && stop_i <= smooth_vect.size() - 1 ) {
		 float slope = (stop_i - start_i) / spike_points;
		 float sl_val = start_i;
		 for ( int b = i - spike_points ; b < i ; b++ ) {
		   sl_val = sl_val + slope;
		   smooth_vect[b] = sl_val;
		 }
		 num_spikes++;
	       }
           }
           spike_points = 0;
       }
   }
   if ( spike_points <= spike_len ) {
       for ( int b = smooth_vect.size() - 1 - spike_points ; b < (int)smooth_vect.size() - 1 ; b++ ) {
           smooth_vect[b] = baseline;
       }
       num_spikes++;
   }
   return num_spikes;
}
   
    


void ChromSmoother::smooth_vect_fft( const std::vector<float> & raw_vec, std::vector<float> & out_vec, bool test) {



/*
    if sum_norm :
        total_weight = sum(weights)
        weights = weights / total_weight

    n = len(values)
    assert(len(weights) % 2 == 1)
    #create a zero-padded array for both the weights and values
    #extra padding needed = (len(weights) / 2 )+ 1
    N = n + len(weights) / 2 + 1
    values_padded = numpy.zeros(N)
    
    weights_padded = numpy.zeros(N)
    values_padded[0:n]    = values
    M = len(weights)
    M_mid = len(weights) / 2
    weights_padded[0] = weights[M_mid]
    for  i in range(1,M_mid)  :
       weights_padded[N-i] = weights[M_mid-i]
       weights_padded[i]   = weights[M_mid+i]
    convolved_fft = data_fft * weights_fft
    convolved_real = numpy.fft.irfft(convolved_fft)
    return convolved_real[:n]    
void cffti( integer_t *n, real_t *wsave, integer_t *ifac );
*/



#ifdef HAVE_FFTPACK

    int n = raw_vec.size();
    //std::basic_ofstream<char> x("foo");
    //x.close();
    //std::ofstream t1("pre_rfftb.test.tab");
    std::cerr << "n % 2" << n % 2 << " , n: " << n << std::endl;

    const int M = this->weights.size();
    assert( M % 2 == 1 );
    const int M_mid = M / 2;
    int N = n + M_mid + 1;
    std::vector<float> input_padded(raw_vec);
    for ( int i = raw_vec.size() ; i < N ; i++ ) {
       input_padded.push_back(0.0f);
    }
    std::vector<float> weights_padded(N,0.0f);
    weights_padded[0] = this->weights[M_mid];
    for ( int i = 1 ; i < M_mid ; i++ ) {
       weights_padded[N-i] = weights[M_mid-i];
       weights_padded[i] = weights[M_mid+i];
    }
    float * wsave_weights = (float*)malloc((8*N+15)*sizeof(float));
    float * wsave_data    = (float*)malloc((8*N+15)*sizeof(float));
    float * wsave_back    = (float*)malloc((8*N+15)*sizeof(float));
    float * fft_prod      = (float*)malloc(N*sizeof(float));
  
    std::cerr << "finished padding" << std::endl;

    int ifac[64];
    rffti( N, wsave_data);
    rffti( N, wsave_weights);
    rffti( n, wsave_back);
    rfftf( N, &(input_padded[0]), wsave_data);
    for ( int i = 0 ; i < N ; i++ ) {
      input_padded[i] /= N;
    }

    if ( true || SG_DALKE_DUMP ) {
      std::vector<float> output_padded(input_padded);

      //crawutils::output_vector(t1,output_padded);
    }
    
    //t1.close();

    std::cerr << "weights fft 1" << std::endl;
    rfftf( N, &(weights_padded[0]), wsave_weights);
    for ( int i = 0 ; i < N ; i++ ) {
       weights_padded[i] *= N;
    }
    //std::vector<float> weights_rev(weights_padded);
    //rfftb( &N, &(weights_rev[0]), wsave, ifac);
    //std::ofstream t3("weights_fb.txt");
    //crawutils::output_vector(t3,weights_rev);

    for ( int i = 0; i < N ; i++ ) {
       fft_prod[i] = input_padded[i] * weights_padded[i];
    }
    std::cerr << "product 1" << std::endl;        

    rfftb( N, fft_prod, wsave_back);
    std::cerr << "backwards fft" << std::endl;        
    for ( int i = 0 ; i < out_vec.size() ; i++ ) {
      out_vec[i] = fft_prod[i];
    }
    std::cerr << "out_vec filled" << std::endl;
    free(wsave_data);
    free(wsave_weights);
    free(wsave_back);
    free(fft_prod);

#else
    throw("Forget about trying to call smooth_vect_fft if you don't have FFTPACK");
#endif

}


void ChromSmoother::smooth_vect( const std::vector<float> & raw_vec, 
                             std::vector<float> & out_vec) {
     if ( this->fft ) {
        smooth_vect_fft(raw_vec,out_vec);               
     }
     else {
        smooth_vect_discrete(raw_vec,out_vec);
     }
}

void ChromSmoother::smooth_vect(std::vector<float> & smooth_vect ) {
    std::vector<float> tmp_vect(smooth_vect);
    this->smooth_vect(smooth_vect,tmp_vect);
    std::copy(tmp_vect.begin(), tmp_vect.end(), smooth_vect.begin());
}
