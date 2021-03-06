﻿namespace LMS.Core.Logging
{
    using System.Collections.Generic;
    using Castle.Core.Logging;
    using Castle.MicroKernel.Registration;
    using Castle.Services.Logging.SerilogIntegration;
    using Serilog;
    using Serilog.Core;
    using Serilog.Sinks.RollingFileAlternate;

    public class LoggingConfiguration
    {
        public static IRegistration[] GetConfiguration(LoggingLevelSwitch loggingLevelSwitch)
        {
            return new List<IRegistration>
            {
                {
                    Component.For<LoggerConfiguration>()
                        .UsingFactoryMethod(
                            () => new LoggerConfiguration()
                                .MinimumLevel.ControlledBy(LMSCoreModule.CurrentLogLevel)
                                .WriteTo.ColoredConsole()
                                .WriteTo.RollingFileAlternate(".\\logs", fileSizeLimitBytes: 5242880)
                        ).LifestyleSingleton()
                },
                {
                    Component.For<ILoggerFactory, SerilogFactory>()
                        .ImplementedBy<SerilogFactory>()
                        .LifestyleSingleton()
                },
                {
                    Component.For<Castle.Core.Logging.ILogger>()
                        .UsingFactoryMethod(
                            (kernel, componentModel, creationContext) => kernel.Resolve<ILoggerFactory>().Create(creationContext.Handler.ComponentModel.Name)
                        ).LifestyleTransient()
                }
            }.ToArray();
        }
    }
}