using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Shared.Messaging;

public static class RabbitMqExtensions
{
    public static IServiceCollection AddRabbitMq(
        this IServiceCollection services, IConfiguration configuration, string clientName)
    {
        services.AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection("RabbitMq"))
            .Validate(options =>
                !string.IsNullOrWhiteSpace(options.HostName) &&
                !string.IsNullOrWhiteSpace(options.UserName) &&
                !string.IsNullOrWhiteSpace(options.Password) &&
                !string.IsNullOrWhiteSpace(options.QueueName), "La configuration RabbitMQ est incomplète.")
            .ValidateOnStart();

        services.AddSingleton<ConnectionFactory>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
            return new ConnectionFactory
            {
                HostName = options.HostName,
                UserName = options.UserName,
                Password = options.Password,
                ClientProvidedName = clientName
            };
        });

        return services;
    }
}
