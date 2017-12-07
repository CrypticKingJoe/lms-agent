﻿namespace LMS.Service
{
    using System.Reflection;
    using Abp.Configuration;
    using Abp.Dependency;
    using Abp.Hangfire;
    using Abp.Hangfire.Configuration;
    using Abp.Modules;
    using Castle.Facilities.Logging;
    using global::Hangfire;
    using global::Hangfire.Common;
    using global::Hangfire.Console;
    using global::Hangfire.MemoryStorage;
    using Hangfire;
    using LMS.Startup;
    using Users;
    using Veeam;

    [DependsOn(typeof(LMSEntityFrameworkModule), typeof(AbpHangfireModule))]
    public class LMSServiceModule : AbpModule
    {
        public override void Initialize()
        {
            IocManager.RegisterAssemblyByConvention(Assembly.GetExecutingAssembly());
        }

        public override void PreInitialize()
        {
            IocManager.IocContainer.AddFacility<LoggingFacility>(f => f.UseLog4Net().WithConfig("log4net.config"));

            Configuration.BackgroundJobs.UseHangfire(config =>
            {
                config.GlobalConfiguration.UseMemoryStorage();
                config.Server = new BackgroundJobServer();
                config.GlobalConfiguration.UseConsole();
            });
        }

        public override void PostInitialize()
        {
            GlobalJobFilters.Filters.Add(new DisableMultipleQueuedItemsFilter());
            ConfigureHangfireJobs();
        }

        private void ConfigureHangfireJobs()
        {
            var recurringJobManager = new RecurringJobManager();

            recurringJobManager.RemoveIfExists(BackgroundJobNames.Users);
            recurringJobManager.RemoveIfExists(BackgroundJobNames.Veeam);

            using (var startupManager = IocManager.ResolveAsDisposable<IStartupManager>())
            {
                if (startupManager.Object.MonitorUsers())
                {
                    recurringJobManager.AddOrUpdate(BackgroundJobNames.Users, Job.FromExpression<IUserWorkerManager>(j => j.Start()), "*/15 * * * *");
                }

                if (startupManager.Object.MonitorVeeam())
                {
                    recurringJobManager.AddOrUpdate(BackgroundJobNames.Veeam, Job.FromExpression<IVeeamWorkerManager>(j => j.Start()), "*/15 * * * *");
                }
            }
        }

        
    }

    public class BackgroundJobNames
    {
        public const string Users = "Users";
        public const string Veeam = "Veeam";
    }
}
