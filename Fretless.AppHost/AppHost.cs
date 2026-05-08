var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Fretless_Web>("fretless-web");

builder.Build().Run();
