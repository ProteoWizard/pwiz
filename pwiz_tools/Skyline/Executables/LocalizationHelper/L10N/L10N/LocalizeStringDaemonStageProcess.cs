/*
 * Original author: Yuval Boss <yuval .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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

using JetBrains.ReSharper.Psi;
// ReSharper disable PossibleNullReferenceException
using JetBrains.ReSharper.Daemon.Stages.Dispatcher;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.Util;
using JetBrains.ReSharper.Feature.Services.Daemon;
// ReSharper disable NonLocalizedString
namespace YuvalBoss.L10N
{
    [ElementProblemAnalyzer(new[] { typeof(ICSharpLiteralExpression) }, HighlightingTypes = new[] { typeof(LocalizeStringSuggestion) })]
    public class LocalizeStringDaemonStageProcess : ElementProblemAnalyzer<ICSharpLiteralExpression>
    {
        // Implements ElementProblemAnalyzer instead of the Daemon because of issues with multi line commenting.
        protected override void Run(ICSharpLiteralExpression element, ElementProblemAnalyzerData data, IHighlightingConsumer consumer)
        {
            if (ElementLocalized(element))
            {
                consumer.AddHighlighting(new LocalizeStringSuggestion(element), element.GetDocumentRange(), element.GetContainingFile());
            }
        }

        // Checks if file is considered localizable.
        // If file is AssemblyInfo.cs or a resx file it will not be localized and warnings will not be thrown
        // on strings in that file.
        protected bool IsLocalizable(ICSharpLiteralExpression declaration)
        {
            if (IsTest(declaration))
                return false;
            var declarationNode = declaration.Parent;
            var fileName = declaration.GetSourceFile().Name;
            var splitter = fileName.Split('.');
            var extension = splitter[splitter.Length - 1];
            if (extension == "resx")
                return false;
            else if (fileName == "AssemblyInfo.cs")
                return false;
            while (!(declarationNode is IAttribute))
            {
                declarationNode = declarationNode.Parent;
                if (declarationNode == null)
                    break;
            }
            if (declarationNode is IAttribute)
                return false;
            else
                return true;
        }

        // This checks if the file is a test.  These will not be localized so there is no need to throw a warning on 
        // strings in test classes.
        // Checks for "test" in file name or "[TestClass]" in document text.
        protected bool IsTest(ICSharpLiteralExpression declaration)
        {
            if (declaration.GetSourceFile().GetProject().Name.ToLower().Contains("test")) // Not L10N
            {
                return true;
            }

            return false;
        }

        protected virtual bool ElementLocalized(ICSharpLiteralExpression declaration)
        {
            if (declaration.Literal.NodeType ==
                        JetBrains.ReSharper.Psi.CSharp.Parsing.CSharpTokenType.STRING_LITERAL_REGULAR)
            {
                if (IsLocalizable(declaration) == false)
                    return false;
                var docRange = declaration.GetDocumentRange();
                bool found = false;
                int i = 0;
                int offset = 0;
                string str = string.Empty;
                while (found == false)
                {
                    string strSearch = "// Not L10N";
                    offset = declaration.GetDocumentRange().TextRange.EndOffset + i;
                    if (strSearch.Length + offset >= declaration.GetDocumentRange().Document.GetTextLength())
                    {
                        break;
                    }
                    str = docRange.Document.GetText(new TextRange(offset, strSearch.Length + offset));
                    if (str == strSearch)
                    {
                        found = true;
                        break;
                    }
                    if (str.IndexOf('\n') >= 0)
                    {
                        break;
                    }
                    i = i + 1;
                }
                if (!found)
                {
                    return true;
                }
            }
            return false;
        }
    }
}