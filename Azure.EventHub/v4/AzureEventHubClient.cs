//*********************************************************************************************
//* File             :   AzureEventHubClient.cs
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
using Microsoft.Azure.EventHubs;
using System.Net;
using System.Threading.Tasks;

namespace Azure.EventHub.v4
{
    /// <summary>
    /// Azure EventHub Client implementation
    /// </summary>
    public class AzureEventHubClient : IAzureEventHubClient
    {
        /// <summary>
        /// EventHub Client
        /// </summary>
        public EventHubClient _eventHubClient { get; set; }

        /// <summary>
        /// Constructor
        /// EventHubClient
        /// </summary>
        public AzureEventHubClient(EventHubClient eventHubClient)
        {
            _eventHubClient = eventHubClient;
        }

        /// <summary>
        /// Default ctor
        /// </summary>
        public AzureEventHubClient()
        {
        }

        /// <summary>
        /// Create EventHubClient From ConnectionString
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="eventHubName"></param>
        /// <param name="webProxy"></param>
        /// <returns></returns>
        public IAzureEventHubClient CreateEventHubClientFromConnectionString(string connectionString, string eventHubName, WebProxy webProxy = null)
        {
            EventHubsConnectionStringBuilder connectionStringBuilder = new EventHubsConnectionStringBuilder(connectionString)
            {
                EntityPath = eventHubName
            };

            //Create an eventhub client
            if (webProxy != null)
            {
                _eventHubClient.WebProxy = webProxy;
            }

            _eventHubClient = EventHubClient.CreateFromConnectionString(connectionStringBuilder.ToString());
            _eventHubClient.RetryPolicy = RetryPolicy.Default;

            return new AzureEventHubClient(_eventHubClient);
        }

        /// <summary>
        /// Close eventhub
        /// </summary>
        /// <returns></returns>
        public Task CloseHubAsync()
        {
            return _eventHubClient.CloseAsync();
        }

        /// <summary>
        /// Send EventData 
        /// </summary>
        /// <param name="eventData"></param>
        /// <param name="partitionKey"></param>
        /// <returns></returns>
        public Task SendEventAsync(EventData eventData, string partitionKey = null)
        {
            if (!string.IsNullOrEmpty(partitionKey))
            {
                return _eventHubClient.SendAsync(eventData, partitionKey);
            }

            return _eventHubClient.SendAsync(eventData);
        }


        /// <summary>
        /// Send Event DataBatch
        /// </summary>
        /// <param name="eventDataBatch"></param>
        /// <returns></returns>
        public Task SendEventBatchAsync(EventDataBatch eventDataBatch)
        {
            return _eventHubClient.SendAsync(eventDataBatch);
        }

        /// <summary>
        /// Set Retry Policy for events
        /// </summary>
        /// <param name="retryPolicy"></param>
        public void SetRetryPolicy(RetryPolicy retryPolicy)
        {
            _eventHubClient.RetryPolicy = retryPolicy;
        }

    }
}
