var builder = DistributedApplication.CreateBuilder(args);

var sqlServer = builder.AddSqlServer("sql")
    .AddDatabase("orleans-storage");

var api = builder.AddProject<Projects.RiskDataPlatform_Api>("risk-api")
    .WithReference(sqlServer)
    .WaitFor(sqlServer);

builder.Build().Run();
