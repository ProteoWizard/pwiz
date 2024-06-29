/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
namespace ResourcesOrganizer.ResourcesModel;

public record EnglishTextChanged() : LocalizationIssue("English text changed")
{
    private const string OldEnglish = "Old English:";
    private const string CurrentEnglish = "Current English:";
    private const string OldLocalized = "Old Localized:";

    public EnglishTextChanged(string reviewedEnglish, string reviewedLocalized) : this()
    {
        ReviewedInvariantValue = reviewedEnglish;
        ReviewedLocalizedValue = reviewedLocalized;
    }

    public string? ReviewedInvariantValue { get; init; }
    public string? ReviewedLocalizedValue { get; init; }

    public override string GetIssueDetails(ResourceEntry? resourceEntry)
    {
        var lines = new List<string>
        {
            Name,
            OldEnglish + ReviewedInvariantValue,
        };
        if (resourceEntry != null)
        {
            lines.Add(CurrentEnglish + resourceEntry.Invariant.Value);
        };
        lines.Add(OldLocalized + ReviewedLocalizedValue);
        return TextUtil.LineSeparate(lines);
    }

    public override LocalizationIssue ParseCommentText(string commentText)
    {
        var oldEnglishList = new List<string>();
        var currentEnglishList = new List<string>();
        var oldLocalizedList = new List<string>();
        List<string>? currentList = null;
        using var reader = new StringReader(commentText);
        while (reader.ReadLine() is { } line)
        {
            if (line.StartsWith(OldEnglish))
            {
                currentList = oldEnglishList;
                line = line.Substring(OldEnglish.Length);
            }
            else if (line.StartsWith(CurrentEnglish))
            {
                currentList = currentEnglishList;
                line = line.Substring(CurrentEnglish.Length);
            }
            else if (line.StartsWith(OldLocalized))
            {
                currentList = oldLocalizedList;
                line = line.Substring(OldLocalized.Length);
            }

            if (currentList != null)
            {
                currentList.Add(line);
            }
        }

        return this with
        {
            ReviewedLocalizedValue = TextUtil.LineSeparate(oldLocalizedList),
            ReviewedInvariantValue = TextUtil.LineSeparate(oldEnglishList)
        };


    }
    public override bool AppliesToTextOnly => false;

    public override LocalizationIssue ParseFromCsvRecord(LocalizationCsvRecord csvRecord)
    {
        return this with {ReviewedInvariantValue = csvRecord.OldEnglish, ReviewedLocalizedValue = csvRecord.OldLocalized};
    }

    public override LocalizationCsvRecord StoreInCsvRecord(LocalizationCsvRecord csvRecord)
    {
        return csvRecord with
        {
            Issue = Name, OldEnglish = ReviewedInvariantValue, OldLocalized = ReviewedLocalizedValue
        };
    }
}