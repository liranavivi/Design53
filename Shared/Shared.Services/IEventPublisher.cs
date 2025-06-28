
namespace Shared.Services;

public interface IEventPublisher
{
    Task PublishAsync<T>(T eventData) where T : class;
}
