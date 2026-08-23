/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using System.Runtime.Serialization;

namespace pwiz.Common.SystemUtil
{
    public abstract class CommonException : ApplicationException
    {
        public static CommonException<TDetail> Create<TDetail>(TDetail detail, Exception innerException)
        {
            return new CommonException<TDetail>(detail, innerException);
        }

        public static CommonException<TDetail> Create<TDetail>(TDetail detail)
        {
            return Create(detail, null);
        }

        protected CommonException(object exceptionDetail, Exception innerException) : base(null, innerException)
        {
            ExceptionDetail = exceptionDetail;
        }

        // Formatter-based serialization: obsolete on .NET 8, which dropped BinaryFormatter, but this
        // constructor is part of the type's shape on both legs and subclasses chain to it.
#pragma warning disable SYSLIB0051
        protected CommonException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
#pragma warning restore SYSLIB0051

        public object ExceptionDetail { get; private set; }

        public override string Message
        {
            get
            {
                if (ExceptionDetail != null)
                {
                    // ToString() is nullable, and Message must not be: fall back rather than
                    // hand a null message to whatever is reporting the error.
                    return ExceptionDetail.ToString() ?? base.Message;
                }
                return base.Message;
            }
        }
    }

    public interface ICommonException<out TDetail>
    {
        TDetail ExceptionDetail { get; }
    }

    public class CommonException<TDetail> : CommonException, ICommonException<TDetail>
    {
        public CommonException(TDetail detail, Exception innerException) : base(detail, innerException)
        {
            
        }
        protected CommonException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        public new TDetail ExceptionDetail { get { return (TDetail) base.ExceptionDetail; } }
    }
}
