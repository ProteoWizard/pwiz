/*
 * Original author: Ali Marsh <alimarsh .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Collections.Immutable;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using SharedBatch;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    [XmlRoot("refine_settings")]
    public class RefineSettings
    {

        // IMMUTABLE - all fields are readonly literals or in an immutable list
        // Holds information for refining the skyline file after data import
        
        public RefineSettings(List<Tuple<RefineVariable, string>> commandValues, bool removeDecoys, bool removeResults,
            string outputFilePath) : this(ImmutableList.Create<Tuple<RefineVariable, string>>().AddRange(commandValues), 
            removeDecoys, removeResults, outputFilePath)
        {
        }

        public RefineSettings(ImmutableList<Tuple<RefineVariable, string>> commandValues, bool removeDecoys, bool removeResults,
            string outputFilePath)
        {
            RemoveDecoys = removeDecoys;
            RemoveResults = removeResults;
            OutputFilePath = outputFilePath ?? string.Empty;
            CommandValues = commandValues;
        }

        public readonly ImmutableList<Tuple<RefineVariable, string>> CommandValues;

        public readonly bool RemoveDecoys;

        public readonly bool RemoveResults;

        public readonly string OutputFilePath;

        public bool WillRefine()
        {
            return !string.IsNullOrEmpty(OutputFilePath);
        }

        public void Validate()
        {
            if (!string.IsNullOrEmpty(OutputFilePath) && !RemoveResults && !RemoveDecoys && CommandValues.IsEmpty)
            {
                throw new ArgumentException(Resources.RefineSettings_Validate_No_refine_commands_have_been_selected_ + Environment.NewLine +
                                            Resources.RefineSettings_Validate_Please_enter_values_for_the_refine_commands_you_wish_to_use__or_skip_the_refinement_step_by_removing_the_file_path_on_the_refine_tab_);

            }
            ValidateOutputFile(OutputFilePath);
        }

        public static void ValidateOutputFile(string outputFilePath)
        {
            if (!string.IsNullOrEmpty(outputFilePath))
            {
                bool validPath;
                try
                {
                    validPath = Directory.Exists(Path.GetDirectoryName(outputFilePath));
                }
                catch (Exception)
                {
                    validPath = false;
                }
                if (!validPath) throw new ArgumentException(string.Format(Resources.RefineSettings_Validate_Cannot_save_the_refined_file_to__0_, outputFilePath) + Environment.NewLine +
                                                            Resources.RefineSettings_ValidateOutputFile_Please_provide_a_valid_output_file_path_);
            }
        }

        public bool RunWillOverwrite(int startStep, string configHeader, out StringBuilder message)
        {
            var tab = "      ";
            message = new StringBuilder(configHeader);
            if (startStep != 3)
                return false;
            if (File.Exists(OutputFilePath))
            {
                message.Append(tab + tab)
                    .Append(OutputFilePath)
                    .AppendLine();
                return true;
            }
            return false;
        }

        public bool TryPathReplace(string oldRoot, string newRoot, out RefineSettings pathReplacedRefineSettings)
        {
            var didReplace = TextUtil.SuccessfulReplace(ValidateOutputFile, oldRoot, newRoot, OutputFilePath, out string replacedOutputPath);
            pathReplacedRefineSettings =
                new RefineSettings(CommandValues, RemoveDecoys, RemoveResults, replacedOutputPath);
            return didReplace;
        }

        
        #region Read/Write XML

        private enum Attr
        {
            RemoveDecoys,
            RemoveResults,
            OutputFilePath
        };

        public static RefineSettings ReadXml(XmlReader reader)
        {
            if (!reader.Name.Equals("refine_settings"))
            {
                // This is an old configuration with no refine settings
                return new RefineSettings(ImmutableList<Tuple<RefineVariable, string>>.Empty, false, false,
                    string.Empty);
            }
            var removeDecoys = reader.GetBoolAttribute(Attr.RemoveDecoys);
            var removeResults = reader.GetBoolAttribute(Attr.RemoveResults);
            var outputFilePath = TextUtil.GetTestPath(Program.FunctionalTest, Program.TestDirectory, reader.GetAttribute(Attr.OutputFilePath));
            var commandList = new List<Tuple<RefineVariable, string>>();
            while (reader.IsStartElement() && !reader.IsEmptyElement)
            {
                if (reader.Name == "command_value")
                {
                    var tupleItems = reader.ReadElementContentAsString().Split(new[] { '(', ',', ')' }, StringSplitOptions.RemoveEmptyEntries);
                    var variable = (RefineVariable)Enum.Parse(typeof(RefineVariable), tupleItems[0].Trim());
                    var value = tupleItems[1].Trim();
                    commandList.Add(new Tuple<RefineVariable, string>(variable, value));
                }
                else
                {
                    reader.Read();
                }
            }
            return new RefineSettings(commandList, removeDecoys, removeResults, outputFilePath);
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("refine_settings");
            writer.WriteAttribute(Attr.RemoveDecoys, RemoveDecoys);
            writer.WriteAttribute(Attr.RemoveResults, RemoveResults);
            writer.WriteAttributeIfString(Attr.OutputFilePath, OutputFilePath);
            foreach (var commandValue in CommandValues)
            {
                writer.WriteElementString("command_value", commandValue);
            }
            writer.WriteEndElement();
        }
        #endregion

        #region Batch Commands

        public const string REMOVE_DECOYS_COMMAND = "--decoys-discard";
        public const string REMOVE_RESULTS_COMMAND = "--remove-all";

        public void WriteRefineCommands(CommandWriter commandWriter)
        {
            if (WillRefine())
            {
                foreach (var commandValue in CommandValues)
                {
                    var variableName = Enum.GetName(typeof(RefineVariable), commandValue.Item1);
                    var command = "-" + (RefineInputObject.REFINE_RESOURCE_KEY_PREFIX + variableName).Replace('_', '-');
                    if (!string.IsNullOrEmpty(commandValue.Item2))
                        commandWriter.Write("{0}={1}", command, commandValue.Item2);
                    else
                        commandWriter.Write(command);
                }

                if (RemoveResults || RemoveDecoys)
                {
                    if (commandWriter.MultiLine)
                        commandWriter.NewLine();
                    else
                    {
                        commandWriter.Write(SkylineBatchConfig.SAVE_AS_NEW_FILE_COMMAND, OutputFilePath);
                        commandWriter.EndCommandGroup();
                    }
                }

                if (RemoveDecoys) commandWriter.Write(REMOVE_DECOYS_COMMAND);
                if (RemoveResults) commandWriter.Write(REMOVE_RESULTS_COMMAND);
                commandWriter.Write(SkylineBatchConfig.SAVE_AS_NEW_FILE_COMMAND, OutputFilePath);
                commandWriter.EndCommandGroup();
            }
        }

        public void WriteOpenRefineFileCommand(CommandWriter commandWriter)
        {
            commandWriter.Write(SkylineBatchConfig.OPEN_SKYLINE_FILE_COMMAND, OutputFilePath);
        }

        #endregion

        protected bool Equals(RefineSettings other)
        {
            if (CommandValues.Count != other.CommandValues.Count) return false;
            for (int i = 0; i < CommandValues.Count; i++)
            {
                if (!CommandValues[i].Item1.Equals(other.CommandValues[i].Item1) || !CommandValues[i].Item2.Equals(other.CommandValues[i].Item2))
                    return false;
            }
            return other.RemoveResults == RemoveResults &&
                    other.RemoveDecoys == RemoveDecoys &&
                    other.OutputFilePath.Equals(OutputFilePath);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((RefineSettings)obj);
        }

        public override int GetHashCode()
        {
            return RemoveResults.GetHashCode() +
                   RemoveDecoys.GetHashCode() +
                   OutputFilePath.GetHashCode();
        }
    }
}