using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using RoomBookingServer.Components;
using RoomBookingServer.Models.Db.Attendance;
using RoomBookingServer.Models.Db.Roombooking;

namespace RoomBookingServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

            builder.Services.AddCascadingAuthenticationState();
            builder.Services.AddAuthorization();
            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.LoginPath = "/login";
                    options.AccessDeniedPath = "/login";
                    options.ExpireTimeSpan = TimeSpan.FromHours(8);
                    options.SlidingExpiration = true;
                });

            builder.Services.AddDbContextFactory<AttendanceContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("Attendance")));

            builder.Services.AddDbContextFactory<RoombookingContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("Roombooking")));

            var app = builder.Build();

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseAntiforgery();
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapPost("/auth/login", async (HttpContext context,
                IDbContextFactory<AttendanceContext> attendanceFactory,
                IDbContextFactory<RoombookingContext> roomFactory) =>
            {
                var form = await context.Request.ReadFormAsync();
                var employeeId = form["employeeId"].ToString().Trim();
                var password = form["password"].ToString();

                if (string.IsNullOrWhiteSpace(employeeId) || string.IsNullOrWhiteSpace(password))
                {
                    return Results.Redirect("/login?error=missing");
                }

                await using var attendanceDb = await attendanceFactory.CreateDbContextAsync();
                var employee = await attendanceDb.Employees.FirstOrDefaultAsync(e => e.Id == employeeId);

                if (employee is null || !PasswordMatches(employee.Password, password))
                {
                    return Results.Redirect("/login?error=invalid");
                }

                await using var roomDb = await roomFactory.CreateDbContextAsync();
                var isAdmin = await IsEmployeeAdminAsync(roomDb, employee);

                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, employee.Id),
                    new("employee_id", employee.Id),
                    new(ClaimTypes.Name, employee.FullName)
                };

                if (!string.IsNullOrWhiteSpace(employee.Email))
                {
                    claims.Add(new Claim(ClaimTypes.Email, employee.Email));
                }

                if (isAdmin)
                {
                    claims.Add(new Claim(ClaimTypes.Role, "Admin"));
                }

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
                return Results.Redirect("/");
            }).DisableAntiforgery();

            app.MapGet("/auth/logout", async (HttpContext context) =>
            {
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return Results.Redirect("/login");
            });

            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            app.Run();
        }

        private static bool PasswordMatches(string storedPassword, string incomingPassword)
        {
            var storedBytes = Encoding.UTF8.GetBytes(storedPassword);
            var incomingBytes = Encoding.UTF8.GetBytes(incomingPassword);
            return CryptographicOperations.FixedTimeEquals(storedBytes, incomingBytes);
        }

        private static Task<bool> IsEmployeeAdminAsync(RoombookingContext db, Employee employee)
        {
            return db.Admins.AnyAsync(a =>
                a.Email == employee.Id ||
                (!string.IsNullOrWhiteSpace(employee.Email) && a.Email == employee.Email));
        }
    }
}
