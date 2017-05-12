﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Queue;

namespace AccidentalFish.Commanding.AzureStorage
{
    /// <summary>
    /// Creates command queue processors for Azure storage that will listen on a queue and remove commands and pass them on for execution to
    /// the command framework
    /// </summary>
    public interface IAzureStorageCommandQueueProcessorFactory
    {
        Task Start<TCommand, TResult>(CloudQueue queue,
            CancellationToken cancellationToken,
            int maxDequeueCount = 10,
            Action<string> traceLogger = null) where TCommand : class;

        Task Start<TCommand, TResult>(CloudQueue queue,
            int maxDequeueCount = 10,
            Action<string> traceLogger = null) where TCommand : class;
    }
}
