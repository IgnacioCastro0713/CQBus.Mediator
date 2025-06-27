using API;
using API.Behaviors;
using CQBus.Mediator;
using CQBus.Mediator.Pipelines;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
