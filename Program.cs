using DailyLogSystem.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// ------------------------------
// Service Configuration
// ------------------------------

// Add session support
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Session timeout
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Add Razor Pages
builder.Services.AddRazorPages();

// Register MongoDB and Email services
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDbSettings"));
builder.Services.AddSingleton<MongoDbService>();
builder.Services.AddSingleton<EmailService>();

// ------------------------------
// Build and Configure the App
// ------------------------------
var app = builder.Build();

// ✅ Add error handling for production
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// ✅ Enforce HTTPS
app.UseHttpsRedirection();

// ✅ Enable static files and routing
app.UseStaticFiles();
app.UseRouting();

// ✅ Enable session (important: place after UseRouting and before MapRazorPages)
app.UseSession();

// ✅ Authorization placeholder (for future admin/user restrictions)
app.UseAuthorization();

// ✅ Map Razor Pages
app.MapRazorPages();

// ✅ Run the application
app.Run();
