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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SkylineMcpServer;

public static class Program
{
    /// <summary>
    /// When true, relaxes checks that assume the host process is Skyline
    /// (e.g. process name validation in <see cref="SkylineConnection"/>).
    /// Set via SKYLINE_MCP_TEST=1 environment variable or --test command-line arg.
    /// </summary>
    public static bool FunctionalTest { get; private set; }

    public static async System.Threading.Tasks.Task Main(string[] args)
    {
        FunctionalTest = Environment.GetEnvironmentVariable("SKYLINE_MCP_TEST") == "1" ||
                         Array.Exists(args, a => a == "--test");

        var builder = Host.CreateApplicationBuilder(args);
        builder.Logging.ClearProviders();
        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly();

        await builder.Build().RunAsync();
    }
}
