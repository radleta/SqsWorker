using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Amib.Threading;
using NLog;

namespace SqsWorker
{
    /// <summary>
    /// A work queue implementation.
    /// </summary>
    /// <typeparam name="TItem">The item to do work on.</typeparam>
    public abstract class WorkQueue<TItem> : IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public WorkQueue(int concurrentReaders = 1, int minimumReadThreashold = 1000)
        {
            this.ConcurrentReaders = concurrentReaders;
            this.MinimumReadThreashold = minimumReadThreashold;
        }

        /// <summary>
        /// Finalizes this instance.
        /// </summary>
        ~WorkQueue()
        {
            this.Dispose(false);
        }

        private int ConcurrentReaders = 1;
        private int MinimumReadThreashold = 1000;
        private bool IsRunning;
        private bool MoreItems;
        private SmartThreadPool _threadPool = new SmartThreadPool();

        /// <summary>
        /// The underlying thread pool for all work done. When the thread pool is empty no more work remains to do.
        /// </summary>
        protected SmartThreadPool ThreadPool
        {
            get
            {
                return _threadPool;
            }
        }

        /// <summary>
        /// Do the work.
        /// </summary>
        public virtual void Work()
        {
            if (IsRunning)
            {
                throw new InvalidOperationException("Already working. Cannot be called again.");
            }

            // there is more items to work on
            IsRunning = true;
            MoreItems = true;

            // queue the read more
            for (var i = 0; i < ConcurrentReaders; i++)
            {
                ThreadPool.QueueWorkItem(DoRead);
            }

            // wait until everything is 
            ThreadPool.WaitForIdle();

            // we're not running any more
            IsRunning = false;
        }

        /// <summary>
        /// Performs the read more operation.
        /// </summary>
        private void DoRead()
        {
            // check to make sure there is more to do
            if (!MoreItems || !ContinueRead())
            {
                return;
            }
            // read more
            var items = Read();
            if (items.Count == 0)
            {
                MoreItems = false;
                return;
            }
            foreach (var item in items)
            {
                ThreadPool.QueueWorkItem(Write, item);
            }
            if (MoreItems)
            {
                // wait for us to get below the minimum threashold of waiting callbacks and there are more items
                while (MoreItems && ContinueRead() && ThreadPool.WaitingCallbacks > MinimumReadThreashold)
                {
                    Thread.Sleep(500);
                }

                // double check to ensure there are more items
                if (MoreItems && ContinueRead())
                {
                    ThreadPool.QueueWorkItem(DoRead, WorkItemPriority.AboveNormal);
                }
            }
        }

        /// <summary>
        /// Reads more items to process.
        /// </summary>
        /// <returns>The items to read.</returns>
        protected abstract List<TItem> Read();

        /// <summary>
        /// Write an item.
        /// </summary>
        /// <param name="item">The item.</param>
        protected abstract void Write(TItem item);

        /// <summary>
        /// Determines whether or not we can continue to read.
        /// </summary>
        /// <returns><c>true</c> continue to read; otherwise, <c>false</c>.</returns>
        protected virtual bool ContinueRead()
        {
            return true;
        }

        /// <summary>
        /// Disposal of the instance.
        /// </summary>
        /// <param name="disposing">Determines whether or not dispose is being called or not.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_threadPool != null)
            {
                _threadPool.Dispose();
                _threadPool = null;
            }
        }

        /// <summary>
        /// Disposes the current instance.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
        }
    }
}
