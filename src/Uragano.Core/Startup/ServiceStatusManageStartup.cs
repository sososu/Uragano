﻿using System.Threading;
using Microsoft.Extensions.Logging;
using Uragano.Abstractions;
using Uragano.Abstractions.ServiceDiscovery;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Uragano.Core.Startup
{
    public class ServiceStatusManageStartup : IHostedService
    {
        private IServiceStatusManageFactory ServiceStatusManageFactory { get; }

        private static System.Timers.Timer Timer { get; set; }

        private UraganoSettings UraganoSettings { get; }

        private ILogger Logger { get; }

        public ServiceStatusManageStartup(IServiceStatusManageFactory serviceStatusManageFactory, UraganoSettings uraganoSettings, ILogger<ServiceStatusManageStartup> logger)
        {
            ServiceStatusManageFactory = serviceStatusManageFactory;
            UraganoSettings = uraganoSettings;
            Logger = logger;
        }


        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (!UraganoSettings.IsDevelopment)
            {
                if (UraganoOptions.Client_Node_Status_Refresh_Interval.Value.Ticks > 0)
                {
                    Timer = new System.Timers.Timer(UraganoOptions.Client_Node_Status_Refresh_Interval.Value.TotalMilliseconds);
                    Timer.Elapsed += async (sender, args) =>
                    {
                        await ServiceStatusManageFactory.Refresh(cancellationToken);
                    };
                    Timer.Enabled = true;
                    //NOTE:Replace with Timer
                    //Task.Factory.StartNew(async () =>
                    //{
                    //    while (!CancellationTokenSource.IsCancellationRequested)
                    //    {
                    //        await ServiceStatusManageFactory.Refresh(CancellationTokenSource.Token);
                    //        if (CancellationTokenSource.IsCancellationRequested)
                    //            break;
                    //        await Task.Delay(UraganoOptions.Client_Node_Status_Refresh_Interval.Value,
                    //            CancellationTokenSource.Token);
                    //    }

                    //    Logger.LogTrace("Stop refreshing service status.");
                    //}, CancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);

                }
            }
            await Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            Logger.LogTrace("Stop refreshing service status.");
            Timer.Enabled = false;
            Timer.Dispose();
            await Task.CompletedTask;
        }
    }
}
