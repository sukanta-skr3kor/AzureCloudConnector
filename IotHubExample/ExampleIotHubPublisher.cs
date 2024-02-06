//*********************************************************************************************
//* File             :   ExampleIotHubPublisher.cs
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
using Azure.IotHub.HubCommunication;
using Serilog;
//using System.Security.Cryptography.X509Certificates;

namespace Azure.Publisher.IotHubDemo
{
    /// <summary>
    /// Example to send data to IotHub from user application
    /// </summary>
    public sealed class ExampleIotHubPublisher
    {
        /// <summary>
        /// Ctor
        /// </summary>
        private ExampleIotHubPublisher()
        { }

        /// <summary>
        /// The main entry point to program
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            try
            {
                //IotHub connection string(read from configuration, environment variable or azure key vault)
                //Below connection string are for demo purpose ,user need to replace with actual connection string
                string iotHubConnString = "HostName=xxx.azure-devices.net;DeviceId=EdgeDevice;SharedAccessKey=xxxxxxxxxxxxxxxxxxx";

                //Create Serilog Logger Instance if required to log
                Log.Logger = new LoggerConfiguration().CreateLogger();

                //If user want to use a X.509 certificate for auth
                //X509Certificate2 certificateFile = new X509Certificate2(@"<path to device-pfx-file>", "<password>");

                //IotHub client with X509 certificate
                //IotHubClient iotHubClient = new IotHubClient("xxxx.azure-devices.net", "xxx", certificateFile);

                //IotHub client with connection string
                IotHubClient iotHubClient = new IotHubClient(iotHubConnString);

                //Communication client, does actual processing and sending of messages
                IotHubCommunication iotHubCommunication = new IotHubCommunication(iotHubClient);

                //default send interval is set to 1 second(s), user can changes as per application requirement
                iotHubCommunication.SendIntervalSeconds = 1;

                //If user wants to send single telemetry messages, set it to true else false
                iotHubCommunication.IsSingleMessageSend = true;//Important to send message in batches not single message(for demo it is set to single message)

                //Start hub communication to send messages to hub, with out start no messages will be sent to hub
                iotHubCommunication.StartHubCommunicationAsync().ConfigureAwait(false);

                //Now just enqueue the json messages you want to send, read from file, device or created by your application
                string jsonPayload = File.ReadAllText("payload.json");

                //enqueue the messages to be sent to hub
                iotHubCommunication.EnqueueMessage(jsonPayload);

                Console.WriteLine("Message Sent to iotHub.");

                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
