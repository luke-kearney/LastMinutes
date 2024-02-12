using LastMinutes.Data;
using LastMinutes.Services;
using Microsoft.AspNetCore.Server.IIS.Core;
using Microsoft.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

#region Load Configuration File(s)

ConfigurationManager configuration = builder.Configuration;
configuration.AddJsonFile("AppData/dataSettings.json");
configuration.AddJsonFile("AppData/apiConnections.json");

#endregion


#region Data Settings

string DB_Connection = configuration.GetValue<string>("DB_Connection") ?? throw new InvalidOperationException("Configuration error: 'DB_Connection' property not found!");
string DB_Type = configuration.GetValue<string>("DB_Type") ?? throw new InvalidOperationException("Configuration error: 'DB_Type' property was not found!");


switch (DB_Type)
{
    case "SQL":
        builder.Services.AddDbContext<LMData>(options => options.UseSqlServer(DB_Connection));
        break;

    default:
        throw new InvalidOperationException("Data configuration error: Invalid or Unsupported DB Type specified!");
}

#endregion


#region Services

// Add Data Grabbers
builder.Services.AddTransient<ILastFMGrabber, LastFMGrabber>();
builder.Services.AddTransient<ISpotifyGrabber, SpotifyGrabber>();
builder.Services.AddTransient<IMusicBrainz, MusicBrainz>();
builder.Services.AddTransient<IDeezerGrabber, DeezerGrabber>();

// Add queue manager
builder.Services.AddTransient<IQueueManager, QueueManager>();

// Add cache manager
builder.Services.AddTransient<ICacheManager, CacheManager>();

// Add custom queue controller
builder.Services.AddHostedService<QueueMonitor>();


// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages().AddRazorRuntimeCompilation();

#endregion

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=LastMinutes}/{action=Index}/{id?}");


app.Run();
