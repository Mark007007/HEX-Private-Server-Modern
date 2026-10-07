using System.Net;
using HexServer.Protocol.Services;
using HexServer.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<SessionRegistry>();
builder.Services.AddSingleton<GameSessionRegistry>();

builder.Services.AddSingleton<ServiceRouter>(services =>
{
    var router = new ServiceRouter();

    router.Register(
        HexServer.Contracts.ServiceIds.LoadBalancer,
        new LoadBalancerServiceHandler(
            services.GetRequiredService<GameSessionRegistry>()));

    return router;
});

builder.Services.AddSingleton<HcpTcpServer>(services =>
    new HcpTcpServer(
        new IPEndPoint(IPAddress.Any, 9933),
        services.GetRequiredService<SessionRegistry>(),
        services.GetRequiredService<ServiceRouter>()));

var app = builder.Build();

var tcp = app.Services.GetRequiredService<HcpTcpServer>();
await tcp.StartAsync();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    server = "hex-server-modern",
    protocol = "hconnect",
    tcpPort = 9933,
    activeSessions =
        app.Services.GetRequiredService<SessionRegistry>().Count
}));

app.MapGet("/protocol", () => Results.Ok(new
{
    framing = "~HCP~ + uint32 BE content/header/body sizes",
    header = "UTF-8 JSON",
    body = "HEX DataWrapper / ObjFmt",
    services = new
    {
        LoadBalancer = HexServer.Contracts.ServiceIds.LoadBalancer,
        GameSession = HexServer.Contracts.ServiceIds.GameSession
    },
    dataTypes = new
    {
        StartSession = HexServer.Contracts.LoadBalancerDataTypes.StartSession,
        FindSession = HexServer.Contracts.LoadBalancerDataTypes.FindSession,
        JoinSession = HexServer.Contracts.LoadBalancerDataTypes.JoinSession,
        PlayerTransaction = HexServer.Contracts.GameSessionDataTypes.PlayerTransaction,
        SessionSyncEvent = HexServer.Contracts.GameSessionDataTypes.SessionSyncEvent
    },
    defaultPort = 9933
}));

app.Lifetime.ApplicationStopping.Register(
    () => tcp.DisposeAsync().AsTask().GetAwaiter().GetResult());

await app.RunAsync();
