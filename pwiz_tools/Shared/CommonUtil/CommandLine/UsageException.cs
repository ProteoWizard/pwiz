/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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

namespace pwiz.Common.CommandLine
{
    /// <summary>
    /// Base for all command-line usage errors (a bad or missing argument value). The
    /// message is supplied pre-formatted/localized; the context-free value exceptions
    /// below build theirs through <see cref="ArgUsage.Provider"/>. Host applications
    /// subclass this for domain-specific value errors.
    /// </summary>
    public class UsageException : ArgumentException
    {
        protected UsageException(string message) : base(message)
        {
        }

        protected UsageException(ArgumentBase arg, string message) : base(message)
        {
            Argument = arg;
        }

        /// <summary>The argument the error is about, or null when it names none (an unknown argument).</summary>
        public ArgumentBase Argument { get; }
    }

    /// <summary>A token spelled like an option that names no declared argument.</summary>
    public class UnknownArgumentException : UsageException
    {
        public UnknownArgumentException(string argText)
            : base(ArgUsage.Provider.UnknownArgumentMessage(argText))
        {
            ArgumentText = argText;
        }

        /// <summary>The token as typed, inline value included.</summary>
        public string ArgumentText { get; }
    }

    public class ValueMissingException : UsageException
    {
        public ValueMissingException(ArgumentBase arg)
            : this(arg, arg.ArgumentText)
        {
        }

        /// <summary>With <paramref name="argText"/> the argument as the user typed it (<c>-o</c> or <c>--output</c>).</summary>
        public ValueMissingException(ArgumentBase arg, string argText)
            : base(arg, ArgUsage.Provider.ValueMissingMessage(argText))
        {
        }
    }

    public class ValueUnexpectedException : UsageException
    {
        public ValueUnexpectedException(ArgumentBase arg)
            : this(arg, arg.ArgumentText)
        {
        }

        /// <summary>With <paramref name="argText"/> the argument as the user typed it.</summary>
        public ValueUnexpectedException(ArgumentBase arg, string argText)
            : base(arg, ArgUsage.Provider.ValueUnexpectedMessage(argText))
        {
        }
    }

    public class ValueInvalidException : UsageException
    {
        public ValueInvalidException(ArgumentBase arg, string value, string[] argValues)
            : base(arg, ArgUsage.Provider.ValueInvalidMessage(arg.ArgumentText, value, argValues))
        {
        }
    }

    public class ValueInvalidBoolException : UsageException
    {
        public ValueInvalidBoolException(ArgumentBase arg, string value)
            : base(ArgUsage.Provider.ValueInvalidBoolMessage(arg.ArgumentText, value))
        {
        }
    }

    public class ValueInvalidDoubleException : UsageException
    {
        public ValueInvalidDoubleException(ArgumentBase arg, string value)
            : base(ArgUsage.Provider.ValueInvalidDoubleMessage(arg.ArgumentText, value))
        {
        }
    }

    public class ValueOutOfRangeDoubleException : UsageException
    {
        public ValueOutOfRangeDoubleException(ArgumentBase arg, double value, double minVal, double maxVal)
            : base(ArgUsage.Provider.ValueOutOfRangeDoubleMessage(arg.ArgumentText, value, minVal, maxVal))
        {
        }
    }

    public class ValueInvalidIntException : UsageException
    {
        public ValueInvalidIntException(ArgumentBase arg, string value)
            : base(ArgUsage.Provider.ValueInvalidIntMessage(arg.ArgumentText, value))
        {
        }
    }

    public class ValueOutOfRangeIntException : UsageException
    {
        public ValueOutOfRangeIntException(ArgumentBase arg, int value, int minVal, int maxVal)
            : base(ArgUsage.Provider.ValueOutOfRangeIntMessage(arg.ArgumentText, value, minVal, maxVal))
        {
        }
    }

    public class ValueInvalidDateException : UsageException
    {
        public ValueInvalidDateException(ArgumentBase arg, string value)
            : base(ArgUsage.Provider.ValueInvalidDateMessage(arg.ArgumentText, value))
        {
        }
    }

    public class ValueInvalidPathException : UsageException
    {
        public ValueInvalidPathException(ArgumentBase arg, string value)
            : base(ArgUsage.Provider.ValueInvalidPathMessage(arg.ArgumentText, value))
        {
        }
    }
}
