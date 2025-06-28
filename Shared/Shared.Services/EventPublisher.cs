using MassTransit;

namespace Shared.Services;

public class EventPublisher : IEventPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;

    public EventPublisher(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public async Task PublishAsync<T>(T eventData) where T : class
    {
        await _publishEndpoint.Publish(eventData);
    }
}
