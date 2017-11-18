﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AccidentalFish.Commanding;
using AccidentalFish.Commanding.Abstractions;
using AccidentalFish.Commanding.AzureStorage;
using AccidentalFish.Commanding.AzureStorage.Strategies;
using AccidentalFish.Commanding.Queue;
using AccidentalFish.DependencyResolver.MicrosoftNetStandard;
using AzureStorageAuditing.Actors;
using AzureStorageAuditing.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;

namespace AzureStorageAuditing
{
    class Program
    {
       // private const string StorageAccountConnectionString =
       //     "YOUR-STORAGE-ACCOUNT-STRING";

        static void Main(string[] args)
        {
            Task dequeueTask;
            CancellationTokenSource source = new CancellationTokenSource();
            do
            {
                Console.WriteLine("1. Use single table storage strategy");
                Console.WriteLine("2. Use per day table storage strategy");
                Console.WriteLine("3. Use queue auditor (enqueue)");
                Console.WriteLine("4. Launch queue auditor processor (dequeue)");
                ConsoleKeyInfo info = Console.ReadKey();
                
                if (info.Key == ConsoleKey.D1 || info.Key <= ConsoleKey.D2)
                {
                    IStorageStrategy storageStrategy = info.Key == ConsoleKey.D1 ? (IStorageStrategy)new SingleTableStrategy() : new TablePerDayStrategy();

#pragma warning disable 4014
                    RunDemo(storageStrategy);
#pragma warning restore 4014
                }
                else if (info.Key == ConsoleKey.D3)
                {
#pragma warning disable 4014
                    RunQueueDemo();
#pragma warning restore 4014
                }
                else if (info.Key == ConsoleKey.D4)
                {
                    dequeueTask = RunDequeueDemo(source.Token);
                }
                else if (info.Key == ConsoleKey.Escape)
                {
                    break;
                }
                Console.ReadKey();
            } while (true);
            source.Cancel();
            Thread.Sleep(500);
        }

        private static async Task RunDemo(IStorageStrategy storageStrategy)
        {
            ICommandDispatcher dispatcher = Configure(storageStrategy);
            await dispatcher.DispatchAsync(new ChainCommand());
        }

        private static int _counter = -1;

        private static ICommandDispatcher Configure(IStorageStrategy storageStrategy)
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.DevelopmentStorageAccount;
            
            MicrosoftNetStandardDependencyResolver resolver = new MicrosoftNetStandardDependencyResolver(new ServiceCollection());
            IReadOnlyDictionary<string, object> Enricher(IReadOnlyDictionary<string, object> existing) => new Dictionary<string, object> { { "ExampleEnrichedCounter", Interlocked.Increment(ref _counter) } };
            Options options = new Options
            {
                CommandActorContainerRegistration = type => resolver.Register(type, type),
                Reset = true, // we reset the registry because we allow repeat runs, in a normal app this isn't required                
                Enrichers = new [] { new FunctionWrapperCommandDispatchContextEnricher(Enricher) }
            };            

            resolver.UseCommanding(options)
                .Register<ChainCommand, ChainCommandActor>()
                .Register<OutputToConsoleCommand, OutputWorldToConsoleCommandActor>();
            resolver.UseAzureStorageCommandAuditing(storageAccount, storageStrategy: storageStrategy);
            resolver.BuildServiceProvider();
            return resolver.Resolve<ICommandDispatcher>();
        }

        private static async void RunQueueDemo()
        {
            ICommandDispatcher dispatcher = await ConfigureEnqueue();
            await dispatcher.DispatchAsync(new ChainCommand());
        }

        private static async Task<ICommandDispatcher> ConfigureEnqueue()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.DevelopmentStorageAccount;
            CloudQueue auditQueue = storageAccount.CreateCloudQueueClient().GetQueueReference("auditqueue");
            await auditQueue.CreateIfNotExistsAsync();

            MicrosoftNetStandardDependencyResolver resolver = new MicrosoftNetStandardDependencyResolver(new ServiceCollection());
            IReadOnlyDictionary<string, object> Enricher(IReadOnlyDictionary<string, object> existing) => new Dictionary<string, object> { { "ExampleEnrichedCounter", Interlocked.Increment(ref _counter) } };
            Options options = new Options
            {
                CommandActorContainerRegistration = type => resolver.Register(type, type),
                Reset = true, // we reset the registry because we allow repeat runs, in a normal app this isn't required                
                Enrichers = new[] { new FunctionWrapperCommandDispatchContextEnricher(Enricher) }
            };

            resolver.UseCommanding(options)
                .Register<ChainCommand, ChainCommandActor>()
                .Register<OutputToConsoleCommand, OutputWorldToConsoleCommandActor>();
            resolver.UseAzureStorageCommandAuditing(auditQueue);
            resolver.BuildServiceProvider();
            return resolver.Resolve<ICommandDispatcher>();
        }

        private static async Task<Task> RunDequeueDemo(CancellationToken cancellationToken)
        {
            IAzureStorageAuditQueueProcessorFactory factory = await ConfigureDequeue(new TablePerDayStrategy());
            return factory.Start(cancellationToken);
        }

        private static async Task<IAzureStorageAuditQueueProcessorFactory> ConfigureDequeue(IStorageStrategy storageStrategy)
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.DevelopmentStorageAccount;
            CloudQueue auditQueue = storageAccount.CreateCloudQueueClient().GetQueueReference("auditqueue");
            await auditQueue.CreateIfNotExistsAsync();

            MicrosoftNetStandardDependencyResolver resolver = new MicrosoftNetStandardDependencyResolver(new ServiceCollection());

            resolver.UseCommanding();
            resolver.UseQueues();
            resolver.UseAzureStorageCommandAuditing(storageAccount, storageStrategy: storageStrategy); // this sets up the table store auditors
            resolver.UseAzureStorageAuditQueueProcessor(auditQueue); // this sets up queue listening
            resolver.BuildServiceProvider();

            return resolver.Resolve<IAzureStorageAuditQueueProcessorFactory>();
        }
    }
}