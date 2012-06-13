/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using System.Windows.Forms;
using NHibernate;
using pwiz.ProteomeDatabase.Util;
using pwiz.Skyline.Model;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// Interface to a "file system" which will access the disk in production
    /// and remain completely in memory during unit tests.
    /// </summary>
    public interface IStreamManager
    {
        /// <summary>
        /// Proxy for <see cref="FileStream"/> constructor.
        /// </summary>
        /// <param name="path">Path to a file</param>
        /// <param name="mode">Mode for the stream</param>
        /// <param name="buffer">True if file i/o should be buffered</param>
        /// <returns>A newly created stream</returns>
        Stream CreateStream(string path, FileMode mode, bool buffer);

        /// <summary>
        /// Proxy for <see cref="Stream.Close"/>, since the in-memory version
        /// has extra book keeping to do, and this is simpler than implementing
        /// a whole <see cref="Stream"/> override for the purpose.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> to close</param>
        void Finish(Stream stream);

        /// <summary>
        /// Creates a <see cref="FileMode.Open"/> stream that is added to a
        /// <see cref="ConnectionPool"/> for later use with <see cref="IPooledStream.Stream"/>.
        /// The stream should be deterministacally closed with <see cref="IPooledStream.CloseStream"/>
        /// when it is no longer in use, though subsequent access of
        /// <see cref="IPooledStream.Stream"/> will reopen the underlying stream.
        /// <para>
        /// For use with streams that are held open as long as they are
        /// associated with the active document, but must be closed when the
        /// referencing document moves into the undo buffer, or is just released.
        /// </para><para>
        /// This extra level of indirection is required to maintain the
        /// immutable nature of the document tree.
        /// </para>
        /// </summary>
        /// <param name="path">Path to a file</param>
        /// <param name="buffer">True if file i/o should be buffered</param>
        /// <returns>A newly opened stream for the file</returns>
        IPooledStream CreatePooledStream(string path, bool buffer);

        /// <summary>
        /// The <see cref="ConnectionPool"/> used to manage <see cref="IPooledStream"/>
        /// instances returned from <see cref="CreatePooledStream"/>, but
        /// which also may be used for other connection types.
        /// </summary>
        ConnectionPool ConnectionPool { get; }

        /// <summary>
        /// Proxy for <see cref="StreamReader"/> constructor.
        /// </summary>
        /// <param name="path">Path to a file</param>
        /// <returns>A newly created <see cref="TextReader"/></returns>
        TextReader CreateReader(string path);

        /// <summary>
        /// Proxy for <see cref="StreamWriter"/> constructor.
        /// </summary>
        /// <param name="path">Path to a file</param>
        /// <returns>A newly created <see cref="TextWriter"/></returns>
        TextWriter CreateWriter(string path);

        /// <summary>
        /// Proxy for <see cref="TextWriter.Close"/>, since the in-memory version
        /// has extra book keeping to do, and this is simpler than implementing
        /// a whole <see cref="TextWriter"/> override for the purpose.
        /// </summary>
        /// <param name="writer">The <see cref="TextWriter"/> to close</param>
        void Finish(TextWriter writer);

        /// <summary>
        /// Proxy for <see cref="File.GetAttributes"/>.
        /// </summary>
        /// <param name="path">Path to get file attributes for</param>
        /// <returns>The file attributes if the file system is on disk, or ?? if not</returns>
        FileAttributes GetAttributes(string path);

        /// <summary>
        /// Gets the length of the specified file.
        /// </summary>
        /// <param name="path">Path to get file length for</param>
        /// <returns>Length of the file</returns>
        long GetLength(string path);

        /// <summary>
        /// Proxy for <see cref="File.Exists"/>.
        /// </summary>
        /// <param name="path">The path to check for existence</param>
        /// <returns>True if the file exists in the managed "file system"</returns>
        bool Exists(string path);

        /// <summary>
        /// Proxy for <see cref="File.Delete"/>.
        /// </summary>
        /// <param name="path">The path to delete from the managed "file system"</param>
        void Delete(string path);

        /// <summary>
        /// Moves a temporary file into its desired final destination, using atomic
        /// operations, possibly overwriting an existing file in the process.
        /// </summary>
        /// <param name="pathTemp">Temporary file path</param>
        /// <param name="pathDestination">Destination file path</param>
        /// <param name="streamDest">A pooled stream that may be connected to the destination</param>
        void Commit(string pathTemp, string pathDestination, IPooledStream streamDest);

        /// <summary>
        /// Gets a guaranteed unique temporary file name for this "file system".
        /// </summary>
        /// <param name="basePath">Destination directory for the temporary file</param>
        /// <param name="prefix">Prefix for the file name</param>
        /// <returns>The temporary file path</returns>
        string GetTempFileName(string basePath, string prefix);

        /// <summary>
        /// Synchronizes modification times on disk.
        /// </summary>
        /// <param name="path">Path being cached</param>
        /// <param name="pathCache">Path to the cache that will have its modified time changed</param>
        void SetCache(string path, string pathCache);

        /// <summary>
        /// Determines whether a file is cached by another file.  True when modified
        /// times are identical on disk.
        /// </summary>
        /// <param name="path">Path that may be cached</param>
        /// <param name="pathCache">Path of potential cache</param>
        /// <returns>True if path is cached</returns>
        bool IsCached(string path, string pathCache);
    }

    /// <summary>
    /// Allows pooling of long-lived connections, like file streams, or database
    /// connections, which should remain open as long as they associated with
    /// the currently active document, but should be deterministically closed
    /// when their only references live on the undo buffer.
    /// </summary>
    public sealed class ConnectionPool
    {
        private readonly Dictionary<int, IDisposable> _connections =
            new Dictionary<int, IDisposable>();
        
        /// <summary>
        /// True if the connection for this <see cref="Identity"/> is currently
        /// pooled.
        /// </summary>
        /// <param name="id">The identity used to get the original connection</param>
        /// <returns>True if the connection is pooled</returns>
        public bool IsInPool(Identity id)
        {
            lock (this)
            {
                return _connections.ContainsKey(id.GlobalIndex);
            }
        }

        /// <summary>
        /// Gets or creates a connection associated with an <see cref="Identity"/>.
        /// If the connection is already in the pool, it is returned.  Otherwise,
        /// a new connection is created.
        /// </summary>
        /// <param name="id">The identity to associate with this connection</param>
        /// <param name="connect">A function for creating a new connection, if necessary</param>
        /// <returns>The pooled connection, or a newly created connection, if no pooled
        ///     connection exists</returns>
        public IDisposable GetConnection(Identity id, Func<IDisposable> connect)
        {
            lock (this)
            {
                IDisposable connection;
                if (_connections.TryGetValue(id.GlobalIndex, out connection))
                    return connection;
                // Connection must be made inside lock to keep the get and add
                // within a single synchronized block.
                connection = connect();
                _connections.Add(id.GlobalIndex, connection);
                return connection;
            }            
        }

        /// <summary>
        /// Disposes a pooled connection.  This must be called, if the connection
        /// is not on the active document.
        /// </summary>
        /// <param name="id">The identity used to get the original connection</param>
        public void Disconnect(Identity id)
        {
            lock (this)
            {
                IDisposable connection;
                if (!_connections.TryGetValue(id.GlobalIndex, out connection))
                    return;
                _connections.Remove(id.GlobalIndex);
                // Disconnect inside lock, since a new attempt to connect
                // may fail if the old connection is not fully disconnected.
                connection.Dispose();
            }
        }

        public void DisconnectWhile(IPooledStream stream, Action act)
        {
            lock (this)
            {
                stream.CloseStream();
                act();
            }
        }
    }

    /// <summary>
    /// Abstract class for a specific long-lived connection.  Must be subclassed
    /// for each new connection type.
    /// </summary>
    /// <typeparam name="TDisp">Type of connection being managed</typeparam>
    public abstract class ConnectionId<TDisp> : Identity
        where TDisp : IDisposable
    {
        private readonly ConnectionPool _connectionPool;

        /// <summary>
        /// Creates the immutable identifier for a long-lived connection.
        /// </summary>
        /// <param name="connectionPool">The pool which will manage this connection</param>
        protected ConnectionId(ConnectionPool connectionPool)
        {
            _connectionPool = connectionPool;
        }

        /// <summary>
        /// The pool managing this connection
        /// </summary>
        protected ConnectionPool ConnectionPool { get { return _connectionPool; } }

        /// <summary>
        /// Must be implemented to actually make the underlying connection that
        /// will be cached, and closed as needed.
        /// </summary>
        /// <returns>A connection that may be disposed</returns>
        protected abstract IDisposable Connect();

        /// <summary>
        /// The actual connection as stored in the pool, or newly created.
        /// </summary>
        public TDisp Connection
        {
            get { return (TDisp) _connectionPool.GetConnection(this, Connect); }
        }

        /// <summary>
        /// Closes the underlying connection and removes it from the pool.
        /// </summary>
        public void Disconnect()
        {
            _connectionPool.Disconnect(this);
        }
    }

    /// <summary>
    /// Interface for a long-lived read-only stream that is held in a
    /// <see cref="ConnectionPool"/>, while it is associated with the active
    /// document.
    /// </summary>
    public interface IPooledStream
    {
        /// <summary>
        /// Globally unique index by which to identify the stream.
        /// </summary>
        int GlobalIndex { get; }

        /// <summary>
        /// The pooled stream.  Use of this property may actually create
        /// the connection.
        /// </summary>
        Stream Stream { get; }

        /// <summary>
        /// True if the stream has been modified, since it was first opened.
        /// May be used to avoid a <see cref="FileModifiedException"/> accessing
        /// the <see cref="Stream"/> property.
        /// </summary>
        bool IsModified { get; }

        /// <summary>
        /// True if the stream is currently open.
        /// </summary>
        bool IsOpen { get; }

        /// <summary>
        /// Used to close the stream when it is not associated with the active
        /// document.
        /// </summary>
        void CloseStream();
    }

    /// <summary>
    /// Concrete implementation of a long-lived pooled stream for a file on disk.
    /// </summary>
    public sealed class PooledFileStream : ConnectionId<Stream>, IPooledStream
    {
        private const int MILLISECOND_TICKS = 10*1000;

        public PooledFileStream(IStreamManager streamManager, string filePath, bool buffered)
            : base(streamManager.ConnectionPool)
        {
            StreamManager = streamManager;
            FilePath = filePath;
            Buffered = buffered;
            FileTime = File.GetLastWriteTime(FilePath);
        }

        public IStreamManager StreamManager { get; private set; }
        public string FilePath { get; private set; }
        public bool Buffered { get; private set; }
        public DateTime FileTime { get; private set; }

        /// <summary>
        /// Handles actually opening the stream.
        /// </summary>
        /// <returns>A new stream</returns>
        protected override IDisposable Connect()
        {
            // Check to see if the file was modified, during the time
            // it was closed.
            if (IsModified)
                throw new FileModifiedException(string.Format("The file {0} has been modified, since it was first opened.", FilePath));
            // Create the stream
            return StreamManager.CreateStream(FilePath, FileMode.Open, Buffered);
        }

        /// <summary>
        /// True if the file has been modified since it was first opened.
        /// </summary>
        public bool IsModified
        {
            get
            {
                // If it is still in the pool, then it can't have been modified
                // Otherwise, if the modified time is less than a millisecond different
                // from when it was opened, then it is considered unmodified.  This is
                // because, differences of ~70 ticks (< 0.01 millisecond) where seen
                // after ZIP file extraction of shared files to a network drive.
                return !IsOpen && Math.Abs(FileTime.Ticks - File.GetLastWriteTime(FilePath).Ticks) > MILLISECOND_TICKS;
            }
        }

        public bool IsOpen
        {
            get { return ConnectionPool.IsInPool(this); }
        }

        /// <summary>
        /// The pooled stream.  Use of this property may actually create
        /// the connection.
        /// </summary>
        public Stream Stream
        {
            get { return Connection; }
        }

        /// <summary>
        /// Used to close the stream when it is not associated with the active
        /// document.
        /// </summary>
        public void CloseStream()
        {
            Disconnect();
        }
    }

    public sealed class PooledSessionFactory : ConnectionId<ISessionFactory>, IPooledStream
    {
        public PooledSessionFactory(ConnectionPool connectionPool, Type typeDb, string filePath)
            : base(connectionPool)
        {
            TypeDb = typeDb;
            FilePath = filePath;
            FileTime = File.GetLastWriteTime(FilePath);
        }

        private Type TypeDb { get; set; }
        private string FilePath { get; set; }
        private DateTime FileTime { get; set; }

        protected override IDisposable Connect()
        {
            return SessionFactoryFactory.CreateSessionFactory(FilePath, TypeDb, false);
        }

        Stream IPooledStream.Stream
        {
            get { throw new InvalidOperationException(); }
        }

        public bool IsModified
        {
            get
            {
                // If it is still in the pool, then it can't have been modified
                return !IsOpen && !Equals(FileTime, File.GetLastWriteTime(FilePath));
            }
        }

        public bool IsOpen
        {
            get { return ConnectionPool.IsInPool(this); }
        }

        public void CloseStream()
        {
            Disconnect();
        }
    }

    public class FileStreamManager : IStreamManager
    {
        static FileStreamManager()
        {
            Default = new FileStreamManager();
        }

        public static FileStreamManager Default { get; private set; }

        private readonly ConnectionPool _connectionPool = new ConnectionPool();

        private FileStreamManager()
        {            
        }

        public Stream CreateStream(string path, FileMode mode, bool buffered)
        {
            Stream stream;
            try
            {
                if (mode != FileMode.Open)
                    stream = new FileStream(path, mode);
                else
                    stream = new FileStream(path, mode, FileAccess.Read, FileShare.Read);
            }
            catch (Exception x)
            {
                // Make sure exceptions thrown from this method are only IOExceptions
                if (!(x is IOException))
                    throw new IOException(string.Format("Unexpected error opening {0}", path), x);
                throw;
            }

            // If writing make sure the stream is buffered.
            if (buffered)
                stream = new BufferedStream(stream, 32*1024);
            return stream;
        }

        public void Finish(Stream stream)
        {
            stream.Close();
        }

        public IPooledStream CreatePooledStream(string path, bool buffer)
        {
            return new PooledFileStream(this, path, buffer);
        }

        public ConnectionPool ConnectionPool
        {
            get { return _connectionPool; }
        }

        public TextReader CreateReader(string path)
        {
            return new StreamReader(path);
        }

        public TextWriter CreateWriter(string path)
        {
            try
            {
                return new StreamWriter(path);
            }
            catch (Exception x)
            {
                // Make sure exceptions thrown from this method are only IOExceptions
                if ((x is IOException))
                    throw new IOException(string.Format("Unexpected error opening {0}", path), x);
                throw;
            }
        }

        public void Finish(TextWriter writer)
        {
            writer.Close();
        }

        public FileAttributes GetAttributes(string path)
        {
            return File.GetAttributes(path);
        }

        public long GetLength(string path)
        {
            FileInfo fileInfo = new FileInfo(path);
            return fileInfo.Length;
        }

        public bool Exists(string path)
        {
            return File.Exists(path) || Directory.Exists(path);
        }

        public void Delete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
                else if (Directory.Exists(path))
                    DirectoryForceDelete(path);

            }
            catch (FileNotFoundException)
            {
            }
            catch(DirectoryNotFoundException)
            {                
            }
        }

        public void Commit(string pathTemp, string pathDestination, IPooledStream streamDest)
        {
            if (streamDest == null)
                Commit(pathTemp, pathDestination);
            else
            {
                _connectionPool.DisconnectWhile(streamDest, () => Commit(pathTemp, pathDestination));
            }
        }

        private static void Commit(string pathTemp, string pathDestination)
        {
            if (Directory.Exists(pathTemp))
            {
                try
                {
                    if (Directory.Exists(pathDestination))
                        DirectoryForceDelete(pathDestination);
                }
                catch (DirectoryNotFoundException)
                {
                }
                Helpers.TryTwice(() => Directory.Move(pathTemp, pathDestination));
            }
            else
            {
                try
                {
                    string backupFile = GetBackupFileName(pathDestination);
                    Helpers.TryTwice(() => File.Delete(backupFile));
                    // First try replacing the destination file, if it exists
                    File.Replace(pathTemp, pathDestination, backupFile, true);
                    try
                    {
                        // Try delete once more if it fails initially, but still swallow any failure to delete
                        Helpers.TryTwice(() => File.Delete(backupFile));
                    }
                    catch (IOException)
                    {
                    }
                }
                catch (FileNotFoundException)
                {
                    // Or just move, if it does not.
                    Helpers.TryTwice(() => File.Move(pathTemp, pathDestination));
                }
            }
        }

        private static string GetBackupFileName(string pathDestination)
        {
            string backupFile = FileSaver.TEMP_PREFIX + Path.GetFileName(pathDestination) + ".bak";
            string dirName = Path.GetDirectoryName(pathDestination);
            if (!string.IsNullOrEmpty(dirName))
                backupFile = Path.Combine(dirName, backupFile);
            // CONSIDER: Handle failure by trying a different name, or use a true temporary name?
            File.Delete(backupFile);
            return backupFile;
        }

        /// <summary>
        /// Recursive delete of a directory that removes the read-only flag
        /// from all files before attempting to remove them.  Necessary for
        /// deleting Analyst methods (*.m directories).
        /// </summary>
        private static void DirectoryForceDelete(string path)
        {
            foreach (var file in Directory.GetFiles(path))
            {
                var attr = File.GetAttributes(file);
                if ((attr & FileAttributes.ReadOnly) != 0)
                    File.SetAttributes(file,  attr & ~FileAttributes.ReadOnly);
                string fileLocal = file;
                Helpers.TryTwice(() => File.Delete(fileLocal));
            }
            foreach (var directory in Directory.GetDirectories(path))
                DirectoryForceDelete(directory);
            Helpers.TryTwice(() => Directory.Delete(path));
        }

        public void SetCache(string path, string pathCache)
        {
            // Cache is only valid, if it has the same time stamp as the cached file.
            File.SetLastWriteTime(pathCache, File.GetLastWriteTime(path));
        }

        public bool IsCached(string path, string pathCache)
        {
            return Exists(pathCache) &&
                Equals(File.GetLastWriteTime(pathCache), File.GetLastWriteTime(path));
        }

        public string GetTempFileName(string basePath, string prefix)
        {
            return GetTempFileName(basePath, prefix, 0);
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern uint GetTempFileName(string lpPathName, string lpPrefixString,
            uint uUnique, [Out] StringBuilder lpTempFileName);

        private static string GetTempFileName(string basePath, string prefix, uint unique)
        {
            // 260 is MAX_PATH in Win32 windows.h header
            // 'sb' needs >0 size else GetTempFileName throws IndexOutOfRangeException.  260 is the most you'd want.
            StringBuilder sb = new StringBuilder(260);

            uint result = GetTempFileName(basePath, prefix, unique, sb);
            if (result == 0)
            {
                throw new IOException("Win32 Error: " + Marshal.GetLastWin32Error());
            }

            return sb.ToString();
        }
    }

    [Serializable]
    public class FileModifiedException : IOException
    {
        public FileModifiedException()
        {
        }

        public FileModifiedException(string message) : base(message)
        {
        }

        public FileModifiedException(string message, Exception inner) : base(message, inner)
        {
        }

        protected FileModifiedException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }

    public static class FileEx
    {
        public static bool IsDirectory(string path)
        {
            return (File.GetAttributes(path) & FileAttributes.Directory) == FileAttributes.Directory;
        }

        public static bool IsFile(string path)
        {
            return !IsDirectory(path);
        }

        public static void DeleteIfPossible(string path)
        {
            try { File.Delete(path); }
            catch(IOException) {}
        }
    }

    public static class DirectoryEx
    {
        public static void DeleteIfPossible(string path)
        {
            try { Directory.Delete(path, true); }
            catch (IOException) { }
        }
    }

    public sealed class FileSaver : IDisposable
    {
        public const string TEMP_PREFIX = "~SK";

        private readonly IStreamManager _streamManager;

        /// <summary>
        /// Construct an instance of <see cref="FileSaver"/> to manage saving to a temporary
        /// file, and then renaming to the final destination.
        /// </summary>
        /// <param name="fileName">File path to the final destination</param>
        /// <throws>IOException</throws>
		public FileSaver(string fileName)
            : this(fileName, FileStreamManager.Default)
		{
		}

        /// <summary>
        /// Construct an instance of <see cref="FileSaver"/> to manage saving to a temporary
        /// file, and then renaming to the final destination.
        /// </summary>
        /// <param name="fileName">File path to the final destination</param>
        /// <param name="streamManager">A stream manager for either disk or memory access</param>
        /// <throws>IOException</throws>
        public FileSaver(string fileName, IStreamManager streamManager)
        {
            _streamManager = streamManager;

            RealName = fileName;

		    string dirName = Path.GetDirectoryName(fileName);
		    string tempName = _streamManager.GetTempFileName(dirName, TEMP_PREFIX);
            // If the directory name is returned, then starting path was bogus.
            if (!Equals(dirName, tempName))
                SafeName = tempName;
		}

        public bool CanSave(bool showMessage)
        {
            if (SafeName == null)
            {
                if (showMessage)
                    MessageBox.Show(string.Format("Cannot save to {0}.  Check the path to make sure the directory exists.", RealName), Program.Name);
                return false;
            }
            if (!_streamManager.Exists(RealName))
                return true;

            try
            {
                if ((_streamManager.GetAttributes(RealName) & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    if (showMessage)
                        MessageBox.Show(string.Format("Cannot save to {0}.  The file is read-only.", RealName), Program.Name);
                    return false;
                }
                return true;
            }
            catch (FileNotFoundException)
            {
                return true;
            }
        }

        public string SafeName { get; private set; }

        public string RealName { get; private set; }

        public bool Commit()
        {
            return Commit(null);
        }

        public bool Commit(IPooledStream streamDest)
        {
            // This is where the file that got written is renamed to the desired file.
	        // Dispose() will do any necessary temporary file clean-up.

	        if (string.IsNullOrEmpty(SafeName))
		        return false;

            _streamManager.Commit(SafeName, RealName, streamDest);

        	Dispose();

            return true;
        }

        public void Dispose()
        {
            // Get rid of the temporary file, if it still exists.

            if (!string.IsNullOrEmpty(SafeName))
            {
                if (_streamManager.Exists(SafeName))
                    _streamManager.Delete(SafeName);
                // Make sure any further calls to Dispose() do nothing.
                SafeName = null;
            }          
        }
    }

    public class TemporaryDirectory : IDisposable
    {
        public const string TEMP_PREFIX = "~SK";

        public TemporaryDirectory()
        {
            DirPath = Path.Combine(Path.GetTempPath(), TEMP_PREFIX + Path.GetRandomFileName());
            Directory.CreateDirectory(DirPath);
        }

        public string DirPath { get; private set; }

        public void Dispose()
        {
            Helpers.TryTwice(() => Directory.Delete(DirPath, true));
        }
    }
}
