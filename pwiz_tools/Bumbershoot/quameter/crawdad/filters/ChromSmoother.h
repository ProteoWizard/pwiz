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
#ifndef CHROMSMOOTHER_H_
#define CHROMSMOOTHER_H_

#ifdef HAVE_FFTPACK
#include "../fftpack/fftpack.h"
#endif

#include <iostream>
#include <fstream>
#include <vector>
#include <map>
#include <math.h>
#include "../msmat/crawutils.h"


class ChromSmoother {
  public :
  /// a threshold for suppressing short spike signals
  float baseline;
  ///if the signal is below 'minimum_baseline' for minimum_spike_len units, then reject the peak
  int spike_len;
  ChromSmoother() {
    weights.resize(0);
    fft = false;
    set_weight_size(0);
    baseline = 0.0f;
    spike_len = 1;
   };
  ChromSmoother( int weight_size ) {
    baseline = 0.0f;
    spike_len = 1;
    weights.resize(weight_size);
    fft = false;
    set_weight_size(weight_size);
  }
  int  spike_filter ( const std::vector<float> & raw_vec, std::vector<float> & smooth_vect ); 
  int  spike_filter_n_of_m ( const std::vector<float> & raw_vec, std::vector<float> & smooth_vect );
  void smooth_vect  ( const std::vector<float> & raw_vec, std::vector<float> & out_vec);
  void smooth_vect  ( std::vector<float> & smooth_vect); 
  void sort_heap    ( std::vector<float> & raw_vec, std::vector<float> & out_vec, bool test = false);
  void resize(size_t new_size) {
      this->weights.resize(new_size);     
  }
  void set_weights ( const std::vector<float> & in_weights ) {
      assert(in_weights.size() == this->weights.size());
      for ( int i = 0 ; i < (int)in_weights.size() ; i++ ) {
          this->weights[i] = in_weights[i];
      }
  }
  void set_weight_size( int weight_size );
  std::vector<float> & get_weights() {
    return this->weights;
  }
  void set_fft(bool v = true) {
#ifndef HAVE_FFTPACK
    if ( v == true ) {
      std::cerr << "No, you have not built this with FFTPACK support, you cannot set the fft flag.";
	}
    else {
      this->fft = v;
    }
#else
    this->fft = v;
#endif
  }
  bool get_fft() {
    return this->fft;
  }
  void invert_weights() {
    for ( int i = 0 ; i < (int)weights.size() ; i++ ) {
      weights[i] *= -1;
    }
  }

  protected :

    std::vector<float> weights;
    bool fft;  
    int half_window_size;
    int window_size;
    void smooth_vect_discrete( const std::vector<float> & raw_vec, std::vector<float> & out_vec);
    void smooth_vect_fft( const std::vector<float> & raw_vec, std::vector<float> & out_vec, bool test = false);
 
};

class UnevenChromSmoother {
public : 
    /// (RT in minutes) if the signal is below 'minimum_baseline' for minimum_spike_len units, then reject the peak
  float minimum_spike_len;
  /// a threshold for suppressing short spike signals
  float minimum_baseline;
  /// size of the window in retention time units
  double rt_filter_half_size;
  double rt_per_filter_pt;
  /// number of points over which to compute the gaussian weights
  const static int sample_half_width   = 5000;
  const static int sample_window_width = sample_half_width * 2 + 1;
  //sample size has to be odd to includ
  std::vector<float> weights;
  bool fft;  
  void init() {
    minimum_baseline  = 0.0f;
    minimum_spike_len = 0.0f; 
  }
  UnevenChromSmoother(double rt_filter_len) : rt_filter_half_size(rt_filter_len / 2.0) {
    rt_per_filter_pt = rt_filter_len / sample_window_width;
    weights.resize(sample_window_width);
    //re-use the related class
    ChromSmoother weight_factory(sample_window_width);
    weights = weight_factory.get_weights();
  }
  int  spike_filter ( const std::vector<float> & raw_vec, std::vector<float> & smooth_vect ); 
  void smooth_vect  ( const std::vector<float> & raw_vec, const std::vector<float> & RT , std::vector<float> & out_vec);
  void smooth_vect  ( const std::vector<float> & smooth_vect, const std::vector<float> & RT); 

  void resize(size_t new_size) {
      this->weights.resize(new_size);     
  }
  void set_fft(bool v = true) {
    this->fft = v;
  }
  bool get_fft() {
    return this->fft;
  }
  void invert_weights() {
    for ( int i = 0 ; i < (int)weights.size() ; i++ ) {
      weights[i] *= -1;
    }
  }


};


#endif
