using Producer.Models;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

using Producer.Services;

var producer = new RabbitMqProducer();

await producer.SendAsync();