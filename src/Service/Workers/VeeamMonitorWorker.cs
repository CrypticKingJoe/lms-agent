﻿namespace Service.Workers
{
    using System;
    using Core.Common.Extensions;
    using Core.Veeam;
    using Core.Veeam.Managers;
    using ServiceTimer;

    internal class VeeamMonitorWorker : TimerWorker
    {
        protected VeeamManager VeeamManager;

        /// <inheritdoc />
        /// <summary>
        ///     30 second start up delay
        ///     10 second check
        ///     180*10/60 = 30 min execute
        /// </summary>
        internal VeeamMonitorWorker()
            : base(30000, 10000, 120)
        {
            FailedMessage = "************ Veeam Monitoring Failed ************";
            SuccessMessage = "************ Veeam Monitoring Successful ************";
            VeeamManager = new VeeamManager();
        }

        /// <inheritdoc />
        protected override void StartWork(TimerWorkerInfo info)
        {
        }

        /// <inheritdoc />
        protected override void Work(TimerWorkerInfo info)
        {
            RavenClient.AddTag("operation", "veeam");

            Logger.Info("Veeam monitoring begin...");

            if (!StartupManager.ValidateCredentials())
            {
                return;
            }

            bool veeamOnline = VeeamManager.VeeamOnline();
            if (!veeamOnline)
            {
                Logger.Error("Cannot contact the Veeam server. Please make sure all the Veeam services are started.");
                Logger.Error("We cannot go on like this.");
                Logger.Error(FailedMessage);
                return;
            }

            using (var orchestrator = new VeeamOrchestrator())
            {
                orchestrator.Start();
            }

            Console.WriteLine(Environment.NewLine);
            Logger.Info(SuccessMessage);
        }
    }
}