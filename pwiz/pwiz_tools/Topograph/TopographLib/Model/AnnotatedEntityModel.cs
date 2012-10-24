/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using pwiz.Topograph.Data;

namespace pwiz.Topograph.Model
{
    public abstract class AnnotatedEntityModel<T> : EntityModel<T> where T : DbAnnotatedEntity<T>
    {
        private ValidationStatus _validationStatus;
        private String _note;
        protected AnnotatedEntityModel(Workspace workspace, T entity) : base(workspace, entity)
        {
        }
        protected override IEnumerable<ModelProperty> GetModelProperties()
        {
            foreach (var modelProperty in base.GetModelProperties())
            {
                yield return modelProperty;
            }
            yield return Property<AnnotatedEntityModel<T>,ValidationStatus>(
                m=>m._validationStatus, 
                (m,v)=>m._validationStatus = v, 
                e=>e.ValidationStatus, 
                (e,v)=>e.ValidationStatus = v);
            yield return Property<AnnotatedEntityModel<T>, string>(
                m=>m._note,
                (m,v)=>m._note = v,
                e=>e.Note,
                (e,v)=>e.Note = v
                );
        }


        public virtual ValidationStatus ValidationStatus
        {
            get { return _validationStatus;}
            set { 
                SetIfChanged(ref _validationStatus, value);
            }
        }
        public String Note
        {
            get { return _note;}
            set
            {
                SetIfChanged(ref _note, value);
            }
        }
    }
}
