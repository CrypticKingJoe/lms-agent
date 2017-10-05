﻿namespace Core.Startup
{
    using System;
    using Administration;
    using Common.Constants;
    using Common.Extensions;
    using Common.Helpers;
    using DirectoryServices;
    using NLog;
    using SharpRaven;
    using SharpRaven.Data;
    using Veeam;
    using Veeam.Managers;

    public class StartupManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        protected OData.PortalClient PortalClient = new OData.PortalClient();

        protected RavenClient RavenClient = Sentry.RavenClient.Instance;
        protected SettingManager SettingManager = new SettingManager();

        public bool Init()
        {
            Logger.Info("Running initialisation...");
            Console.WriteLine(Environment.NewLine);

            try
            {
                ValidateCredentials();
            }
            catch (Exception ex)
            {
                RavenClient.Capture(new SentryEvent(ex));
                Logger.Error(ex.Message);
            }

            Console.WriteLine(Environment.NewLine);

            try
            {
                bool monUsers = MonitorUsers();
                SettingManager.ChangeSetting(SettingNames.MonitorUsers, monUsers.ToString());
                Logger.Info(monUsers ? "Monitoring Users" : "Not Monitoring Users");
                Console.WriteLine(Environment.NewLine);
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
            }

            try
            {
                bool monVeeam = MonitorVeeam();
                SettingManager.ChangeSetting(SettingNames.MonitorVeeam, monVeeam.ToString());
                Logger.Info(monVeeam ? "Monitoring Veeam" : "Not Monitoring Veeam");
                Console.WriteLine(Environment.NewLine);
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
            }

            Logger.Info("************ Initialisation  Successful ************");
            return true;
        }

        private bool MonitorUsers()
        {
            Logger.Info("Dermining whether to monitor users...");
            var directoryServicesManager = new DirectoryServicesManager();
            try
            {
                bool userOverride = SettingManager.GetSettingValue<bool>(SettingNames.UsersOverride);
                if (userOverride)
                {
                    Logger.Warn("User monitoring has been manually disabled.");
                    return false;
                }

                // check if a domain exists
                bool domainExists = directoryServicesManager.DomainExist();
                if (!domainExists)
                {
                    Logger.Warn("Check Domain: FAIL");
                    return false;
                }

                Logger.Info("Check Domain: OK");

                // check if this is a primary domain controller
                bool pdc = directoryServicesManager.PrimaryDomainController();
                if (!pdc)
                {
                    Logger.Warn("Check PDC: FAIL");

                    // check override is enabled
                    bool pdcOverride = SettingManager.GetSettingValue<bool>(SettingNames.PrimaryDomainControllerOverride);
                    if (!pdcOverride)
                    {
                        Logger.Warn("Check PDC Override: FAIL");
                        return false;
                    }

                    Logger.Warn("Check PDC Override: OK");
                }

                Logger.Info("Check PDC: OK");
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                Logger.Debug(ex);
                return false;
            }

            return true;
        }

        private bool MonitorVeeam()
        {
            Logger.Info("Dermining whether to monitor veeam...");
            try
            {
                bool veeamOverride = SettingManager.GetSettingValue<bool>(SettingNames.VeeamOverride);
                if (veeamOverride)
                {
                    Logger.Warn("Veeam monitoring has been manually disabled.");
                    return false;
                }

                var veeamManager = new VeeamManager();

                // check if veeam is installed
                bool veeamInstalled = veeamManager.VeeamInstalled();
                if (!veeamInstalled)
                {
                    Logger.Warn("Check Veeam Installed: FAIL");
                    return false;
                }

                Logger.Info("Check Veeam Installed: OK");

                // check the veeam version
                string veeamVersion = veeamManager.VeeamVersion();
                if (veeamVersion == null)
                {
                    Logger.Warn("Check Veeam Version: FAIL");
                    return false;
                }

                Logger.Info("Check Veeam Version: OK");
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                Logger.Debug(ex);
                return false;
            }

            return true;
        }

        protected bool ValidateAutotask()
        {
            try
            {
                Guid deviceId = SettingManagerHelper.DeviceId;

                int accountId;
                int storedAccount = SettingManagerHelper.AccountId;

                if (storedAccount == default(int))
                {
                    int reportedAccount = PortalClient.GetAccountIdByDeviceId(deviceId);

                    if (reportedAccount == default(int))
                    {
                        Logger.Warn("Check Account: FAIL");
                        Logger.Error("Failed to get the autotask account id from the api. This application cannot work without the autotask account id. Please enter it manually through the menu system.");
                        return false;
                    }

                    SettingManager.ChangeSetting(SettingNames.AutotaskAccountId, reportedAccount.ToString());
                    accountId = reportedAccount.To<int>();
                }
                else
                {
                    accountId = storedAccount;
                }

                Logger.Info("Check Account: OK");
                Logger.Info($"Account: {accountId}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Warn("Check Account: FAIL");
                Logger.Error("Failed to get the autotask account id from the api. This application cannot work without the autotask account id. Please enter it manually through the menu system.");
                Logger.Error(ex.Message);
                Logger.Debug(ex);
                return false;
            }
        }

        protected bool ValidateCentraStage()
        {
            try
            {
                Guid deviceId;
                var storedDevice = SettingManager.GetSettingValue<Guid>(SettingNames.CentrastageDeviceId);
                if (storedDevice == default(Guid))
                {
                    Guid? reportedDevice = Constants.CentraStage.GetCentrastageId();

                    if (reportedDevice == null)
                    {
                        Logger.Warn("Check Centrastage: FAIL");
                        Logger.Error("Failed to get the centrastage device id from the registry. This application cannot work without the centrastage device id. Please enter it manually through the menu system.");
                        return false;
                    }

                    SettingManager.ChangeSetting(SettingNames.CentrastageDeviceId, reportedDevice.ToString());
                    deviceId = reportedDevice.To<Guid>();
                }
                else
                {
                    deviceId = storedDevice;
                }

                Logger.Info("Check Centrastage: OK");
                Logger.Info($"Device: {deviceId}");
                return true;
            }
            catch (Exception)
            {
                Logger.Warn("Check Centrastage: FAIL");
                Logger.Error("Failed to get the centrastage device id from the registry. This application cannot work without the centrastage device id. Please enter it manually through the menu system.");
                return false;
            }
        }

        public bool ValidateCredentials()
        {
            Logger.Info("Validating api credentials...");

            bool centraStage = ValidateCentraStage();
            return centraStage && ValidateAutotask();
        }
    }
}