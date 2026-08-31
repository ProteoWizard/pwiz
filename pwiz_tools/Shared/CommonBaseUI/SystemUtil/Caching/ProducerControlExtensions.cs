/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Windows.Forms;

namespace pwiz.Common.SystemUtil.Caching
{
    /// <summary>
    /// The Control-bound half of <see cref="Producer{TParameter,TResult}"/>.
    ///
    /// <see cref="Receiver{TParam,TResult}"/> is genuinely tied to a Control lifetime - it
    /// hooks HandleDestroyed and marshals completions with BeginInvoke - so it lives here in
    /// CommonBaseUI. Producer itself is not: WorkOrder and ProductionFacility depend on it, and
    /// those have to stay in CommonUtil, which is WinForms-free. So the one factory method
    /// that named a Control is an extension method rather than a member.
    ///
    /// It is in the SAME namespace as Producer, so every existing call site
    /// (producer.RegisterCustomer(control, action)) compiles unchanged.
    /// </summary>
    public static class ProducerControlExtensions
    {
        public static Receiver<TParameter, TResult> RegisterCustomer<TParameter, TResult>(
            this Producer<TParameter, TResult> producer, Control ownerControl, Action productAvailableAction)
        {
            var customer = new Receiver<TParameter, TResult>(ProductionFacility.DEFAULT, ownerControl, producer);
            if (productAvailableAction != null)
            {
                customer.ProductAvailable += productAvailableAction;
            }

            return customer;
        }
    }
}
