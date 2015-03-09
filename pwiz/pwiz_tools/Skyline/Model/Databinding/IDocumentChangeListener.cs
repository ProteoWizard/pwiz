/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
namespace pwiz.Skyline.Model.Databinding
{
    /// <summary>
    /// Interface which behaves like EventHandler&lt;DocumentChangedEventArgs>.
    /// This interface exists because Delegates are not efficient to put into
    /// hash tables, and we need to be able to have thousands of listeners and
    /// efficiently add and remove them.
    /// </summary>
    public interface IDocumentChangeListener
    {
        void DocumentOnChanged(object sender, DocumentChangedEventArgs args);
    }
}
