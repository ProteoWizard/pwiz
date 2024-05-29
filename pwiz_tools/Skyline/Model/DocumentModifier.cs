/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using System.Diagnostics.Contracts;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model
{
    /// <summary>
    /// Holds a new document and the AuditLogEntry that resulted in it
    /// </summary>
    public class ModifiedDocument : Immutable
    {
        public ModifiedDocument(SrmDocument document)
        {
            Document = document;
        }

        [Pure]
        public ModifiedDocument ChangeAuditLogEntry(AuditLogEntry auditLogEntry)
        {
            return ChangeProp(ImClone(this), im => im.AuditLogEntry = auditLogEntry);
        }

        [Pure]
        public ModifiedDocument ChangeAuditLogException(Exception auditLogException)
        {
            return ChangeProp(ImClone(this), im => im.AuditLogException = auditLogException);
        }

        public SrmDocument Document { get; }
        public AuditLogEntry AuditLogEntry { get; private set; }

        public Exception AuditLogException { get; private set; }
    }

    /// <summary>
    /// Interface for something which operates on a <see cref="SrmDocument"/>
    /// and produces a <see cref="ModifiedDocument" />.
    /// </summary>
    public interface IDocumentModifier
    {
        ModifiedDocument ModifyDocument(SrmDocument document, SrmDocument.DOCUMENT_TYPE modeUi);
    }

    /// <summary>
    /// Helper functions for creating <see cref="IDocumentModifier"/> from functions
    /// </summary>
    public static class DocumentModifier 
    {
        /// <summary>
        /// Creates an IDocumentModifier from separate functions for modifying the document
        /// and creating the audit log entry
        /// </summary>
        public static IDocumentModifier Create(Func<SrmDocument, SrmDocument> modifyFunc,
            Func<SrmDocumentPair, AuditLogEntry> logFunc)
        {
            return new Impl((docOriginal, modeUi) =>
            {
                var modifiedDocument = new ModifiedDocument(modifyFunc(docOriginal));
                if (ReferenceEquals(modifiedDocument.Document, docOriginal))
                {
                    return null;
                }
                try
                {
                    return modifiedDocument.ChangeAuditLogEntry(logFunc?.Invoke(SrmDocumentPair.Create(docOriginal, modifiedDocument.Document, modeUi)));
                }
                catch (Exception ex)
                {
                    return modifiedDocument.ChangeAuditLogException(ex);
                }
            });
        }

        /// <summary>
        /// Creates an IDocumentModifier from a single function.
        /// </summary>
        public static IDocumentModifier Create(Func<SrmDocument, ModifiedDocument> impl)
        {
            return new Impl((doc, modeUi)=>impl(doc));
        }

        /// <summary>
        /// Creates an IDocumentModifier from a ModifiedDocument which has already been created.
        /// This should typically only be used if it is known that the document in the document container
        /// cannot be changed, usually because <see cref="SkylineWindow.GetDocumentChangeLock"/> is
        /// locked in the current thread.
        /// </summary>
        public static IDocumentModifier FromResult(SrmDocument originalDocument, ModifiedDocument modifiedDocument)
        {
            return new Impl((docOriginal, modeUi) =>
            {
                if (!ReferenceEquals(docOriginal, originalDocument))
                {
                    throw new ApplicationException(Resources
                        .SkylineDataSchema_VerifyDocumentCurrent_The_document_was_modified_in_the_middle_of_the_operation_);
                }

                return modifiedDocument;
            });
        }

        private class Impl : IDocumentModifier
        {
            private Func<SrmDocument, SrmDocument.DOCUMENT_TYPE, ModifiedDocument> _impl;
            public Impl(Func<SrmDocument, SrmDocument.DOCUMENT_TYPE, ModifiedDocument> impl)
            {
                _impl = impl;
            }

            public ModifiedDocument ModifyDocument(SrmDocument document, SrmDocument.DOCUMENT_TYPE modeUi)
            {
                return _impl(document, modeUi);
            }
        }
    }
}
