//*********************************************************************************************
//* File             :   IIotHubClient.cs
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

using Microsoft.Azure.Devices.Client;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Azure.IotHub.HubClient
{
    public interface IIotHubClient : IDisposable
    {
        /// <summary>
        /// open the DeviceClient instance.
        /// </summary>
        Task OpenIotHubAsync();

        /// <summary>
        /// Close the client instance
        /// </summary>
        Task CloseIotHubAsync();

        /// <summary>
        /// Sets the retry policy used in the operation retries.
        /// </summary>
        void SetHubRetryPolicy(IRetryPolicy retryPolicy);

        /// <summary>
        /// Sends message to iothub
        /// </summary>
        Task SendMessageAsync(Message message);

        /// <summary>
        /// Sends batch of messages to iothub
        /// </summary>
        Task SendMessageBatchAsync(IList<Message> messages);

        /// <summary>
        /// IotHub connection change handler
        /// </summary>
        /// <param name="statusChangesHandler"></param>
        void SetIotHubConnectionStatusChangesHandler(ConnectionStatusChangesHandler statusChangesHandler);

        /// <summary>
        /// Cleanup Resources
        /// </summary>
        void Dispose();
    }
}
