using API;
using API.Behaviors;
using CQBus.Mediator;
using CQBus.Mediator.Pipelines;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMediator(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);

    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehavior<,>));
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));

    cfg.AddStreamBehavior(typeof(IStreamPipelineBehavior<,>), typeof(StreamUnhandledExceptionBehavior<,>));
    cfg.AddOpenStreamBehavior(typeof(StreamLoggingBehavior<,>));
});

builder.Services.AddScoped<IWeatherService, WeatherService>();

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
