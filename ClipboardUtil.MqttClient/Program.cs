﻿using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Extensions.Configuration;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace ClipboardUtil.MqttClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Load configuration from appsettings.json
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var mqttSettings = configuration.GetSection("MqttSettings").Get<MqttSettings>();

            string broker = mqttSettings.Broker;
            int port = mqttSettings.Port;
            string clientId = mqttSettings.ClientId ?? Guid.NewGuid().ToString();
            string topic = mqttSettings.Topic;
            string username = mqttSettings.Username;
            string password = mqttSettings.Password;
            string certificatePath = mqttSettings.CertificatePath;

            // Create a MQTT client factory
            var factory = new MqttFactory();

            // Create a MQTT client instance
            var mqttClient = factory.CreateMqttClient();

            // Convert cert file to text
            X509Certificate2Collection caChain = new X509Certificate2Collection();
            string cert = File.ReadAllText(certificatePath);
            caChain.ImportFromPem(cert);

            // Create MQTT client options
            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(broker, port) // MQTT broker address and port
                .WithCredentials(username, password) // Set username and password
                .WithClientId(clientId)
                .WithCleanSession()
                .WithTlsOptions(
                o =>
                {
                    // The used public broker sometimes has invalid certificates.
                    // This sample accepts all certificates. This should not be used in live environments.
                    // Works with EMQX platform
                    o.WithCertificateValidationHandler(_ => true);

                    // The default value is determined by the OS. Set manually to force version.
                    o.WithSslProtocols(SslProtocols.Tls12);

                    // Please provide the file path of your certificate file.
                    o.WithTrustChain(caChain);
                })

                .Build();

            // Connect to MQTT broker
            var connectResult = await mqttClient.ConnectAsync(options);

            if (connectResult.ResultCode == MqttClientConnectResultCode.Success)
            {
                Console.WriteLine("Connected to MQTT broker successfully.");

                // Subscribe to a topic
                await mqttClient.SubscribeAsync(topic);

                // Callback function when a message is received
                mqttClient.ApplicationMessageReceivedAsync += e =>
                {
                    Console.WriteLine($"Received message: {Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment)}");
                    return Task.CompletedTask;
                };

                // Publish a message 10 times
                for (int i = 0; i < 10; i++)
                {
                    var message = new MqttApplicationMessageBuilder()
                        .WithTopic(topic)
                        .WithPayload($"Hello, MQTT! Message number {i}")
                        .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                        .WithRetainFlag()
                        .Build();

                    await mqttClient.PublishAsync(message);
                    await Task.Delay(1000); // Wait for 1 second
                }

                // Unsubscribe and disconnect
                await mqttClient.UnsubscribeAsync(topic);
                await mqttClient.DisconnectAsync();
            }
            else
            {
                Console.WriteLine($"Failed to connect to MQTT broker: {connectResult.ResultCode}");
            }
        }
    }

    public class MqttSettings
    {
        public string Broker { get; set; }
        public int Port { get; set; }
        public string ClientId { get; set; }
        public string Topic { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string CertificatePath { get; set; }
    }
}
