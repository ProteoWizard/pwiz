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
using System.Linq;
using System.Text;
using pwiz.Topograph.Data;
using NHibernate;

namespace pwiz.Topograph.Model
{
    public abstract class AnnotatedEntityModel<T> : EntityModel<T> where T : DbAnnotatedEntity<T>
    {
        private ValidationStatus _validationStatus;
        private String _note;
        protected AnnotatedEntityModel(Workspace workspace, T entity) : base(workspace, entity)
        {
        }
        protected override void Load(T entity)
        {
            base.Load(entity);
            _validationStatus = entity.ValidationStatus;
            _note = entity.Note;
        }

        protected override T UpdateDbEntity(ISession session)
        {
            T result = base.UpdateDbEntity(session);
            result.ValidationStatus = _validationStatus;
            result.Note = _note;
            return result;
        }

        public ValidationStatus ValidationStatus
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
