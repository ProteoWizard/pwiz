/*
 * Copyright 2025 University of Washington - Seattle, WA
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

namespace pwiz.Skyline.Model.DocSettings
{
    /// <summary>
    /// Implement on <see cref="Common.SystemUtil.Immutable"/> subclasses to indicate
    /// the type represents a file. This simple interface simplifies using different
    /// types from <see cref="SrmSettings"/> as files (especially in tests) but is not
    /// intended to be a first-class file model.
    /// </summary>
    public interface IFile
    {
        Identity Id { get; }
        string Name { get; }
        string FilePath { get; }
    }
}