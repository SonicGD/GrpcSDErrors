using Grpc.Core;
using Grpc.Net.Client.Balancer;
using Grpc.Net.Client.Configuration;
using GrpcSDErrors;
using GrpcSDErrors.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddGrpc();
builder.Services.AddSingleton<ResolverFactory>(new StaticResolverFactory(_ =>
    [new BalancerAddress("localhost", 7289)]));
builder.Services.AddGrpcClient<Greeter.GreeterClient>(options => { options.Address = new Uri("static:///Greeter"); })
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler // Can remove this whole block and still get "errors"
    {
        ConnectTimeout = TimeSpan.FromSeconds(5),
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
        PooledConnectionLifetime = TimeSpan.FromMinutes(2), // remove or comment to fix "errors"
        EnableMultipleHttp2Connections = true
    })
    .ConfigureChannel(options =>
    {
        options.Credentials = ChannelCredentials.SecureSsl;
        options.ServiceConfig = new ServiceConfig();
        options.ServiceConfig.LoadBalancingConfigs.Add(new RoundRobinConfig()); // remove or comment to fix "errors"
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGrpcService<GreeterService>();
app.MapGet("/",
    () =>
        "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

await app.StartAsync();

var range = Enumerable.Range(1, 10);
var tasks = range.Select(_ => Task.Run(async () =>
{
    var scope = app.Services.CreateScope();
    var client = scope.ServiceProvider.GetRequiredService<Greeter.GreeterClient>();
    while (!app.Lifetime.ApplicationStopping.IsCancellationRequested)
    {
        await client.SayHelloAsync(new HelloRequest { Name = "GreeterClient" });
        await Task.Delay(5000, app.Lifetime.ApplicationStopping);
    }
}));
await Task.WhenAll(tasks);