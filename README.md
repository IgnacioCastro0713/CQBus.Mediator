# CQBus.Mediator

![Release](https://github.com/IgnacioCastro0713/CQBus.Mediator/actions/workflows/build-release.yml/badge.svg)
[![NuGet](https://img.shields.io/nuget/dt/CQBus.Mediator.svg)](https://www.nuget.org/packages/CQBus.Mediator) 
[![NuGet](https://img.shields.io/nuget/vpre/CQBus.Mediator.svg)](https://www.nuget.org/packages/CQBus.Mediator)
[![GitHub](https://img.shields.io/github/license/IgnacioCastro0713/CQBus.Mediator?style=flat-square)](https://github.com/IgnacioCastro0713/CQBus.Mediator/blob/main/LICENSE)

CQBus.Mediator is a lightweight, extensible library for implementing the Mediator design pattern in .NET applications.

## Features

- **Request/Response Handling**: Supports sending requests and receiving responses with strongly-typed handlers.
- **Notification Publishing**: Enables broadcasting notifications to multiple handlers.
- **High Performance**: Optimized with caching and efficient reflection techniques.
- **Extensibility**: Easily integrates with dependency injection and custom publishers.
- **Cross-Platform**: Target `.NET 8`, ensuring compatibility with a wide range of applications.
- **Compatible with MediatR** — migrate with minimal effort
- **Currently supports**
  1. Simple Request:
     1. `IRequest<TRquest, TResponse>`
     2. `IRequestHandler<TRequest, TResponse>`
     3. `IPipelineBehavior<TRequest, TResponse>`
  2. Stream Request:
     1. `IStreamRequest<TRquest, TResponse>`
     2. `IStreamRequestHandler<TRequest, TResponse>`
     3. `IStreamPipelineBehavior<TRequest, TResponse>`
  3. Notifications:
     1. `INotification`
     2. `INotificationHandler<TNotification>`
  4. Publishers:
     1. `INotificationPublisher`

## Getting Started

### Installation

To use the `Mediator` library in your project, add a reference to the `CQBus.Mediator` project or include it as a NuGet package.

### Registering Services

In your `Program.cs` or DI configuration, register the mediator and its handlers:
```csharp

// Add the Mediator to your service collection
builder.Services.AddMediator(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);

    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehavior<,>));
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));

    cfg.AddStreamBehavior(typeof(IStreamPipelineBehavior<,>), typeof(StreamUnhandledExceptionBehavior<,>));
    cfg.AddOpenStreamBehavior(typeof(StreamLoggingBehavior<,>));

    cfg.PublisherStrategyType = typeof(ForeachAwaitPublisher);
});

```

#### Example Request and Handler

```csharp
// Requests/CreateUserCommand.cs
public class CreateUserCommand : IRequest<int>
{
    public string UserName { get; set; }
    public string Email { get; set; }
}

// Handlers/CreateUserCommandHandler.cs
public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, int>
{
    // Inject dependencies via constructor (e.g., IUserRepository)
    public TaskValue<int> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Creating user: {request.UserName} with email: {request.Email}");
        // Simulate database operation
        return TaskValue.FromResult(123); // Return new user ID
    }
}
```

#### Example Notification and Handler

```csharp
// Notifications/UserCreatedNotification.cs
public class UserCreatedNotification : INotification
{
    public int UserId { get; set; }
    public string UserName { get; set; }
}

// Handlers/EmailNotificationHandler.cs
public class EmailNotificationHandler : INotificationHandler<UserCreatedNotification>
{
    public TaskValue Handle(UserCreatedNotification notification, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Sending welcome email to user {notification.UserName} (ID: {notification.UserId})");
        return TaskValue.CompletedTask;
    }
}
```

#### Example Pipeline Behavior (Logging)

```csharp
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public async TaskValue<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Pre-processing
        var response = await next();
        // Post-processing
        return response;
    }
}
```

### Inject and Use the Mediator

```csharp
// In a Controller, Service, etc.
public class UserController : ControllerBase
{
    private readonly IMediator _mediator;

    public UserController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("create")]
    public async TaskValue<IActionResult> CreateUser([FromBody] CreateUserCommand command)
    {
        // Send a request and get a response
        int userId = await _mediator.Send(command);

        // Publish a notification (fire-and-forget for concurrent handlers)
        await _mediator.Publish(new UserCreatedNotification { UserId = userId, UserName = command.UserName });

        return Ok(userId);
    }
}
```

## Samples

The `samples/API/` directory provides a working ASP\.NET Core Web API example that demonstrates how to integrate and use the mediator library in a real application
To run the sample:

1. Navigate to the `samples/API/` directory.
2. Build and run the project:

   ```sh
   cd samples/API
   dotnet run
   ```

Explore the code in `samples/API/` for practical usage patterns and integration tips.
## Testing

The `tests/Mediator.Tests/` project contains comprehensive unit tests for the mediator library. These tests cover:

- **Request/Response Handling:** Verifies correct execution of commands and queries with their handlers.
- **Notification Publishing:** Ensures notifications are delivered to all registered handlers.
- **Pipeline Behaviors:** Tests the integration and execution order of custom pipeline behaviors.
- **Error Handling:** Validates exception propagation and error scenarios.
- **Dependency Injection:** Confirms correct resolution and lifetime of handlers and services.

To run the tests:

1. Navigate to the `tests/Mediator.Tests/` directory.
2. Build the project.
3. Run the tests using your terminal:

   ```sh
   dotnet test tests/Mediator.Tests/Mediator.Tests.csproj --configuration Debug --framework net8.0
   ```

Test results will be displayed in the console, helping ensure the reliability and correctness of the mediator library.

## Benchmarks

The `tests/Mediator.Benchmarks/` project uses [BenchmarkDotNet](https://benchmarkdotnet.org/) to measure the performance
of the mediator library in various scenarios. These benchmarks help evaluate and compare:

- **Request/Response Throughput:** Measures how quickly the mediator processes commands and queries.
- **Notification Publishing:** Assesses the speed of publishing notifications to multiple handlers, using different
  strategies (e.g., `ForeachAwaitPublisher`, `TaskWhenAllPublisher`).
- **Pipeline Behavior Overhead:** Evaluates the impact of adding pipeline behaviors (such as logging or validation) on
  request handling performance.
- **Handler Resolution:** Tests the efficiency of resolving and invoking handlers via dependency injection.

![Benchmarks](/img/benchmarks.png "Benchmarks")

To run the benchmarks:

1. Navigate to the `tests/Mediator.Benchmarks/` directory.
2. Build the project.
3. Run the benchmarks using your terminal:

   ```sh
   dotnet run -c Release
   ```

Benchmark results will be displayed in the console and exported to files for further analysis. Use these results to
optimize your usage and configuration of the mediator library.

## Contributing

Contributions, issues, and feature requests are welcome\! Feel free to check.

## Acknowledgments

- Inspired by the [MediatR](https://github.com/jbogard/MediatR) library.
- Benchmarks powered by [BenchmarkDotNet](https://benchmarkdotnet.org/).

-----