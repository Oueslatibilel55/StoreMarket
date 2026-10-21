var builder = DistributedApplication.CreateBuilder(args);

var pgUser = builder.AddParameter("pg-user", secret: false);
var pgPassword = builder.AddParameter("pg-password", secret: true);

var postgres = builder
    .AddPostgres("postgres", userName: pgUser, password: pgPassword)
    // A fixed host port, so `dotnet ef database update` and a standalone run of the API
    // reach the same database with a static connection string. Without it the container
    // gets a random host port on every run.
    .WithHostPort(62750)
    .WithDataVolume("webapp-pgdata")
    // One container reused for the life of the project. Aspire's default is ephemeral,
    // which leaks a stopped container after every force-kill, crash or reboot.
    .WithContainerName("webapp-postgres-aspire")
    .WithLifetime(ContainerLifetime.Persistent);

var webappDb = postgres.AddDatabase("webapp");

builder.AddProject<Projects.WebApp_Api>("api")
    .WithReference(webappDb)
    .WaitFor(webappDb)
    // Pin the API to 5185 with no Aspire proxy: the Angular dev proxy and CORS both
    // depend on a stable address, and a proxied resource gets a random target port.
    .WithEndpoint("http", endpoint =>
    {
        endpoint.Port = 5185;
        endpoint.TargetPort = 5185;
        endpoint.IsProxied = false;
    })
    .WithExternalHttpEndpoints();

// pgAdmin comes from docker-compose (http://localhost:5051), not from Aspire.
builder.AddExternalService("pgadmin", "http://localhost:5051")
    .WithHttpHealthCheck("/login");

builder.Build().Run();
