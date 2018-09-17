/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace pwiz.Common.PeakFinding
{
    internal class ChromSmoother
    {
        protected List<float> weights;

        protected int half_window_size
        {
            get { return weights.Count/2; }
        }

        public void InvertWeights()
        {
            for (int i = 0; i < weights.Count; i++)
            {
                weights[i] *= -1;
            }
        }

    }

    internal class GaussSmother : ChromSmoother
    {
        public void SetGaussWeights(float sd, int derivative)
        {
            //hw should be width at half-height
            int hw = (int) (4.0*(sd + 0.5));
            int wlen = hw*2 + 1;
            /* do the same thing as python where the size of the window is set to 99% of the total area? */
            float[] newWeights = new float[wlen];
            newWeights[hw] = 1.0f;
            float sum = newWeights[hw];
            for (int i = 1; i < hw + 1; i++)
            {
                float t = (float) Math.Exp(-0.5f*i*i/sd);
                newWeights[hw + i] = t;
                newWeights[hw - i] = t;
                sum += t*2;
            }
            for (int i = 0; i < wlen; i++)
            {
                newWeights[i] = newWeights[i]/sum;
            }
            if (derivative > 0)
            {
                if (derivative == 1)
                {
                    newWeights[hw] = 0.0f;
                    for (int i = 1; i < hw + 1; i++)
                    {
                        float tmp = (i*-1.0f/sd)*newWeights[hw + i];
                        newWeights[hw + i] = tmp*-1.0f;
                        newWeights[hw - i] = tmp;
                    }
                }
                else if (derivative == 2)
                {
                    newWeights[hw] *= -1.0f/sd;
                    for (int i = 1; i < hw + 1; i++)
                    {
                        float tmp = (i*i/sd - 1.0f)*newWeights[hw + i]/sd;
                        newWeights[hw + i] = tmp;
                        newWeights[hw - i] = tmp;
                    }
                }
                else if (derivative == 3)
                {
                    newWeights[hw] = 0.0f;
                    for (int i = 1; i < hw + 1; i++)
                    {
                        /* TODO CHECK THIS FORMULA */
                        float tmp = (3.0f - i*i/sd)*i*newWeights[hw + i]/sd/sd;
                        newWeights[hw + i] = tmp*-1.0f;
                        newWeights[hw - i] = tmp;
                    }
                }
                else if (derivative > 3)
                {
                    throw new Exception(@"gaussian derivative of greater than 3rd order not supported");
                }
            }
            weights = newWeights.ToList();
        }

        public void TrimWeightsByFracMax(float frac)
        {
            if (frac >= 1.0f)
            {
                throw new ArgumentException();
            }
            int last_keep = -1;
            int first_keep = -1;
            float weights_max = weights.Max();
            float thresh = weights_max*frac;
            for (var i = 0; i < weights.Count; i++)
            {
                if (Math.Abs(weights[i]) >= thresh)
                {
                    first_keep = i;
                    break;
                }
            }
            for (int i = weights.Count - 1; i >= first_keep; i--)
            {
                if (Math.Abs(weights[i]) >= thresh)
                {
                    last_keep = i;
                    break;
                }
            }
            Debug.Assert(first_keep > -1 && last_keep >= first_keep);
            var newWeights = new List<float>(new float[last_keep - first_keep + 1]);
            for (int i = 0; i < newWeights.Count; i++)
            {
                newWeights[i] = weights[first_keep + i];
            }
            weights = newWeights;
        }

        public List<float> SmoothVect(IList<float> rawVector)
        {
            if (rawVector.Count <= half_window_size*2 + 1)
            {
                return new List<float>(rawVector);
            }
            var out_vec = new List<float>(new float[rawVector.Count]);
                for (int i = 0; i < half_window_size; i++)
                {
                    out_vec[i] = rawVector[i];
                }
                for (int i = rawVector.Count - half_window_size; i < rawVector.Count; i++)
                {
                    out_vec[i] = rawVector[i];
                }
                /* we assume the weights are normalized */
                for (int i = half_window_size;
                    i < rawVector.Count - half_window_size;
                    i++)
                {
                    double t = 0.0;
                    for (int offset = 0; offset < weights.Count; offset++)
                    {
                        int raw_idx = i - half_window_size + offset;
                        t += rawVector[raw_idx]*weights[offset];
                    }
                    out_vec[i] = (float) t;
                }
            return out_vec;
        }
    }
}
