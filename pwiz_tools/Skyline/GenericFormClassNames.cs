/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2022 University of Washington - Seattle, WA
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

/*
 * When a form class has generic arguments, it is necessary to declare a non-generic class with the same name.
 * If that class definition appears in the same source file as the generic class name, the Visual Studio designer
 * gets confused about what to display in the designer. For this reason, these class names are declared in this source file.
 */
namespace pwiz.Skyline.SettingsUI
{
    public class EditListDlg
    {
    }
}

namespace pwiz.Skyline.FileUI
{
    public class ShareListDlg
    {
    }

}
