using Transformer_.Data.NASA;
using Radzen;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddHttpClient();
builder.Services.AddServerSideBlazor();
builder.Services.AddScoped<ContextMenuService>();
builder.Services.AddScoped<NasaService>();
builder.Services.AddTransient(sp => new HttpClient { BaseAddress = new Uri(builder.Environment.WebRootPath) });
builder.Services.AddRadzenComponents();

var app = builder.Build();

var provider = new FileExtensionContentTypeProvider();
// Add new MIME type mappings
provider.Mappings[".res"] = "application/octet-stream";
provider.Mappings[".pexe"] = "application/x-pnacl";
provider.Mappings[".nmf"] = "application/octet-stream";
provider.Mappings[".mem"] = "application/octet-stream";
provider.Mappings[".wasm"] = "application/wasm";

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")),
    ContentTypeProvider = provider
});

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

await app.RunAsync();
