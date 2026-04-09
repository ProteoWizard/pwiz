/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.6) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.IO;

namespace SkylineTool
{
    /// <summary>
    /// Attribute providing a stable, culture-invariant, user-friendly name for settings
    /// list classes. Used by JsonToolServer so clients can refer to settings lists
    /// by recognizable names (e.g. "Isotope Modifications") instead of internal class
    /// names (e.g. "HeavyModList").
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class LlmNameAttribute : Attribute
    {
        public string Name { get; }
        public LlmNameAttribute(string name) { Name = name; }
    }

    /// <summary>
    /// Constants for the JSON tool service protocol, connection file infrastructure,
    /// and API values. Used internally by <see cref="SkylineJsonToolClient"/> and
    /// JsonToolServer -- tool developers generally do not need these directly.
    /// </summary>
    public static class JsonToolConstants
    {
        // ReSharper disable InconsistentNaming

        /// <summary>Connection file and MCP status property names (use nameof() as keys).</summary>
        public enum JSON
        {
            pipe_name, process_id, connected_at, skyline_version,     // connection file
            status, auto_connect, version,                            // MCP connector status
        }

        /// <summary>JSON-RPC 2.0 protocol field names (use nameof() as keys).</summary>
        public enum JSON_RPC
        {
            jsonrpc, method, @params, id, result, error, code, message, _log,
        }

        // ReSharper restore InconsistentNaming

        // --- JSON-RPC 2.0 protocol constants ---

        public const string JSONRPC_VERSION = @"2.0";
        public const int ERROR_PARSE = -32700;
        public const int ERROR_INVALID_REQUEST = -32600;
        public const int ERROR_METHOD_NOT_FOUND = -32601;
        public const int ERROR_INVALID_PARAMS = -32602;
        public const int ERROR_INTERNAL = -32603;

        // --- API value constants ---

        public const string LEVEL_GROUP = @"group";
        public const string LEVEL_MOLECULE = @"molecule";
        public const string LEVEL_PRECURSOR = @"precursor";
        public const string LEVEL_TRANSITION = @"transition";

        public const string CULTURE_INVARIANT = @"invariant";
        public const string CULTURE_LOCALIZED = @"localized";

        public const string SORT_ASC = @"asc";
        public const string SORT_DESC = @"desc";

        public const string DEFAULT_REPORT_NAME = @"Custom";

        // --- Connection file infrastructure ---

        public const string DEPLOY_FOLDER_NAME = @".skyline-mcp";
        public const string CONNECTION_FILE_PREFIX = @"connection-";
        public const string CONNECTION_FILE_EXT = @".json";
        public const string JSON_PIPE_PREFIX = @"SkylineMcpJson-";

        public static string GetJsonPipeName(string legacyToolServiceName)
        {
            return JSON_PIPE_PREFIX + legacyToolServiceName.Replace(@"-", string.Empty);
        }

        public static string GetConnectionDirectory()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                DEPLOY_FOLDER_NAME);
        }

        public static string GetConnectionFilePath(string pipeName)
        {
            return Path.Combine(GetConnectionDirectory(),
                CONNECTION_FILE_PREFIX + pipeName + CONNECTION_FILE_EXT);
        }
    }
}
