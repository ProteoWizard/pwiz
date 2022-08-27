using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public class DocumentContainerWrapper : IDocumentContainer
    {
        private IDocumentContainer _documentContainer;
        private HashSet<EventHandler<DocumentChangedEventArgs>> _listeners;
        protected SrmDocument _document;

        public DocumentContainerWrapper(IDocumentContainer documentContainer)
        {
            _documentContainer = documentContainer;
        }

        public void Listen(EventHandler<DocumentChangedEventArgs> listener)
        {
            lock (this)
            {
                if (_listeners == null)
                {
                    BeforeFirstListenerAdded();
                }

                if (!_listeners.Add(listener))
                {
                    throw new InvalidOperationException(@"Listener already added");
                }
            }
        }

        public void Unlisten(EventHandler<DocumentChangedEventArgs> listener)
        {
            lock (this)
            {
                if (_listeners == null || !_listeners.Remove(listener))
                {
                    throw new InvalidOperationException(@"Listener has not been added");
                }

                if (_listeners.Count == 0)
                {
                    AfterLastListenerRemoved();
                }
            }
        }

        protected virtual void BeforeFirstListenerAdded()
        {
            _documentContainer.Listen(DocumentContainerOnChanged);
            _listeners = new HashSet<EventHandler<DocumentChangedEventArgs>>();
            _document = WrapDocument(_documentContainer.Document);
        }

        protected virtual void AfterLastListenerRemoved()
        {
            _documentContainer.Unlisten(DocumentContainerOnChanged);
            _listeners = null;
        }



        private void DocumentContainerOnChanged(object sender, DocumentChangedEventArgs args)
        {
            var previous = _document;
            _document = WrapDocument(_documentContainer.Document);
            EventHandler<DocumentChangedEventArgs>[] listeners;
            lock (this)
            {
                listeners = _listeners?.ToArray();
            }

            if (listeners != null)
            {
                var eventArgs = new DocumentChangedEventArgs(previous, args.IsOpeningFile, args.IsInSelUpdateLock);
                foreach (var listener in listeners)
                {
                    listener(this, eventArgs);
                }
            }
        }

        protected virtual SrmDocument WrapDocument(SrmDocument originalDocument)
        {
            return originalDocument;
        }

        public SrmDocument Document
        {
            get
            {
                return _document;
            }
        }

        public string DocumentFilePath
        {
            get
            {
                return _documentContainer.DocumentFilePath;
            }
        }

        public bool SetDocument(SrmDocument docNew, SrmDocument docOriginal)
        {
            throw new InvalidOperationException();
        }

        public bool IsClosing
        {
            get
            {
                return _documentContainer.IsClosing;
            }
        }

        public IEnumerable<BackgroundLoader> BackgroundLoaders {
            get
            {
                return _documentContainer.BackgroundLoaders;
            }
        }
        public void AddBackgroundLoader(BackgroundLoader loader)
        {
            _documentContainer.AddBackgroundLoader(loader);
        }

        public void RemoveBackgroundLoader(BackgroundLoader loader)
        {
            _documentContainer.RemoveBackgroundLoader(loader);
        }
    }
}
