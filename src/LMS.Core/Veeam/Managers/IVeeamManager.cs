﻿namespace LMS.Core.Veeam.Managers
{
    using Abp.Domain.Services;
    using global::Hangfire.Server;
    using Portal.LicenseMonitoringSystem.Veeam.Entities;

    public interface IVeeamManager: IDomainService
    {
        Veeam GetLicensingInformation(PerformContext performContext);

        bool IsInstalled(PerformContext performContext);

        string GetVersion();
        bool IsOnline();
    }
}
