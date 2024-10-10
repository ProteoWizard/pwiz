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
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using NHibernate;
using pwiz.Common.Database.NHibernate;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util.Extensions;

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

        /// <summary>
        /// True when the pool contains open streams. Useful for debugging
        /// </summary>
        bool HasPooledStreams { get; }

        /// <summary>
        /// Returns a string enumeration of the open streams and their GlobalIndex values. Useful for debugging
        /// </summary>
        string ReportPooledStreams();
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
                using (stream.ReaderWriterLock.CancelAndGetWriteLock())
                {
                    stream.CloseStream();
                    act();
                }
            }
        }

        public bool HasPooledConnections
        {
            get
            {
                lock (this)
                {
                    return _connections.Count > 0;
                }
            }
        }

        public string ReportPooledConnections()
        {
            lock (this)
            {
                var sb = new StringBuilder();
                foreach (var connection in _connections)
                {
                    sb.AppendLine(string.Format(@"{0}. {1}", connection.Key, connection.Value));
                }
                return sb.ToString();
            }
        }

        public void DisposeAll()
        {
            lock (this)
            {
                foreach (var connection in _connections.Values)
                    connection.Dispose();
                _connections.Clear();
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
        private QueryLock _readerWriterLock = new QueryLock(CancellationToken.None);

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
            using (ReaderWriterLock.CancelAndGetWriteLock())
            {
                _connectionPool.Disconnect(this);
            }
        }

        public QueryLock ReaderWriterLock 
        {
            get
            {
                return _readerWriterLock;
            }
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
        /// An explanation of the modified state of this stream. Useful in
        /// debugging issues in testing.
        /// </summary>
        string ModifiedExplanation { get; }

        /// <summary>
        /// True if the stream is currently open.
        /// </summary>
        bool IsOpen { get; }

        /// <summary>
        /// Used to close the stream when it is not associated with the active
        /// document.
        /// </summary>
        void CloseStream();

        QueryLock ReaderWriterLock { get; }
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
                throw new FileModifiedException(string.Format(UtilResources.PooledFileStream_Connect_The_file__0__has_been_modified_since_it_was_first_opened, FilePath));
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
                try
                {
                    return !IsOpen && Math.Abs(FileTime.Ticks - File.GetLastWriteTime(FilePath).Ticks) > MILLISECOND_TICKS;
                }
                catch (UnauthorizedAccessException)
                {
                    // May have had access privileges changed, reporting IsModified better than throwing an unhandled exception
                    return true;
                }
                catch (IOException)
                {
                    // May have been removed, reporting IsModified better than throwing an unhandled exception
                    return true;
                }
            }
        }

        public string ModifiedExplanation
        {
            get
            {
                if (!IsModified)
                    return @"Unmodified";
                return FileEx.GetElapsedTimeExplanation(FileTime, File.GetLastWriteTime(FilePath));
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

        public string ModifiedExplanation
        {
            get
            {
                if (!IsModified)
                    return @"Unmodified";
                return FileEx.GetElapsedTimeExplanation(FileTime, File.GetLastWriteTime(FilePath));
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

        public bool HasPooledStreams
        {
            get { return _connectionPool.HasPooledConnections; }
        }

        public string ReportPooledStreams()
        {
            return _connectionPool.ReportPooledConnections();
        }

        public void CloseAllStreams()
        {
            _connectionPool.DisposeAll();
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
                    throw new IOException(string.Format(UtilResources.FileStreamManager_CreateStream_Unexpected_error_opening__0__, path), x);
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
                    throw new IOException(string.Format(UtilResources.FileStreamManager_CreateStream_Unexpected_error_opening__0__, path), x);
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
                    FileEx.SafeDelete(path);
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
                    FileEx.SafeDelete(backupFile, true);

                    // First try replacing the destination file, if it exists
                    if (File.Exists(pathDestination))
                    {
                        File.Replace(pathTemp, pathDestination, backupFile, true);
                        FileEx.SafeDelete(backupFile, true);
                        return;
                    }
                }
                catch (FileNotFoundException)
                {
                }

                // Or just move, if it does not.
                Helpers.TryTwice(() => File.Move(pathTemp, pathDestination));
            }
        }

        private static string GetBackupFileName(string pathDestination)
        {
            string backupFile = FileSaver.TEMP_PREFIX + Path.GetFileName(pathDestination) + @".bak";
            string dirName = Path.GetDirectoryName(pathDestination);
            if (!string.IsNullOrEmpty(dirName))
                backupFile = Path.Combine(dirName, backupFile);
            // CONSIDER: Handle failure by trying a different name, or use a true temporary name?
            FileEx.SafeDelete(backupFile);
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
                FileEx.SafeDelete(fileLocal);
            }
            foreach (var directory in Directory.GetDirectories(path))
                DirectoryForceDelete(directory);
            DirectoryEx.SafeDelete(path);
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

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern uint GetTempFileName(string lpPathName, string lpPrefixString,
            uint uUnique, [Out] StringBuilder lpTempFileName);

        private static string GetTempFileName(string basePath, string prefix, uint unique)
        {
            // 260 is MAX_PATH in Win32 windows.h header
            // 'sb' needs >0 size else GetTempFileName throws IndexOutOfRangeException.  260 is the most you'd want.
            StringBuilder sb = new StringBuilder(260);

            Directory.CreateDirectory(basePath);
            uint result = GetTempFileName(basePath, prefix, unique, sb);
            if (result == 0)
            {
                var lastWin32Error = Marshal.GetLastWin32Error();
                if (lastWin32Error == 5)
                {
                    throw new IOException(string.Format(UtilResources.FileStreamManager_GetTempFileName_Access_Denied__unable_to_create_a_file_in_the_folder___0____Adjust_the_folder_write_permissions_or_retry_the_operation_after_moving_or_copying_files_to_a_different_folder_, basePath));
                }
                else
                {
                    throw new IOException(TextUtil.LineSeparate(string.Format(UtilResources.FileStreamManager_GetTempFileName_Failed_attempting_to_create_a_temporary_file_in_the_folder__0__with_the_following_error_, basePath),
                        string.Format(UtilResources.FileStreamManager_GetTempFileName_Win32_Error__0__, lastWin32Error)));
                }
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

        public static bool AreIdenticalFiles(string pathA, string pathB)
        {
            var infoA = new FileInfo(pathA);
            var infoB = new FileInfo(pathB);
            if (infoA.Length != infoB.Length)
                return false;
            // Credit from here to https://stackoverflow.com/questions/968935/compare-binary-files-in-c-sharp
            using (var s1 = new FileStream(pathA, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var s2 = new FileStream(pathB, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var b1 = new BinaryReader(s1))
            using (var b2 = new BinaryReader(s2))
            {
                while (true)
                {
                    var data1 = b1.ReadBytes(64 * 1024);
                    var data2 = b2.ReadBytes(64 * 1024);
                    if (data1.Length != data2.Length)
                        return false;
                    if (data1.Length == 0)
                        return true;
                    if (!data1.SequenceEqual(data2))
                        return false;
                }
            }
        }

        public static void SafeDelete(string path, bool ignoreExceptions = false)
        {
            var hint = $@"File.Delete({path})";
            if (ignoreExceptions)
            {
                try
                {
                    if (path != null && File.Exists(path))
                        Helpers.TryTwice(() => File.Delete(path), hint);
                }
// ReSharper disable EmptyGeneralCatchClause
                catch (Exception)
// ReSharper restore EmptyGeneralCatchClause
                {
                }

                return;
            }

            try
            {
                Helpers.TryTwice(() => File.Delete(path), hint);
            }
            catch (ArgumentException e)
            {
                if (path == null || string.IsNullOrEmpty(path.Trim()))
                    throw new DeleteException(UtilResources.FileEx_SafeDelete_Path_is_empty, e);
                throw new DeleteException(string.Format(UtilResources.FileEx_SafeDelete_Path_contains_invalid_characters___0_, path), e);
            }
            catch (DirectoryNotFoundException e)
            {
                throw new DeleteException(string.Format(UtilResources.FileEx_SafeDelete_Directory_could_not_be_found___0_, path), e);
            }
            catch (NotSupportedException e)
            {
                throw new DeleteException(string.Format(UtilResources.FileEx_SafeDelete_File_path_is_invalid___0_, path), e);
            }
            catch (PathTooLongException e)
            {
                throw new DeleteException(string.Format(UtilResources.FileEx_SafeDelete_File_path_is_too_long___0_, path), e);
            }
            catch (IOException e)
            {
                throw new DeleteException(string.Format(UtilResources.FileEx_SafeDelete_Unable_to_delete_file_which_is_in_use___0_, path), e);
            }
            catch (UnauthorizedAccessException e)
            {
                var fileInfo = new FileInfo(path);
                if (fileInfo.IsReadOnly)
                    throw new DeleteException(string.Format(UtilResources.FileEx_SafeDelete_Unable_to_delete_read_only_file___0_, path), e);
                if (Directory.Exists(path))
                    throw new DeleteException(string.Format(UtilResources.FileEx_SafeDelete_Unable_to_delete_directory___0_, path), e);
                throw new DeleteException(string.Format(UtilResources.FileEx_SafeDelete_Insufficient_permission_to_delete_file___0_, path), e);
            }
        }

        public class DeleteException : IOException
        {
            public DeleteException(string message, Exception innerException)
                : base(message, innerException)
            {
            }
        }

        /// <summary>
        /// Appends a time stamp value to the given Skyline file name.
        /// </summary>
        /// <param name="fileName"></param>
        public static string GetTimeStampedFileName(string fileName)
        {
            string path;
            do
            {
                path = Path.Combine(Path.GetDirectoryName(fileName) ?? String.Empty,
                    Path.GetFileNameWithoutExtension(fileName) + @"_" +
                    DateTime.Now.ToString(@"yyyy-MM-dd_HH-mm-ss") +
                    SrmDocumentSharing.EXT_SKY_ZIP);
            }
            while (File.Exists(path));
            return path;
        }

        public static string GetElapsedTimeExplanation(DateTime startTime, DateTime endTime)
        {
            long deltaTicks = endTime.Ticks - startTime.Ticks;
            var elapsedSpan = new TimeSpan(deltaTicks);
            if (elapsedSpan.TotalMinutes > 0)
                return string.Format(@"{0} minutes, {1} seconds", elapsedSpan.TotalMinutes, elapsedSpan.Seconds);
            if (elapsedSpan.TotalSeconds > 0)
                return elapsedSpan.TotalSeconds + @" seconds";
            if (elapsedSpan.TotalMilliseconds > 0)
                return elapsedSpan.TotalMilliseconds + @" milliseconds";
            return deltaTicks + @" ticks";
        }

        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode)]
        static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

        /// <summary>
        /// Tries to create a hard-link from sourceFilepath to destinationFilepath and returns true if the link was successfully created.
        /// </summary>
        public static bool CreateHardLink(string sourceFilepath, string destinationFilepath)
        {
            return CreateHardLink(destinationFilepath, sourceFilepath, IntPtr.Zero);
        }

        /// <summary>
        /// Tries to create a hard-link from sourceFilepath to destinationFilepath and if that fails, it copies the file instead.
        /// </summary>
        public static void HardLinkOrCopyFile(string sourceFilepath, string destinationFilepath, bool overwrite = false)
        {
            Directory.CreateDirectory(PathEx.GetDirectoryName(destinationFilepath));
            if (!CreateHardLink(sourceFilepath, destinationFilepath))
                File.Copy(sourceFilepath, destinationFilepath, overwrite);
        }
    }

    public static class DirectoryEx
    {
        public static void SafeDelete(string path)
        {
            try
            {
                Helpers.TryTwice(() =>
                    {
                        if (path != null && Directory.Exists(path)) // Don't waste time trying to delete something that's already deleted
                        {
                            Directory.Delete(path, true);
                        }
                    }, $@"Directory.Delete({path})");
            }
// ReSharper disable EmptyGeneralCatchClause
            catch (Exception) { }
// ReSharper restore EmptyGeneralCatchClause
        }

        public static string GetUniqueName(string dirName)
        {
            return Directory.Exists(dirName)
                       ? Helpers.GetUniqueName(dirName, value => !Directory.Exists(value))
                       : dirName;
        }

        public static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);           

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    UtilResources.DirectoryEx_DirectoryCopy_Source_directory_does_not_exist_or_could_not_be_found__
                    + sourceDirName);
            }
            DirectoryInfo[] dirs = dir.GetDirectories();

            // If the destination directory doesn't exist, create it. 
            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string temppath = Path.Combine(destDirName, file.Name);
                file.CopyTo(temppath, false);
            }

            // If copying subdirectories, copy them and their contents to new location. 
            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string temppath = Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, temppath, true);
                }
            }
        }

        /// <summary>
        /// Checks to see if the path looks like it is a temporary folder that Windows extracts zip contents to
        /// when a user opens a file inside of the zip.
        /// Sets zipFileName to the name of the .zip file.
        /// </summary>
        public static bool IsTempZipFolder(string path, out string zipFileName)
        {
            zipFileName = null;
            int indexAppData = path.IndexOf(@"appdata\local\temp", StringComparison.OrdinalIgnoreCase);
            if (indexAppData < 0)
            {
                return false;
            }

            int indexZipExtension = path.IndexOf(@".zip\", indexAppData, StringComparison.OrdinalIgnoreCase);
            if (indexZipExtension < 0)
            {
                return false;
            }

            zipFileName = Path.GetFileName(path.Substring(0, indexZipExtension + 4));

            // Windows usually prepends "Temp1_" to the name of the folder, so strip that off if present
            if (zipFileName.StartsWith(@"Temp1_"))
            {
                zipFileName = zipFileName.Substring(6);
            }
            return true;
        }

        /// <summary>
        /// Returns true if a new file can be created in directoryPath.
        /// </summary>
        public static bool IsWritable(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
                throw new DirectoryNotFoundException(directoryPath);

            try
            {
                // generate random filenames until the path doesn't exist (technically has a race condition, but extremely unlikely)
                string randomFilepath;
                do
                {
                    randomFilepath = Path.Combine(directoryPath, Path.GetRandomFileName());
                } while (File.Exists(randomFilepath));

                // create the file with write permission
                using (new FileStream(randomFilepath, FileMode.Create, FileAccess.ReadWrite))
                {
                }

                // cleanup the file
                File.Delete(randomFilepath);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Utility class to update progress while reading a large file line by line.
    /// </summary>
    public sealed class LineReaderWithProgress : StreamReader
    {
        private readonly IProgressMonitor _progressMonitor;
        private IProgressStatus _status;
        private long _totalChars;
        private long _charsRead;

        public LineReaderWithProgress(string path, IProgressMonitor progressMonitor, IProgressStatus status = null) : base(path, Encoding.UTF8)
        {
            _progressMonitor = progressMonitor;
            _status = (status ?? new ProgressStatus()).ChangeMessage(Path.GetFileName(path));
            _totalChars = new FileInfo(PathEx.SafePath(path)).Length;
        }

        public override string ReadLine()
        {
            var result = base.ReadLine();
            if (result != null)
            {
                _charsRead += result.Length + 1; // This will be increasingly wrong if file has CRLF instead of just LF but should be good enough for a progress bar 
            }
            if (_progressMonitor != null)
            {
                if (_progressMonitor.IsCanceled)
                {
                    throw new OperationCanceledException();
                }
                _status = _status.UpdatePercentCompleteProgress(_progressMonitor, _charsRead, _totalChars);
            }
            return result;
        }

        protected override void Dispose(bool disposing)
        {
            // Make sure we reach 100%
            if (_progressMonitor != null)
                _status.UpdatePercentCompleteProgress(_progressMonitor, _totalChars, _totalChars);

            base.Dispose(disposing);
        }
    }



    /// <summary>
    /// Utility class to update progress while reading a Skyline document.
    /// </summary>
    public sealed class HashingStreamReaderWithProgress : StreamReader
    {
        private readonly IProgressMonitor _progressMonitor;
        private IProgressStatus _status;
        private long _totalChars;
        private long _charsRead;

        public HashingStreamReaderWithProgress(string path, IProgressMonitor progressMonitor)
            : base(HashingStream.CreateReadStream(path), Encoding.UTF8)
        {
            _progressMonitor = progressMonitor;
            _status = new ProgressStatus(Path.GetFileName(path));
            _totalChars = new FileInfo(PathEx.SafePath(path)).Length;
        }

        public HashingStream Stream
        {
            get { return (HashingStream) BaseStream; }
        }

        public override int Read(char[] buffer, int index, int count)
        {
            if (_progressMonitor.IsCanceled)
                throw new OperationCanceledException();
            var byteCount = base.Read(buffer, index, count);
            _charsRead += byteCount;
            _status = _status.UpdatePercentCompleteProgress(_progressMonitor, _charsRead, _totalChars);
            return byteCount;
        }
    }

    public sealed class StringListReader : TextReader
    {
        private readonly IList<string> _lines;
        private int _currentLine;

        public StringListReader(IList<string> lines)
        {
            _lines = lines;
        }

        public override string ReadLine()
        {
            if (_currentLine < _lines.Count)
                return _lines[_currentLine++];
            return null;
        }
    }


    public sealed class FileSaver : IDisposable
    {
        public const string TEMP_PREFIX = "~SK";

        private readonly IStreamManager _streamManager;
        private Stream _stream;

        /// <summary>
        /// Construct an instance of <see cref="FileSaver"/> to manage saving to a temporary
        /// file, and then renaming to the final destination.
        /// </summary>
        /// <param name="fileName">File path to the final destination</param>
        /// <param name="createStream">If true, create a Stream for the temporary file</param>
        /// <throws>IOException</throws>
        public FileSaver(string fileName, bool createStream = false)
            : this(fileName, FileStreamManager.Default, createStream)
        {
        }

        /// <summary>
        /// Construct an instance of <see cref="FileSaver"/> to manage saving to a temporary
        /// file, and then renaming to the final destination.
        /// </summary>
        /// <param name="fileName">File path to the final destination</param>
        /// <param name="streamManager">A stream manager for either disk or memory access</param>
        /// <param name="createStream">If true, create a Stream for the temporary file</param>
        /// <throws>IOException</throws>
        public FileSaver(string fileName, IStreamManager streamManager, bool createStream = false)
        {
            _streamManager = streamManager;

            RealName = fileName;

            string dirName = Path.GetDirectoryName(fileName);
            string tempName = _streamManager.GetTempFileName(dirName, TEMP_PREFIX);
            // If the directory name is returned, then starting path was bogus.
            if (!Equals(dirName, tempName))
                SafeName = tempName;
            if (createStream)
                CreateStream();
        }

        public void CreateStream()
        {
            if (_stream == null)
                _stream = new FileStream(SafeName, FileMode.Create, FileAccess.ReadWrite);
        }

        public Stream Stream
        {
            get { return _stream; }
            set { _stream = value; }
        }

        public FileStream FileStream
        {
            get { return _stream as FileStream; }
        }

        public bool CanSave(IWin32Window parent = null)
        {
            Exception ex;
            try
            {
                CheckException();
                return true;
            }
            catch (FileNotFoundException x) { ex = x; }
            catch (UnauthorizedAccessException x) { ex = x; }

            if (parent != null)
                MessageDlg.ShowException(parent, ex);
            return false;
        }

        public void CheckException()
        {
            if (SafeName == null)
            {
                throw new DirectoryNotFoundException(
                    string.Format(UtilResources.FileSaver_CanSave_Cannot_save_to__0__Check_the_path_to_make_sure_the_directory_exists, RealName));
            }

            if (_streamManager.Exists(RealName))
            {
                try
                {
                    if ((_streamManager.GetAttributes(RealName) & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                    {
                        throw new UnauthorizedAccessException(
                            string.Format(UtilResources.FileSaver_CanSave_Cannot_save_to__0__The_file_is_read_only, RealName));
                    }
                }
                catch (FileNotFoundException)
                {
                }
            }
        }

        public string SafeName { get; private set; }

        public string RealName { get; private set; }

        public void CopyFile(string sourceFile)
        {
            // Copy the specified file to the new name using a FileSaver
            CheckException();
            File.Copy(sourceFile, SafeName, true);
        }

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

            if (_stream != null)
            {
                _stream.Close();
                _stream = null;
            }

            _streamManager.Commit(SafeName, RealName, streamDest);

            // Also move any files with maching basenames (useful for debugging with extra output files
//            foreach (var baseMatchFile in Directory.EnumerateFiles(Path.GetDirectoryName(SafeName) ?? @".", Path.GetFileNameWithoutExtension(SafeName) + @".*"))
//            {
//                _streamManager.Commit(baseMatchFile, Path.ChangeExtension(RealName, baseMatchFile.Substring(SafeName.LastIndexOf('.'))), null);
//            }

            Dispose();

            return true;
        }

        public void Dispose()
        {
            if (_stream != null)
            {
                try
                {
                    _stream.Close();
                }
                catch (Exception e)
                {
                    Messages.WriteAsyncDebugMessage(@"Exception in FileSaver.Dispose: {0}", e);
                }
                _stream = null;
            }

            // Get rid of the temporary file, if it still exists.

            if (!string.IsNullOrEmpty(SafeName))
            {
                try
                {
                    if (_streamManager.Exists(SafeName))
                        _streamManager.Delete(SafeName);
                }
                catch (Exception e)
                {
                    Messages.WriteAsyncDebugMessage(@"Exception in FileSaver.Dispose: {0}", e);
                }
                // Make sure any further calls to Dispose() do nothing.
                SafeName = null;
            }          
        }
    }

    public class TemporaryDirectory : IDisposable
    {
        public const string TEMP_PREFIX = "~SK";

        public TemporaryDirectory(string dirPath = null, string tempPrefix = TEMP_PREFIX)
        {
            if (string.IsNullOrEmpty(dirPath))
                DirPath = Path.Combine(Path.GetTempPath(), tempPrefix + PathEx.GetRandomFileName()); // N.B. FileEx.GetRandomFileName adds unusual characters in test mode
            else
                DirPath = dirPath;
            Helpers.TryTwice(() => Directory.CreateDirectory(DirPath));
        }

        public string DirPath { get; private set; }

        public void Dispose()
        {
            DirectoryEx.SafeDelete(DirPath);
        }
    }

    public class TemporaryEnvironmentVariable : IDisposable
    {
        public TemporaryEnvironmentVariable(string name, string newValue)
        {
            Name = name;
            OldValue = Environment.GetEnvironmentVariable(name);
            NewValue = newValue;
            Environment.SetEnvironmentVariable(name, newValue);
        }

        public string Name { get; }
        public string OldValue { get; }
        public string NewValue { get; }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(Name, OldValue);
        }
    }

    public static class FastRead
    {
        /// <summary>
        /// Direct read of a byte array using p-invoke of Win32 ReadFile.  This seems
        /// to coexist with FileStream reading that the write version, but its use case
        /// is tightly limited.
        /// <para>
        /// Contributed by Randy Kern.  See:
        /// http://randy.teamkern.net/2009/02/reading-arrays-from-files-in-c-without-extra-copy.html
        /// </para>
        /// </summary>
        /// <param name="file">File handler returned from <see cref="FileStream.SafeFileHandle"/></param>
        /// <param name="bytes">Pointer to buffer for results</param>
        /// <param name="byteCount">How many bytes to read</param>
        public static unsafe void ReadBytes(SafeHandle file, byte* bytes, int byteCount)
        {
            uint bytesRead;
            bool ret = Kernel32.ReadFile(file, bytes, (uint)byteCount, &bytesRead, null);
            if (!ret || bytesRead != byteCount)
            {
                // If nothing was read, it may be possible to recover by
                // reading the slow way.
                if (bytesRead == 0)
                    throw new BulkReadException();
                throw new InvalidDataException();
            }
        }

        /// <summary>
        /// Read an array of floats from a file using p-invoke of Win32 ReadFile.
        /// This might seem like a good candidate for a generic template, but C# can't
        /// "fix" the address of a managed type.
        /// </summary>
        /// <param name="file">File handler returned from <see cref="FileStream.SafeFileHandle"/></param>
        /// <param name="data">Array of floats to be read</param>
        /// <param name="itemCount">How many floats to read</param>
        /// <param name="offset">Optional offset specifies where to put items in the array</param>
        public static unsafe void ReadFloats(SafeHandle file, float[] data, int itemCount, int offset = 0)
        {
            fixed (float* p = &data[offset])
            {
                ReadBytes(file, (byte*)p, itemCount * sizeof(float));
            }
        }

        /// <summary>
        /// Set file pointer position.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="position"></param>
        public static unsafe void SetFilePointer(SafeHandle file, long position)
        {
            Kernel32.SetFilePointerEx(file, position, null, 0);
        }
    }

    public static class FastWrite
    {
        /// <summary>
        /// Direct write of byte array using p-invoke of Win32 WriteFile.  This cannot
        /// be mixed with standard writes to a FileStream, or .NET throws an exception
        /// about the file location not being what it expected.
        /// </summary>
        /// <param name="file">File handler returned from <see cref="FileStream.SafeFileHandle"/></param>
        /// <param name="bytes">Pointer to buffer to be written</param>
        /// <param name="byteCount">How many bytes to write</param>
        public static unsafe void WriteBytes(SafeHandle file, byte* bytes, int byteCount)
        {
            uint bytesWritten;
            bool ret = Kernel32.WriteFile(file, bytes, (uint)byteCount, &bytesWritten, null);
            if (!ret || bytesWritten != byteCount)
                throw new IOException();
        }

        /// <summary>
        /// Write an array of floats to a file using p-invoke of Win32 WriteFile.
        /// This might seem like a good candidate for a generic template, but C# can't
        /// "fix" the address of a managed type.
        /// </summary>
        /// <param name="file">File handler returned from <see cref="FileStream.SafeFileHandle"/></param>
        /// <param name="data">Array of floats to be written</param>
        /// <param name="index">Index in data array to write from</param>
        /// <param name="itemCount">Number of elements to write</param>
        public static unsafe void WriteFloats(SafeHandle file, float[] data, int index, int itemCount)
        {
            fixed (float* p = &data[index])
            {
                WriteBytes(file, (byte*)p, itemCount * sizeof(float));
            }
        }

        /// <summary>
        /// Write an array of ints to a file using p-invoke of Win32 WriteFile.
        /// This might seem like a good candidate for a generic template, but C# can't
        /// "fix" the address of a managed type.
        /// </summary>
        /// <param name="file">File handler returned from <see cref="FileStream.SafeFileHandle"/></param>
        /// <param name="data">Array of ints to be written</param>
        /// <param name="index">Index in data array to write from</param>
        /// <param name="itemCount">Number of elements to write</param>
        public static unsafe void WriteInts(SafeHandle file, int[] data, int index, int itemCount)
        {
            fixed (int* p = &data[index])
            {
                WriteBytes(file, (byte*)p, itemCount * sizeof(int));
            }
        }

        /// <summary>
        /// Write an array of shorts to a file using p-invoke of Win32 WriteFile.
        /// This might seem like a good candidate for a generic template, but C# can't
        /// "fix" the address of a managed type.
        /// </summary>
        /// <param name="file">File handler returned from <see cref="FileStream.SafeFileHandle"/></param>
        /// <param name="data">Array of shorts to be written</param>
        /// <param name="index">Index in data array to write from</param>
        /// <param name="itemCount">Number of elements to write</param>
        public static unsafe void WriteShorts(SafeHandle file, short[] data, int index, int itemCount)
        {
            fixed (short* p = &data[index])
            {
                WriteBytes(file, (byte*)p, itemCount * sizeof(short));
            }
        }
    }

    public class NamedPipeServerConnector
    {
        private static readonly object SERVER_CONNECTION_LOCK = new object();
        private bool _connected;
        
        public bool WaitForConnection(NamedPipeServerStream serverStream, string outPipeName)
        {
            Thread connector = new Thread(() =>
            {
                serverStream.WaitForConnection();

                lock (SERVER_CONNECTION_LOCK)
                {
                    _connected = true;
                    Monitor.Pulse(SERVER_CONNECTION_LOCK);
                }
            });

            connector.Start();

            bool connected;
            lock (SERVER_CONNECTION_LOCK)
            {
                Monitor.Wait(SERVER_CONNECTION_LOCK, 5 * 1000);
                connected = _connected;
            }

            if (!connected)
            {
                // Clear the waiting thread.
                try
                {
                    using (var pipeFake = new NamedPipeClientStream(@"SkylineOutputPipe"))
                    {
                        pipeFake.Connect(10);
                    }
                    return false;
                }
                catch (Exception)
                {
                    return false;
                }
            }
            return true;
        }
    }

    public static class SkylineProcessRunner
    {
        /// <summary>
        /// Runs the SkylineProcessRunner executable file with the given arguments. These arguments
        /// are passed to CMD.exe within the NamedPipeProcessRunner
        /// </summary>
        /// <param name="arguments">The arguments to run at the command line</param>
        /// <param name="runAsAdministrator">If true, this process will be run as administrator, which
        /// allows for the CMD.exe process to be ran with elevated privileges</param>
        /// <param name="writer">The textwriter to which the command lines output will be written to</param>
        /// <returns>The exitcode of the CMD process ran with the specified arguments</returns>
        public static int RunProcess(string arguments, bool runAsAdministrator, TextWriter writer)
        {
            // create GUID
            string guidSuffix = string.Format(@"-{0}", Guid.NewGuid());
            var startInfo = new ProcessStartInfo
                {
                    FileName = GetSkylineProcessRunnerExePath(),
                    Arguments = guidSuffix + @" " + arguments,
                };
                
            if (runAsAdministrator)
                startInfo.Verb = @"runas";

            var process = new Process {StartInfo = startInfo, EnableRaisingEvents = true};

            string pipeName = @"SkylineProcessRunnerPipe" + guidSuffix;

            using (var pipeStream = new NamedPipeServerStream(pipeName))
            {
                bool processFinished = false;
                process.Exited += (sender, args) => processFinished = true;
                try
                {
                    process.Start();
                }
                catch (System.ComponentModel.Win32Exception win32Exception)
                {
                    const int ERROR_CANCELLED = 1223;
                    // If the user cancelled running as an administrator, then try again
                    // not as administrator
                    if (runAsAdministrator && win32Exception.NativeErrorCode == ERROR_CANCELLED)
                    {
                        return RunProcess(arguments, false, writer);
                    }
                    throw;
                }

                var namedPipeServerConnector = new NamedPipeServerConnector();
                if (namedPipeServerConnector.WaitForConnection(pipeStream, pipeName))
                {
                    using (var reader = new StreamReader(pipeStream))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            writer.WriteLine(line);
                        }
                    }

                    while (!processFinished)
                    {
                        // wait for process to finish
                    }

                    return process.ExitCode;
                }
                else
                {
                    throw new IOException(@"Error running process"); // CONSIDER: localize? Does user see this?
                }
            }
        }

        private static string GetSkylineProcessRunnerExePath()
        {
            string skylineFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return Path.Combine(skylineFolder ?? string.Empty, @"SkylineProcessRunner.exe");
        }
    }
    
    internal static class Kernel32
    {
        [DllImport("kernel32", SetLastError = true)]
        internal static extern unsafe bool ReadFile(
            SafeHandle hFile,
            byte* lpBuffer,
            UInt32 numberOfBytesToRead,
            UInt32* lpNumberOfBytesRead,
            NativeOverlapped* lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern unsafe bool WriteFile(
            SafeHandle handle,
            byte* lpBuffer,
            UInt32 numBytesToWrite,
            UInt32* numBytesWritten,
            NativeOverlapped* lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern unsafe bool SetFilePointerEx(
            SafeHandle handle,
            Int64 liDistanceToMove,
            Int64* lpNewFilePointer,
            UInt32 dwMoveMethod);
    }
}
