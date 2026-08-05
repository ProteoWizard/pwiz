/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2026 University of Washington - Seattle, WA
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

using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Skyline.Model
{
    /// <summary>
    /// Which position each of a list of <see cref="Identity"/> objects sits at, so that finding one
    /// does not mean walking the list. Read only once made.
    /// <para>
    /// Matched by reference, which is what an <see cref="Identity"/> means: two of them are the same
    /// thing only when they are the same object.
    /// </para>
    /// <para>
    /// Worth sharing rather than rebuilding. Nearly every change to a document replaces one node and
    /// leaves the order alone, and then the same instance answers for the new list as well - see
    /// <see cref="DocNodeChildren.ReplaceAt"/>. Anything which holds one alongside a list of values
    /// in the same order, as <see cref="Results.TransitionGroupResults"/> does, is holding a
    /// reference rather than a copy.
    /// </para>
    /// </summary>
    public class IdentityIndex
    {
        public static readonly IdentityIndex EMPTY = new IdentityIndex(new Identity[0]);

        private readonly Dictionary<ReferenceValue<Identity>, int> _indexes;

        public IdentityIndex(IEnumerable<Identity> identities)
        {
            _indexes = new Dictionary<ReferenceValue<Identity>, int>();
            foreach (var identity in identities)
            {
                _indexes.Add(identity, _indexes.Count);
            }
        }

        public int Count
        {
            get { return _indexes.Count; }
        }

        /// <summary>
        /// Where an identity sits, or -1 when it is not one of these.
        /// </summary>
        public int IndexOf(Identity identity)
        {
            if (identity == null || !_indexes.TryGetValue(identity, out int index))
            {
                return -1;
            }

            return index;
        }

        /// <summary>
        /// The identities in position order, which is the order they were given in.
        /// </summary>
        public IEnumerable<Identity> Identities
        {
            get { return _indexes.OrderBy(entry => entry.Value).Select(entry => entry.Key.Value); }
        }
    }
}
