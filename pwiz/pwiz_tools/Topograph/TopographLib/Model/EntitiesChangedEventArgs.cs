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
using pwiz.Common.Collections;
using pwiz.Topograph.Data;

namespace pwiz.Topograph.Model
{
    public class EntitiesChangedEventArgs
    {
        readonly Dictionary<EntityModel,EntityModel> allEntities
            = new Dictionary<EntityModel, EntityModel>();
        
        readonly Dictionary<EntityModel,EntityModel> newEntities 
            = new Dictionary<EntityModel,EntityModel>();

        private readonly Dictionary<EntityModel, EntityModel> changedEntities
            = new Dictionary<EntityModel, EntityModel>();

        private readonly Dictionary<EntityModel, EntityModel> removedEntities
            = new Dictionary<EntityModel, EntityModel>();

        private readonly Dictionary<long, DbPeptideAnalysis> changedPeptideAnalyses
            = new Dictionary<long, DbPeptideAnalysis>();

        private bool readOnly = false;

        private Dictionary<Type, List<EntityModel>> entitiesByType;

        private void CheckReadOnly()
        {
            if (readOnly)
            {
                throw new InvalidOperationException("Event is read-only");
            }
        }
        
        public void AddNewEntity(EntityModel entityModel)
        {
            lock(this)
            {
                CheckReadOnly();
                allEntities[entityModel] = entityModel;
                newEntities[entityModel] = entityModel;
            }
        }
        public void AddChangedEntity(EntityModel entityModel)
        {
            lock(this)
            {
                CheckReadOnly();
                allEntities[entityModel] = entityModel;
                changedEntities[entityModel] = entityModel;
            }
        }
        public void RemoveEntity(EntityModel entityModel)
        {
            lock(this)
            {
                CheckReadOnly();
                allEntities[entityModel] = entityModel;
                removedEntities[entityModel] = entityModel;
                newEntities.Remove(entityModel);
                changedEntities.Remove(entityModel);
            }
        }
        public void AddChangedPeptideAnalyses(IEnumerable<KeyValuePair<long, DbPeptideAnalysis>> dict)
        {
            lock(this)
            {
                CheckReadOnly();
                foreach (var entry in dict)
                {
                    changedPeptideAnalyses[entry.Key] = entry.Value;
                }
            }
        }

        public void SetReadOnly()
        {
            lock(this)
            {
                if (readOnly)
                {
                    return;
                }
                entitiesByType = new Dictionary<Type, List<EntityModel>>();
                foreach (EntityModel entityModel in allEntities.Values)
                {
                    List<EntityModel> list;
                    if (!entitiesByType.TryGetValue(entityModel.GetType(), out list))
                    {
                        list = new List<EntityModel>();
                        entitiesByType.Add(entityModel.GetType(), list);
                    }
                    list.Add(entityModel);
                }
                readOnly = true;
            }
        }
        public ICollection<EntityModel> GetAllEntities()
        {
            return allEntities.Values;
        }
        public ICollection<T> GetEntities<T>() where T : EntityModel
        {
            List<EntityModel> list;
            if (!entitiesByType.TryGetValue(typeof(T), out list))
            {
                return new List<T>(0);
            }
            var result = new List<T>();
            foreach (T e in list)
            {
                result.Add(e);
            }
            return result;
        }
        public ICollection<EntityModel> GetChangedEntities()
        {
            return changedEntities.Values;
        }
        public ICollection<EntityModel> GetRemovedEntities()
        {
            return removedEntities.Values;
        }
        public ICollection<EntityModel> GetNewEntities()
        {
            return newEntities.Values;
        }
        public bool IsRemoved(EntityModel e)
        {
            return removedEntities.ContainsKey(e);
        }
        public bool IsNew(EntityModel e)
        {
            return newEntities.ContainsKey(e);
        }
        public bool IsChanged(EntityModel e)
        {
            return changedEntities.ContainsKey(e);
        }
        public bool Contains(EntityModel e)
        {
            return allEntities.ContainsKey(e);
        }
        public bool ContainsAny<T>(IEnumerable<T> entities) where T:EntityModel
        {
            foreach (var entity in entities)
            {
                if (allEntities.ContainsKey(entity))
                {
                    return true;
                }
            }
            return false;
        }
        public IDictionary<long, DbPeptideAnalysis> GetChangedPeptideAnalyses()
        {
            return new ImmutableDictionary<long, DbPeptideAnalysis>(changedPeptideAnalyses);
        }
    }
}
