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

using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Model.Files
{
    public class Replicate : FileModel, IFileRenameable
    {
        public static Replicate Create(string documentFilePath, [NotNull] ChromatogramSet chromSet)
        {
            var name = chromSet.Name ?? string.Empty;
            var identityPath = new IdentityPath((ChromatogramSetId)chromSet.Id);
            var files = 
                chromSet.MSDataFileInfos.Select(chromFileInfo => ReplicateSampleFile.Create(documentFilePath, identityPath, chromFileInfo)).ToList();

            return new Replicate(documentFilePath, identityPath, name, files);
        }

        private Replicate(string documentFilePath, IdentityPath identityPath, string name, IList<ReplicateSampleFile> files) : 
            base(documentFilePath, identityPath)
        {
            Name = name;
            Files = files.Cast<FileModel>().ToList();
        }

        public override string Name { get; }
        public override string FilePath => null;
        public override string FileName => null;
        protected override string FileTypeText => string.Empty; // Replicates don't use type prefix
        public override ImageId ImageAvailable => ImageId.replicate;
        public override ImageId ImageMissing => ImageId.replicate_missing;
        public override IList<FileModel> Files { get; }

        public static ChromatogramSet LoadChromSetFromDocument(SrmDocument document, Replicate replicate)
        {
            var chromSetId = replicate.IdentityPath.GetIdentity(0);
            document.MeasuredResults.TryGetChromatogramSet(chromSetId.GlobalIndex, out var chromSet, out _);
            return chromSet;
        }

        public static ModifiedDocument Delete(SrmDocument document, SrmSettingsChangeMonitor monitor, List<FileModel> models)
        {
            var deleteIds = models.Select(model => ReferenceValue.Of(model.IdentityPath.Child)).ToHashSet();
            var deleteNames = models.Select(item => item.Name).ToList();

            var remainingChromatograms = 
                document.MeasuredResults.Chromatograms.Where(chrom => !deleteIds.Contains(chrom.Id)).ToList();

            MeasuredResults newMeasuredResults = null;
            if (remainingChromatograms.Count > 0)
            {
                newMeasuredResults = document.MeasuredResults.ChangeChromatograms(remainingChromatograms);
            }

            var newDocument = document.ChangeMeasuredResults(newMeasuredResults, monitor);
            newDocument.ValidateResults();
            
            var entry = AuditLogEntry.CreateCountChangeEntry(
                MessageType.files_tree_replicates_remove_one,
                MessageType.files_tree_replicates_remove_several,
                document.DocumentType,
                deleteNames
            );

            if (deleteNames.Count > 1)
            {
                entry = entry.AppendAllInfo(deleteNames.
                    Select(name => new MessageInfo(MessageType.removed_replicate, document.DocumentType, name)).
                    ToList());
            }

            return new ModifiedDocument(newDocument).ChangeAuditLogEntry(entry);
        }

        public static ModifiedDocument Rearrange(SrmDocument document, SrmSettingsChangeMonitor monitor, List<FileModel> draggedModels, FileModel dropModel, MoveType moveType)
        {
            var draggedChromSets = draggedModels.Cast<Replicate>().Select(model => LoadChromSetFromDocument(document, model)).ToList();

            var newChromatograms = document.MeasuredResults.Chromatograms.Except(draggedChromSets).ToList();

            switch (moveType)
            {
                case MoveType.move_to:
                {
                    var dropChromSet = LoadChromSetFromDocument(document, (Replicate)dropModel);
                    var insertAt = newChromatograms.IndexOf(dropChromSet);
                    newChromatograms.InsertRange(insertAt, draggedChromSets);
                    break;
                }
                case MoveType.move_last:
                default:
                    newChromatograms.AddRange(draggedChromSets);
                    break;
            }

            var newMeasuredResults = document.MeasuredResults.ChangeChromatograms(newChromatograms);
            var newDocument = document.ChangeMeasuredResults(newMeasuredResults, monitor);
            newDocument.ValidateResults();

            var readableNames = draggedChromSets.Select(item => item.Name).ToList();

            var entry = AuditLogEntry.CreateCountChangeEntry(
                MessageType.files_tree_node_drag_and_drop,
                MessageType.files_tree_nodes_drag_and_drop,
                document.DocumentType, 
                readableNames,
                readableNames.Count,
                str => MessageArgs.Create(str, dropModel.Name),
                MessageArgs.Create(readableNames.Count, dropModel.Name)
            );
            
            if (readableNames.Count > 1)
            {
                entry = entry.ChangeAllInfo(readableNames.
                    Select(node => new MessageInfo(MessageType.files_tree_node_drag_and_drop, newDocument.DocumentType, node, dropModel.Name)).
                    ToList());
            }

            return new ModifiedDocument(newDocument).ChangeAuditLogEntry(entry);
        }

        #region IFileRenameable implementation

        public bool HasItemWithName(SrmDocument document, string newName)
        {
            Assume.IsNotNull(document);
            Assume.IsNotNull(document.MeasuredResults);

            var chromSets = document.MeasuredResults.Chromatograms;
            return chromSets.Any(item => string.Equals(item.Name, newName, StringComparison.CurrentCulture));
        }

        public bool ValidateNewName(SrmDocument document, string newName, out string errorMessage)
        {
            if (HasItemWithName(document, newName))
            {
                errorMessage = string.Format(Controls.FilesTree.FilesTreeResources.FilesTreeForm_Error_Renaming_Replicate, newName);
                return false;
            }
            errorMessage = null;
            return true;
        }

        public ModifiedDocument PerformRename(SrmDocument document, SrmSettingsChangeMonitor monitor, string newName)
        {
            var chromSet = LoadChromSetFromDocument(document, this);

            var oldName = chromSet.Name;
            var newChromatogram = (ChromatogramSet)chromSet.ChangeName(newName);
            var measuredResults = document.MeasuredResults;

            var chromatograms = measuredResults.Chromatograms.ToArray();
            for (var i = 0; i < chromatograms.Length; i++)
            {
                if (ReferenceEquals(chromatograms[i].Id, newChromatogram.Id))
                {
                    chromatograms[i] = newChromatogram;
                }
            }

            measuredResults = measuredResults.ChangeChromatograms(chromatograms);
            var newDocument = document.ChangeMeasuredResults(measuredResults, monitor);
            newDocument.ValidateResults();

            var entry = AuditLogEntry.CreateSimpleEntry(
                MessageType.files_tree_node_renamed,
                document.DocumentType,
                oldName,
                newName);

            return new ModifiedDocument(newDocument).ChangeAuditLogEntry(entry);
        }

        public string AuditLogMessageResource => Controls.FilesTree.FilesTreeResources.Change_ReplicateName;

        #endregion
    }
}
