using System;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model
{
    public interface IDocumentModifier
    {
        ModifiedDocument ModifyDocument(SrmDocument document, SrmDocument.DOCUMENT_TYPE modeUi);
    }

    public class DocumentModifier {
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

        public static IDocumentModifier Create(Func<SrmDocument, ModifiedDocument> impl)
        {
            return new Impl((doc, modeUi)=>impl(doc));
        }

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

    public class ModifiedDocument : Immutable
    {
        public ModifiedDocument(SrmDocument document)
        {
            Document = document;
        }

        public ModifiedDocument ChangeAuditLogEntry(AuditLogEntry auditLogEntry)
        {
            return ChangeProp(ImClone(this), im => im.AuditLogEntry = auditLogEntry);
        }

        public ModifiedDocument ChangeAuditLogException(Exception auditLogException)
        {
            return ChangeProp(ImClone(this), im => im.AuditLogException = auditLogException);
        }

        public SrmDocument Document { get; }
        public AuditLogEntry AuditLogEntry { get; private set; }

        public Exception AuditLogException { get; private set; }
    }
}
