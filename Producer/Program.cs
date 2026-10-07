
using Producer.Services;

var producer = new RabbitMqProducer();

await producer.SendAsync();