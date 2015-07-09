/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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

namespace pwiz.Skyline.Controls.Graphs
{
    /// <summary>
    /// Utility class to animate double values with pleasing acceleration/deceleration.
    /// </summary>
    public class Animation
    {
        private readonly double _startValue;
        private readonly double _endValue;
        private readonly int _updateMsec;
        private readonly DateTime _startTime;
        private double[] _scaleFactors;

        /// <summary>
        /// Create an animation object that can smoothly animate between values.
        /// </summary>
        /// <param name="startValue">Value at start of animation.</param>
        /// <param name="endValue">Value at end of animation.</param>
        /// <param name="steps">Number of steps in the animation.</param>
        /// <param name="updateMsec">Expected update interval in milliseconds.</param>
        /// <param name="acceleration">Acceleration rate (how fast the animation speeds up and slows down).</param>
        public Animation(double startValue, double endValue, int steps, int updateMsec = 200, double acceleration = 1.4)
        {
            _startValue = startValue;
            _endValue = endValue;
            _updateMsec = updateMsec;
            _startTime = DateTime.Now;

            // Calculate smoothly accelerating and decelerating scale factors.
            steps |= 1;
            _scaleFactors = new double[steps];
            var sum = 0.0;
            for (int i = 0; i < steps / 2; i++)
                sum += Math.Pow(acceleration, i);
            sum *= 2;
            _scaleFactors[0] = 0.0;
            _scaleFactors[steps - 1] = 1.0;
            _scaleFactors[steps / 2] = 0.5;
            for (int i = 1; i < steps / 2; i++)
            {
                _scaleFactors[i] = _scaleFactors[i - 1] + Math.Pow(acceleration, i - 1) / sum;
                _scaleFactors[steps - 1 - i] = 1.0 - _scaleFactors[i];
            }

            Value = _startValue;
        }

        public double Value { get; private set; }

        public bool Done { get { return _scaleFactors == null; } }

        /// <summary>
        /// Do the next step of the animation.
        /// </summary>
        public double Step()
        {
            if (_scaleFactors != null)
            {
                // Choose the next animation step depending on how much time has elapsed
                // since the animation started. Skip frames if we're not called fast enough.
                var elapsed = (int)(DateTime.Now - _startTime).TotalMilliseconds;
                var step = Math.Min(
                    _scaleFactors.Length - 1,
                    elapsed / _updateMsec + 1);

                Value = _startValue + (_endValue - _startValue) * _scaleFactors[step];

                if (step == _scaleFactors.Length - 1)
                {
                    _scaleFactors = null;
                    Value = _endValue;
                }
            }

            return Value;
        }
    }
}
