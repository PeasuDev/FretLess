using Fretless.Web.Client;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddSingleton<MidiService>();
builder.Services.AddSingleton<AudioInputNoteService>();
builder.Services.AddSingleton<InstrumentConnectionService>();

await builder.Build().RunAsync();
