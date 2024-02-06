//*********************************************************************************************
//* File             :   EventHubCommunication.cs
//* Author           :   Rout, Sukanta 
//* Date             :   16/8/2023
//* Description      :   Initial version
//* Version          :   1.0
//*-------------------------------------------------------------------------------------------
//* dd-MMM-yyyy	: Version 1.x, Changed By : xxx
//*
//*                 - 1)
//*                 - 2)
//*                 - 3)
//*                 - 4)
//*
//*********************************************************************************************

using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.EventHub.v5
{
    /// <summary>
    /// Implementation of EventHub Communication class
    /// </summary>
    public class EventHubCommunication : IEventHubCommunication
    {
        private long _messageEnqueueCount;
        private long _messageEnqueueFailureCount;
        private Task _eventProcessorTask;
        private CancellationTokenSource _eventHubCommunicationCts;
        private readonly CancellationToken _shutdownToken;
        private readonly string hubConnectionString;
        private readonly string _eventHubName;

        /// <summary>
        /// Specifies the send interval in seconds after which a message is sent to the hub.
        /// </summary>
        public int SendIntervalSeconds { get; set; } = 1;//default message send interval used as 1 second, can be changed

        /// <summary>
        /// Number of messages sent to hub.
        /// </summary>
        public long SentEvents { get; private set; }

        /// <summary>
        /// Time when sent the last event sent to event hub
        /// </summary>
        public DateTime LastEventSentTime { get; private set; }

        /// <summary>
        /// Number of events in a batch that can be added
        /// </summary>
        public int EventsPerBatch { get; private set; } = 100;

        /// <summary>
        /// Number of batches to create to send those evnts
        /// </summary>
        public int EventBatchCount { get; private set; } = 10;

        /// <summary>
        /// Specifies the queue capacity for monitored item events.
        /// </summary>
        public int MessageQueueSize { get; private set; } = 100000;//default messsages the internal queue can hold, can be changed

        /// <summary>
        /// Number of messages in the internal message queue waiting to be sent.
        /// </summary>
        public long MessagesCount => _eventQueue.Count;

        /// <summary>
        /// Internal message queue for regular messages
        /// </summary>
        private BlockingCollection<string> _eventQueue;

        /// <summary>
        /// Logging
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        /// Event producer client
        /// </summary>
        private EventHubProducerClient eventHubProducerClient;


        /// <summary>
        /// EventHubCommunication
        /// </summary>
        /// <param name="eventHubConnectionString"></param>
        /// <param name="eventHubName"></param>
        /// <param name="logger"></param>
        public EventHubCommunication(string eventHubConnectionString, string eventHubName, ILogger logger)
        {
            hubConnectionString = eventHubConnectionString;
            _eventHubName = eventHubName;
            _logger = logger;
            _eventQueue = new BlockingCollection<string>(MessageQueueSize);
            _eventHubCommunicationCts = new CancellationTokenSource();
            _shutdownToken = _eventHubCommunicationCts.Token;
        }

        /// <summary>
        /// Enqueue message(Events)
        /// </summary>
        /// <param name="message"></param>
        public bool EnqueueMessage(string message)
        {
            // Add json message to internal queue
            Interlocked.Increment(ref _messageEnqueueCount);

            if (!_eventQueue.TryAdd(message))
            {
                Interlocked.Increment(ref _messageEnqueueFailureCount);

                if (_messageEnqueueFailureCount % 10000 == 0)
                {
                    _logger?.Error($"The internal message queue is above its capacity of {_eventQueue.BoundedCapacity}. Already lost {_messageEnqueueFailureCount} mesaages to sent to hub.");
                }

                return false;
            }
            return true;
        }

        /// <summary>
        ///Initialize Hub Communication
        /// </summary>
        /// <param name="hubClient"></param>
        /// <returns></returns>
        public async Task<bool> StartEventHubCommunicationAsync()
        {
            _logger?.Information($"Creating event producer client");
            eventHubProducerClient = new EventHubProducerClient(hubConnectionString, _eventHubName);

            // start up task to send telemetry to EventHub
            _eventProcessorTask = null;

            _eventProcessorTask = Task.Run(() => ProcessEventsAsync(_shutdownToken).ConfigureAwait(false), _shutdownToken);
            _logger?.Information($"Started event processing.");

            return await Task.FromResult(true);
        }


        /// <summary>
        /// Send events to Hub
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task ProcessEventsAsync(CancellationToken cancellationToken)
        {
            long maximumBatchSize;
            int currentBatch = 0;

            using (EventDataBatch measureBatch = await eventHubProducerClient.CreateBatchAsync())
            {
                maximumBatchSize = measureBatch.MaximumSizeInBytes;
            }

            int EventCount = EventBatchCount * EventsPerBatch;
            long EventSize = maximumBatchSize / EventsPerBatch;

            Queue<EventData> eventsToPublish = new Queue<EventData>(EventCount);


            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    for (int count = 0; count < EventCount; ++count)
                    {
                        //Take from internal queue
                        _eventQueue.TryTake(out string currentEvent, 1, cancellationToken);

                        if (currentEvent != null)
                        {
                            var eventByteData = Encoding.UTF8.GetBytes(currentEvent);

                            if (eventByteData.Length <= EventSize)
                            {
                                //add to final publish queue
                                eventsToPublish.Enqueue(new EventData(eventByteData));
                            }
                            else
                            {
                                _logger?.Warning($"Event size is more than the allowed size, discarding the event {currentEvent}.");
                            }
                        }
                    }

                    using (EventDataBatch eventDataBatch = await eventHubProducerClient.CreateBatchAsync())
                    {
                        while ((TryDequeue(eventsToPublish, out EventData currentEvent) && eventDataBatch.TryAdd(currentEvent)))

                            // When an event could not be dequeued or could not be added to the batch, then the batch is ready to be published.

                            if (eventDataBatch.Count > 0)
                            {
                                await eventHubProducerClient.SendAsync(eventDataBatch);

                                ++currentBatch;
                                ++SentEvents;
                                LastEventSentTime = DateTime.UtcNow;

                                _logger?.Information($"Batch: {currentBatch} containing {eventDataBatch.Count} events was published.  {eventsToPublish.Count} remains in queue.");
                            }
                    }
                }
                catch (Exception exp)
                {
                    _logger?.Error(exp, exp.Message);
                }
            }
        }

        /// <summary>
        /// Dequeue events from the final publish queue
        /// </summary>
        /// <param name="eventsToPublish"></param>
        /// <param name="currentEvent"></param>
        /// <returns></returns>
        private bool TryDequeue(Queue<EventData> eventsToPublish, out EventData currentEvent)
        {
            if (eventsToPublish.Count > 0)
            {
                currentEvent = eventsToPublish.Dequeue();
                return true;
            }

            currentEvent = null;
            return false;
        }

        /// <summary>
        ///Stop Hub Communication
        /// </summary>
        /// <param name="hubClient"></param>
        /// <returns></returns>
        public void StopEventHubCommunication()
        {
            //cleanup
            Dispose(true);
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            StopEventHubCommunication();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Cleanup resources.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                //wait till last message sent to eventHub
                _eventHubCommunicationCts?.Cancel();

                try
                {
                    _eventProcessorTask?.Wait();//wait for the event processor to finish sending the last batch of event
                    _eventQueue = null;
                    _eventProcessorTask = null;
                }
                catch (Exception exp)
                {
                    _logger?.Error(exp, "Error while shutting down eventhub publisher.");
                }

                _eventHubCommunicationCts?.Dispose();
                _eventHubCommunicationCts = null;
            }
        }
    }
}
