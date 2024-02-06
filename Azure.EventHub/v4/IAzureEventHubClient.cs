//*********************************************************************************************
//* File             :   IAzureEventHubClient.cs
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
    public interface IAzureEventHubClient
    {
        /// <summary>
        /// EventHub client from connection string
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="eventHubName"></param>
        /// <param name="webProxy"></param>
        /// <returns></returns>
        IAzureEventHubClient CreateEventHubClientFromConnectionString(string connectionString, string eventHubName, WebProxy webProxy = null);

        /// <summary>
        /// Close event hub
        /// </summary>
        /// <returns></returns>
        Task CloseHubAsync();

        /// <summary>
        /// Send Event Async(Non Blocking)
        /// </summary>
        /// <param name="eventData"></param>
        /// <param name="partitionKey"></param>
        /// <returns></returns>
        Task SendEventAsync(EventData eventData, string partitionKey = null);

        /// <summary>
        /// Send event batch data
        /// </summary>
        /// <param name="eventDataBatch"></param>
        /// <returns></returns>
        Task SendEventBatchAsync(EventDataBatch eventDataBatch);

        /// <summary>
        /// Retry policy
        /// </summary>
        /// <param name="retryPolicy"></param>
        void SetRetryPolicy(RetryPolicy retryPolicy);
    }
}
