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
#include "GaussSmoother.H"


void GaussSmoother::set_gauss_weights ( float sd, int derivative ) {

   //hw should be width at half-height
   int hw = static_cast<int>(4.0 * (sd + 0.5));
   int wlen = hw * 2 + 1;
   /* do the same thing as python where the size of the window is set to 99% of the total area? */
   this->set_weight_size(wlen);
   std::vector<float> weights(wlen,0.0f);
   weights[hw] = 1.0f;
   float sum = weights[hw];
   for ( int i = 1; i < hw + 1 ; i++ ) {
       float t = exp(-0.5f * i*i / sd );
       weights[hw+i] = t;
       weights[hw-i] = t;
       sum += t * 2;
   }
   for ( int i = 0 ; i < wlen ; i++ ) {
      weights[i] = weights[i] / sum;
   }
   if ( derivative > 0 ) {
       if ( derivative == 1 ) {
          weights[hw] = 0.0;
          for  (int i = 1 ; i < hw+1 ; i++ ) { 
                float tmp = (i * -1.0f / sd )* weights[hw+i] ;
                weights[hw+i] = tmp * -1.0f;
                weights[hw-i] = tmp;
          }
       }
       else if ( derivative == 2 ) {
            weights[hw] *= -1.0f / sd;
                for ( int i = 1 ; i < hw+1 ; i++ ) {
                  float tmp = ( i * i /  sd - 1.0f ) * weights[hw+i] / sd;
                  weights[hw+i] = tmp;
                  weights[hw-i] = tmp;
                }
       }
       else if ( derivative == 3 ) {
           weights[hw] =0.0f;
           float sd2 = sd * sd;
           for ( int i = 1 ;i < hw+1 ; i++ )  {
                /* TODO CHECK THIS FORMULA */
                float tmp = ( 3.0f - i*i / sd) * i * weights[hw+i] / sd / sd;
                weights[hw+i] = tmp * -1.0f;
                weights[hw-i] = tmp;
           }
       }
       else if ( derivative > 3 ) {
           throw("gaussian derivative of greater than 3rd order not supported");
       }
   }
   this->set_weights(weights);
}

void GaussSmoother::trim_weights_by_frac_max(float frac ) {
    assert(frac < 1.0f);
    int first_keep, last_keep; 
    first_keep = last_keep = -1;
    float weights_max = crawutils::max_vect(this->weights);
    float thresh = weights_max * frac;
    for ( size_t i = 0 ; i < this->weights.size() ; i++ ) {
        if ( fabs(weights[i]) >= thresh ) {
            first_keep = (int)i;
            break;
        }
    }
    for ( size_t i = weights.size() - 1; i>= (size_t) first_keep ; i-- ) {
        if ( fabs(weights[i]) >= thresh ) {
            last_keep = (int)i;
            break;
        }
    }
    assert( first_keep > -1 && last_keep >= first_keep);
    std::vector<float> new_weights(last_keep - first_keep + 1);
    for ( int i = 0 ; i < (int)new_weights.size() ; i++ ) {
       new_weights[i] = weights[first_keep+i];
    }
    this->set_weight_size((int)new_weights.size());
    this->set_weights(new_weights);
}

