var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.MauiApp1>("mauiapp1");

builder.Build().Run();
