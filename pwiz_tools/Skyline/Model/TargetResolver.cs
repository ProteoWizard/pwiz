using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Skyline.SettingsUI;

namespace pwiz.Skyline.Model
{
    public class TargetResolver
    {
        public static readonly TargetResolver EMPTY = new TargetResolver(new Target[0]);
        private ILookup<string, Target> _targetsByName;

        public TargetResolver(IEnumerable<Target> targets)
        {
            _targetsByName = targets.Select(t => t.ToSerializableString())
                .Distinct()
                .Select(Target.FromSerializableString).ToLookup(GetTargetName);
        }

        public static TargetResolver MakeTargetResolver(SrmDocument document, params IEnumerable<Target>[] otherTargets)
        {
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

            string name = GetTargetName(target);
            if (_targetsByName[name].Count() == 1)
            {
                return name;
            }

            return target.ToSerializableString();
        }

        private string GetTargetName(Target target)
        {
            return target.ToString();
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
                errorMessage = string.Format("Unable to resolve molecule from '{0}'.", text);
            }

            return target;

        }
    }
}
