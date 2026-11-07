/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
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

namespace pwiz.BiblioSpec.IO
{
    /// <summary>
    /// Copies spectra from one library to another through <see cref="BlibReader"/> and
    /// <see cref="BlibWriter"/>, with everything that hangs off a spectrum: its peak blobs as
    /// they are, its modifications, its annotations, and its proteins re-registered in the
    /// target by accession with their global values. The source files are copied first, and
    /// each spectrum's source maps to the target's row for the same file. BlibFilter copies the
    /// one spectrum it picks per precursor this way, BlibBuild every spectrum of a <c>.blib</c>
    /// input; before this, both ran SQL between an attached library and the one being written.
    ///
    /// <para>The optional columns (<see cref="BlibSchema.OptionalRefSpectraColumns"/>,
    /// <see cref="BlibSchema.OptionalProteinsColumns"/>) the source has are added to the target
    /// by <see cref="CopySourceFiles"/>, so a copy keeps the global values; a caller that merges
    /// libraries of different experiments drops them again
    /// (<see cref="BlibWriter.RemoveOptionalColumns"/>).</para>
    /// </summary>
    public sealed class BlibCopier
    {
        private readonly BlibReader _source;
        private readonly BlibWriter _target;
        private readonly Dictionary<long, long> _fileIds = new Dictionary<long, long>();
        private readonly Dictionary<long, BlibProtein> _proteins = new Dictionary<long, BlibProtein>();
        private ILookup<long, long> _proteinIds;
        private bool _proteinsLoaded;
        private bool _sourceFilesCopied;

        public BlibCopier(BlibReader source, BlibWriter target)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _target = target ?? throw new ArgumentNullException(nameof(target));
        }

        /// <summary>
        /// Copies every <c>SpectrumSourceFiles</c> row of the source, in id order, and adds the
        /// source's optional columns to the target. A file the target already has by name is
        /// reused, its cutoff set to unknown (-1) when the two disagree, as BiblioSpec records a
        /// file whose spectra were filtered at more than one threshold. Called once, before the
        /// spectra; <see cref="CopySpectrum(BlibSpectrumRecord,int,bool)"/> calls it if not.
        /// </summary>
        public void CopySourceFiles()
        {
            if (_sourceFilesCopied)
                return;
            _sourceFilesCopied = true;
            foreach (var column in BlibSchema.OptionalRefSpectraColumns)
            {
                if (_source.HasColumn(BlibReader.TABLE_REF_SPECTRA, column.Name))
                    _target.AddOptionalColumn(column);
            }
            foreach (var column in BlibSchema.OptionalProteinsColumns)
            {
                if (_source.HasColumn(BlibReader.TABLE_PROTEINS, column.Name))
                    _target.AddOptionalColumn(column);
            }
            foreach (var file in _source.ReadSourceFiles())
            {
                // As BiblioSpec has always copied a source file: one without an identification
                // file is its own, and one without a cutoff was filtered at an unknown one (-1).
                double cutoff = file.CutoffScore ?? -1;
                if (_target.TryFindSourceFile(file.FileName, out long existingId, out double? existingCutoff))
                {
                    if (!Equals(existingCutoff, cutoff))
                        _target.SetCutoffScore(existingId, -1);
                    _fileIds[file.Id] = existingId;
                    continue;
                }
                _fileIds[file.Id] = _target.AddSourceFile(file.FileName, file.IdFileName ?? file.FileName, cutoff, file.WorkflowType);
            }
        }

        /// <summary>
        /// The target's source file for the source's <paramref name="sourceFileId"/>: the row
        /// <see cref="CopySourceFiles"/> mapped it to, or the UNKNOWN file for an id the source
        /// does not have (a library that records no source files).
        /// </summary>
        public long TargetFileId(long sourceFileId)
        {
            CopySourceFiles();
            return _fileIds.TryGetValue(sourceFileId, out long targetId) ? targetId : _target.EnsureUnknownSourceFile();
        }

        /// <summary>
        /// Copies the spectrum of id <paramref name="sourceId"/>, as
        /// <see cref="CopySpectrum(BlibSpectrumRecord,int,bool)"/> does one already read; null
        /// when the source has no such spectrum.
        /// </summary>
        public long? CopySpectrum(long sourceId, int copies, bool keepId)
        {
            var spectrum = _source.ReadSpectrum(sourceId, true);
            return spectrum == null ? null : CopySpectrum(spectrum, copies, keepId);
        }

        /// <summary>
        /// Writes <paramref name="spectrum"/>, read from the source with its peaks, to the
        /// target with <paramref name="copies"/> as its copy count, its source file mapped, and
        /// its modifications, annotations and proteins read from the source and written with it;
        /// a protein keeps its global q-value and PEP where the target has those columns. With
        /// <paramref name="keepId"/> the spectrum keeps its id (<see cref="BlibWriter.AddSpectrum(BlibSpectrumRecord,bool)"/>).
        /// Returns the target's id. The record is left as it was written: its copies and
        /// source file are the target's, its modifications and annotations filled in, and its
        /// protein names cleared (the proteins go through <see cref="BlibWriter.AddProtein"/>
        /// with their values), so a caller that needs the source's values reads them first.
        /// </summary>
        public long CopySpectrum(BlibSpectrumRecord spectrum, int copies, bool keepId)
        {
            if (spectrum == null)
                throw new ArgumentNullException(nameof(spectrum));
            CopySourceFiles();
            long sourceId = spectrum.Id;
            spectrum.Copies = copies;
            spectrum.FileId = TargetFileId(spectrum.FileId);
            // A library of a schema before the ion mobility columns gets them as 0, as BiblioSpec
            // writes a spectrum without ion mobility (every such column written, zero included); a
            // NULL the source does hold stays NULL.
            if (!_source.HasSpectrumColumn(@"ionMobility"))
                spectrum.IonMobility = spectrum.IonMobility ?? 0;
            if (!_source.HasSpectrumColumn(@"collisionalCrossSectionSqA"))
                spectrum.CollisionalCrossSectionSqA = spectrum.CollisionalCrossSectionSqA ?? 0;
            if (!_source.HasSpectrumColumn(@"ionMobilityHighEnergyOffset"))
                spectrum.IonMobilityHighEnergyOffset = spectrum.IonMobilityHighEnergyOffset ?? 0;
            spectrum.Modifications = _source.ReadModifications(sourceId);
            spectrum.Annotations = _source.ReadAnnotations(sourceId);
            // The proteins are written below, with their values; the writer's own loop takes names only.
            spectrum.Proteins = Array.Empty<string>();
            long targetId = _target.AddSpectrum(spectrum, keepId);
            foreach (long proteinId in ProteinIds()[sourceId])
            {
                if (!Proteins().TryGetValue(proteinId, out var protein) || string.IsNullOrEmpty(protein.Accession))
                    continue;
                _target.AddSpectrumProtein(targetId, _target.AddProtein(protein.Accession, protein.QValue, protein.Pep));
            }
            return targetId;
        }

        private ILookup<long, long> ProteinIds()
        {
            return _proteinIds ?? (_proteinIds = _source.ReadProteinIds());
        }

        private Dictionary<long, BlibProtein> Proteins()
        {
            if (!_proteinsLoaded)
            {
                foreach (var protein in _source.ReadProteinTable())
                    _proteins[protein.Id] = protein;
                _proteinsLoaded = true;
            }
            return _proteins;
        }
    }
}
