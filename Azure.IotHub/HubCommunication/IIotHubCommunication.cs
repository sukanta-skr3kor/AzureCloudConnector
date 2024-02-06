//*********************************************************************************************
//* File             :   IIotHubCommunication.cs
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

using System;
using System.Threading.Tasks;

namespace Azure.IotHub.HubCommunication
{
    /// <summary>
    /// Class to handle IoTHub/EdgeHub communication.
    /// </summary>
    public interface IIotHubCommunication : IDisposable
    {
        /// <summary>
        /// Initializes and start the iothub communication.
        /// </summary>
        Task<bool> StartHubCommunicationAsync();

        /// <summary>
        /// Stops the hub communication.
        /// </summary>
        void StopHubCommunication();

        /// <summary>
        /// Enqueue a JSON message for sending to IoTHub.
        /// </summary>
        /// <param name="message"></param>
        bool EnqueueMessage(string message);
    }
}
