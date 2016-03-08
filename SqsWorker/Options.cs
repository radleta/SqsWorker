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
    }
}
