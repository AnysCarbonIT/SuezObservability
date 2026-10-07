using PersistenceApi.Messaging;
using PersistenceApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IMessageProcessor, MessageProcessor>();

builder.Services.AddHostedService<RabbitMqConsumer>();

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok("PersistenceApi is running"));

app.Run();