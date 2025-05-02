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

using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Model.Files
{
    public class Replicate : FileNode
    {
        public Replicate(IDocumentContainer documentContainer, ChromatogramSetId chromSetId) : 
            base(documentContainer, new IdentityPath(chromSetId), ImageId.replicate)
        {
        }

        public override Immutable Immutable => ChromatogramSet;

        // CONSIDER: improve UI if this ChromatogramSet is invalid because the underlying document changed.
        //           Similar point for properties on all other file model types
        public override string Name => ChromatogramSet?.Name ?? string.Empty;
        public override string FilePath => null;
        public override string FileName => null;

        public override IList<FileNode> Files
        {
            get
            {
                var files = ChromatogramSet.MSDataFileInfos.Select(fileInfo =>
                    new ReplicateSampleFile(DocumentContainer, (ChromatogramSetId)IdentityPath.GetIdentity(0), (ChromFileInfoId)fileInfo.Id));

                return ImmutableList.ValueOf(files.ToList<FileNode>());
            }
        }

        public ModifiedDocument Delete(SrmDocument document, List<FileNode> models)
        {
            var deleteIds = models.Select(model => ReferenceValue.Of(model.IdentityPath.Child)).ToHashSet();
            var deleteNames = models.Select(item => item.Name).ToList();

            var remainingChromatograms = 
                document.MeasuredResults.Chromatograms.Where(chrom => !deleteIds.Contains(chrom.Id));

            var newMeasuredResults = document.MeasuredResults.ChangeChromatograms(remainingChromatograms.ToList());
            var newDocument = document.ChangeMeasuredResults(newMeasuredResults);
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

        private ChromatogramSet ChromatogramSet =>
            DocumentContainer.Document.MeasuredResults.FindChromatogramSet((ChromatogramSetId)IdentityPath.GetIdentity(0));

        public ModifiedDocument ChangeName(SrmDocument document, string newName)
        {
            var oldName = ChromatogramSet.Name;
            var newChromatogram = (ChromatogramSet)ChromatogramSet.ChangeName(newName);
            var measuredResults = Document.MeasuredResults;

            var chromatograms = measuredResults.Chromatograms.ToArray();
            for (var i = 0; i < chromatograms.Length; i++)
            {
                if (ReferenceEquals(chromatograms[i].Id, newChromatogram.Id))
                {
                    chromatograms[i] = newChromatogram;
                }
            }

            measuredResults = measuredResults.ChangeChromatograms(chromatograms);
            var newDocument = document.ChangeMeasuredResults(measuredResults);
            newDocument.ValidateResults();

            var entry = AuditLogEntry.CreateSimpleEntry(
                MessageType.files_tree_node_renamed,
                document.DocumentType,
                oldName,
                newName);

            return new ModifiedDocument(newDocument).ChangeAuditLogEntry(entry);

            // var oldName = chromatogram.Name;
            // var newName = newLabel;
            //
            // SkylineWindow.ModifyDocument(FilesTreeResources.Change_ReplicateName,
            //     doc =>
            //     {
            //         var newChromatogram = (ChromatogramSet)chromatogram.ChangeName(newName);
            //         var measuredResults = SkylineWindow.Document.MeasuredResults;
            //
            //         var chromatograms = measuredResults.Chromatograms.ToArray();
            //         for (var i = 0; i < chromatograms.Length; i++)
            //         {
            //             if (ReferenceEquals(chromatograms[i].Id, newChromatogram.Id))
            //             {
            //                 chromatograms[i] = newChromatogram;
            //             }
            //         }
            //
            //         measuredResults = measuredResults.ChangeChromatograms(chromatograms);
            //         var newDoc = doc.ChangeMeasuredResults(measuredResults);
            //         newDoc.ValidateResults();
            //         return newDoc;
            //     },
            //     docPair =>
            //        
            // );
        }
    }
}
