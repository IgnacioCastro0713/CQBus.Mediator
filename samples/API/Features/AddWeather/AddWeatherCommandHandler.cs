using CQBus.Mediator;
using CQBus.Mediator.Handlers;
using CQBus.Mediator.Messages;

namespace API.Features.AddWeather;

public sealed class AddWeatherCommandHandler(IWeatherService weatherService, IPublisher publisher)
    : IRequestHandler<AddWeatherCommand, Unit>
{
    public async ValueTask<Unit> Handle(AddWeatherCommand request, CancellationToken cancellationToken = default)
    {
        weatherService.AddWeatherForecast(request.Name);

        await publisher.Publish(new AddWeatherDomainEvent(request.Name + "Event"), cancellationToken);

        return await Task.FromResult(Unit.Value);
    }
}
