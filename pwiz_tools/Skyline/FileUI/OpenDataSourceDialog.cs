/*
 * Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
 *
 * Copyright 2009 Vanderbilt University - Nashville, TN 37232
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
using pwiz.CommonMsData;
using pwiz.CommonMsData.RemoteApi;


namespace pwiz.Skyline.FileUI
{
    public class OpenDataSourceDialog : OpenFileDialogNE
    {
        private static string[] SOURCE_TYPES => new[]
        {
            FileUIResources.OpenDataSourceDialog_OpenDataSourceDialog_Any_spectra_format,   // Localized text can't be stored in a static variable
            DataSourceUtil.TYPE_WIFF,
            DataSourceUtil.TYPE_WIFF2,
            DataSourceUtil.TYPE_AGILENT,
            DataSourceUtil.TYPE_BRUKER,
            DataSourceUtil.TYPE_MBI,
            DataSourceUtil.TYPE_SHIMADZU,
            DataSourceUtil.TYPE_THERMO_RAW,
            DataSourceUtil.TYPE_WATERS_RAW,
            DataSourceUtil.TYPE_MZML,
            DataSourceUtil.TYPE_MZXML,
            DataSourceUtil.TYPE_MZ5,
            DataSourceUtil.TYPE_UIMF
        };

        /// <summary>
        /// File picker which is aware of mass spec "files" that are really directories
        /// </summary>
        /// <param name="remoteAccounts">For UNIFI</param>
        /// <param name="specificDataSourceFilter">Optional list of specific files the user needs to located, ignoring the rest</param>
        public OpenDataSourceDialog(IList<RemoteAccount> remoteAccounts, IList<string> specificDataSourceFilter = null)
            : base(SOURCE_TYPES, remoteAccounts, specificDataSourceFilter )
        {
        }

        public MsDataFileUri DataSource => FileName;
        public MsDataFileUri[] DataSources => FileNames;
    }
}
