using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SqsWorker
{
    /// <summary>
    /// The command line options.
    /// </summary>
    class Options
    {
        [Option('q', "QueueUrl", Required = true, HelpText = "Required. The url of the SQS Queue. Example: https://sqs.us-east-1.amazonaws.com/0123456789/my-queue-name")]
        public string QueueUrl { get; set; }

        [Option('w', "WorkerUrl", Required = true, HelpText = "Required. The url of the worker to POST the SQS Messages. Expects 200 status on success. Example: https://localhost/api/queue")]
        public string WorkerUrl { get; set; }

        [Option('t', "WorkerTimeout", DefaultValue = 90000, HelpText = "The maximum amount of time to allow the worker to process the SQS Message in milliseconds.")]
        public int WorkerTimeout { get; set; }

        [Option('r', "ConcurrentReaders", DefaultValue = 1, HelpText = "The number of concurrent readers to use.")]
        public int ConcurrentReaders { get; set; }

        [Option('m', "MinimumReadThreashold", DefaultValue = 100, HelpText = "The minimum threashold before a reader will be run to get more items.")]
        public int MinimumReadThreashold { get; set; }
                
        [Option('p', "Poll", DefaultValue = 0, HelpText = "The interval in milliseconds to poll for more SQS Messages. A value of 0 equals no polling.")]
        public int Poll { get; set; }
        
        [Option('l', "Log", DefaultValue = false, HelpText = "Determines whether or not to write to the log file.")]
        public bool Log { get; set; }

        [Option('f', "LogFile", DefaultValue = "${basedir}/logs/log.txt", HelpText = "The log file to output.")]
        public string LogFile { get; set; }

        [Option('a', "LogArchiveFile", DefaultValue = "${basedir}/logs/archives/log.{#}.txt", HelpText = "The archive file name format.")]
        public string LogArchiveFile { get; set; }

        [Option('e', "LogLevel", DefaultValue = "Info",  HelpText = "The log level.")]
        public string LogLevel { get; set; }

        [Option("AwsRegion", HelpText = "The AWS Region to use when connecting through AWS SDK.")]
        public string AwsRegion { get; set; }

        [Option("AwsProfileName", HelpText = "The AWS Profile Name to use when connecting through AWS SDK.")]
        public string AwsProfileName { get; set; }

        [Option('c', "LogConsole", DefaultValue = false, HelpText = "Determines whether or not output is written to the console.")]
        public bool LogConsole { get; set; }

        [OptionArray("MessageAttributeNames", HelpText = "A list of attributes that need to be returned along with each message.")]
        public string[] MessageAttributeNames { get; set; }

        [OptionArray("AttributeNames", HelpText = "A list of attributes that need to be returned along with each message.")]
        public string[] AttributeNames { get; set; }
    }
}
