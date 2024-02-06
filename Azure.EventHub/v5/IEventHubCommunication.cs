//*********************************************************************************************
//* File             :   IEventHubCommunication.cs
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
using System;
using System.Threading.Tasks;

namespace Azure.EventHub.v5
{
    /// <summary>
    /// EventHub communication Apis
    /// </summary>
    public interface IEventHubCommunication : IDisposable
    {
        /// <summary>
        /// Initializes the hub communication.
        /// </summary>
        Task<bool> StartEventHubCommunicationAsync();

        /// <summary>
        /// Stops the hub communication.
        /// </summary>
        void StopEventHubCommunication();

        /// <summary>
        /// Enqueue json messages for sending to EventHub.
        /// </summary>
        /// <param name="message"></param>
        bool EnqueueMessage(string message);
    }
}
