/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using pwiz.Skyline.Controls.SeqNode;

namespace pwiz.Skyline.Model
{
    public abstract class PasteFormat
    {
        private readonly String _name;
        protected PasteFormat(String name)
        {
            _name = name;
        }
        public String Description { get; protected set; }
        public abstract IdentityPath Insert(SrmDocument document, SrmTreeNode selectedNode, String text);
        public abstract List<PasteError> Validate(SrmDocument document, SrmTreeNode selectedNode, String text);
        protected void ModifyDocument(string description, Func<SrmDocument,SrmDocument> func)
        {
            Program.MainWindow.ModifyDocument(description, func);
        }
        public override String ToString()
        {
            return _name;
        }
    }

    public static class PasteFormats
    {
        public static readonly PasteFormat FASTA = new Fasta();
        public static readonly IList<PasteFormat> ALL = new List<PasteFormat>{FASTA};
        class Fasta : PasteFormat
        {
            public Fasta() : base("FASTA Records")
            {
                Description =
                    "FASTA records begin with '>' and have the protein name followed by the optional protein description.  "
                    + "The lines following the header line are the protein sequence, and must consist of capital letters denoting amino acids.  "
                    + "Blank lines separate fasta records.";
            }
            public override List<PasteError> Validate(SrmDocument document, SrmTreeNode selectedNode, String text)
            {
                List<PasteError> pasteErrors = new List<PasteError>();
                if (!text.StartsWith(">"))
                {
                    pasteErrors.Add(new PasteError
                                        {
                                            Message = "This must start with '>'",
                                            Column = 0,
                                            Length = 1,
                                            Line = 0,
                                        });
                }
                string[] lines = text.Split('\n');
                int aa = 0;
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (line.StartsWith(">"))
                    {
                        if (i > 0 && aa == 0)
                        {
                            pasteErrors.Add(new PasteError
                                                {
                                                    Message = "There is no sequence for this protein",
                                                    Column = 0,
                                                    Line = i - 1,
                                                    Length = lines[i-1].Length
                                                });
                            return pasteErrors;
                        }
                        aa = 0;
                        continue;
                    }

                    for (int column = 0; column < line.Length; column++)
                    {
                        char c = line[column];
                        if (AminoAcid.IsExAA(c))
                            aa++;
                        else if (!char.IsWhiteSpace(c) && c != '*')
                        {
                            pasteErrors.Add(new PasteError
                                {
                                    Message =
                                        string.Format("'{0}' is not a capital letter that corresponds to an amino acid.", c),
                                    Column = column,
                                    Line = i,
                                    Length = 1,
                                });
                        }
                    }
                }
                return pasteErrors;
            }

            public override IdentityPath Insert(SrmDocument document, SrmTreeNode selectedNode, string text)
            {
                IdentityPath selectPath = null;

                ModifyDocument("Paste FASTA", doc => doc.ImportFasta(new StringReader(text), false,
                    selectedNode == null ? null : selectedNode.Path, out selectPath));
                return selectPath;
            }
        }

        class ProteinNames : PasteFormat
        {
            public ProteinNames() : base("Protein Names")
            {
                Description = "Protein names, followed optionally by ";
            }

            public override IdentityPath Insert(SrmDocument document, SrmTreeNode selectedNode, string text)
            {
                throw new NotImplementedException();
            }

            public override List<PasteError> Validate(SrmDocument document, SrmTreeNode selectedNode, string text)
            {
                return new List<PasteError>();
            }
        }

        //class MassList : PasteFormat
        //{
        //    public MassList() : base("Transition List")
        //    {
        //        Description = "Description coming soon";
        //    }
        //    public override List<PasteError> Validate(SrmDocument document, SrmTreeNode selectedNode, string text)
        //    {
        //        char separator = text.IndexOf(',') >= 0 ? ',' : '\t';
        //        TextReader reader = new StringReader(text);
        //        List<PasteError> pasteErrors = new List<PasteError>();
        //        string line = reader.ReadLine();
        //        if (line == null)
        //        {
        //            pasteErrors.Add(new PasteError("No ")
        //                                {
        //                                    Column = 0,Line=0,Length = 0,Message = "No mass list"
        //                                });
        //            return pasteErrors;
        //        }
        //        string[] fields = MassListRowReader.GetFields(line, separator);
        //        if (fields.Length < 3)
        //            throw new InvalidDataException("Invalid mass list.  Mass lists must contain at least precursor m/z, product m/z, and peptide sequence.");


        //    }
        //}
    }

    public class PasteError
    {
        public String Message { get; set; }
        public int Line { get; set;}
        public int Column { get; set;}
        public int Length { get; set; }
    }

    public class ImportException : Exception
    {
        public ImportException(String message, int line, int column, int length) : base(message)
        {
            Line = line;
            Column = column;
            Length = length;
        }
        public ImportException(String message, int line, int column) : this(message, line, column, 0)
        {
        }

        public ImportException(String message, int line) : this(message, line, 0, 0)
        {
        }
        public int Line { get; private set; }
        public int Column { get; private set; }
        public int Length { get; private set; }
    }
}
