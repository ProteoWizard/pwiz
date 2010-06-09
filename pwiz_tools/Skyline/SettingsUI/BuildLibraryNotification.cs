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
using System.ComponentModel;
using System.Windows.Forms;
using pwiz.Skyline.Util;
using Timer=System.Windows.Forms.Timer;

namespace pwiz.Skyline.SettingsUI
{
    /// <summary>
    /// The interface a "client" of this notification must implement in order
    /// to get/set information pertaining to showing/hiding the notification.
    /// </summary>
    public interface IBuildNotificationClient
    {
        /// <summary>
        /// The notification window needs to listen for the "build complete"
        /// event so that it can show itself at the appropriate time.
        /// </summary>
        /// <param name="listener"> Handler for "build complete" event. </param>
        void BuildCompleteEventListen(EventHandler<BuildNotificationEventArgs> listener);

        /// <summary>
        /// The notification window needs to be able to remove the listener for
        /// the build complete event before it closes.
        /// </summary>
        /// <param name="listener"> Handler for "build complete" event. </param>
        void BuildCompleteEventUnlisten(EventHandler<BuildNotificationEventArgs> listener);

        /// <summary>
        /// The notification window needs to listen for the "build notification
        /// opened" event in order to close itself in case ANOTHER build
        /// notification window has opened for a different library build.
        /// </summary>
        /// <param name="listener"></param>
        void BuildNotificationOpenedEventListen(EventHandler<BuildNotificationEventArgs> listener);

        /// <summary>
        /// The notification window needs to be able to remove the listener for
        /// the "build notification opened" event before it closes.
        /// </summary>
        /// <param name="listener"></param>
        void BuildNotificationOpenedEventUnlisten(EventHandler<BuildNotificationEventArgs> listener);

        /// <summary>
        /// The notification window needs to know when the client window moves
        /// so that it can close itself.
        /// </summary>
        /// <param name="listener"> Handler for location changed event. </param>
        void LocationChangedEventListen(EventHandler listener);

        /// <summary>
        /// The notification window needs to be able to remove the lister for
        /// the client location changed even before it closes itself.
        /// </summary>
        /// <param name="listener"> Handler for location changed event. </param>
        void LocationChangedEventUnlisten(EventHandler listener);

        /// <summary>
        /// The notification window needs to know when the client has closed
        /// so that it can close itself along with the client.
        /// </summary>
        /// <param name="listener"></param>
        void ClientClosingEventListen(CancelEventHandler listener);

        /// <summary>
        /// The notification window needs to be able to remove the listener for
        /// the "client window closed" event before it closes itself.
        /// </summary>
        /// <param name="listener"></param>
        void ClientClosingEventUnlisten(CancelEventHandler listener);

        /// <summary>
        /// The notification window will use this method to let the client know
        /// when it has opened itself by associating it with the "Show" event.
        /// </summary>
        /// <param name="sender"> sender of the Show event </param>
        /// <param name="e"> EventArgs for the Show event </param>
        void OnBuildNotificationOpened(object sender, BuildNotificationEventArgs e);

        /// <summary>
        /// The notification window needs to know where to display itself 
        /// relative to the client.
        /// </summary>
        /// <param name="x"> The x-coordinate of the position </param>
        /// <param name="y"> The y-coordinate of the position </param>
        void GetDisplayLocation(ref int x, ref int y);

        /// <summary>
        /// The notification window will call this method on the client when
        /// the user clicks on the "View Library" link.
        /// </summary>
        /// <param name="libName"></param>
        void ShowLibrary(String libName);
    }

    public partial class BuildLibraryNotification : Form
    {
        private const int ANIMATION_DURATION = 1000;
        private const int DISPLAY_DURATION = 10000;
        private const int DISPLAY_PADDING = 8;
        private const int WINDOW_HEIGHT = 120;
        private const int WINDOW_WIDTH = 165;

        private readonly IBuildNotificationClient _client;
        private readonly FormAnimator _animator;
        private readonly Timer _displayTimer;
        private readonly String _libraryName;

        public event EventHandler<BuildNotificationEventArgs> BuildLibraryNotificationShown;

        public BuildLibraryNotification(IBuildNotificationClient client, String libraryName)
        {
            InitializeComponent();

            _client = client;
            
            _libraryName = libraryName;
            LibraryNameLabel.Text = String.Format("Library {0}", _libraryName);

            var showParams = new FormAnimator.AnimationParams(
                                    FormAnimator.AnimationMethod.slide, 
                                    FormAnimator.AnimationDirection.up, 
                                    0);
            var hideParams = new FormAnimator.AnimationParams(
                                    FormAnimator.AnimationMethod.blend, 
                                    FormAnimator.AnimationDirection.up, 
                                    0);
            _animator = new FormAnimator(this, showParams, hideParams);

            _displayTimer = new Timer();
            _displayTimer.Tick += OnDisplayTimerEvent;
            _displayTimer.Interval = DISPLAY_DURATION;
        }

        public void BuildNotificationThread()
        {
            _client.BuildCompleteEventListen(OnLibraryBuildComplete);
            _client.ClientClosingEventListen(OnClientClosing);
            
            // Don't show the form until the build complete event is fired. 
            // This seems to be the easiest way to launch it hidden.
            Opacity = 0;

            Application.Run(this);
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        private void InvokeAction(Action action)
        {
            Invoke(action);
        }

        private void PositionNotificationWindow()
        {
            int x = 0;
            int y = 0;
            _client.GetDisplayLocation(ref x, ref y);
            
            // Place the notification window relative to the client window
            SetBounds(x + DISPLAY_PADDING,
                      y - WINDOW_HEIGHT - DISPLAY_PADDING,
                      WINDOW_WIDTH,
                      WINDOW_HEIGHT);
        }

        private void ShowNotification()
        {
            // First, ensure the notification is actually hidden, so that
            // the animation plays on show.
            HideNotification(false /* no animation */);

            _client.BuildNotificationOpenedEventListen(OnBuildLibraryNotificationOpened);

            // Prepare the client to listen for the "Shown" event
            BuildLibraryNotificationShown += _client.OnBuildNotificationOpened;

            // Set up to listen for the client location changed
            _client.LocationChangedEventListen(OnClientLocationChanged);

            // Position the notification at the bottom left corner of client
            PositionNotificationWindow();

            // Show the notification with animation
            Opacity = 1;
            _animator.ShowParams.Duration = ANIMATION_DURATION;
            Show();

            // We've already let the client know the notification has shown
            BuildLibraryNotificationShown -= _client.OnBuildNotificationOpened;

            // Start the timer that will count how long to display it
            _displayTimer.Start();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            // If the form is visible and opaque, it has "officially" shown.
            // Fire the "Shown" event the client is listening for, indicating
            // which library the event is associated with.
            if (Visible && (Opacity == 1))
            {
                if (BuildLibraryNotificationShown != null)
                {
                    BuildLibraryNotificationShown(this, new BuildNotificationEventArgs(_libraryName));
                }
            }
            base.OnVisibleChanged(e);
        }

        private void OnLibraryBuildComplete(object sender, BuildNotificationEventArgs e)
        {
            if (e.LibName == _libraryName)
            {   
                // We don't want to listen for any more "build completed"
                // events since they are not for this form.
                _client.BuildCompleteEventUnlisten(OnLibraryBuildComplete);

                InvokeAction(() => ShowNotification());
            }
        }

        private void OnClientClosing(object sender, CancelEventArgs e)
        {
            InvokeAction(() => CloseNotification(false));
        }

        private void HideNotification(bool animate)
        {
            _animator.HideParams.Duration = animate ? ANIMATION_DURATION : 0;
            Hide();
        }

        private void CloseNotification(bool animate)
        {
            _displayTimer.Stop();
            HideNotification(animate);
            
            // Remove all event listeners, we're about to close
            _client.LocationChangedEventUnlisten(OnClientLocationChanged);
            _client.ClientClosingEventUnlisten(OnClientClosing);
            BuildLibraryNotificationShown -= _client.OnBuildNotificationOpened;
            _client.BuildNotificationOpenedEventUnlisten(OnBuildLibraryNotificationOpened);
            
            Close();
            Application.ExitThread();
        }

        private void OnDisplayTimerEvent(object sender, EventArgs e)
        {
            CloseNotification(true);
        }

        private void NotificationCloseButton_Click(object sender, EventArgs e)
        {
            CloseNotification(false);
        }

        private void ViewLibraryLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            CloseNotification(false);
            _client.ShowLibrary(_libraryName);
        }

        private void OnClientLocationChanged(object sender, EventArgs e)
        {
            InvokeAction(() => CloseNotification(false));
        }

        private void OnBuildLibraryNotificationOpened(object sender, BuildNotificationEventArgs e)
        {
            if ((e.LibName != _libraryName) && Visible)
            {
                InvokeAction(() => CloseNotification(false));
            }
        }
    }

    /// <summary>
    /// Custom event arguments for library build notification related events
    /// </summary>
    public class BuildNotificationEventArgs : EventArgs
    {
        /// <summary>
        /// The library associated with the event
        /// </summary>
        public String LibName { get; private set; }

        /// <summary>
        /// Constructor for custom library build notification events
        /// </summary>
        /// <param name="libName"> Library associated with the event </param>
        public BuildNotificationEventArgs(String libName)
        {
            LibName = libName;
        }
    }
}
