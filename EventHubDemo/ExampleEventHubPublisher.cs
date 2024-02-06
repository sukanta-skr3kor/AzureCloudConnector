//*********************************************************************************************
//* File             :   ExampleEventHubPublisher.cs
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

using Azure.EventHub.v5;
using Serilog;

namespace Azure.Publisher.EventHubDemo
{
    /// <summary>
    /// Example to send data to EventHub from user application
    /// </summary>
    public sealed class ExampleEventHubPublisher
    {
        /// <summary>
        /// Ctor
        /// </summary>
        private ExampleEventHubPublisher()
        { }

        /// <summary>
        /// The main entry point to program
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            try
            {
                //Below connection string are for demo purpose ,user need to replace with actual connection string
                string eventHubConnString = @"Endpoint=sb://xxxxxx.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";
                string eventHubName = "<yourEventHubName>";

                //send events using v5 the latest library , recommended by Microsoft
                //Communication client, does actual processing and sending of messages
                EventHubCommunication eventHubCommunication = new EventHubCommunication(eventHubConnString, eventHubName, Log.Logger);

                //Start hub communication to send messages to hub, with out start no messages will be sent to hub
                eventHubCommunication.StartEventHubCommunicationAsync().ConfigureAwait(false);

                //Now just enqueue the json messages you want to send, read from file, device or created by your application
                for (int i = 0; i <= 5; i++)
                {
                    eventHubCommunication.EnqueueMessage($"Message{i} using v5 library");
                }

                Console.WriteLine("Event Sent to Hub.");

                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
