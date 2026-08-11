using Microsoft.EntityFrameworkCore;
using RssOverEmail.Data;
using RssOverEmail.Models;
using RssOverEmail.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseSqlite(builder.Configuration.GetConnectionString("Sqlite")));

builder.Services.Configure<RssOverEmailOptions>(
    builder.Configuration.GetSection(RssOverEmailOptions.SectionName));

builder.Services.AddHttpClient<IFeedFetcher, FeedFetcher>();
builder.Services.AddScoped<IFeedRepository, FeedRepository>();
builder.Services.AddScoped<IItemRepository, ItemRepository>();
builder.Services.AddSingleton<IRssParser, RssParser>();
builder.Services.AddHostedService<RssFetchService>();

builder.Services.AddControllers();
builder.Services.AddCors(opts =>
    opts.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.UseCors();
app.MapControllers();

app.Run();
