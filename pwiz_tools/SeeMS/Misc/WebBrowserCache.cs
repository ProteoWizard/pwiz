using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace seems
{
	// For PInvoke: Contains information about an entry in the Internet cache
	[StructLayout( LayoutKind.Explicit, Size = 80 )]
	internal struct INTERNET_CACHE_ENTRY_INFOA
	{
		[FieldOffset( 0 )]
		public uint dwStructSize;
		[FieldOffset( 4 )]
		public IntPtr lpszSourceUrlName;
		[FieldOffset( 8 )]
		public IntPtr lpszLocalFileName;
		[FieldOffset( 12 )]
		public uint CacheEntryType;
		[FieldOffset( 16 )]
		public uint dwUseCount;
		[FieldOffset( 20 )]
		public uint dwHitRate;
		[FieldOffset( 24 )]
		public uint dwSizeLow;
		[FieldOffset( 28 )]
		public uint dwSizeHigh;
		[FieldOffset( 32 )]
		public System.Runtime.InteropServices.ComTypes.FILETIME LastModifiedTime;
		[FieldOffset( 40 )]
		public System.Runtime.InteropServices.ComTypes.FILETIME ExpireTime;
		[FieldOffset( 48 )]
		public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
		[FieldOffset( 56 )]
		public System.Runtime.InteropServices.ComTypes.FILETIME LastSyncTime;
		[FieldOffset( 64 )]
		public IntPtr lpHeaderInfo;
		[FieldOffset( 68 )]
		public uint dwHeaderInfoSize;
		[FieldOffset( 72 )]
		public IntPtr lpszFileExtension;
		[FieldOffset( 76 )]
		public uint dwReserved;
		[FieldOffset( 76 )]
		public uint dwExemptDelta;
	}

	public struct WebBrowserCacheEntry
	{
		internal WebBrowserCacheEntry( INTERNET_CACHE_ENTRY_INFOA entryInfo )
		{
			string sourceUrlStr = (string) Marshal.PtrToStringAnsi( entryInfo.lpszSourceUrlName );
			if( Uri.IsWellFormedUriString( sourceUrlStr, UriKind.Absolute ) )
				m_sourceUrl = new Uri( sourceUrlStr );
			else
				m_sourceUrl = new Uri( "http://localhost" );
			m_localFilepath = (string) Marshal.PtrToStringAnsi( entryInfo.lpszLocalFileName );
			m_useCount = (int) entryInfo.dwUseCount;
			m_hitRate = (int) entryInfo.dwHitRate;
			if( System.IO.File.Exists( m_localFilepath ) )
			{
				m_lastModifiedTime = System.IO.File.GetLastWriteTime( m_localFilepath );
				m_lastAccessTime = System.IO.File.GetLastAccessTime( m_localFilepath );
			} else
			{
				m_lastModifiedTime = new DateTime();
				m_lastAccessTime = new DateTime();
			}
			//m_lastSyncTime = ConvertFILETIME( entryInfo.LastSyncTime );
		}

		/*private static DateTime ConvertFILETIME( System.Runtime.InteropServices.ComTypes.FILETIME time )
		{
			long timeInNanoseconds = ( ( (long) time.dwHighDateTime ) << 32 ) + ( ( (long) time.dwLowDateTime ) >> 32 );
			DateTime convertedTime = new DateTime( timeInNanoseconds );
			convertedTime.AddYears( -1600 ); // FILETIME starts at 1601 CE, DateTime starts at 0001 CE
			return convertedTime;
		}*/

		public Uri SourceUrl { get { return m_sourceUrl; } }
		public string LocalFilepath { get { return m_localFilepath; } }
		public int UseCount { get { return m_useCount; } }
		public int HitRate { get { return m_hitRate; } }
		public DateTime LastModifiedTime { get { return m_lastModifiedTime; } }
		public DateTime LastAccessTime { get { return m_lastAccessTime; } }
		//public DateTime LastSyncTime { get { return m_lastSyncTime; } }

		Uri m_sourceUrl;
		string m_localFilepath;
		int m_useCount;
		int m_hitRate;
		DateTime m_lastModifiedTime;
		DateTime m_lastAccessTime;
		//DateTime m_lastSyncTime;
	}

	public class WebBrowserCache
	{
		// For PInvoke: Initiates the enumeration of the cache groups in the Internet cache
		[DllImport( @"wininet",
			SetLastError = true,
			CharSet = CharSet.Auto,
			EntryPoint = "FindFirstUrlCacheGroup",
			CallingConvention = CallingConvention.StdCall )]
		internal static extern IntPtr FindFirstUrlCacheGroup(
			int dwFlags,
			int dwFilter,
			IntPtr lpSearchCondition,
			int dwSearchCondition,
			ref long lpGroupId,
			IntPtr lpReserved );

		// For PInvoke: Retrieves the next cache group in a cache group enumeration
		[DllImport( @"wininet",
			SetLastError = true,
			CharSet = CharSet.Auto,
			EntryPoint = "FindNextUrlCacheGroup",
			CallingConvention = CallingConvention.StdCall )]
		internal static extern bool FindNextUrlCacheGroup(
			IntPtr hFind,
			ref long lpGroupId,
			IntPtr lpReserved );

		// For PInvoke: Releases the specified GROUPID and any associated state in the cache index file
		[DllImport( @"wininet",
			SetLastError = true,
			CharSet = CharSet.Auto,
			EntryPoint = "DeleteUrlCacheGroup",
			CallingConvention = CallingConvention.StdCall )]
		internal static extern bool DeleteUrlCacheGroup(
			long GroupId,
			int dwFlags,
			IntPtr lpReserved );

		// For PInvoke: Begins the enumeration of the Internet cache
		[DllImport( @"wininet",
			SetLastError = true,
			CharSet = CharSet.Auto,
			EntryPoint = "FindFirstUrlCacheEntryA",
			CallingConvention = CallingConvention.StdCall )]
		internal static extern IntPtr FindFirstUrlCacheEntry(
			[MarshalAs( UnmanagedType.LPTStr )] string lpszUrlSearchPattern,
			IntPtr lpFirstCacheEntryInfo,
			ref int lpdwFirstCacheEntryInfoBufferSize );

		// For PInvoke: Retrieves the next entry in the Internet cache
		[DllImport( @"wininet",
			SetLastError = true,
			CharSet = CharSet.Auto,
			EntryPoint = "FindNextUrlCacheEntryA",
			CallingConvention = CallingConvention.StdCall )]
		internal static extern bool FindNextUrlCacheEntry(
			IntPtr hFind,
			IntPtr lpNextCacheEntryInfo,
			ref int lpdwNextCacheEntryInfoBufferSize );

		// For PInvoke: Removes the file that is associated with the source name from the cache, if the file exists
		[DllImport( @"wininet",
			SetLastError = true,
			CharSet = CharSet.Auto,
			EntryPoint = "DeleteUrlCacheEntryA",
			CallingConvention = CallingConvention.StdCall )]
		internal static extern bool DeleteUrlCacheEntry(
			IntPtr lpszUrlName );

		public static WebBrowserCacheEntry[] FindCacheFilesForUrl( string url )
		{
			const int ERROR_NO_MORE_ITEMS = 259; // No more items have been found.

			// Local variables
			int cacheEntryInfoBufferSizeInitial = 0;
			int cacheEntryInfoBufferSize = 0;
			IntPtr cacheEntryInfoBuffer = IntPtr.Zero;
			INTERNET_CACHE_ENTRY_INFOA internetCacheEntry;
			IntPtr enumHandle = IntPtr.Zero;
			bool returnValue = false;
			System.Collections.Generic.List<WebBrowserCacheEntry> wbcEntries = new System.Collections.Generic.List<WebBrowserCacheEntry>();
			try
			{
				enumHandle = FindFirstUrlCacheEntry( null, IntPtr.Zero, ref cacheEntryInfoBufferSizeInitial );
				if( enumHandle != IntPtr.Zero && ERROR_NO_MORE_ITEMS == Marshal.GetLastWin32Error() )
					return wbcEntries.ToArray();

				cacheEntryInfoBufferSize = cacheEntryInfoBufferSizeInitial;
				cacheEntryInfoBuffer = Marshal.AllocHGlobal( cacheEntryInfoBufferSize );
				enumHandle = FindFirstUrlCacheEntry( null, cacheEntryInfoBuffer, ref cacheEntryInfoBufferSizeInitial );

				while( true )
				{
					internetCacheEntry = (INTERNET_CACHE_ENTRY_INFOA) Marshal.PtrToStructure( cacheEntryInfoBuffer, typeof( INTERNET_CACHE_ENTRY_INFOA ) );
					WebBrowserCacheEntry wbcEntry = new WebBrowserCacheEntry( internetCacheEntry );
					if( wbcEntry.SourceUrl == new Uri( url ) )
						wbcEntries.Add( wbcEntry );

					cacheEntryInfoBufferSizeInitial = cacheEntryInfoBufferSize;
					returnValue = FindNextUrlCacheEntry( enumHandle, cacheEntryInfoBuffer, ref cacheEntryInfoBufferSizeInitial );
					if( !returnValue && ERROR_NO_MORE_ITEMS == Marshal.GetLastWin32Error() )
					{
						Console.WriteLine( new System.ComponentModel.Win32Exception( Marshal.GetLastWin32Error() ).Message );
						break;
					}

					if( !returnValue && cacheEntryInfoBufferSizeInitial > cacheEntryInfoBufferSize )
					{
						cacheEntryInfoBufferSize = cacheEntryInfoBufferSizeInitial;
						cacheEntryInfoBuffer = Marshal.ReAllocHGlobal( cacheEntryInfoBuffer, (IntPtr) cacheEntryInfoBufferSize );
						returnValue = FindNextUrlCacheEntry( enumHandle, cacheEntryInfoBuffer, ref cacheEntryInfoBufferSizeInitial );
						if( !returnValue )
							Console.WriteLine( new System.ComponentModel.Win32Exception( Marshal.GetLastWin32Error() ).Message );
					}
				}
				Marshal.FreeHGlobal( cacheEntryInfoBuffer );
			} catch( Exception e )
			{
				throw new InvalidOperationException( "error reading cache", e );
			}
			return wbcEntries.ToArray();
		}
	}
}