using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.IO;
using System.Text;

namespace Logging
{
    public static class SerilogConfiguration
    {
        private const string ApplicationNamePropertyName = "Application";

        public static void Configure(HostBuilderContext context, LoggerConfiguration config)
        {
            config
            .MinimumLevel.Debug()
            .ReadFrom.Configuration(BuildConfiguration(context.HostingEnvironment.EnvironmentName))
            .Enrich.WithProperty(ApplicationNamePropertyName, context.HostingEnvironment.ApplicationName)
            .Enrich.WithMachineName()
            .Enrich.FromLogContext();
        }

        internal static IConfiguration BuildConfiguration(string environment)
        {
            var builder = new ConfigurationBuilder();

            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
                builder.SetBasePath(Directory.GetCurrentDirectory());

            builder.AddJsonFile("serilog.json", optional: false, reloadOnChange: true);

            // Sinks are defined per environment (serilog.Development.json, serilog.Production.json, ...)
            if (!string.IsNullOrWhiteSpace(environment))
                builder.AddJsonFile($"serilog.{environment}.json", optional: true, reloadOnChange: true);

            // Environment variables override the JSON files (used by Kubernetes to set the log path)
            builder.AddEnvironmentVariables();

            return builder.Build();
        }

    }

    public static class Start
    {
        private const string ApplicationNamePropertyName = "Application";

        public static void WithStartupDiagnostics(string applicationName, Action programExecution)
        {
            Serilog.Debugging.SelfLog.Enable(TextWriter.Synchronized(Console.Error));

            // The bootstrap logger only writes to the console. It must not open the log file:
            // the real logger (SerilogConfiguration.Configure) opens the same rolling file, and two
            // writers on one file collide (the second one rolls to app-<date>_001.json).

            var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

            Serilog.Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(SerilogConfiguration.BuildConfiguration(environment))
                .Enrich.WithProperty(ApplicationNamePropertyName, applicationName)
                .Enrich.WithMachineName()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateBootstrapLogger();

            try
            {
                Serilog.Log.Information(new ServerInfo().ToString());

                Serilog.Log.Information($"Starting {applicationName} host...");

                programExecution();

                Serilog.Log.Information($"Host {applicationName} has stopped gracefully!");
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"{applicationName} host terminated unexpectedly!");
                Environment.ExitCode = 1;
            }
            finally
            {
                // Drains the async queue and flushes the file buffer before the process exits
                Serilog.Log.CloseAndFlush();
            }
        }

        public class ServerInfo
        {
            public ServerInfo()
            {
                Environment = $"{System.Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT", EnvironmentVariableTarget.Process)} {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}";
                LocalTime = DateTimeOffset.Now.ToString();
                TimeZone = TimeZoneInfo.Local.ToSerializedString();
                OS = $"{System.Runtime.InteropServices.RuntimeInformation.OSDescription} {System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}";
                Framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
            }

            public string Environment { get; set; }

            public string LocalTime { get; set; }

            public string TimeZone { get; set; }

            public string OS { get; set; }

            public string Framework { get; set; }

            public override string ToString()
            {
                StringBuilder buffer = new StringBuilder();
                buffer.AppendLine($"Environment: {Environment}");
                buffer.AppendLine($"OS: {OS}");
                buffer.AppendLine($"Framework: {Framework}");
                buffer.AppendLine($"LocalTime: {LocalTime}");

                return buffer.ToString();
            }
        }

    }
}
