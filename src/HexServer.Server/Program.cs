using System.Net;
using HexServer.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<SessionRegistry>();
builder.Services.AddSingleton<HcpTcpServer>(_ =>
    new HcpTcpServer(
        new IPEndPoint(IPAddress.Any, 9933),
        _.GetRequiredService<SessionRegistry>()));

var app = builder.Build();

var tcp = app.Services.GetRequiredService<HcpTcpServer>();
await tcp.StartAsync();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    server = "hex-server-modern",
    protocol = "hconnect",
    tcpPort = 9933,
    activeSessions = app.Services.GetRequiredService<SessionRegistry>().Count
}));

app.MapGet("/protocol", () => Results.Ok(new
{
    framing = "~HCP~ + uint32 BE content/header/body sizes",
    header = "UTF-8 JSON",
    body = "HEX DataWrapper / EncData / ObjFmt",
    defaultPort = 9933
}));

app.Lifetime.ApplicationStopping.Register(() => tcp.DisposeAsync().AsTask().GetAwaiter().GetResult());

await app.RunAsync();
