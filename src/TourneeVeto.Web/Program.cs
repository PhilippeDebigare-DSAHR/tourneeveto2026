using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using TourneeVeto.Web;
using TourneeVeto.Ui.Data;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddScoped<IVisitRepository, IndexedDbVisitRepository>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<DemoStartupService>();

await builder.Build().RunAsync();
