using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Threading;

namespace SqsWorker
{
    /// <summary>
    /// Processes items in batches defined by the payload.
    /// </summary>
    /// <typeparam name="TItem">The type of item to process.</typeparam>
    public class Cargo<TItem>
    {
        /// <summary>
        /// The state of a queued item.
        /// </summary>
        private class QueuedItem
        {
            /// <summary>
            /// The item.
            /// </summary>
            public TItem Item;

            /// <summary>
            /// The completion task.
            /// </summary>
            public Task<TItem> Completed;

            /// <summary>
            /// The exception thrown when work was attempted.
            /// </summary>
            public Exception ThrownException;
        }

        /// <summary>
        /// Instaniates a new instance of this class.
        /// </summary>
        /// <param name="worker">The work to call to process the items.</param>
        /// <param name="payload">The amount of items to process per call. Less than zero is infinite.</param>
        /// <param name="concurrency">The amount of workers to run at the same time. Less than zero is infinite.</param>
        public Cargo(Action<List<TItem>> worker, int payload = 0, int concurrency = 0)
        {
            if (worker == null)
            {
                throw new System.ArgumentNullException("worker");
            }

            _worker = worker;
            Payload = payload;
            Concurrency = concurrency;
        }

        /// <summary>
        /// The worker called to process lists of <c>TItem</c>.
        /// </summary>
        private readonly Action<List<TItem>> _worker;

        /// <summary>
        /// Sync lock to ensure we don't step on ourselves.
        /// </summary>
        private readonly object _syncLock = new object();

        /// <summary>
        /// Triggered when the cargo is idle.
        /// </summary>
        private readonly AutoResetEvent _idle = new AutoResetEvent(false);

        /// <summary>
        /// The number of active workers.
        /// </summary>
        private int _activeWorkers = 0;

        /// <summary>
        /// The cargo awaiting work.
        /// </summary>
        private List<QueuedItem> _cargo = new List<QueuedItem>();

        /// <summary>
        /// The amount of tasks to be processed per round. Less than zero is infinite.
        /// </summary>
        public int Payload { get; set; }

        /// <summary>
        /// The maximum amount of concurrency to allow. Less than zero is infinite.
        /// </summary>
        public int Concurrency { get; set; }

        /// <summary>
        /// Pushes a new <c>item</c> into the cargo.
        /// </summary>
        /// <param name="item">The item to work on.</param>
        /// <returns>The completion task called when the item has completed work.</returns>
        public Task<TItem> Push(TItem item)
        {
            lock (_syncLock)
            {
                // add our item to the queue
                var queuedItem = new QueuedItem()
                {
                    Item = item,
                };

                // assign the completed task
                queuedItem.Completed = new Task<TItem>(() =>
                {
                    if (queuedItem.ThrownException != null)
                    {
                        throw queuedItem.ThrownException;
                    }
                    return item;
                });

                // enqueue
                _cargo.Add(queuedItem);

                // determine whether we've exceeded the payload
                if (_activeWorkers == 0
                    || Payload < 1
                    || _cargo.Count >= Payload)
                {
                    // IF no active workers
                    //   OR payload is set to infinite (less than 1)
                    //   OR we've exceed the payload
                    // THEN start another worker

                    StartWorker();
                }

                return queuedItem.Completed;
            }
        }

        /// <summary>
        /// Blocks until the cargo is idle.
        /// </summary>
        public void WaitForIdle()
        {
            while (true)
            {
                // figure out whether we need to wait or not
                bool needToWait;
                lock (_syncLock)
                {
                    needToWait = (_activeWorkers > 0 || _cargo.Count > 0);
                }

                // decide whether to wait or not for idle
                if (needToWait)
                {
                    _idle.WaitOne();
                }
                else
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Actions to perform when a worker is completed work.
        /// </summary>
        private void WorkerCompleted()
        {
            lock (_syncLock)
            {
                // worker completed
                _activeWorkers--;

                // determine whether to start more work
                if ((Concurrency < 1 || _activeWorkers < Concurrency)
                    && _cargo.Count > 0)
                {
                    // kick it off
                    StartWorker();
                }
                else if (_activeWorkers == 0 && _cargo.Count == 0)
                {
                    // IF no active workers
                    //   AND no cargo
                    // THEN we're idle

                    // notify
                    _idle.Set();
                }
            }
        }

        /// <summary>
        /// Starts work with the proper completion task.
        /// </summary>
        /// <returns>The task.</returns>
        private Task StartWorker()
        {
            lock (_syncLock)
            {
                // starting another worker
                _activeWorkers++;
            }

            // do the task
            return Task.Run(new Action(Work))
                .ContinueWith(task => WorkerCompleted());
        }

        /// <summary>
        /// Processes the queued items.
        /// </summary>
        private void Work()
        {
            // figure out the items we need to work on
            List<QueuedItem> thisPayload;
            lock (_syncLock)
            {
                if (_cargo.Count == 0)
                {
                    // IF no cargo
                    // THEN bail because nothing to do
                    return;
                }
                else if (Payload > 0 && _cargo.Count > Payload)
                {
                    // IF payload is set
                    //   AND cargo exceeds the payload
                    // THEN slice out the payload

                    thisPayload = _cargo.GetRange(0, Payload);
                    _cargo.RemoveRange(0, Payload);
                }
                else
                {
                    // ELSE we can absorbe all the cargo
                    thisPayload = _cargo;
                    _cargo = new List<QueuedItem>();
                }
            }

            // do the work on the items
            var workableItems = thisPayload.Select(q => q.Item).ToList();
            Exception thrownException = null;
            try
            {
                _worker(workableItems);
            }
            catch (Exception ex)
            {
                thrownException = ex;
            }

            // iterate through all the payload and start the completion tasks
            foreach (var queuedItem in thisPayload)
            {
                queuedItem.ThrownException = thrownException;

                // start the completed tasks
                queuedItem.Completed.Start();
            }
        }
    }
}
