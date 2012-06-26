/*
 * Original author: Daniel Broudy <daniel.broudy .at. gmail.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{

    // A common interface for returning the relevant string for Tool Macros
    public interface IToolMacroProvider
    {
        string DocumentFilePath { get; }
        string SelectedProteinName { get; }
        string SelectedPeptideSequence { get; }       
        string SelectedPrecursor { get; }
        string ResultNameCurrent { get; }
    }

    [XmlRoot("ToolDescription")]
    public class ToolDescription : IXmlSerializable
    {
        public ToolDescription(string title, string command, string arguments, string initialDirectory)
        {
            Title = title;
            Command = command;
            Arguments = arguments;
            InitialDirectory = initialDirectory;
        }

        public string Title { get; set; }
        public string Command { get; set; }
        public string Arguments { get; set; }
        public string InitialDirectory { get; set; }

        /// <summary>
        ///  Return a string that is the Arguments string with the macros replaced.
        /// </summary>
        /// <param name="parent"> Window to center the error message in if null is returned for one of the macros. </param>
        /// <param name="toolMacroProvider"> Interface to use to get the current macro values </param>
        /// <returns> Arguments with macros replaced or null if one of the macros was missing 
        /// (eg. no selected peptide for $(SelPeptide) then the return value is null </returns>
        public string GetArguments(Form parent, IToolMacroProvider toolMacroProvider)
        {
            return ToolMacros.ReplaceMacrosArguments(Arguments, parent, toolMacroProvider);
        }
        // In this case SkylineWindow serves as both the form and the interface.
        public string GetArguments(SkylineWindow window)
        {
            return ToolMacros.ReplaceMacrosArguments(Arguments, window, window);
        }

        /// <summary>
        ///  Return a string that is the InitialDirectoy string with the macros replaced.
        /// </summary>
        /// <param name="parent"> Window to center the error message in if null is returned for one of the macros. </param>
        /// <param name="toolMacroProvider"> Interface to use to get the current macro values </param>
        /// <returns> InitialDirectory with macros replaced or null if one of the macros was missing 
        /// (eg. no document for $(DocumentDir) then the return value is null </returns>
        public string GetInitialDirectory(Form parent, IToolMacroProvider toolMacroProvider)
        {
            return ToolMacros.ReplaceMacrosInitialDirectory(InitialDirectory, parent, toolMacroProvider);
        }
        // In this case SkylineWindow serves as both the form and the interface.
        public string GetInitialDirectory(SkylineWindow window)
        {
            return ToolMacros.ReplaceMacrosInitialDirectory(InitialDirectory, window, window);
        }

        #region Implementation of IXmlSerializable
        private  ToolDescription()
        {
        }

        public static ToolDescription Deserializer(XmlReader reader)
        {
            return reader.Deserialize(new ToolDescription());
        }

        private enum ATTR
        {
            title,
            command,
            arguments,
            initial_directory,
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            Title = reader.GetAttribute(ATTR.title);
            Command = reader.GetAttribute(ATTR.command);
            Arguments = reader.GetAttribute(ATTR.arguments);
            InitialDirectory = reader.GetAttribute(ATTR.initial_directory);
            reader.Read();
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteAttribute(ATTR.title, Title);
            writer.WriteAttribute(ATTR.command, Command);
            writer.WriteAttribute(ATTR.arguments, Arguments);
            writer.WriteAttribute(ATTR.initial_directory, InitialDirectory);
        }
        #endregion

        public bool Equals(ToolDescription tool)
        {
            return (Equals(Title, tool.Title) &&
                    Equals(Command, tool.Command) &&
                    Equals(Arguments, tool.Arguments) &&
                    Equals(InitialDirectory, tool.InitialDirectory));
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = Title.GetHashCode();
                result = (result * 397) ^ Command.GetHashCode();
                result = (result * 397) ^ Arguments.GetHashCode();
                result = (result * 397) ^ InitialDirectory.GetHashCode();
                return result;
            }
        }
    }

    public static class ToolMacros
    {
        // Macros for Arguments.
        public static Macro[] _listArguments = new[]
            {
                new Macro("Document Path", "$(DocumentPath)", GetDocumentFilePath, "This tool requires a Document Path to run"),
                new Macro("Document Directory", "$(DocumentDir)", GetDocumentDir, "This tool requires a Document Directory to run"),
                new Macro("Document File Name", "$(DocumentFileName)", GetDocumentFileName, "This tool requires a Document File Name to run"),
                new Macro("Document File Name Without Extension", "$(DocumentBaseName)", GetDocumentFileNameWithoutExtension, "This tool requires a Document File Name  to run."),
                new Macro("Selected Protein Name", "$(SelProtein)", GetSelectedProteinName, "This tool requires a Selected Protein to run. \n Please select a protein before running this tool."),
                new Macro("Selected Peptide Sequence", "$(SelPeptide)", GetSelectedPeptideSequence, "This tool requires a Selected Peptide Sequence to run. \n Please select a peptide sequence before running this tool." ),
                new Macro("Selected Precursor", "$(SelPrecursor)", GetSelectedPrecursor, "This tool requires a Selected Precursor to run. \n Please select a precursor before running this tool."),
                new Macro("Active Replicate Name", "$(ReplicateName)", GetActiveReplicateName, "This tool requires an Active Replicate Name to run")
            };

        // Macros for InitialDirectory.
        public static Macro[] _listInitialDirectory = new[]
            {
                new Macro("Document Directory", "$(DocumentDir)", GetDocumentDir, "This tool requires a Document Directory to run") 
            };
        
        // Check string arguments for the ShortText of each macro in the macro list.
        // If the short text is present, get the actual value and replace it 
        // If the actual value turns out to be null, display error message and return null.
        public static string ReplaceMacrosArguments(string arguments, Form parent, IToolMacroProvider toolMacroProvider)
        {
            foreach (Macro macro in _listArguments)
            {
                if (arguments.Contains(macro.ShortText))
                {
                    string contents = macro.GetContents(toolMacroProvider);
                    if (contents == null)
                    {
                        MessageDlg.Show(null , macro.ErrorMessage);
                        return null;
                    }
                    else
                    {
                        arguments = arguments.Replace(macro.ShortText, contents);    
                    }     
                }                               
            }
            return arguments; 
        }

        // Check string initialDirectory for the ShortText of each macro in the macro list.
        // If the short text is present, get the actual value and replace it 
        // If the actual value turns out to be null, display error message and return null.
        public static string ReplaceMacrosInitialDirectory(string initialDirectory, Form parent, IToolMacroProvider toolMacroProvider)
        {
            foreach (Macro macro in _listInitialDirectory)
            {
                if (initialDirectory.Contains(macro.ShortText))
                {
                    string contents = macro.GetContents(toolMacroProvider);
                    if (contents == null)
                    {
                        MessageDlg.Show(null, macro.ErrorMessage);
                        return null;
                    }
                    else
                    {
                        initialDirectory = initialDirectory.Replace(macro.ShortText, contents);
                    }
                }
            }
            return initialDirectory;
        }

        private static string GetDocumentFilePath(IToolMacroProvider toolMacroProvider)
        {
            return toolMacroProvider.DocumentFilePath;
        }

        private static string GetDocumentDir(IToolMacroProvider toolMacroProvider)
        {
            return Path.GetDirectoryName(toolMacroProvider.DocumentFilePath);
        }

        private static string GetDocumentFileName(IToolMacroProvider toolMacroProvider)
        {
            return Path.GetFileName(toolMacroProvider.DocumentFilePath);
        }

        private static string GetDocumentFileNameWithoutExtension(IToolMacroProvider toolMacroProvider)
        {
            return Path.GetFileNameWithoutExtension(toolMacroProvider.DocumentFilePath);
        }

        private static string GetSelectedProteinName (IToolMacroProvider toolMacroProvider)
        {
            return toolMacroProvider.SelectedProteinName;
        }

        private static string GetSelectedPeptideSequence(IToolMacroProvider toolMacroProvider)
        {
            return toolMacroProvider.SelectedPeptideSequence;
        }

        private static string GetSelectedPrecursor(IToolMacroProvider toolMacroProvider)
        {
            return toolMacroProvider.SelectedPrecursor;
        }

        private static string GetActiveReplicateName(IToolMacroProvider toolMacroProvider)
        {
            return toolMacroProvider.ResultNameCurrent;
        }
    }

    public class Macro
    {
        /// <summary>
        ///  A decription for Macros
        /// </summary>
        /// <param name="plainText"> The text that shows up on the drop down menu (eg. "Document Path")</param>
        /// <param name="shortText"> The text that shows up in the text box (eg. "$(DocumentPath)")</param>
        /// <param name="getContents"> A function that when passed an IToolMacroProvider returns the actual string value the macro represents. </param>
        /// <param name="errorMessage">The message that will be displayed if GetContents returns null in the replace macro methods. </param>
        public Macro(string plainText, string shortText, Func<IToolMacroProvider, string> getContents, string errorMessage)
        {
            PlainText = plainText;
            ShortText = shortText;
            GetContents = getContents;
            ErrorMessage = errorMessage;
        }
        public string ErrorMessage { get; set; }
        public string PlainText { get; set; }
        public string ShortText { get; set; }
        public Func<IToolMacroProvider, string> GetContents { get; set; } 
    }
}