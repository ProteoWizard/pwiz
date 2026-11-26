/*
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

using System.Runtime.CompilerServices;

// Model internals were visible to the Skyline UI and its tests while they shared one assembly.
// Attributes rather than <InternalsVisibleTo> items: GenerateAssemblyInfo is off for the version
// stamping, and that also turns the items off. Skyline is Skyline-daily in the daily channel.
[assembly: InternalsVisibleTo("Skyline-daily")]
[assembly: InternalsVisibleTo("Skyline")]
[assembly: InternalsVisibleTo("Test")]
[assembly: InternalsVisibleTo("TestFunctional")]
[assembly: InternalsVisibleTo("TestTutorial")]
