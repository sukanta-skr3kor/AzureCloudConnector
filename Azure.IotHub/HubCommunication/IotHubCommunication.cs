//*********************************************************************************************
//* File             :   IotHubCommunication.cs
//* Author           :   Rout, Sukanta
//* Date             :   9/8/2023
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

using Azure.IotHub.HubClient;
using Microsoft.Azure.Devices.Client;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.IotHub.HubCommunication
{
    /// <summary>
    /// IotHubCommunication class implementation
    /// </summary>
    public class IotHubCommunication : IIotHubCommunication
    {
        //Global Variables used
        private long _messageEnqueueCount;
        private long _messageEnqueueFailureCount;
        private Task _messageProcessorTask;
        private CancellationTokenSource _iotHubCommunicationCts;
        private readonly CancellationToken _shutdownToken;
        private readonly int _retryCount;

        /// <summary>
        /// Max message size in byte for Iothub communication allowed.
        /// </summary>
        private const uint HubMessageSizeMax = 256 * 1024; //Max message size is 256 KBytes for IotHub packet to be sent

        /// <summary>
        /// Message size in bytes for Iothub communication.
        /// </summary>
        public uint HubMessageSize { get; set; } = HubMessageSizeMax;

        /// <summary>
        /// Specifies the send interval in seconds after which a message is sent to the hub.
        /// </summary>
        public int SendIntervalSeconds { get; set; } = 10;//default message send interval used, can be changed

        /// <summary>
        /// Number of events sent to the IotHub.
        /// </summary>
        public long NumberOfEvents { get; private set; }

        /// <summary>
        /// If the application wants to send single telemetry messages
        /// </summary>
        public bool IsSingleMessageSend { get; set; }

        /// <summary>
        /// Number of times we were not able to make the send interval, because too high load.
        /// </summary>
        private long MissedMessageSendIntervalCount;

        /// <summary>
        /// Number of times the size for the event payload was too large for a message.
        /// </summary>
        private long MessageTooLargeCount;

        /// <summary>
        /// Number of payload bytes we sent to the cloud.
        /// </summary>
        public long SentDataBytes { get; private set; }

        /// <summary>
        /// Number of messages sent to hub.
        /// </summary>
        public long SentMessages { get; private set; }

        /// <summary>
        /// Time when sent the last message to hub
        /// </summary>
        public DateTime LastMessageSentTime { get; private set; }

        /// <summary>
        /// Number of times we were not able to sent the telemetry message to the cloud.
        /// </summary>
        public long FailedMessages { get; private set; }

        /// <summary>
        /// Check if transport type to use is HTTP.
        /// </summary>
        public TransportType HubProtocol { get; set; } = TransportType.Http1;//default HTTP used, can be changed as per requirement

        /// <summary>
        /// Specifies the queue capacity for monitored item events.
        /// </summary>
        public int MessageQueueSize { get; set; } = 10000;//default messsages the internal queue can hold, can be changed

        /// <summary>
        /// Number of messages in the internal message queue waiting to be sent.
        /// </summary>
        public long MessagesCount => _applicationMessageQueue.Count;

        /// <summary>
        /// Json contenet type
        /// </summary>
        private const string CONTENT_TYPE_JSON = "application/json"; //Default content type for IotHub message

        /// <summary>
        /// UTF-8 encoded content
        /// </summary>
        private const string CONTENT_ENCODING_UTF8 = "UTF-8";

        /// <summary>
        /// Internal message queue for regular messages
        /// </summary>
        private BlockingCollection<string> _applicationMessageQueue;

        /// <summary>
        /// Logging
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        /// IotHub Client
        /// </summary>
        private IotHubClient HubClient;

        /// <summary>
        ///  IotHubCommunication
        /// </summary>
        /// <param name="hubClient"></param>
        /// <param name="logger"></param>
        /// <param name="retryCount"></param>
        public IotHubCommunication(IotHubClient hubClient, ILogger logger = null, int retryCount = 3)
        {
            HubClient = hubClient;
            _logger = logger;
            _retryCount = retryCount;
            _applicationMessageQueue = new BlockingCollection<string>(MessageQueueSize);
            _iotHubCommunicationCts = new CancellationTokenSource();
            _shutdownToken = _iotHubCommunicationCts.Token;
        }

        /// <summary>
        /// Enqueue messages(json payload)
        /// </summary>
        /// <param name="jsonMessage"></param>
        public bool EnqueueMessage(string jsonMessage)
        {
            // Add the json  message to the internal queue
            Interlocked.Increment(ref _messageEnqueueCount);

            if (!_applicationMessageQueue.TryAdd(jsonMessage))
            {
                Interlocked.Increment(ref _messageEnqueueFailureCount);

                if (_messageEnqueueFailureCount % 10000 == 0)
                {
                    _logger?.Error($"The internal message queue is above it's capacity of {_applicationMessageQueue.BoundedCapacity}. Already lost {_messageEnqueueFailureCount} mesaages to sent to hub.");
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
        public async Task<bool> StartHubCommunicationAsync()
        {
            _logger?.Information($"Starting Azure IotHub communication.");

            //publisher info
            HubClient.PublisherInfo = "Azure IotHub Publisher";

            //Setting retry policy
            ExponentialBackoff exponentialRetryPolicy = new ExponentialBackoff(_retryCount, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(100));
            HubClient.SetHubRetryPolicy(exponentialRetryPolicy);

            _logger?.Information($"Connecting to Azure IotHub...");
            // open connection
            await HubClient.OpenIotHubAsync().ConfigureAwait(false);

            //Set connection change handler
            HubClient.SetIotHubConnectionStatusChangesHandler(HubConnectionStatusChange);

            //Start Processing messages in the hub message queue
            //Start up task to send telemetry to IoTHub
            _messageProcessorTask = null;

            _messageProcessorTask = Task.Run(() => ProcessMessageAsync(_shutdownToken).ConfigureAwait(false), _shutdownToken);
            _logger?.Information($"Connected to Hub, Starting message processing");

            return await Task.FromResult(true);
        }

        /// <summary>
        /// HubConnection Status Change handler 
        /// </summary>
        /// <param name="connectionStatus"></param>
        /// <param name="connectionStatusChangeReason"></param>
        private void HubConnectionStatusChange(ConnectionStatus connectionStatus, ConnectionStatusChangeReason connectionStatusChangeReason)
        {
            if (connectionStatusChangeReason == ConnectionStatusChangeReason.Connection_Ok)
            {
                _logger?.Information($"Hub connection success, status changed to '{connectionStatus}', reason '{connectionStatusChangeReason}'");
            }
            else
            {
                _logger?.Error($"Hub connection error, status changed to '{connectionStatus}', reason '{connectionStatusChangeReason}'");
            }
        }


        /// <summary>
        /// Process and send messages to hub
        /// Cloud-to-device and device-to-cloud throttles determine the maximum rate at which you can send messages irrespective of 4 KB chunks.
        /// Device-to-cloud messages can be up to 256 KB
        /// https://learn.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-quotas-throttling
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task ProcessMessageAsync(CancellationToken cancellationToken)
        {
            Message message = new Message();
            uint jsonBracketLength = 2;

            //system properties are MessageId (max 128 byte), Sequence number (ulong), ExpiryTime (DateTime) 
            int systemPropertyLength = 128 + sizeof(ulong) + message.ExpiryTimeUtc.ToString(CultureInfo.InvariantCulture).Length;

            int applicationPropertyLength = Encoding.UTF8.GetByteCount($"iothub-content-type={CONTENT_TYPE_JSON}") + Encoding.UTF8.GetByteCount($"iothub-content-encoding={CONTENT_ENCODING_UTF8}");

            // if batching is requested the buffer will have the requested size, otherwise the max size used
            uint iotHubMessageBufferSize = (HubMessageSize > 0 ? HubMessageSize : HubMessageSizeMax) - (uint)systemPropertyLength - jsonBracketLength - (uint)applicationPropertyLength;

            //InMemory buffer to hold the message
            byte[] hubMessageBuffer = new byte[iotHubMessageBufferSize];

            MemoryStream iotHubMessage = new MemoryStream(hubMessageBuffer);

            //Calculate send time,the messages shall be snt based on the time set by user
            var nextSendTime = DateTime.UtcNow + TimeSpan.FromSeconds(SendIntervalSeconds);

            //If we want to send single messages then no need to pack the message as (256KB packet)
            if (IsSingleMessageSend)
            {
                SendIntervalSeconds = 0;
                HubMessageSize = 0;
            }
            else
            {
                _logger?.Information("Batch messages to IotHub is enabled");
            }

            //If user sets send interval to zero and HubMessagesize to zero then it means single message send also
            bool singleMessageSend = SendIntervalSeconds == 0 && HubMessageSize == 0;

            if (singleMessageSend || IsSingleMessageSend)
            {
                _logger?.Information("Single data messages to Hub is enabled");
            }

            //Start processing 
            using (iotHubMessage)
            {
                try
                {
                    int jsonMessageSize = 0;
                    bool needToBufferMessage = false;
                    string jsonMessage = string.Empty;

                    //Set hub message initial position and length to zero
                    iotHubMessage.Position = 0;
                    iotHubMessage.SetLength(0);

                    if (!singleMessageSend)//For a single message send
                    {
                        iotHubMessage.Write(Encoding.UTF8.GetBytes("["), 0, 1);
                    }

                    do
                    {
                        TimeSpan timeTillNextMessageSend;
                        int millisecondsToWait;

                        // sanity check the send interval, compute the timeout and get the next monitored item message
                        if (SendIntervalSeconds > 0)
                        {
                            timeTillNextMessageSend = nextSendTime.Subtract(DateTime.UtcNow);

                            if (timeTillNextMessageSend < TimeSpan.Zero)
                            {
                                MissedMessageSendIntervalCount++;
                                // do not wait if we missed the send interval
                                timeTillNextMessageSend = TimeSpan.Zero;
                            }

                            long millisLong = (long)timeTillNextMessageSend.TotalMilliseconds;

                            if (millisLong < 0 || millisLong > int.MaxValue)
                            {
                                millisecondsToWait = 0;
                            }
                            else
                            {
                                millisecondsToWait = (int)millisLong;
                            }
                        }
                        else
                        {
                            //wait infinite if send interval is not set
                            millisecondsToWait = cancellationToken.IsCancellationRequested ? 0 : -1;
                        }

                        //try taking out the message from the internal application queue
                        bool gotMessage = _applicationMessageQueue.TryTake(out jsonMessage, millisecondsToWait, cancellationToken);

                        // check if we got a message enqueued or the timeout happened or got canceled
                        if (gotMessage)
                        {
                            NumberOfEvents++;
                            jsonMessageSize = Encoding.UTF8.GetByteCount(jsonMessage);

                            // sanity check that the user has set a large enough messages size
                            if ((HubMessageSize > 0 && jsonMessageSize > HubMessageSize) || (HubMessageSize == 0 && jsonMessageSize > iotHubMessageBufferSize))
                            {
                                _logger?.Error($"There is a message (size: {jsonMessageSize}), which will not fit into hub message (max size: {iotHubMessageBufferSize}].");
                                _logger?.Error($"Check message size, the message will be discarded");
                                MessageTooLargeCount++;
                                continue;
                            }

                            // if batching is requested or we need to send at intervals, batch it otherwise send it right away
                            needToBufferMessage = false;

                            if (HubMessageSize > 0 || (HubMessageSize == 0 && SendIntervalSeconds > 0))
                            {
                                // if there is still space to batch, do it. otherwise send the buffer and flag the message for later buffering
                                if (iotHubMessage.Position + jsonMessageSize + 1 <= iotHubMessage.Capacity)
                                {
                                    // add the message and a comma to the buffer, batching the individual messages
                                    iotHubMessage.Write(Encoding.UTF8.GetBytes(jsonMessage), 0, jsonMessageSize);
                                    iotHubMessage.Write(Encoding.UTF8.GetBytes(","), 0, 1);
                                    continue;
                                }
                                else
                                {
                                    needToBufferMessage = true;
                                }
                            }
                        }
                        else
                        {
                            // if we got no message, we either reached the interval or we are in shutdown and have processed all messages
                            if (cancellationToken.IsCancellationRequested)
                            {
                                _logger?.Information($"Shutdown of iothub publisher application started.");
                                _applicationMessageQueue.CompleteAdding();
                                _applicationMessageQueue.Dispose();
                                break;
                            }
                        }

                        // Batching is completed or reached the send interval or application get a cancellation request
                        try
                        {
                            Message iotHubEncodedMessage = null;

                            // if we reached the send interval, but have nothing to send (only the opening square bracket is there), we continue
                            if (!gotMessage && iotHubMessage.Position == 1)
                            {
                                nextSendTime += TimeSpan.FromSeconds(SendIntervalSeconds);
                                iotHubMessage.Position = 0;
                                iotHubMessage.SetLength(0);

                                if (!singleMessageSend)
                                {
                                    iotHubMessage.Write(Encoding.UTF8.GetBytes("["), 0, 1);
                                }
                                continue;
                            }

                            // if there is no batching and no send interval configured, we send the JSON message we just got, otherwise we send the buffer
                            if (singleMessageSend)
                            {
                                // create the message without brackets
                                iotHubEncodedMessage = new Message(Encoding.UTF8.GetBytes(jsonMessage));
                            }
                            else
                            {
                                // remove the trailing comma and add a closing square bracket
                                iotHubMessage.SetLength(iotHubMessage.Length - 1);
                                iotHubMessage.Write(Encoding.UTF8.GetBytes("]"), 0, 1);
                                iotHubEncodedMessage = new Message(iotHubMessage.ToArray());
                            }

                            if (HubClient != null)
                            {
                                iotHubEncodedMessage.ContentType = CONTENT_TYPE_JSON;
                                iotHubEncodedMessage.ContentEncoding = CONTENT_ENCODING_UTF8;

                                nextSendTime += TimeSpan.FromSeconds(SendIntervalSeconds);

                                try
                                {
                                    SentDataBytes += iotHubEncodedMessage.GetBytes().Length;
                                    await HubClient.HubDeviceClient.SendEventAsync(iotHubEncodedMessage).ConfigureAwait(false);

                                    SentMessages++;
                                    LastMessageSentTime = DateTime.UtcNow;
                                    _logger?.Information($"Sent {SentMessages} messages and {iotHubEncodedMessage.BodyStream.Length} bytes to iothub.");
                                }
                                catch
                                {
                                    FailedMessages++;//increment the failed message count

                                    //Try add the failed message back to message queue again
                                    _applicationMessageQueue.TryAdd(jsonMessage);

                                    _logger?.Warning($"Failed to sent message to hub, adding back to queue for retry.");
                                }

                                // reset message body
                                iotHubMessage.Position = 0;
                                iotHubMessage.SetLength(0);

                                if (!singleMessageSend)
                                {
                                    iotHubMessage.Write(Encoding.UTF8.GetBytes("["), 0, 1);
                                }

                                // if we had not yet buffered the last message because there was not enough space it has to buffer now,
                                // in case the last message not added it will add to the next message, so no message loss shall happen
                                if (needToBufferMessage)
                                {
                                    // Add the message and a comma to the buffer
                                    iotHubMessage.Write(Encoding.UTF8.GetBytes(jsonMessage), 0, jsonMessageSize);
                                    iotHubMessage.Write(Encoding.UTF8.GetBytes(","), 0, 1);
                                }
                            }
                            else
                            {
                                _logger?.Information("Hub client not available, messages are dropped.");
                            }
                        }
                        catch (Exception exp)
                        {
                            _logger?.Error(exp, "Error while sending message to iot hub, dropping message.");
                        }
                    } while (true);
                }
                catch (Exception exp)
                {
                    if (!(exp is OperationCanceledException))
                    {
                        _logger?.Error(exp, "Error processing messages for iotHub.");
                    }
                }
            }
        }

        /// <summary>
        ///Stop Hub Communication
        /// </summary>
        /// <param name="hubClient"></param>
        /// <returns></returns>
        public void StopHubCommunication()
        {
            //cleanup
            Dispose(true);
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            StopHubCommunication();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Cleanup resources
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                //wait till last message sent to iothub
                _iotHubCommunicationCts?.Cancel();

                try
                {
                    _messageProcessorTask?.Wait();//wait for the message processor to finish sending the last message

                    _applicationMessageQueue = null;
                    _messageProcessorTask = null;

                    HubClient?.Dispose();
                    HubClient = null;
                }
                catch (Exception exp)
                {
                    _logger?.Error(exp, "Error while shutting down iothub publisher.");
                }

                _iotHubCommunicationCts?.Dispose();
                _iotHubCommunicationCts = null;
            }
        }

    }
}
