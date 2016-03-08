using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using NLog;
using Amib.Threading;
using System.Threading;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Diagnostics;

namespace SqsWorker
{
    /// <summary>
    /// The work queue for <see cref="Amazon.SQS.Model.Message"/>.
    /// </summary>
    class SqsWorkQueue : WorkQueue<Amazon.SQS.Model.Message>
    {
        /// <summary>
        /// The logger for this class.
        /// </summary>
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Initalizes a new instnace of the <see cref="QueuedAlertBatchWorkQueue"/>.
        /// </summary>
        /// <param name="options">The options.</param>
        public SqsWorkQueue(Options options) : base(options.ConcurrentReaders, options.MinimumReadThreashold)
        {
            if (options == null)
            {
                throw new System.ArgumentNullException("options");
            }

            this._options = options;

            // perform the deletes in batches of 10
            this._deleteMessageCargo = new Cargo<Amazon.SQS.Model.Message>(this.DeleteMessageBatch, 10);
        }

        /// <summary>
        /// The options.
        /// </summary>
        private readonly Options _options;

        /// <summary>
        /// The cargo to transmit the queue status updates in batch.
        /// </summary>
        private readonly Cargo<Amazon.SQS.Model.Message> _deleteMessageCargo;

        /// <summary>
        /// The total number of alerts read so far.
        /// </summary>
        private int _readTotal;

        /// <summary>
        /// The total number of alerts improved.
        /// </summary>
        private int _writeTotal;

        /// <summary>
        /// The total number of alerts improved.
        /// </summary>
        private int _successTotal;

        /// <summary>
        /// The total number of alerts errored.
        /// </summary>
        private int _deleteTotal;

        /// <summary>
        /// The total number of batches read so far.
        /// </summary>
        private int _errorTotal;
        
        /// <summary>
        /// Does the work based on the options.
        /// </summary>
        public override void Work()
        {
            do
            {
                // do the core work
                base.Work();

                // wait for all the updates to finish
                _deleteMessageCargo.WaitForIdle();
                
                // when polling then let's sleep until the next interval
                if (_options.Poll > 0)
                {
                    // IF we're suppose to poll
                    // THEN let's sleep until the next interval

                    Thread.Sleep(_options.Poll);
                }

                // keep looping when polling is enabled
            } while (_options.Poll > 0);
        }

        /// <summary>
        /// Reads more <see cref="Amazon.SQS.Model.Message"/>.
        /// </summary>
        /// <returns>A list of <see cref="Amazon.SQS.Model.Message"/>.</returns>
        protected override List<Amazon.SQS.Model.Message> Read()
        {
            using (var sqs = new Amazon.SQS.AmazonSQSClient())
            {
                var response = sqs.ReceiveMessage(new Amazon.SQS.Model.ReceiveMessageRequest()
                {
                    QueueUrl = _options.QueueUrl,
                    MaxNumberOfMessages = 10,
                });

                Interlocked.Add(ref _readTotal, response.Messages.Count);

                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug("Received messages. Count: {0}", response.Messages.Count);
                }

                return response.Messages;
            }
        }

        /// <summary>
        /// Tries to improve the alert batch.
        /// </summary>
        /// <param name="batch">The batch.</param>
        protected override void Write(Amazon.SQS.Model.Message message)
        {
            if (message == null)
            {
                throw new System.ArgumentNullException("message");
            }

            // keep track of stats
            Interlocked.Increment(ref _writeTotal);
            
            // create the json payload for the message
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(message);

            // submit the json to the worker
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromMilliseconds(_options.WorkerTimeout);
                client.BaseAddress = new Uri(_options.WorkerUrl);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                var sw = new Stopwatch();
                sw.Start();
                var response = Task.Run(async () => await client.PostAsync("", new StringContent(json, Encoding.UTF8, "application/json"))).Result;
                sw.Stop();
                if (response.IsSuccessStatusCode)
                {
                    // keep track of stats
                    Interlocked.Increment(ref _successTotal);
                    
                    if (Logger.IsDebugEnabled)
                    {
                        Logger.Debug("Message succeed. MessageId: {0}, Elapsed: {1}", message.MessageId, sw.Elapsed);
                    }

                    // delete the message because it was successfully processed
                    _deleteMessageCargo.Push(message);
                }
                else
                {
                    // keep track of stats
                    Interlocked.Increment(ref _errorTotal);

                    Logger.Error("Message errored. MessageId: {0}, StatusCode: {1}, ReasonPhrase: {2}, Elapsed: {3}", message.MessageId, response.StatusCode, response.ReasonPhrase, sw.Elapsed);
                }
            }
        }

        /// <summary>
        /// Deletes the list of messages from SQS.
        /// </summary>
        /// <param name="messages">The messages to delete.</param>
        private void DeleteMessageBatch(List<Amazon.SQS.Model.Message> messages)
        {
            if (messages == null)
            {
                throw new System.ArgumentNullException("messages");
            }

            using (var sqs = new Amazon.SQS.AmazonSQSClient())
            {
                sqs.DeleteMessageBatch(new Amazon.SQS.Model.DeleteMessageBatchRequest()
                {
                    QueueUrl = _options.QueueUrl,
                    Entries = messages.Select(ToDeleteMessageBatchRequestEntry).ToList(),
                });
            }

            // keep track of stats
            Interlocked.Add(ref _deleteTotal, messages.Count);
        }

        /// <summary>
        /// Converts the <see cref="Amazon.SQS.Model.Message"/> to <see cref="Amazon.SQS.Model.DeleteMessageBatchRequestEntry"/>.
        /// </summary>
        /// <param name="message">The message to convert.</param>
        /// <returns>The <see cref="Amazon.SQS.Model.DeleteMessageBatchRequestEntry"/>.</returns>
        private Amazon.SQS.Model.DeleteMessageBatchRequestEntry ToDeleteMessageBatchRequestEntry(Amazon.SQS.Model.Message message)
        {
            if (message == null)
            {
                throw new System.ArgumentNullException("message");
            }

            return new Amazon.SQS.Model.DeleteMessageBatchRequestEntry()
            {
                Id = message.MessageId,
                ReceiptHandle = message.ReceiptHandle,
            };
        }
    }
}
