using System.Diagnostics;
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;

namespace APIContagem.Messaging;

public class MessageSender
{
    private readonly ILogger<MessageSender> _logger;
    private readonly IConfiguration _configuration;
    private readonly TelemetryConfiguration _telemetryConfig;

    public MessageSender(ILogger<MessageSender> logger,
        IConfiguration configuration,
        TelemetryConfiguration telemetryConfig)
    {
        _logger = logger;
        _configuration = configuration;
        _telemetryConfig = telemetryConfig;
    }

    public void SendMessage<T>(T message)
    {
        try
        {
            DateTimeOffset inicio = DateTime.Now;
            var watch = new Stopwatch();
            watch.Start();

            var queueName = _configuration["RabbitMQ-Queue"];
            var bodyContent = JsonSerializer.Serialize(message);
            var factory = new ConnectionFactory()
            {
                Uri = new Uri(_configuration.GetConnectionString("RabbitMQ"))
            };
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            channel.QueueDeclare(queue: queueName,
                                 durable: false,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: null);

            channel.BasicPublish(exchange: String.Empty,                                 
                                 routingKey: queueName,
                                 basicProperties: null,
                                 body: Encoding.UTF8.GetBytes(bodyContent));

            watch.Stop();
            var client = new TelemetryClient(_telemetryConfig);
            client.TrackDependency(
                "RabbitMQ", $"{queueName} send", bodyContent,
                inicio, watch.Elapsed, true);

            _logger.LogInformation(
                $"RabbitMQ - Envio para a fila {queueName} concluído | " +
                $"{bodyContent}");
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Erro na publicação da mensagem.");
            throw;
        }
    }
}