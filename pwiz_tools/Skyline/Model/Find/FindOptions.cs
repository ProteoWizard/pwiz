/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
namespace pwiz.Skyline.Model.Find
{
    public class FindOptions
    {
        public FindOptions()
        {
        }

        public FindOptions(FindOptions options)
        {
            Text = options.Text;
            CaseSensitive = options.CaseSensitive;
            Forward = options.Forward;
        }

        public string Text { get; private set; }
        public FindOptions ChangeText(string value)
        {
            return new FindOptions(this){Text = value};
        }
        public bool CaseSensitive { get; private set; }
        public FindOptions ChangeCaseSensitive(bool value)
        {
            return new FindOptions(this){CaseSensitive = value};
        }
        public bool Forward { get; private set; }
        public FindOptions ChangeForward(bool value)
        {
            return new FindOptions(this){Forward = value};
        }

        public string GetDescription()
        {
            return Text;
        }
    }
}
