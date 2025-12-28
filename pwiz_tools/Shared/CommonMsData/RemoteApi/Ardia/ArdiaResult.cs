/*
 * Copyright 2025 University of Washington - Seattle, WA
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
using System.Net; // HttpStatusCode

namespace pwiz.CommonMsData.RemoteApi.Ardia
{
    public class ArdiaResult<T> : ArdiaResult
    {
        public new static ArdiaResult<T> Default = Failure(null, null, null);
        public static ArdiaResult<T> Canceled = Failure(null, null, null);

        public static ArdiaResult<T> Success(T value)
        {
            return new ArdiaResult<T>(value);
        }

        public new static ArdiaResult<T> Failure(string message, HttpStatusCode? statusCode, Exception exception)
        {
            return new ArdiaResult<T>(default, message, statusCode, exception);
        }

        public static ArdiaResult<T> Failure(ArdiaResult other)
        {
            return new ArdiaResult<T>(default, other.ErrorMessage, other.ErrorStatusCode, other.ErrorException);
        }

        private ArdiaResult(T value) : base(true, null, null, null)
        {
            Value = value;
        }

        private ArdiaResult(T value, string message, HttpStatusCode? statusCode, Exception exception) : base(false, message, statusCode, exception)
        {
            Value = default;
        }

        public T Value { get; }
    }

    public class ArdiaResult
    {
        public static ArdiaResult Default = Failure(null, null, null);

        public static ArdiaResult Success()
        {
            return new ArdiaResult(true, null, null, null);
        }

        public static ArdiaResult Failure(string message, HttpStatusCode? statusCode, Exception exception)
        {
            return new ArdiaResult(false, message, statusCode, exception);
        }

        internal ArdiaResult(bool success, string message, HttpStatusCode? statusCode, Exception exception)
        {
            IsSuccess = success;
            ErrorMessage = message;
            ErrorStatusCode = statusCode;
            ErrorException = exception;
        }

        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;

        public HttpStatusCode? ErrorStatusCode { get; }
        public string ErrorMessage { get; }
        public Exception ErrorException { get; }
    }
}