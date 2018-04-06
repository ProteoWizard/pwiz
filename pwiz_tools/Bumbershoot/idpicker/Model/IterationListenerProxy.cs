//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2014 Vanderbilt University
//
// Contributor(s):
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace IDPicker
{
    public class UpdateMessageProxy : CancelEventArgs
    {
        public int IterationCount { get; set; }
        public int IterationIndex { get; set; }
        public string Message { get; set; }
    }

    public class IterationListenerProxy<UpdateEventArgs> : pwiz.CLI.util.IterationListener where UpdateEventArgs : UpdateMessageProxy, new()
    {
        public event EventHandler<UpdateEventArgs> UpdateEvent;

        public IterationListenerProxy(EventHandler<UpdateEventArgs> updateEvent)
        {
            UpdateEvent = updateEvent;
        }

        public override Status update(UpdateMessage updateMessage)
        {
            if (UpdateEvent == null)
                return Status.Ok;

            var eventArgs = new UpdateEventArgs() { Message = updateMessage.message, IterationIndex = updateMessage.iterationIndex, IterationCount = updateMessage.iterationCount };
            UpdateEvent(UpdateEvent.Target, eventArgs);
            return eventArgs.Cancel ? Status.Cancel : Status.Ok;
        }
    }
}
