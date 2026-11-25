/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com>
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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

using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;

namespace pwiz.Skyline.Model.Irt
{
    /// <summary>
    /// Calibrates the retention times of a spectral library to an iRT standard and writes the
    /// resulting iRT database into the library file. The work is split into steps so that a caller
    /// can make the choices the calibration needs between them: how many CiRT peptides to use,
    /// which standard to use when "Auto" matches several, whether to accept the calibrated values
    /// and whether to recalibrate the standard. The user interface asks the user, the command line
    /// reads its arguments, and a run that collects its settings up front answers from those.
    /// </summary>
    public class LibraryIrtCalibration
    {
        private readonly LibrarySpec _librarySpec;
        private readonly IrtRegressionType _regressionType;
        private IRetentionTimeProvider[] _providers;

        public LibraryIrtCalibration(Library library, LibrarySpec librarySpec, IrtStandard standard, IrtRegressionType regressionType)
        {
            Library = library;
            _librarySpec = librarySpec;
            Standard = standard;
            _regressionType = regressionType;
            AutoStandards = Array.Empty<IrtStandard>();
            CirtPeptides = Array.Empty<DbIrtPeptide>();
        }

        /// <summary>
        /// True when the library and standard allow a calibration at all.
        /// </summary>
        public static bool CanCalibrate(Library library, IrtStandard standard)
        {
            return library != null && library.IsLoaded && standard != null && !standard.IsEmpty;
        }

        public Library Library { get; }

        /// <summary>
        /// The standard the calibration uses. It starts as the requested standard and changes when
        /// an "Auto" standard is resolved or when CiRT peptides make a new standard.
        /// </summary>
        public IrtStandard Standard { get; private set; }

        /// <summary>
        /// The known standards whose peptides are all in the library, when the requested standard is "Auto".
        /// </summary>
        public IList<IrtStandard> AutoStandards { get; private set; }

        /// <summary>
        /// The CiRT peptides found in the library.
        /// </summary>
        public DbIrtPeptide[] CirtPeptides { get; private set; }

        /// <summary>
        /// True when the library holds enough CiRT peptides that the caller must choose how many to
        /// use as standards before <see cref="ProcessRetentionTimes"/>.
        /// </summary>
        public bool UsesCirtPeptides => CirtPeptides.Length >= RCalcIrt.MIN_PEPTIDES_COUNT;

        /// <summary>
        /// True when the standard is "Auto" and several known standards match the library, so the
        /// caller must choose one with <see cref="ChooseStandard"/>.
        /// </summary>
        public bool NeedsStandardChoice => !UsesCirtPeptides && Standard.IsAuto && AutoStandards.Count > 1;

        public ProcessedIrtAverages Processed { get; private set; }

        /// <summary>
        /// True when processing found library peptides to calibrate.
        /// </summary>
        public bool HasCalibratedPeptides => Processed != null && Processed.DbIrtPeptides.Any();

        public bool CanRecalibrateStandards => Processed != null && Processed.CanRecalibrateStandards(Standard.Peptides);

        /// <summary>
        /// Reads the retention times in the library and looks for known standards and CiRT peptides.
        /// When the standard is "Auto" and no more than one known standard matches, the standard is
        /// resolved here.
        /// </summary>
        public void LoadRetentionTimes(IProgressMonitor monitor)
        {
            ImportPeptideSearch.GetLibIrtProviders(Library, Standard, monitor,
                out _providers, out var autoStandards, out var cirtPeptides);
            AutoStandards = autoStandards ?? new List<IrtStandard>();
            CirtPeptides = cirtPeptides;

            if (UsesCirtPeptides || !Standard.IsAuto)
                return;

            switch (AutoStandards.Count)
            {
                case 0:
                    Standard = new IrtStandard(XmlNamedElement.NAME_INTERNAL, null, null, IrtPeptidePicker.Pick(_providers, 10));
                    break;
                case 1:
                    Standard = AutoStandards[0];
                    break;
            }
        }

        /// <summary>
        /// Chooses one of the <see cref="AutoStandards"/> when several match.
        /// </summary>
        public void ChooseStandard(IrtStandard standard)
        {
            Standard = standard;
        }

        /// <summary>
        /// Calibrates the library retention times to the standard. When <paramref name="numCirt"/>
        /// has a value, that many CiRT peptides become the standard instead.
        /// </summary>
        public void ProcessRetentionTimes(int? numCirt, IProgressMonitor monitor)
        {
            Processed = ImportPeptideSearch.ProcessRetentionTimes(numCirt, _providers, Standard.Peptides.ToArray(),
                CirtPeptides, _regressionType, monitor, out var newStandardPeptides);
            if (newStandardPeptides != null)
                Standard = new IrtStandard(XmlNamedElement.NAME_INTERNAL, null, null, newStandardPeptides);
        }

        /// <summary>
        /// Writes the calibrated values into the library file, recalibrating the standard first when
        /// <paramref name="recalibrate"/> is true and <see cref="CanRecalibrateStandards"/> allows it.
        /// </summary>
        public void CreateIrtDb(bool recalibrate, IProgressMonitor monitor)
        {
            ImportPeptideSearch.CreateIrtDb(_librarySpec.FilePath, Processed, Standard.Peptides.ToArray(),
                recalibrate && CanRecalibrateStandards, _regressionType, monitor);
        }
    }
}
