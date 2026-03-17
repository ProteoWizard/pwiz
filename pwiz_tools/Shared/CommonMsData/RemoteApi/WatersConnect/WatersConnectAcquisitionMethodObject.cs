/*
 * Original author: Rita Chupalov <ritach .at. uw.edu>
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
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
using Newtonsoft.Json.Linq;
using System;

namespace pwiz.CommonMsData.RemoteApi.WatersConnect
{
    public class WatersConnectAcquisitionMethodObject : WatersConnectObject
    {
        /*

        Comment out since in parent class.  Put here for info on min and max length

        /// <summary>
        /// The method name
        /// maxLength: 100
        /// minLength: 1
        /// example: Method 1
        /// </summary>
        // public string Name { get; private set; }

        */

        /// <summary>
        /// The method description
        /// maxLength: 250
        /// nullable: true
        /// </summary>
        public string Description { get; private set; }


        /// <summary>
        /// The identifier of the version of the method. It will be different for different versions of the same method.
        /// string($uuid)
        /// </summary>
        public string MethodVersionId { get; private set; }
        /// <summary>
        /// The identifier of the method, it remains the same for new versions of the method.
        /// string($uuid)
        /// </summary>
        public string MethodId { get; private set; }
        /// <summary>
        /// The method type identifier. The base category of the item type has to be Method
        /// string($uuid)
        /// </summary>
        public string DataRecordTypeId { get; private set; }
        /// <summary>
        /// The method type name The item type name will be translated
        /// nullable: true
        /// </summary>
        public string TypeName { get; private set; }
        /// <summary>
        /// The folder identifier where the method is located
        /// string($uuid)
        /// </summary>
        public string FolderId { get; private set; }
        /// <summary>
        /// Date when the method was created on the server (ISO 8601, UTC + offset)
        /// string($date-time)
        /// Cannot be null but method called to get value returns nullable vale
        /// </summary>
        public DateTime? CreatedDateTime { get; private set; }
        /// <summary>
        /// Date when the method was last modified on the server (ISO 8601, UTC + offset). The value can be null if the version had not been modified.
        /// string($date-time)
        /// nullable: true
        /// </summary>
        public DateTime? ModifiedDateTime { get; private set; }
        /// <summary>
        /// integer($int32)
        /// The method version. Starts at 1 when created, then increments only when the method is edited (goes from PUBLISHED to DRAFT state)
        /// Cannot be null but method called to get value returns nullable vale
        /// </summary>
        public int? Version { get; private set; }
        /// <summary>
        /// Modification version. Starts at 1 for each new Version of the method and is incremented with each operation that modifies the method, until the method is published.
        /// integer($int32)
        /// Cannot be null but method called to get value returns nullable vale
        /// </summary>
        public int? ModVersion { get; private set; }
        /// <summary>
        /// Identifier of the user that created the method
        /// string($uuid)
        /// </summary>
        public string CreatedById { get; private set; }
        /// <summary>
        /// Username associated with the user that created the method
        /// nullable: true
        /// </summary>
        public string CreatedByUsername { get; private set; }
        /// <summary>
        /// The full name of the user who created the initial version of the record in format: Last Name, First Name
        /// nullable: true
        /// </summary>
        public string CreatedByUserFullName { get; private set; }
        /// <summary>
        /// Identifier of the user that last updated the method
        /// string($uuid)
        /// nullable: true
        /// </summary>
        public string ModifiedById { get; private set; }
        /// <summary>
        /// Username associated with the user that last modified the method
        /// nullable: true
        /// </summary>
        public string ModifiedByUsername { get; private set; }
        /// <summary>
        /// The full name of the user who modified the record in format: Last Name, First Name
        /// nullable: true
        /// </summary>
        public string ModifiedByUserFullName { get; private set; }
        /// <summary>
        /// Method version state.
        /// string($int32)
        /// Enum: [ DRAFT, PUBLISHED, SUPERSEDED ]
        /// </summary>
        public string State { get; private set; }
        /// <summary>
        /// True if the method is locked for editing
        /// </summary>
        public bool? Locked { get; private set; }

        ///  Included if 'expand' option is on the request


        /// <summary>
        /// The method specific data, used in the analysis. There is no validation on this data, consumers are expected to understand the format of this data. This property will not be returned if the expand option was not used.
        /// nullable: true
        /// </summary>
        public string Definition { get; private set; }

        /// <summary>
        /// The schema version of the method specific data, used in analysis, indicated by the consumers, who are responsible for incrementing the schema version with each schema change that is published. This property will not be returned if the expand option was not used.
        /// maxLength: 20
        /// nullable: true
        /// </summary>
        public string DefinitionSchemaVersion { get; private set; }





        public WatersConnectAcquisitionMethodObject(JObject jobject)
        {
            // ReSharper disable LocalizableElement

            //  Sub object in JSON
            var readOnlyPropertiesSubObject = jobject["readOnlyProperties"] as JObject;

            MethodId = GetProperty(readOnlyPropertiesSubObject, "methodVersionId");

            Id = MethodId; // Id is in parent class

            Name = GetProperty(jobject, "name");

            Description = GetProperty(jobject, "description");

            //  In readOnlyPropertiesSubObject

            MethodVersionId = GetProperty(readOnlyPropertiesSubObject, "methodVersionId");
            DataRecordTypeId = GetProperty(readOnlyPropertiesSubObject, "dataRecordTypeId");
            TypeName = GetProperty(readOnlyPropertiesSubObject, "typeName");
            FolderId = GetProperty(readOnlyPropertiesSubObject, "folderId");

            CreatedDateTime = GetDateProperty(readOnlyPropertiesSubObject, "createdDateTime");
            ModifiedDateTime = GetDateProperty(readOnlyPropertiesSubObject, "modifiedDateTime");

            Version = GetIntegerProperty(readOnlyPropertiesSubObject, "version");
            ModVersion = GetIntegerProperty(readOnlyPropertiesSubObject, "modVersion");

            CreatedById = GetProperty(readOnlyPropertiesSubObject, "createdById");
            CreatedByUsername = GetProperty(readOnlyPropertiesSubObject, "createdByUsername");
            CreatedByUserFullName = GetProperty(readOnlyPropertiesSubObject, "createdByUserFullName");

            ModifiedById = GetProperty(readOnlyPropertiesSubObject, "modifiedById");
            ModifiedByUsername = GetProperty(readOnlyPropertiesSubObject, "modifiedByUsername");
            ModifiedByUserFullName = GetProperty(readOnlyPropertiesSubObject, "modifiedByUserFullName");

            State = GetProperty(readOnlyPropertiesSubObject, "state");
            Locked = GetBooleanProperty(readOnlyPropertiesSubObject, "locked");

            //  Root jobject

            Definition = GetProperty(jobject, "definition");
            DefinitionSchemaVersion = GetProperty(jobject, "definitionSchemaVersion");

            // ReSharper restore LocalizableElement
        }

        public override WatersConnectUrl ToUrl(WatersConnectUrl currentConnectUrl)
        {
            return currentConnectUrl
                .ChangeType(WatersConnectUrl.ItemType.method)
                .ChangeFolderOrSampleSetId(Id);
        }
    }
}
