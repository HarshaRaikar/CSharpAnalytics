﻿﻿// Copyright (c) Attack Pattern LLC.  All rights reserved.
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CSharpAnalytics.Network
{
    /// <summary>
    /// Responsible for requesting a queue of URIs over HTTP or HTTPS in background.
    /// </summary>
    public abstract class BackgroundHttpRequester : IDisposable
    {
        private const int MaxUriLength = 2000;
        protected static readonly TimeSpan NetworkRetryWaitStep = TimeSpan.FromSeconds(5);
        protected static readonly TimeSpan NetworkRetryWaitMax = TimeSpan.FromMinutes(10);

        private readonly Queue<Uri> currentRequests = new Queue<Uri>();

        private CancellationTokenSource cancellationTokenSource;
        private Queue<Uri> priorRequests = new Queue<Uri>();
        private Task backgroundSender;
        private TimeSpan currentUploadInterval;
        private Uri currentlySending;

        /// <summary>
        /// Determines whether the BackgroundHttpRequester is currently started.
        /// </summary>
        public bool IsStarted
        {
            get { return cancellationTokenSource != null && !cancellationTokenSource.IsCancellationRequested; }
        }

        /// <summary>
        /// Add a URI to be requested to the queue.
        /// </summary>
        /// <param name="uri">URI to be requested.</param>
        public void Add(Uri uri)
        {
            currentRequests.Enqueue(uri);
        }

        /// <summary>
        /// Start the BackgroundHttpRequester with a given upload interval and a list of previously unrequested URIs.
        /// </summary>
        /// <param name="uploadInterval">How often to send the contents of the queue.</param>
        /// <param name="previouslyUnrequested">List of previously unrequested URIs obtained last time the requester was stopped.</param>
        public void Start(TimeSpan uploadInterval, IEnumerable<Uri> previouslyUnrequested = null)
        {
            if (IsStarted)
                throw new InvalidOperationException(String.Format("Cannot start a {0} when already started", GetType().Name));

            if (previouslyUnrequested != null)
                priorRequests = new Queue<Uri>(previouslyUnrequested);

            cancellationTokenSource = new CancellationTokenSource();
            currentUploadInterval = uploadInterval;
            backgroundSender = Task.Factory.StartNew(RequestLoop, cancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        /// <summary>
        /// Stop the BackgroundHttpRequester and return a list of URIs that were not requested.
        /// </summary>
        /// <returns>List of unrequested URIs that should be passed into Start next time.</returns>
        public async Task<List<Uri>> StopAsync()
        {
            if (!IsStarted)
                throw new InvalidOperationException(String.Format("Cannot stop a {0} when already stopped", GetType().Name));

            cancellationTokenSource.Cancel();
            await backgroundSender;

            return priorRequests
                .Concat(new [] { currentlySending })
                .Concat(currentRequests)
                .Where(r => r != null)
                .ToList();
        }

        /// <summary>
        /// Loop that keeps requesting URIs in the queue until there are none left, then sleeps.
        /// </summary>
        private void RequestLoop()
        {
            using (var queueEmptyWait = new ManualResetEventSlim())
            {
                try
                {
                    while (IsStarted)
                    {
                        // Always empty the priorRequest queue first
                        var requestQueue = priorRequests.Count > 0 ? priorRequests : currentRequests;
                        // Send all the requests we currently have
                        while (requestQueue.Count > 0)
                        {
                            try
                            {
                                currentlySending = requestQueue.Dequeue();
                                RequestWithFailureRetry(currentlySending);
                                currentlySending = null;
                            }
                            catch (InvalidOperationException)
                            {
                                // Just in case the queue is emptied because of an opt-out
                            }
                        }

                        queueEmptyWait.Wait(currentUploadInterval, cancellationTokenSource.Token);
                    }
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Delay for a period of time between failed network requests.
        /// </summary>
        /// <param name="previousRetryDelay">Previous retry delay value to base delay on.</param>
        protected void WaitBetweenFailedRequests(ref TimeSpan previousRetryDelay)
        {
            previousRetryDelay = previousRetryDelay + NetworkRetryWaitStep;
            if (previousRetryDelay > NetworkRetryWaitMax)
                previousRetryDelay = NetworkRetryWaitMax;

            using (var failedRequestWait = new ManualResetEventSlim())
                failedRequestWait.Wait(previousRetryDelay, cancellationTokenSource.Token);
        }

        /// <summary>
        /// Request the URI retrying as appropriate if a failure occurs.
        /// </summary>
        /// <param name="requestUri">URI to requqest.</param>
        protected abstract void RequestWithFailureRetry(Uri requestUri);

        /// <summary>
        /// Total count of all remaining items in the queue.
        /// </summary>
        internal int QueueCount
        {
            get
            {
                return priorRequests.Count
                            + currentRequests.Count
                            + (currentlySending == null ? 0 : 1);
            }
        }

        /// <summary>
        /// Whether a URI request is too long to be sent as a GET and instead the query
        /// parameters should be sent as the body of a POST instead.
        /// </summary>
        /// <param name="requestUri">URI request being considered.</param>
        /// <returns>True if a POST should be used, false if GET should be used.</returns>
        internal static bool ShouldUsePostForRequest(Uri requestUri)
        {
            return requestUri.AbsoluteUri.Length > MaxUriLength;
        }

        /// <summary>
        /// Obtain the innermost Exception from within an Exception.
        /// </summary>
        /// <param name="ex">Exception to obtain the innermost exception from.</param>
        /// <returns>Innermost exception that could be obtained.</returns>
        protected static Exception GetInnermostException(Exception ex)
        {
            var nextException = ex;
            while (nextException.InnerException != null)
                nextException = nextException.InnerException;
            return nextException;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool isDisposing)
        {
            var safeCancellationTokenSource = cancellationTokenSource;
            if (isDisposing && safeCancellationTokenSource != null)
            {
                safeCancellationTokenSource.Dispose();
                cancellationTokenSource = null;
            }
        }
    }
}