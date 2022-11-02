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
using System.Globalization;
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
        public const string XML_EL = "refine_settings";

        // IMMUTABLE - all fields are readonly literals or in an immutable list
        // Holds information for refining the skyline file after data import

        private readonly RefineInputObject _commandValues;

        public static RefineSettings GetPathChanged(RefineSettings baseRefineSettings, string outputFilePath)
        {
            return new RefineSettings(baseRefineSettings._commandValues, baseRefineSettings.RemoveDecoys, 
                baseRefineSettings.RemoveResults, outputFilePath);
        }

        public static RefineSettings Empty()
        {
            return new RefineSettings(new RefineInputObject(), false, false, null);
        }

        public RefineSettings(RefineInputObject commandValues, bool removeDecoys, bool removeResults,
            string outputFilePath)
        {
            RemoveDecoys = removeDecoys;
            RemoveResults = removeResults;
            OutputFilePath = outputFilePath ?? string.Empty;
            _commandValues = commandValues.Copy();
        }

        public readonly bool RemoveDecoys;

        public readonly bool RemoveResults;

        public readonly string OutputFilePath;

        public RefineInputObject CommandValuesCopy => _commandValues.Copy();

        public bool WillRefine()
        {
            return !string.IsNullOrEmpty(OutputFilePath);
        }

        public void Validate()
        {
            if (!string.IsNullOrEmpty(OutputFilePath) && !RemoveResults && !RemoveDecoys && _commandValues.NoCommands())
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
                FileUtil.ValidateNotInDownloads(outputFilePath, Resources.RefineSettings_ValidateOutputFile_refined_output_file);
            }
        }

        public bool RunWillOverwrite(RunBatchOptions runOption, string configHeader, out StringBuilder message)
        {
            var tab = "      ";
            message = new StringBuilder(configHeader);
            if (runOption != RunBatchOptions.FROM_REFINE)
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
            var didReplace = TextUtil.SuccessfulReplace(ValidateOutputFile, oldRoot, newRoot, OutputFilePath, Program.FunctionalTest, out string replacedOutputPath);
            pathReplacedRefineSettings =
                new RefineSettings(_commandValues, RemoveDecoys, RemoveResults, replacedOutputPath);
            return didReplace;
        }

        public RefineSettings ForcePathReplace(string oldRoot, string newRoot)
        {
            var path = !string.IsNullOrEmpty(OutputFilePath) ? FileUtil.ForceReplaceRoot(oldRoot, newRoot, OutputFilePath) : string.Empty;
            return new RefineSettings(_commandValues, RemoveDecoys, RemoveResults, path);
        }

        #region Read/Write XML

        public static RefineSettings ReadXml(XmlReader reader)
        {
            XmlUtil.ReadUntilElement(reader);
            var removeDecoys = reader.GetBoolAttribute(XML_TAGS.remove_decoys);
            var removeResults = reader.GetBoolAttribute(XML_TAGS.remove_results);
            var outputFilePath = reader.GetAttribute(XML_TAGS.output_file_path);
            var commandList = new List<Tuple<RefineVariable, string>>();
            if (reader.ReadToDescendant(XMLElements.COMMAND_ARGUMENT))
            {
                do
                {
                    var variable =
                        (RefineVariable) Enum.Parse(typeof(RefineVariable), reader.GetAttribute(XML_TAGS.name));
                    var value = reader.GetAttribute(XML_TAGS.value);
                    commandList.Add(new Tuple<RefineVariable, string>(variable, value));
                } while (reader.ReadToNextSibling(XMLElements.COMMAND_ARGUMENT));
            }
            var commandValues = RefineInputObject.FromInvariantCommandList(commandList);
            return new RefineSettings(commandValues, removeDecoys, removeResults, outputFilePath);
        }

        public static RefineSettings ReadXmlVersion_20_2(XmlReader reader)
        {
            if (!XmlUtil.ReadNextElement(reader, "refine_settings"))
            {
                // This is a very old configuration with no refine settings
                return new RefineSettings(new RefineInputObject(), false, false,
                    string.Empty);
            }
            var removeDecoys = reader.GetBoolAttribute(OLD_XML_TAGS.RemoveDecoys);
            var removeResults = reader.GetBoolAttribute(OLD_XML_TAGS.RemoveResults);
            var outputFilePath = reader.GetAttribute(OLD_XML_TAGS.OutputFilePath);
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
            var commandValues = RefineInputObject.FromInvariantCommandList(commandList);
            return new RefineSettings(commandValues, removeDecoys, removeResults, outputFilePath);
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement(XML_EL);
            writer.WriteAttribute(XML_TAGS.remove_decoys, RemoveDecoys);
            writer.WriteAttribute(XML_TAGS.remove_results, RemoveResults);
            writer.WriteAttributeIfString(XML_TAGS.output_file_path, OutputFilePath);
            var commandList = _commandValues.AsCommandList(CultureInfo.InvariantCulture);
            foreach (var commandValue in commandList)
            {
                writer.WriteStartElement(XMLElements.COMMAND_ARGUMENT);
                writer.WriteAttributeIfString(XML_TAGS.name, commandValue.Item1.ToString());
                var value = !string.IsNullOrEmpty(commandValue.Item2) ? commandValue.Item2 : true.ToString();
                writer.WriteAttributeIfString(XML_TAGS.value, value);
                writer.WriteEndElement();
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
                var commandList = _commandValues.AsCommandList(CultureInfo.CurrentCulture);
                foreach (var commandValue in commandList)
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
            return  other._commandValues.Equals(_commandValues) &&
                    other.RemoveResults == RemoveResults &&
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