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

using System;
using JetBrains.Application.Progress;
using JetBrains.DocumentManagers.Transactions;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.TextControl;
using JetBrains.Util;
using JetBrains.DocumentModel;
using JetBrains.ReSharper.Feature.Services.ContextActions;
using JetBrains.ReSharper.Feature.Services.QuickFixes;

namespace YuvalBoss.L10N
{
    /// <summary>
    /// This context action marks unlocalized strings as // Not L10N
    /// availability and execution of this action.
    /// </summary>
    [QuickFix]
    public class LocalizeContextAction : ContextActionBase, IQuickFix
    {
        readonly LocalizeStringSuggestion _highlighter;

        public LocalizeContextAction(LocalizeStringSuggestion highlighter)
        {
            _highlighter = highlighter;
        }

        public override bool IsAvailable(IUserDataHolder cache)
        {
            return _highlighter != null && _highlighter.IsValid();
        }

        protected override Action<ITextControl> ExecutePsiTransaction(ISolution solution, IProgressIndicator progress)
        {
            return null;
        }

        // To fix offset issue had to implement ExecuteAfterPsiTransaction. 
        // http://stackoverflow.com/questions/22545558/resharper-quickfix-highlighter-offset-issue
        protected override Action<ITextControl> ExecuteAfterPsiTransaction(ISolution solution,
            IProjectModelTransactionCookie cookie, IProgressIndicator progress)
        {
            DocumentRange docRange = _highlighter.Declaration.GetDocumentRange();
            IDocument textDoc = _highlighter.Declaration.GetSourceFile().Document;

            if (textDoc != null)
            {
                for (int i = 0; i < textDoc.GetTextLength(); i++)
                {
                    int docRangeEndOffSet = docRange.TextRange.EndOffset + i;
                    int docRangeStart = docRange.TextRange.StartOffset;
                    string docRangeString = docRange.Document.GetText(new TextRange(docRange.TextRange.StartOffset, docRangeEndOffSet));

                    int location;
                    if (docRangeString.EndsWith("\r\n"))
                    {
                        var lineEnd = docRangeString.LastIndexOf(";\r\n");
                        if (lineEnd >= 0)
                        {
                            location = docRangeStart + lineEnd + 1;
                            using (solution.CreateTransactionCookie(DefaultAction.Commit, "L10N insterted"))
                            {
                                textDoc.InsertText(location, " // Not L10N");
                            }
                            break;
                        }
                        location = docRangeEndOffSet - 2;
                        using (solution.CreateTransactionCookie(DefaultAction.Commit, "L10N insterted"))
                        {
                            textDoc.InsertText(location, " // Not L10N");
                        }
                        break;
                    }
                }
            }
            return null;
        }
        public override string Text
        {
            get { return "Mark line as '// Not L10N'"; }
        }

    }
}