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
        private bool _firstTime = true;
        private double _startValue;
        private double _endValue;
        private readonly int _updateMsec;
        private DateTime _startTime;
        private double[] _scaleFactors;

        public Animation(int updateMsec)
        {
            _updateMsec = updateMsec;
        }

        /// <summary>
        /// Create an animation object that can smoothly animate between values.
        /// </summary>
        /// <param name="startValue">Value at start of animation.</param>
        /// <param name="endValue">Value at end of animation.</param>
        /// <param name="steps">Number of steps in the animation.</param>
        /// <param name="acceleration">Acceleration rate (how fast the animation speeds up and slows down).</param>
        public void SetTarget(double startValue, double endValue, int steps, double acceleration = 1.4)
        {
            if (_firstTime || endValue == startValue || endValue == _endValue)
            {
                Value = _endValue = endValue;
                return;
            }

            _startValue = startValue;
            _startTime = DateTime.UtcNow; // Said to be 117x faster than Now and this is for a delta
            _endValue = endValue;

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

        public bool IsActive { get { return _firstTime || _scaleFactors != null; } }

        /// <summary>
        /// Do the next step of the animation.
        /// </summary>
        public double Step()
        {
            if (_scaleFactors != null)
            {
                // Choose the next animation step depending on how much time has elapsed
                // since the animation started. Skip frames if we're not called fast enough.
                var elapsed = (int)(DateTime.UtcNow - _startTime).TotalMilliseconds;
                var step = Math.Min(
                    _scaleFactors.Length - 1,
                    elapsed / _updateMsec + 1);

                try
                {
                    Value = _startValue + (_endValue - _startValue) * _scaleFactors[step];
                }
                catch (Exception exception)
                {
                    string msg = string.Format(@"Step: {0} Array length: {1}. Elapsed time: {2}", 
                        step, _scaleFactors.Length, elapsed);
                    throw new Exception(msg, exception);
                }
                if (step == _scaleFactors.Length - 1)
                {
                    _scaleFactors = null;
                    Value = _endValue;
                }
            }
            _firstTime = false;

            return Value;
        }
    }
}
