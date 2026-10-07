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
        public static void Configure(HostBuilderContext context, LoggerConfiguration config)
        {
            config
                .MinimumLevel.Debug()
                .ReadFrom.Configuration(BuildConfiguration(context.HostingEnvironment.EnvironmentName))
                .Enrich.WithMachineName()
                .Enrich.FromLogContext();
        }

        internal static IConfiguration BuildConfiguration(string environment)
        {
            var builder = new ConfigurationBuilder();

            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
                builder.SetBasePath(Directory.GetCurrentDirectory());

            builder
                .AddJsonFile($"serilog.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"serilog.{environment}.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();

            return builder.Build();
        }
    }

    public static class Start
    {
        public static void WithStartupDiagnostics(string applicationName, Action programExecution)
        {
            Serilog.Debugging.SelfLog.Enable(Console.Error);

            var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT", EnvironmentVariableTarget.Process);
            Serilog.Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(SerilogConfiguration.BuildConfiguration(environment))

                .Enrich.WithMachineName()
                .Enrich.FromLogContext()
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
                Serilog.Log.CloseAndFlush();
            }
        }
    }
        public class ServerInfo
        {
            public ServerInfo()
            {
                Environment = $"{System.Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT", EnvironmentVariableTarget.Process)} {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}";
                TimeZone = TimeZoneInfo.Local.ToSerializedString();
                OS = $"{System.Runtime.InteropServices.RuntimeInformation.OSDescription} {System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}";
                Framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
            }

            public string Environment { get; set; }

            public string TimeZone { get; set; }

            public string OS { get; set; }

            public string Framework { get; set; }

            public override string ToString()
            {
                StringBuilder buffer = new StringBuilder();
                buffer.AppendLine($"Environment: {Environment}");
                buffer.AppendLine($"OS: {OS}");
                buffer.AppendLine($"Framework: {Framework}");
                buffer.AppendLine($"TimeZone: {TimeZone}");

                return buffer.ToString();
            }
        }
}
