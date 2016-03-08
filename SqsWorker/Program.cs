using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using CommandLine.Text;
using NLog.Config;

namespace SqsWorker
{
    class Program
    {
        /// <summary>
        /// The NLog logger.
        /// </summary>
        private static readonly Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        /// <param name="args">The parsable arguments into <see cref="Options"/>.</param>
        /// <returns>Errorcode. Success is <c>0</c>; otherwise, <c>1</c>.</returns>
        static int Main(string[] args)
        {
            try
            {
                var options = new Options();
                if (CommandLine.Parser.Default.ParseArguments(args, options))
                {
                    ConfigureLogging(options);
                    ConfigureAws(options);

                    using (var queue = new SqsWorkQueue(options))
                    {
                        queue.Work();
                    }
                }
                else
                {                    
                    Console.WriteLine(HelpText.AutoBuild(options));
                }
                return 0;
            }
            catch (Exception ex)
            {
                Logger.Fatal("An unhandled exception occured during execution. Exception: {0}", ex.ToString());
                return 1;
            }
        }

        /// <summary>
        /// Configures the AWS environment.
        /// </summary>
        /// <param name="options"></param>
        private static void ConfigureAws(Options options)
        {
            if (!string.IsNullOrEmpty(options.AwsRegion))
            {
                Amazon.AWSConfigs.AWSRegion = options.AwsRegion;
            }
            if (!string.IsNullOrEmpty(options.AwsProfileName))
            {
                Amazon.AWSConfigs.AWSProfileName = options.AwsProfileName;
            }
        }

        /// <summary>
        /// Configures the logging of the application.
        /// </summary>
        private static void ConfigureLogging(Options options)
        {
            if (options == null)
            {
                throw new System.ArgumentNullException("options");
            }

            if (LogManager.Configuration != null)
            {
                return;
            }

            var config = new LoggingConfiguration();

            // add console logging
            if (options.LogConsole)
            {
                var consoleTarget = new NLog.Targets.ColoredConsoleTarget()
                {
                    Name = "console",
                    Layout = "${message}",
                };
                config.AddTarget(consoleTarget);
                config.LoggingRules.Add(new LoggingRule("SqsWorker*", LogLevel.Info, consoleTarget));
            }

            // add file logging
            if (options.Log)
            {
                var fileTarget = new NLog.Targets.FileTarget()
                {
                    Name = "file",
                    Layout = "${longdate} ${logger} ${message}",
                    FileName = options.LogFile,
                    ArchiveFileName = options.LogArchiveFile,
                    ArchiveEvery = NLog.Targets.FileArchivePeriod.Day,
                    ArchiveNumbering = NLog.Targets.ArchiveNumberingMode.Rolling,
                    ArchiveAboveSize = 5242880,
                    ConcurrentWrites = true,
                    MaxArchiveFiles = 5,
                    KeepFileOpen = false,
                    Encoding = Encoding.UTF8,
                };
                config.AddTarget(fileTarget);
                config.LoggingRules.Add(new LoggingRule("*", LogLevel.FromString(options.LogLevel), fileTarget));
            }

            LogManager.Configuration = config;
        }
    }
}
