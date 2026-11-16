/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SkylineTool;

namespace SkylineMcpServer;

/// <summary>
/// Decides which MCP tools the targeted Skyline can run, from the methods each tool declares with
/// <see cref="RequiresJsonToolServiceMethodAttribute"/> and the <see cref="IJsonToolService"/> methods
/// in that Skyline's own SkylineTool.dll. When the methods cannot be read (no Skyline running, or its
/// files are inaccessible), every tool is offered and an unsupported call reports itself.
/// </summary>
public class ToolAvailability
{
    private const string SKYLINE_TOOL_DLL = "SkylineTool.dll";

    private readonly Dictionary<string, string[]> _requiredMethods;
    private readonly Dictionary<string, (DateTime LastWriteTime, HashSet<string> Methods)> _methodsByDllPath = new();
    private readonly object _lock = new();
    // The methods the client was last given a tool list for; null when that list was unfiltered
    private HashSet<string> _listedMethods;

    public ToolAvailability(Type toolType)
    {
        _requiredMethods = new Dictionary<string, string[]>();
        foreach (var method in toolType.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            var tool = method.GetCustomAttribute<McpServerToolAttribute>();
            if (tool?.Name == null)
                continue;
            var required = method.GetCustomAttributes<RequiresJsonToolServiceMethodAttribute>()
                .Select(a => a.MethodName).ToArray();
            if (required.Length > 0)
                _requiredMethods.Add(tool.Name, required);
        }
    }

    /// <summary>
    /// Removes the tools the targeted Skyline cannot run, and remembers what the client was told.
    /// </summary>
    public IList<Tool> FilterTools(IList<Tool> tools)
    {
        lock (_lock)
        {
            _listedMethods = GetTargetMethods();
            if (_listedMethods == null)
                return tools;
            return tools.Where(tool => IsSupported(tool.Name, _listedMethods)).ToList();
        }
    }

    /// <summary>
    /// True when the targeted Skyline differs in its methods from the one the client last listed tools
    /// for. Returns true once per change, so the caller notifies the client once.
    /// </summary>
    public bool CheckListChanged()
    {
        lock (_lock)
        {
            var methods = GetTargetMethods();
            bool unchanged = methods == null
                ? _listedMethods == null
                : _listedMethods != null && methods.SetEquals(_listedMethods);
            if (unchanged)
                return false;
            _listedMethods = methods;
            return true;
        }
    }

    private bool IsSupported(string toolName, HashSet<string> methods)
    {
        return !_requiredMethods.TryGetValue(toolName, out var required) || required.All(methods.Contains);
    }

    private HashSet<string> GetTargetMethods()
    {
        string dllPath = GetTargetSkylineToolPath();
        if (dllPath == null)
            return null;
        try
        {
            var lastWriteTime = File.GetLastWriteTimeUtc(dllPath);
            if (_methodsByDllPath.TryGetValue(dllPath, out var cached) && cached.LastWriteTime == lastWriteTime)
                return cached.Methods;
            var methods = ReadInterfaceMethods(dllPath);
            _methodsByDllPath[dllPath] = (lastWriteTime, methods);
            return methods;
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is BadImageFormatException)
        {
            return null;
        }
    }

    private static string GetTargetSkylineToolPath()
    {
        int? processId = SkylineConnection.GetTargetProcessId();
        if (processId == null)
            return null;
        try
        {
            using var process = Process.GetProcessById(processId.Value);
            string exePath = process.MainModule?.FileName;
            if (exePath == null)
                return null;
            string dllPath = Path.Combine(Path.GetDirectoryName(exePath)!, SKYLINE_TOOL_DLL);
            return File.Exists(dllPath) ? dllPath : null;
        }
        catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException ||
                                   ex is System.ComponentModel.Win32Exception)
        {
            // Exited, or (e.g. Skyline elevated and this server not) its modules cannot be read
            return null;
        }
    }

    /// <summary>
    /// Reads the IJsonToolService method names from the metadata of another Skyline's SkylineTool.dll
    /// without loading it, since this server has its own copy of the interface. Returns an empty set
    /// when the DLL predates the interface.
    /// </summary>
    private static HashSet<string> ReadInterfaceMethods(string dllPath)
    {
        using var stream = File.OpenRead(dllPath);
        using var peReader = new PEReader(stream);
        var reader = peReader.GetMetadataReader();
        foreach (var typeHandle in reader.TypeDefinitions)
        {
            var type = reader.GetTypeDefinition(typeHandle);
            if (reader.GetString(type.Name) != nameof(IJsonToolService) ||
                reader.GetString(type.Namespace) != typeof(IJsonToolService).Namespace)
            {
                continue;
            }
            return type.GetMethods()
                .Select(methodHandle => reader.GetString(reader.GetMethodDefinition(methodHandle).Name))
                .ToHashSet();
        }
        return new HashSet<string>();
    }
}
