using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Abstractions;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.Resource;
using RoomBookingBackend.Models;
using RoomBookingBackend.Utilities;
using System.Reflection.PortableExecutable;

namespace RoomBookingBackend
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

#if DEBUG
            builder.Services.AddReverseProxy()
                .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
#endif

            // Add services to the container.
            builder.Services
                .AddAuthentication(options =>
                {
                    // Set your primary cookie scheme as the default.
                    // This will be the scheme that manages the logged-in session.
                    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme; // Also default challenge to the cookie scheme
                })
                .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
                {
                    // This is your main application cookie.
                    options.LoginPath = "/api/authenticate"; // This will be the page for unauthenticated users
                    options.LogoutPath = "/api/signout";
                    options.SlidingExpiration = true;
                    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
                    // You might need to set a specific cookie name if you anticipate conflicts
                    // options.Cookie.Name = "YourAppNameCookie";
                })
                .AddYandex("Yandex", options => // Use a consistent casing for scheme names
                {
                    var config = builder.Configuration.GetSection("Yandex");
                    options.ClientId = config["ClientId"] ?? throw new ArgumentNullException("Yandex ClientId cannot be null");
                    options.ClientSecret = config["ClientSecret"] ?? throw new ArgumentNullException("Yandex ClientSecret cannot be null");

                    // Yandex specific options:
                    // By default, Yandex will try to sign in to the default cookie scheme (CookieAuthenticationDefaults.AuthenticationScheme).
                    // This is usually what you want.
                    // options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme; // Explicitly state if needed
                })
                .AddMicrosoftIdentityWebApp(builder.Configuration,
                    openIdConnectScheme: "AzureAD", // A distinct name for the OpenID Connect scheme
                    cookieScheme: "AzureADCookie"); // A distinct name for the cookie scheme implicitly added by MicrosoftIdentityWebApp

            // Post-configuration for AzureAD
            builder.Services.Configure<OpenIdConnectOptions>("AzureAD", options =>
            {
                // IMPORTANT: Tell Azure AD to sign in to your main application cookie scheme.
                // This is crucial for unifying the session management.
                options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;

                // You might also need to set the CallbackPath based on your Azure AD app registration
                // options.CallbackPath = "/signin-oidc"; // Default is usually fine, but confirm
            });


            builder.Services.AddControllers().AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = null;
            });

            builder.Services.AddDbContext<RoombookingContext>(options =>
                           options.UseSqlServer(builder.Configuration.GetConnectionString("RoombookingContext")));

            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders =
                    ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

                try
                {
                    // Get "InternalNet" string from config
                    var internalNet = builder.Configuration.GetValue<string>("InternalNet");

                    // Parse the string into an IPNetwork, the string is in CIDR format with slash prefix length
                    var ipNetwork = IPNetwork.Parse(internalNet);

                    // Add the IPNetwork to the options
                    options.KnownNetworks.Add(ipNetwork);

                    Console.WriteLine("Added internal network: " + ipNetwork.ToString());
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Couldn't get internal network info: " + ex.ToString());
                }
            });
            builder.Services.AddSingleton<TelegramBotService>(); // Register as singleton so controllers can access the same instance
            builder.Services.AddHostedService<TelegramBotService>();

            var app = builder.Build();

            app.UseForwardedHeaders();

            // Configure the HTTP request pipeline.
            //app.UseCors(builder =>
            //{
            //    builder.AllowAnyHeader();
            //    builder.AllowAnyMethod();
            //    builder.AllowAnyOrigin();
            //});

            app.UseHttpsRedirection();

#if !DEBUG
            app.UseDefaultFiles();
            app.UseStaticFiles(); // Static files will be in wwwroot folder
#endif

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

#if DEBUG
            //app.MapReverseProxy();
#endif
            app.Run();
        }
    }
}
