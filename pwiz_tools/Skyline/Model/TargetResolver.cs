/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.Linq;
using pwiz.Skyline.Model.Irt;

namespace pwiz.Skyline.Model
{
    /// <summary>
    /// Given a list of known targets, decides how to reversibly convert them to strings so they can
    /// be looked up by peptide sequence, name, accession number etc.
    /// CONSIDER(bspratt) make this case insensitive? More useful for user interaction that way
    /// </summary>
    public class TargetResolver
    {
        public static readonly TargetResolver EMPTY = new TargetResolver(new Target[0]);
        private ILookup<string, Target> _targetsByName;

        public TargetResolver(IEnumerable<Target> targetsEnum)
        {
            var targets = targetsEnum.Select(t => t.ToSerializableString())
                .Distinct()
                .Select(Target.FromSerializableString).ToArray(); // Normalizes masses (e.g. "344.300548579909" vs "344.300548580")

            // For molecules allow lookup by formula, InChIKey etc in addition to display name
            var accessions = new HashSet<Tuple<string, Target>>();
            foreach (var target in targets.Where(t => !t.IsProteomic))
            {
                var accessionNumbers = target.Molecule.AccessionNumbers;
                if (accessionNumbers != null)
                {
                    // Allow lookup by InChiKey, HMDB etc
                    foreach (var name in accessionNumbers.GetAllKeys())
                    {
                        var key = name.Substring(name.IndexOf(':') + 1); // e.g. drop the "InChikey:" from ""InChiKey:ZXPLRDFHBYIQOX-BTBVOZEKSA-N"
                        if (!string.IsNullOrEmpty(key))
                        {
                            accessions.Add(new Tuple<string, Target>(key, target));
                        }
                    }
                }

                // Allow lookup by formula
                var formula = target.Molecule.Formula;
                if (!string.IsNullOrEmpty(formula))
                {
                    accessions.Add(new Tuple<string, Target>(formula, target));
                }
            }

            if (accessions.Any())
            {
                foreach (var target in targets)
                {
                    // Also lookup by name
                    accessions.Add(new Tuple<string, Target>(GetTargetDisplayName(target), target));

                    // And by small molecule encoding string
                    var encoded = target.ToSerializableString();
                    if (!string.IsNullOrEmpty(encoded))
                    {
                        accessions.Add(new Tuple<string, Target>(encoded, target));
                    }
                }

                _targetsByName = accessions.ToLookup(a => a.Item1, a => a.Item2);
            }
            else
            {
                _targetsByName = targets.ToLookup(GetTargetDisplayName);
            }

        }

        public static TargetResolver MakeTargetResolver(SrmDocument document, params IEnumerable<Target>[] otherTargets)
        {
            // CONSIDER(bspratt) if document is purely proteomic, we could skip all this initialization since a peptide is self describing
            var allTargets = Enumerable.Empty<Target>();
            if (document != null)
            {
                allTargets = allTargets.Concat(document.Molecules.Select(m => m.Target));
            }

            foreach (var others in otherTargets)
            {
                if (others != null)
                {
                    allTargets = allTargets.Concat(others);
                }
            }
            return new TargetResolver(allTargets);
        }

        public string FormatTarget(Target target)
        {
            if (target == null)
            {
                return string.Empty;
            }

            if (target.IsProteomic)
            {
                return target.Sequence;
            }

            string name = GetTargetDisplayName(target);
            if (_targetsByName[name].Count() == 1)
            {
                return name;
            }

            return target.ToSerializableString();
        }

        /// <summary>
        /// Returns the string to be used when presenting a target in the UI
        /// </summary>
        private string GetTargetDisplayName(Target target)
        {
            return target.DisplayName;
        }

        public Target ResolveTarget(string text)
        {
            string errorMessage;
            var target = TryResolveTarget(text, out errorMessage);
            if (errorMessage != null)
            {
                throw new FormatException(errorMessage);
            }
            return target;
        }

        public Target TryResolveTarget(string text, out string errorMessage)
        {
            errorMessage = null;
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }
            var matches = _targetsByName[text].ToArray();
            if (matches.Length == 1)
            {
                return matches.First();
            }
            if (matches.Length > 1)
            {
                errorMessage = string.Format(ModelResources.TargetResolver_TryResolveTarget_Unable_to_resolve_molecule_from___0____could_be_any_of__1_, text, string.Join(@", ",matches.Select(t => t.InvariantName)));
                return null;
            }
            Target target;
            try
            {
                target = Target.FromSerializableString(text);
                errorMessage = MeasuredPeptide.ValidateSequence(target);
                if (errorMessage != null)
                {
                    return null;
                }
            }
            catch (Exception)
            {
                target = null;
            }

            if (target == null)
            {
                errorMessage = string.Format(ModelResources.TargetResolver_TryResolveTarget_Unable_to_resolve_molecule_from___0___, text);
            }

            return target;

        }
    }
}
