using Microsoft.Extensions.DependencyInjection;
using TaskManagement.Application.Abstractions;
using TaskManagement.Domain.Common;

namespace TaskManagement.Infrastructure.DomainEvents;

internal sealed class DomainEventDispatcher(IServiceProvider provider) : IDomainEventDispatcher
{
    public async Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken ct)
    {
        foreach (var domainEvent in events)
        {
            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
            foreach (dynamic? handler in provider.GetServices(handlerType))
                await handler!.Handle((dynamic)domainEvent, ct);
        }
    }
}
