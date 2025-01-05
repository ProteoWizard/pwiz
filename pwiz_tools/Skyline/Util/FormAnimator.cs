/*
 * Original author: Tahmina Baker <tabaker .at. u.washington.edu>,
 *                  UWPR, Department of Genome Sciences, UW
 *
 * Copyright 2010 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using pwiz.Common.SystemUtil.PInvoke;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// Animates a form when it is shown/hidden.
    /// </summary>
    /// <remarks>
    /// MDI child forms do not support the blend method and only support other
    /// methods while being displayed for the first time and when closing.
    /// </remarks>
    public sealed class FormAnimator
    {
        /// <summary>
        /// The methods of animation available.
        /// </summary>
        public enum AnimationMethod : uint
        {
            /// <summary>
            /// Rolls out from edge when showing and into edge when hiding.
            /// </summary>
            /// <remarks>
            /// This is the default animation method and requires a direction.
            /// </remarks>
            roll = User32.AnimateWindowFlags.ROLL,
            /// <summary>
            /// Expands out from centre when showing and collapses into centre when hiding.
            /// </summary>
            centre = User32.AnimateWindowFlags.CENTER,
            /// <summary>
            /// Slides out from edge when showing and slides into edge when hiding.
            /// </summary>
            /// <remarks>
            /// Requires a direction.
            /// </remarks>
            slide = User32.AnimateWindowFlags.SLIDE,
            /// <summary>
            /// Fades from transaprent to opaque when showing and from opaque to transparent when hiding.
            /// </summary>
            blend = User32.AnimateWindowFlags.BLEND
        }

        /// <summary>
        /// The directions in which the roll and slide animations can be shown.
        /// </summary>
        /// <remarks>
        /// Horizontal and vertical directions can be combined to create diagonal animations.
        /// </remarks>
        [Flags]
        public enum AnimationDirection : uint
        {
            /// <summary>
            /// From left to right.
            /// </summary>
            right = User32.AnimateWindowFlags.HORIZONTAL_POSITIVE,
            /// <summary>
            /// From right to left.
            /// </summary>
            left = User32.AnimateWindowFlags.HORIZONTAL_NEGATIVE,
            /// <summary>
            /// From top to bottom.
            /// </summary>
            down = User32.AnimateWindowFlags.VERTICAL_POSITIVE,
            /// <summary>
            /// From bottom to top.
            /// </summary>
            up = User32.AnimateWindowFlags.VERTICAL_NEGATIVE
        }

        /// <summary>
        /// The number of milliseconds over which the animation occurs if no 
        /// value is specified.
        /// </summary>
        private const int DEFAULT_DURATION = 250;

        /// <summary>
        /// The method, direction and duration parameters to be used with the
        /// animation.
        /// </summary>
        public class AnimationParams
        {
            public AnimationMethod Method { get; set; }
            public AnimationDirection Direction { get; set; }
            public int Duration { get; set; }

            public AnimationParams(AnimationMethod method,
                                   AnimationDirection direction)
            {
                Method = method;
                Direction = direction;
                Duration = DEFAULT_DURATION;
            }

            public AnimationParams(AnimationMethod method,
                                   AnimationDirection direction,
                                   int duration)
                                   : this(method, direction)
            {
                Duration = duration;
            }
        }

        /// <summary>
        /// Gets the form to be animated.
        /// </summary>
        /// <value>
        /// The form to be animated.
        /// </value>
        public Form Form { get; private set; }

        /// <summary>
        /// Gets/sets the parameters to be used for the "show" animation
        /// </summary>
        public AnimationParams ShowParams { get; set; }

        /// <summary>
        /// Gets/sets the parameters to be used for the "hide" animation
        /// </summary>
        public AnimationParams HideParams { get; set; }

        /// <summary>
        /// Constructor used to specify the form to be animated
        /// </summary>
        /// <param name="form"> The form to be animated. </param>
        public FormAnimator(Form form)
        {
            Form = form;
            Form.VisibleChanged += Form_VisibleChanged;
        }

        /// <summary>
        /// Use this constructor if your show/hide parameters are identical.
        /// </summary>
        /// <param name="form"> The form to be animated. </param>
        /// <param name="animationParams"> Parameters used for both show and hide animations. </param>
        public FormAnimator(Form form,
                    AnimationParams animationParams)
            : this(form)
        {
            ShowParams = animationParams;
            HideParams = animationParams;
        }

        /// <summary>
        /// Use this constructor if you want to specify different parameters
        /// for the show and hide animations.
        /// </summary>
        /// <param name="form"> The form to be animated. </param>
        /// <param name="showParams"> Parameters used for the show animation. </param>
        /// <param name="hideParams"> Parameters used for the hide animation. </param>
        public FormAnimator(Form form,
                            AnimationParams showParams,
                            AnimationParams hideParams)
            : this(form)
        {
            ShowParams = showParams;
            HideParams = hideParams;
        }

        public void Release()
        {
            if (Form != null)
            {
                Form.VisibleChanged -= Form_VisibleChanged;
                Form = null;
            }
        }

        /// <summary>
        /// Animates the form automatically when it is shown or hidden.
        /// </summary>
        private void Form_VisibleChanged(object sender, EventArgs e)
        {
            // CONSIDER: pinvoke - Skyline has two distinct ways of specifying form animations - here and
            //           in CustomTip. Consider consolidating these into one set of flags compatible with
            //           User32.AnimateWindowFlags and updating User32.AnimateWindow to be more strongly typed.

            // Do not attempt to animate MDI child forms while showing or hiding as they do not behave as expected.
            if (Form.MdiParent == null)
            {
                int flags;
                int duration;

                if (Form.Visible)
                {
                    // Activate the form.
                    flags = (int)User32.AnimateWindowFlags.ACTIVATE | (int)ShowParams.Method | (int)ShowParams.Direction;
                    duration = ShowParams.Duration;
                }
                else
                {
                    // Hide the form.
                    flags = (int)User32.AnimateWindowFlags.HIDE | (int)HideParams.Method | (int)HideParams.Direction;
                    duration = HideParams.Duration;
                }

                User32.AnimateWindow(Form.Handle, duration, flags);
            }
        }
    }
}
