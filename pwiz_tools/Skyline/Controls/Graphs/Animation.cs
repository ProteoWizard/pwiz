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
        private double _startValue;
        private double _endValue;
        private double[] _scaleFactors;
        private readonly double _acceleration;
        private readonly Action<Animation> _valueChangedAction;
        private readonly Action _doneAction;
        private readonly int _updateMsec;
        private DateTime _startTime;

        /// <summary>
        /// Create an animation object that can smoothly animate between values.
        /// </summary>
        /// <param name="valueChangedAction">Action to perform when the animated value changes.</param>
        /// <param name="doneAction">Action to perform when the animation is finished.</param>
        /// <param name="updateMsec">Expected update interval in milliseconds.</param>
        /// <param name="acceleration">Acceleration rate (how fast the animation speeds up and slows down).</param>
        public Animation(Action<Animation> valueChangedAction, Action doneAction = null, int updateMsec = 200, double acceleration = 1.4)
        {
            _valueChangedAction = valueChangedAction;
            _doneAction = doneAction;
            _updateMsec = updateMsec;
            _acceleration = acceleration;
        }

        public double Value { get; private set; }

        public bool Done { get { return _scaleFactors == null; } }

        /// <summary>
        /// Animate a value in the given range.
        /// </summary>
        /// <param name="startValue">Starting value.</param>
        /// <param name="endValue">Ending value.</param>
        /// <param name="steps">How many steps to take in the animation.</param>
        public void Animate(double startValue, double endValue, int steps)
        {
            // Don't reset clock if animation is already running.
            if (Value <= _endValue)
                _startTime = DateTime.Now;

            _startValue = startValue;
            _endValue = endValue;

            // Calculate smoothly accelerating and decelerating scale factors.
            steps |= 1;
            _scaleFactors = new double[steps];
            var sum = 0.0;
            for (int i = 0; i < steps / 2; i++)
                sum += Math.Pow(_acceleration, i);
            sum *= 2;
            _scaleFactors[0] = 0.0;
            _scaleFactors[steps - 1] = 1.0;
            _scaleFactors[steps / 2] = 0.5;
            for (int i = 1; i < steps / 2; i++)
            {
                _scaleFactors[i] = _scaleFactors[i - 1] + Math.Pow(_acceleration, i - 1) / sum;
                _scaleFactors[steps - 1 - i] = 1.0 - _scaleFactors[i];
            }

            NextStep();
        }

        /// <summary>
        /// Do the next step of the animation.
        /// </summary>
        public void NextStep()
        {
            if (_scaleFactors != null)
            {
                // Choose the next animation step depending on how much time has elapsed
                // since the animation started. Skip frames if we're not called fast enough.
                var elapsed = (int)(DateTime.Now - _startTime).TotalMilliseconds;
                var step = Math.Min(
                    _scaleFactors.Length - 1,
                    elapsed / _updateMsec + 1);

                var newValue = _startValue + (_endValue - _startValue) * _scaleFactors[step];

                if (Value != newValue)
                {
                    Value = newValue;
                    if (_valueChangedAction != null)
                        _valueChangedAction(this);
                }

                if (step == _scaleFactors.Length - 1)
                {
                    _scaleFactors = null;
                    if (_doneAction != null)
                        _doneAction();
                }
            }
        }
    }
}
