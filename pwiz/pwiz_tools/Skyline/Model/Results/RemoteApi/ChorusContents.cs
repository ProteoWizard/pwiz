/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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
using System.Collections.Generic;

namespace pwiz.Skyline.Model.Results.RemoteApi
{
    public class ChorusContents
    {
        public interface IChorusItem
        {
            string GetName();
            IEnumerable<IChorusItem> ListChildren();
            DateTime? GetDateTime();
            long GetSize();
        }

        public Project[] myProjects { get; set; }
        public Project[] sharedProjects { get; set; }
        public Project[] publicProjects { get; set; }
        public Experiment[] myExperiments { get; set; }
        public Experiment[] sharedExperiments { get; set; }
        public Experiment[] publicExperiments { get; set; }
        public File[] myFiles { get; set; }
        public File[] sharedFiles { get; set; }
        public File[] publicFiles { get; set; }

        public ChorusContents Merge(ChorusContents chorusContents)
        {
            return new ChorusContents
            {
                myProjects = myProjects ?? chorusContents.myProjects,
                sharedProjects = sharedProjects ?? chorusContents.sharedProjects,
                publicProjects = publicProjects ?? chorusContents.publicProjects,
                myExperiments = myExperiments ?? chorusContents.myExperiments,
                sharedExperiments = sharedExperiments ?? chorusContents.sharedExperiments,
                publicExperiments = publicExperiments ?? chorusContents.publicExperiments,
                myFiles = myFiles ?? chorusContents.myFiles,
                sharedFiles = sharedFiles ?? chorusContents.sharedFiles,
                publicFiles = publicFiles ?? chorusContents.publicFiles,
            };
        }

        public class Project : IChorusItem
        {
            public string projectName { get; set; }
            public string labName { get; set; }
            public string owner { get; set; }
            public long? lastModified { get; set; }
            public Experiment[] experiments { get; set; }
            public string GetName()
            {
                return projectName;
            }

            public IEnumerable<IChorusItem> ListChildren()
            {
                return experiments;
            }

            public DateTime? GetDateTime()
            {
                return ToDateTime(lastModified);
            }

            public long GetSize()
            {
                return 0;
            }
        }

        public class Experiment : IChorusItem
        {
            public string experimentName { get; set; }
            public string owner { get; set; }
            public long? lastModified { get; set; }
            public File[] files { get; set; }
            public string GetName()
            {
                return experimentName;
            }

            public IEnumerable<IChorusItem> ListChildren()
            {
                return files;
            }

            public DateTime? GetDateTime()
            {
                return ToDateTime(lastModified);
            }

            public long GetSize()
            {
                return 0;
            }
        }

        public class File : IChorusItem
        {
            public int id { get; set; }
            public string name { get; set; }
            public string instrumentName { get; set; }
            public string instrumentModel { get; set; }
            public long? uploadDate { get; set; }
            public long fileSizeBytes { get; set; }
            public string GetName()
            {
                return name;
            }

            public IEnumerable<IChorusItem> ListChildren()
            {
                return null;
            }

            public DateTime? GetDateTime()
            {
                return ToDateTime(uploadDate);
            }

            public long GetSize()
            {
                return fileSizeBytes;
            }
        }

        public static DateTime? ToDateTime(long? milliSeconds)
        {
            if (!milliSeconds.HasValue)
            {
                return null;
            }
            return new DateTime(1970, 1, 1, 0, 0, 0, 0).AddMilliseconds(milliSeconds.Value);
        }
    }
}
