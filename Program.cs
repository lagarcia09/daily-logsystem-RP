using DailyLogSystem.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QuestPDF;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// -----------------------------------------
// QUESTPDF LICENSE
// -----------------------------------------
QuestPDF.Settings.License = LicenseType.Community;

// -----------------------------------------
// SESSION
// -----------------------------------------
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// -----------------------------------------
// RAZOR PAGES
// -----------------------------------------
builder.Services.AddRazorPages();

// -----------------------------------------
// MONGO + EMAIL + PDF
// -----------------------------------------
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDbSettings"));

builder.Services.AddSingleton<MongoDbService>();
builder.Services.AddSingleton<EmailService>();
builder.Services.AddSingleton<IPdfService, PdfService>();

// -----------------------------------------
// TOKEN STORE
// -----------------------------------------
builder.Services.AddSingleton<ITokenStore, InMemoryTokenStore>();

// -----------------------------------------
// AUTHENTICATION
// -----------------------------------------
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
        options.SlidingExpiration = false;
        options.LoginPath = "/Index";          // fallback login page
        options.AccessDeniedPath = "/AccessDenied";
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// ======================================================
// MIDDLEWARE PIPELINE
// ======================================================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseSession();
app.UseAuthorization();

// ======================================================
// DEFAULT ROUTE TO INTRO PAGE
// ======================================================
app.MapGet("/", context =>
{
    context.Response.Redirect("/Intro");
    return Task.CompletedTask;
});

app.MapRazorPages();

app.Run();
