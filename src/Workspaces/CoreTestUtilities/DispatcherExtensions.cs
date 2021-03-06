﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows.Threading;

namespace Roslyn.Test.Utilities
{
    public static class DispatcherExtensions
    {
        public static void DoEvents(this Dispatcher dispatcher)
        {
            // A DispatcherFrame represents a loop that processes pending work
            // items.
            var frame = new DispatcherFrame();
            var callback = (Action<DispatcherFrame>)(f => f.Continue = false);

            // Executes the specified delegate asynchronously.  When it is 
            // complete mark the frame as complete so the dispatcher loop
            // pops out (stops).
#pragma warning disable VSTHRD001 // Avoid legacy thread switching APIs
            var operation = dispatcher.BeginInvoke(
#pragma warning restore VSTHRD001 // Avoid legacy thread switching APIs
                DispatcherPriority.ApplicationIdle, callback, frame);

            // Start the loop.  It will process all items in the queue, then 
            // will process the above callback.  That callback will tell the
            // loop to then stop processing.
            Dispatcher.PushFrame(frame);

            if (operation.Status != DispatcherOperationStatus.Completed)
            {
                operation.Abort();
            }
        }
    }
}
