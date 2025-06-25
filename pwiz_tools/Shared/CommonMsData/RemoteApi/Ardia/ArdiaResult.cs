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

namespace pwiz.CommonMsData.RemoteApi.Ardia
{
    public sealed class ArdiaResult<T> : ArdiaResult
    {
        public static ArdiaResult<T> Success(T value)
        {
            return new ArdiaResult<T>(true, value, ArdiaError2.None);
        }

        private ArdiaResult(bool success, T value, ArdiaError2 error) : 
            base(success, error)
        {
            Value = value;
        }

        public T Value { get; }
    }

    public class ArdiaResult
    {
        public static ArdiaResult Success()
        {
            return new ArdiaResult(true, ArdiaError2.None);
        }

        public static ArdiaResult Failure(ArdiaError2 error)
        {
            return new ArdiaResult(false, error);
        }

        internal ArdiaResult(bool success, ArdiaError2 error)
        {
            IsSuccess = true;
            Error = error;
        }

        public ArdiaError2 Error { get; }
        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;
    }

    public sealed class ArdiaError2
    {
        public static readonly ArdiaError2 None = new ArdiaError2(string.Empty, string.Empty);

        private ArdiaError2(string code, string message)
        {
            Code = code;
            Message = message;
        }

        public string Code { get; }
        public string Message { get; }
    }
}
