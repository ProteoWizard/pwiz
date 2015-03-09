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

using JetBrains.ReSharper.Feature.Services.CSharp.Daemon;
using JetBrains.ReSharper.Feature.Services.Daemon;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using YuvalBoss.L10N;

[assembly: RegisterConfigurableSeverity(LocalizeStringSuggestion.SeverityId,
  null,
  HighlightingGroupIds.BestPractice,
  "Non Localized String",
  "All strings must be marked with either '// Not L10N' or localized to a resx file.",
  Severity.WARNING,
  false)]

namespace YuvalBoss.L10N
{
    [ConfigurableSeverityHighlightingAttribute(SeverityId, CSharpLanguage.Name, OverlapResolve = OverlapResolveKind.WARNING)]
    public class LocalizeStringSuggestion : CSharpHighlightingBase, IHighlighting
    {
        public ICSharpLiteralExpression Declaration { get; private set; }
        //SeverityId is the name of the warning thrown both in the code inspector and in block comments.
        public const string SeverityId = "NonLocalizedString";

        public LocalizeStringSuggestion(ICSharpLiteralExpression declaration)
        {
            Declaration = declaration;
        }

        public string ToolTip
        {
            get { return "String must be Localized or marked as // Not L10N"; }
        }

        public string ErrorStripeToolTip
        {
            get { return ToolTip; }

        }

        public override bool IsValid()
        {
            return Declaration != null && Declaration.IsValid();
        }

        public int NavigationOffsetPatch
        {
            get { return 0; }
        }
    }
}
