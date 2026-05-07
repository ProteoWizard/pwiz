/*
 * Original author: Brian Pratt <bspratt .at. proteinms dot net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;
using pwiz.Common.SystemUtil;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]

    // Verify that background-scanning services on this machine are not monitoring the
    // test directories. Such services slow tests, lock files, and can produce intermittent
    // hangs (e.g. SQLite BUSY on .blib loads, never-completing WaitForComplete polls).
    //
    // Checks performed for each test directory:
    //   - Antivirus (behavioral): write the standard EICAR test string and verify it
    //     survives. See https://www.eicar.org/download-anti-malware-testfile/
    //   - Cloud-storage placeholders: directory has none of Offline / ReparsePoint /
    //     RecallOnOpen / RecallOnDataAccess attributes (catches OneDrive Files-on-Demand,
    //     Dropbox Smart Sync, Google Drive File Stream, Box Drive, etc.).
    //   - OneDrive sync root: directory is not under any account's UserFolder per HKCU.
    //   - Windows Search index: directory is not in the SystemIndex crawl scope.
    //
    // Directories checked: the test runner working directory (where Skyline.exe lives at
    // test time) plus the test data download subfolders (Tutorials, Perftests) if they
    // exist. We deliberately do not check the parent of those subfolders because, when
    // SKYLINE_DOWNLOAD_PATH is unset, that resolves to the user's standard Downloads
    // folder, which is reasonable for them to leave un-excluded.
    //
    // On a system that's not configured properly this may set off antivirus alarms,
    // but that's just as well.

    public class AntivirusExclusionTest : AbstractUnitTest
    {
        [TestMethod]
        public void AaantivirusTestExclusion() // Intentional misspelling to encourage this as first test in nightlies
        {
            foreach (var directory in GetDirectoriesToCheck())
            {
                CheckDirectory(directory);
                CheckCloudPlaceholder(directory);
                CheckOneDriveSyncRoot(directory);
                CheckWindowsSearchIndex(directory);
            }
        }

        private static IEnumerable<string> GetDirectoriesToCheck()
        {
            yield return Path.GetFullPath(@".");

            var downloadsRoot = PathEx.GetDownloadsPath();
            foreach (var subFolderName in new[] { @"Tutorials", @"Perftests" })
            {
                var subDir = Path.Combine(downloadsRoot, subFolderName);
                if (Directory.Exists(subDir))
                    yield return subDir;
            }
        }

        private static void CheckDirectory(string directory)
        {
            var eicarTestString = @"X5O!P%@AP[4\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*"; // See https://www.eicar.org/download-anti-malware-testfile/
            if (!Directory.Exists(directory))
            {
                return; // First run of tutorials, presumably
            }

            var eicarTestFilename = $@"eicar_fake_threat_{LocalizationHelper.CurrentCulture.ThreeLetterISOLanguageName}.com";
            var eicarTestFile = Path.Combine(directory, eicarTestFilename);
            if (!File.Exists(eicarTestFile))
            {
                File.WriteAllText(eicarTestFile, eicarTestString); // If we are being watched, this should get removed immediately
            }
            string test = string.Empty;
            try
            {
                test = File.ReadAllText(eicarTestFile); // This should succeed - if not we are probably under antivirus scrutiny, which can mess with other tests
            }
            catch
            {
                 // Do nothing
            }
            if (!eicarTestString.Equals(test))
            {
                Assert.Fail($"Could not read contents of the (completely harmless!) antivirus test file \"{eicarTestFile}\", probably because it was quarantined by antivirus software.  If your antivirus flagged on \"{eicarTestFilename}\", don't panic - that's part of the test (see https://www.eicar.org/download-anti-malware-testfile/).  Now go exclude that directory from further antivirus scrutiny, as it causes file locking problems in the tests.");
            }
            if (File.Exists(eicarTestFile))  // Don't leave this lying around - it can cause problems with automated backups etc
            {
                TryHelper.TryTwice(() =>
                {
                    File.WriteAllText(eicarTestFile, string.Empty); // So antivirus doesn't flag on recycle bin
                    File.Delete(eicarTestFile);
                });
            }
        }

        // FILE_ATTRIBUTE_RECALL_ON_OPEN and FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS are used
        // by Windows Files-on-Demand cloud sync but are not in the .NET FileAttributes enum.
        private const int FILE_ATTRIBUTE_RECALL_ON_OPEN = 0x00040000;
        private const int FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS = 0x00400000;
        private const int CLOUD_PLACEHOLDER_MASK =
            (int)FileAttributes.Offline | (int)FileAttributes.ReparsePoint |
            FILE_ATTRIBUTE_RECALL_ON_OPEN | FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS;

        private static void CheckCloudPlaceholder(string directory)
        {
            if (!Directory.Exists(directory))
                return;
            var attrs = (int)File.GetAttributes(directory);
            if ((attrs & CLOUD_PLACEHOLDER_MASK) != 0)
            {
                Assert.Fail($"Directory \"{directory}\" has cloud-placeholder file attributes set (0x{attrs:X8}).  " +
                    @"Cloud sync (OneDrive, Dropbox, Google Drive, Box, etc.) can stall, lock, or rehydrate " +
                    @"files during tests, producing intermittent hangs.  Move the directory out of any cloud-synced location.");
            }
        }

        private static void CheckOneDriveSyncRoot(string directory)
        {
            if (!Directory.Exists(directory))
                return;
            using (var accountsKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\OneDrive\Accounts"))
            {
                if (accountsKey == null)
                    return; // OneDrive not installed for this user
                foreach (var accountName in accountsKey.GetSubKeyNames())
                {
                    using (var accountKey = accountsKey.OpenSubKey(accountName))
                    {
                        if (!(accountKey?.GetValue(@"UserFolder") is string userFolder) || string.IsNullOrEmpty(userFolder))
                            continue;
                        if (IsPathUnder(directory, userFolder))
                        {
                            Assert.Fail($"Directory \"{directory}\" is inside OneDrive sync root \"{userFolder}\" (account \"{accountName}\").  " +
                                @"OneDrive can stall, lock, or rehydrate files during tests.  Move the directory outside any OneDrive sync folder.");
                        }
                    }
                }
            }
        }

        private static bool IsPathUnder(string child, string parent)
        {
            var c = Path.GetFullPath(child).TrimEnd('\\', '/');
            var p = Path.GetFullPath(parent).TrimEnd('\\', '/');
            if (c.Length < p.Length)
                return false;
            if (!c.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                return false;
            // Match must be on a directory boundary so "C:\OneDriveTest" isn't considered under "C:\OneDrive".
            return c.Length == p.Length || c[p.Length] == '\\' || c[p.Length] == '/';
        }

        private static readonly Guid CLSID_CSearchManager = new Guid(@"7D096C5F-AC08-4F1F-BEB7-5C22C517CE39");

        private static void CheckWindowsSearchIndex(string directory)
        {
            if (!Directory.Exists(directory))
                return;
            try
            {
                var managerType = Type.GetTypeFromCLSID(CLSID_CSearchManager);
                if (managerType == null)
                    return; // Search service component not registered on this machine
                var mgr = (ISearchManager)Activator.CreateInstance(managerType);
                if (mgr == null)
                    return;
                if (mgr.GetCatalog(@"SystemIndex", out var catalog) != 0 || catalog == null)
                    return;
                if (catalog.GetCrawlScopeManager(out var csm) != 0 || csm == null)
                    return;
                if (csm.IncludedInCrawlScope(Path.GetFullPath(directory), out var included) != 0)
                    return;
                if (included)
                {
                    Assert.Fail($"Directory \"{directory}\" is in the Windows Search index crawl scope.  " +
                        @"Indexing can lock files (e.g. SQLite BUSY on .blib loads) and produce intermittent test hangs.  " +
                        @"Open Indexing Options in Control Panel and remove this directory from the indexed locations.");
                }
            }
            catch (COMException)
            {
                // Search service unavailable / RPC failure - best-effort, don't fail the test on probe failure
            }
        }

        // Windows Search COM interop. GUIDs and method declaration order verified against the
        // Windows SDK IDLs (SearchAdmin.idl / SearchCatalog.idl / SearchCrawlScopeManager.idl).
        // Methods we don't call are declared as void slot-fillers so the runtime maps the
        // method we do call to its correct vtable slot.

        [ComImport, Guid(@"AB310581-AC80-11D1-8DF3-00C04FB6EF69"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ISearchManager
        {
            void GetIndexerVersionStr();
            void GetIndexerVersion();
            void GetParameter();
            void SetParameter();
            void get_ProxyName();
            void get_BypassList();
            void SetProxy();
            [PreserveSig] int GetCatalog([MarshalAs(UnmanagedType.LPWStr)] string pszCatalog,
                [MarshalAs(UnmanagedType.Interface)] out ISearchCatalogManager ppCatalogManager);
            // Trailing methods (UserAgent get/put, UseProxy, LocalBypass, PortNumber) omitted - not called.
        }

        [ComImport, Guid(@"AB310581-AC80-11D1-8DF3-00C04FB6EF50"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ISearchCatalogManager
        {
            void get_Name();
            void GetParameter();
            void SetParameter();
            void GetCatalogStatus();
            void Reset();
            void Reindex();
            void ReindexMatchingURLs();
            void ReindexSearchRoot();
            void put_ConnectTimeout();
            void get_ConnectTimeout();
            void put_DataTimeout();
            void get_DataTimeout();
            void NumberOfItems();
            void NumberOfItemsToIndex();
            void URLBeingIndexed();
            void GetURLIndexingState();
            void GetPersistentItemsChangedSink();
            void RegisterViewForNotification();
            void GetItemsChangedSink();
            void UnregisterViewForNotification();
            void SetExtensionClusion();
            void EnumerateExcludedExtensions();
            void GetQueryHelper();
            void put_DiacriticSensitivity();
            void get_DiacriticSensitivity();
            [PreserveSig] int GetCrawlScopeManager(
                [MarshalAs(UnmanagedType.Interface)] out ISearchCrawlScopeManager ppCrawlScopeManager);
        }

        [ComImport, Guid(@"AB310581-AC80-11D1-8DF3-00C04FB6EF55"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ISearchCrawlScopeManager
        {
            void AddDefaultScopeRule();
            void AddRoot();
            void RemoveRoot();
            void EnumerateRoots();
            void AddHierarchicalScope();
            void AddUserScopeRule();
            void RemoveScopeRule();
            void EnumerateScopeRules();
            void HasParentScopeRule();
            void HasChildScopeRule();
            [PreserveSig] int IncludedInCrawlScope(
                [MarshalAs(UnmanagedType.LPWStr)] string pszURL,
                [MarshalAs(UnmanagedType.Bool)] out bool pfIsIncluded);
            // Trailing methods (IncludedInCrawlScopeEx, RevertToDefaultScopes, SaveAll,
            // GetParentScopeVersionId, RemoveDefaultScopeRule) omitted - not called.
        }
    }
}
