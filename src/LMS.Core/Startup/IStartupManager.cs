﻿namespace LMS.Core.StartUp
{
    using Abp.Domain.Services;
    using global::Hangfire.Server;

    public interface IStartupManager : IDomainService
    {
        bool Init(PerformContext performContext);
        bool ShouldMonitorUsers(PerformContext performContext);
        bool MonitorVeeam(PerformContext performContext);
    }
}