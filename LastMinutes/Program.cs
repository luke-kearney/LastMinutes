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

bool ConfigPass = true;

if (!CheckConfig("LastFMApiUrl")) { ConfigPass = false; }
if (!CheckConfig("LastFMApiKey")) { ConfigPass = false; }

if (!CheckConfig("SpotifyApiUrl")) { ConfigPass = false; }
if (!CheckConfig("SpotifyAccUrl")) { ConfigPass = false; }
if (!CheckConfig("SpotifyClientId")) { ConfigPass = false; }
if (!CheckConfig("SpotifyClientSecret")) { ConfigPass = false; }

if (!CheckConfig("MusicBrainzApiUrl")) { ConfigPass = false; }
if (!CheckConfig("MusicBrainzUserAgent")) { ConfigPass = false; }

if (!CheckConfig("DeezerApiUrl")) { ConfigPass = false; }

if (!CheckConfig("LastMinutesApiKey")) { ConfigPass = false; }

if (!ConfigPass)
{
    throw new InvalidOperationException("Configuration error: could not find a configuration value in apiConnections.json. Please ensure it exists and has the correct format.");
}

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


#region Invoke Services

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

app.UseStatusCodePagesWithReExecute("/Error/{0}");

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=LastMinutes}/{action=Index}/{id?}");


app.Run();



#region Custom Methods
bool CheckConfig(string value)
{
    string gotValue = configuration.GetValue<string>(value) ?? string.Empty;
    if (string.IsNullOrEmpty(gotValue))
    {
        return false;
    } else
    {
        return true;
    }
}
#endregion