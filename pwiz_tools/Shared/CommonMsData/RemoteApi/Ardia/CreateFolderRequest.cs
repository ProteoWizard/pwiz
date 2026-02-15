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

using Newtonsoft.Json;

namespace pwiz.CommonMsData.RemoteApi.Ardia
{
    public class CreateFolderRequest
    {
        public static CreateFolderRequest Create(string parentFolderPath, string newFolderName)
        {
            var model = new CreateFolderRequest
            {
                FolderPath = parentFolderPath,
                Name = newFolderName
            };

            return model;
        }

        private CreateFolderRequest()
        {
        }

        public string FolderPath { get; private set; }
        public string Name { get; private set; }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }
    }

    public class CreateFolderResponse
    {
        public static CreateFolderResponse Create()
        {
            return new CreateFolderResponse();
        }

        private CreateFolderResponse()
        {
        }
    }
}