//*********************************************************************************************
//* File             :   IotHubClient.cs
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
using System.Net;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Azure.IotHub.HubClient
{
    /// <summary>
    /// Implementation of iotHubClient 
    /// </summary>
    public class IotHubClient : IIotHubClient
    {
        /// <summary>
        /// Device Client
        /// </summary>
        public DeviceClient HubDeviceClient { get; set; }

        /// <summary>
        /// Edge Client
        /// </summary>
        public ModuleClient HubEdgeClient { get; set; }

        /// <summary>
        /// Information about the publisher to be sent to iothub
        /// </summary>
        public string PublisherInfo
        {
            get
            {
                if (HubDeviceClient == null)
                {
                    return HubEdgeClient.ProductInfo;
                }
                return HubDeviceClient.ProductInfo;
            }
            set
            {
                if (HubDeviceClient == null)
                {
                    HubEdgeClient.ProductInfo = value;
                    return;
                }
                HubDeviceClient.ProductInfo = value;
            }
        }

        /// <summary>
        /// IotHubClient
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="transportType"></param>
        /// <param name="isEdgeClient"></param>
        /// <param name="webProxy"></param>
        public IotHubClient(string connectionString, TransportType transportType = TransportType.Http1, bool isEdgeClient = false, WebProxy webProxy = null)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentNullException(nameof(connectionString));
            }

            if (isEdgeClient)
            {
                //For edge device
                CreateEdgeClient(connectionString, transportType, webProxy);
            }
            else
            {
                //for iot device
                CreateDeviceClient(connectionString, transportType, webProxy);
            }
        }

        /// <summary>
        /// IotHubClient with https support using X509 certificate
        /// </summary>
        /// <param name="hostName"></param>
        /// <param name="deviceCertificate"></param>
        /// <param name="transportType"></param>
        public IotHubClient(string hostName, string deviceId, X509Certificate2 deviceCertificate, TransportType transportType = TransportType.Amqp_Tcp_Only, bool isEdgeClient = false)
        {
            if (string.IsNullOrEmpty(hostName))
            {
                throw new ArgumentNullException(nameof(hostName));
            }

            if (string.IsNullOrEmpty(deviceId))
            {
                throw new ArgumentNullException(nameof(deviceId));
            }

            if (deviceCertificate == null)
            {
                throw new ArgumentNullException(nameof(deviceCertificate));
            }

            DeviceAuthenticationWithX509Certificate authenticationWithX509Certificate = new DeviceAuthenticationWithX509Certificate(deviceId, deviceCertificate);
            CreateClient(hostName, authenticationWithX509Certificate, transportType, isEdgeClient);
        }

        /// <summary>
        /// Constructor
        /// Device Client
        /// </summary>
        public IotHubClient(DeviceClient iotHubDeviceClient)
        {
            HubDeviceClient = iotHubDeviceClient;
        }

        /// <summary>
        /// Constructor
        /// Edge Client
        /// </summary>
        public IotHubClient(ModuleClient iotHubEdgeClient)
        {
            HubEdgeClient = iotHubEdgeClient;
        }

        /// <summary>
        /// Create Device/Edge Client
        /// </summary>
        /// <param name="hostName"></param>
        /// <param name="deviceAuthenticationWithX509Certificate"></param>
        /// <param name="transportType"></param>
        /// <param name="isEdgeClient"></param>
        /// <returns></returns>
        public IIotHubClient CreateClient(string hostName, DeviceAuthenticationWithX509Certificate deviceAuthenticationWithX509Certificate,
                                           TransportType transportType = TransportType.Amqp_Tcp_Only, bool isEdgeClient = false)
        {
            try
            {
                string appName = Assembly.GetEntryAssembly().FullName?.Split(',')?[0];

                if (isEdgeClient)
                {
                    HubEdgeClient = ModuleClient.Create(hostName, deviceAuthenticationWithX509Certificate, transportType);

                    //Application publisher information
                    PublisherInfo = $"{appName} IotHub Publisher";

                    //Create edge client
                    return new IotHubClient(HubEdgeClient);
                }
                else
                {
                    HubDeviceClient = DeviceClient.Create(hostName, deviceAuthenticationWithX509Certificate, transportType);

                    //Application publisher information
                    PublisherInfo = $"{appName} IotHub Publisher";

                    //Create device client
                    return new IotHubClient(HubDeviceClient);
                }
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Create device client from the iot hub connection string
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="transportType"></param>
        /// <param name="webProxy"></param>
        /// <returns></returns>
        public IIotHubClient CreateDeviceClient(string connectionString, TransportType transportType = TransportType.Http1, WebProxy webProxy = null)
        {
            try
            {
                string appName = Assembly.GetEntryAssembly().FullName?.Split(',')?[0];

                //If proxy settings has to be used
                if (webProxy != null)
                {
                    Http1TransportSettings http1TransportSettings = new Http1TransportSettings
                    {
                        Proxy = webProxy
                    };

                    //Create client with proxy settings
                    HubDeviceClient = DeviceClient.CreateFromConnectionString(connectionString, new ITransportSettings[] { http1TransportSettings });

                    //Application publisher information
                    PublisherInfo = $"{appName} IotHub Publisher";

                    //Create device client
                    return new IotHubClient(HubDeviceClient);
                }

                //Create client from connection string without proxy settings
                else
                {
                    HubDeviceClient = DeviceClient.CreateFromConnectionString(connectionString, transportType);

                    //Create device client
                    return new IotHubClient(HubDeviceClient);
                }
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Create Edge(Module) client from the iothub connection string
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="transportType"></param>
        /// <param name="webProxy"></param>
        /// <returns></returns>
        public IIotHubClient CreateEdgeClient(string connectionString, TransportType transportType = TransportType.Http1, WebProxy webProxy = null)
        {
            try
            {
                string appName = Assembly.GetEntryAssembly().FullName?.Split(',')?[0];

                //If proxy settings has to be used
                if (webProxy != null)
                {
                    Http1TransportSettings http1TransportSettings = new Http1TransportSettings
                    {
                        Proxy = webProxy
                    };

                    //Create client with proxy settings
                    //Create an edge client
                    HubEdgeClient = ModuleClient.CreateFromConnectionString(connectionString, new ITransportSettings[] { http1TransportSettings });

                    //Application publisher information
                    PublisherInfo = $"{appName} IotHub Publisher";

                    return new IotHubClient(HubEdgeClient);
                }
                //Create client from connection string without proxy settings
                else
                {
                    //Create the client
                    HubEdgeClient = ModuleClient.CreateFromConnectionString(connectionString, transportType);

                    //Application publisher information
                    PublisherInfo = $"{appName} IotHub Publisher";

                    //Create a  edge client
                    return new IotHubClient(HubEdgeClient);
                }
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Send message to hub async(Non blocking)
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public Task SendMessageAsync(Message message)
        {
            if (HubDeviceClient == null)
            {
                return HubEdgeClient.SendEventAsync(message);
            }

            return HubDeviceClient.SendEventAsync(message);
        }

        /// <summary>
        /// Sending batch of messages as 256kb pack of size
        /// </summary>
        /// <param name="messages"></param>
        /// <returns></returns>
        public Task SendMessageBatchAsync(IList<Message> messages)
        {
            if (HubDeviceClient == null)
            {
                return HubEdgeClient.SendEventBatchAsync(messages);
            }

            return HubDeviceClient.SendEventBatchAsync(messages);
        }

        /// <summary>
        /// Open communication channel to hub
        /// </summary>
        /// <returns></returns>
        public Task OpenIotHubAsync()
        {
            if (HubDeviceClient == null)
            {
                return HubEdgeClient.OpenAsync();
            }

            return HubDeviceClient.OpenAsync();
        }

        /// <summary>
        ///Set Retry Policy for the messages 
        ///which are failed to be sent to hub on first try 
        /// </summary>
        /// <param name="retryPolicy"></param>
        public void SetHubRetryPolicy(IRetryPolicy retryPolicy)
        {
            if (HubDeviceClient == null)
            {
                HubEdgeClient.SetRetryPolicy(retryPolicy);
                return;
            }

            HubDeviceClient.SetRetryPolicy(retryPolicy);
        }

        /// <summary>
        /// Close the device client communication
        /// </summary>
        /// <returns></returns>
        public Task CloseIotHubAsync()
        {
            if (HubDeviceClient == null)
            {
                return HubEdgeClient.CloseAsync();
            }

            return HubDeviceClient.CloseAsync();
        }

        /// <summary>
        /// IotHub connection change handler
        /// </summary>
        /// <param name="statusChangesHandler"></param>
        public void SetIotHubConnectionStatusChangesHandler(ConnectionStatusChangesHandler statusChangesHandler)
        {
            if (HubDeviceClient == null)
            {
                HubEdgeClient.SetConnectionStatusChangesHandler(statusChangesHandler);
                return;
            }

            HubDeviceClient.SetConnectionStatusChangesHandler(statusChangesHandler);
        }

        /// <summary>
        /// Cleanup activity
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///  Dispose client
        /// </summary>
        protected virtual void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                if (HubDeviceClient == null)
                {
                    HubEdgeClient.Dispose();//dispose edge client
                    return;
                }

                HubDeviceClient.Dispose();
            }
        }

    }
}