using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.RetentionTimes
{
    public abstract class RtCalculatorOption
    {
        private const string PREFIX_IRT = "irt:";
        private const string PREFIX_LIBRARY = "library:";
        private readonly string _persistentString;
        public static readonly RtCalculatorOption MedianDocRetentionTimes = new MedianDocumentRetentionTimes();


        protected RtCalculatorOption(string persistentString)
        {
            _persistentString = persistentString;
        }

        public virtual RetentionScoreCalculatorSpec GetRetentionScoreCalculatorSpec(SrmDocument document, ImmutableList<RetentionScoreCalculatorSpec> calculators)
        {
            return GetAlignmentTarget(document, calculators, false)?.AsRetentionScoreCalculator();
        }

        protected abstract AlignmentTarget GetAlignmentTarget(SrmSettings settings, ImmutableList<RetentionScoreCalculatorSpec> calculators);

        protected virtual AlignmentTarget GetAlignmentTarget(SrmDocument document,
            ImmutableList<RetentionScoreCalculatorSpec> calculators, bool ensureCurrent)
        {
            return GetAlignmentTarget(document.Settings, calculators);
        }

        public AlignmentTarget GetAlignmentTarget(SrmSettings settings)
        {
            return GetAlignmentTarget(settings,
                ImmutableList.Singleton(settings.PeptideSettings.Prediction?.RetentionTime?.Calculator));
        }

        public static IEnumerable<RtCalculatorOption> GetOptions(SrmDocument document)
        {
            var result =
                new List<RtCalculatorOption>(Settings.Default.RTScoreCalculatorList.Select(calc => new Irt(calc.Name)));
            result.AddRange(GetLibraryOptions(document));
            result.Add(MedianDocRetentionTimes);
            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="peptideSettings"></param>
        /// <param name="option"></param>
        /// <returns></returns>
        public static bool TryGetDefault(PeptideSettings peptideSettings, out RtCalculatorOption option)
        {
            var calculator = peptideSettings.Prediction?.RetentionTime?.Calculator;
            if (calculator is RCalcIrt)
            {
                option = new Irt(calculator.Name);
                return true;
            }

            option = null;
            if (peptideSettings.Libraries.Libraries.Count != 1)
            {
                return true;
            }
            var firstLibrary = peptideSettings.Libraries.Libraries[0];
            if (true != firstLibrary?.IsLoaded)
            {
                return false;
            }

            if (firstLibrary.ListRetentionTimeSources().Any())
            {
                option = new Library(peptideSettings.Libraries.Libraries[0].Name);
            }
            return true;
        }

        private static IEnumerable<Library> GetLibraryOptions(SrmDocument document)
        {
            foreach (var library in document.Settings.PeptideSettings.Libraries.Libraries)
            {
                if (true != library?.IsLoaded)
                {
                    continue;
                }

                if (library.ListRetentionTimeSources().Any())
                {
                    yield return new Library(library.Name);
                }
            }
        }

        public static RtCalculatorOption FromPersistentString(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return null;
            }

            if (str == MedianDocRetentionTimes.ToPersistentString())
            {
                return MedianDocRetentionTimes;
            }
            
            if (str.StartsWith(PREFIX_IRT))
            {
                return new Irt(str.Substring(PREFIX_IRT.Length));
            }

            if (str.StartsWith(PREFIX_LIBRARY))
            {
                return new Library(str.Substring(PREFIX_LIBRARY.Length));
            }

            // legacy settings value:
            return new Irt(str);
        }

        protected bool Equals(RtCalculatorOption other)
        {
            return ToPersistentString() == other?.ToPersistentString();
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((RtCalculatorOption)obj);
        }

        public override int GetHashCode()
        {
            return ToPersistentString().GetHashCode();
        }


        public string ToPersistentString()
        {
            return _persistentString;
        }

        public virtual string AxisTitle
        {
            get { return DisplayName; }
        }

        public class Irt : RtCalculatorOption
        {
            public Irt(string name) : base(PREFIX_IRT + name)
            {
                Name = name;
            }

            public string Name { get; }

            public override string DisplayName => Name;

            protected bool Equals(Irt other)
            {
                return Name == other.Name;
            }

            public override bool Equals(object obj)
            {
                if (obj is null) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((Irt)obj);
            }

            public override int GetHashCode()
            {
                return (Name != null ? Name.GetHashCode() : 0);
            }

            protected override AlignmentTarget GetAlignmentTarget(SrmSettings settings, ImmutableList<RetentionScoreCalculatorSpec> calculators)
            {
                var calculator = GetRetentionScoreCalculatorSpec(null, calculators);
                if (true == calculator?.IsUsable)
                {
                    return new AlignmentTarget.Irt(calculator);
                }

                return null;
            }

            public override RetentionScoreCalculatorSpec GetRetentionScoreCalculatorSpec(SrmDocument document, ImmutableList<RetentionScoreCalculatorSpec> calculators)
            {
                return calculators.FirstOrDefault(calc => calc.Name == Name);
            }

            public override string AxisTitle
            {
                get { return Name; }
            }

            public override AlignmentTargetSpec ToAlignmentTargetSpec()
            {
                return AlignmentTargetSpec.Calculator;
            }
        }



        public class Library : RtCalculatorOption
        {
            public Library(string name) : base(PREFIX_LIBRARY + name)
            {
                LibraryName = name;
            }

            public string LibraryName { get; }

            protected override AlignmentTarget GetAlignmentTarget(SrmSettings settings, ImmutableList<RetentionScoreCalculatorSpec> calculators)
            {
                var library = settings.PeptideSettings.Libraries.Libraries.FirstOrDefault(lib => lib?.Name == LibraryName);
                if (true == library?.IsLoaded)
                {
                    return new AlignmentTarget.LibraryTarget(RegressionMethodRT.loess, library);
                }

                return null;
            }

            public override string DisplayName => string.Format(RetentionTimesResources.Library_DisplayName_Library__0_, LibraryName);

            public override string AxisTitle
            {
                get
                {
                    return string.Format(RetentionTimesResources.Library_AxisTitle_Median_Time_from_Library__0_, LibraryName);
                }
            }

            public override AlignmentTargetSpec ToAlignmentTargetSpec()
            {
                return AlignmentTargetSpec.Library.ChangeName(LibraryName);
            }
        }

        private class MedianDocumentRetentionTimes : RtCalculatorOption
        {
            public MedianDocumentRetentionTimes() : base(@"MedianDocumentRetentionTimes")
            {

            }
            protected override AlignmentTarget GetAlignmentTarget(SrmSettings settings, ImmutableList<RetentionScoreCalculatorSpec> calculators)
            {
                return settings.DocumentRetentionTimes.MedianDocumentRetentionTimes;
            }
 
            protected override AlignmentTarget GetAlignmentTarget(SrmDocument document, ImmutableList<RetentionScoreCalculatorSpec> calculators, bool ensureCurrent)
            {
                AlignmentTarget target = null;
                if (!ensureCurrent)
                {
                    target = GetAlignmentTarget(document.Settings, calculators);
                }
                target ??= new AlignmentTarget.MedianDocumentRetentionTimes(document);
                return target;
            }

            public override string DisplayName => RetentionTimesResources.MedianDocumentRetentionTimes_DisplayName_Median_LC_Peak_Times;

            public override AlignmentTargetSpec ToAlignmentTargetSpec()
            {
                return AlignmentTargetSpec.ChromatogramPeaks;
            }
        }

        public abstract string DisplayName { get; }

        public abstract AlignmentTargetSpec ToAlignmentTargetSpec();
    }
}
