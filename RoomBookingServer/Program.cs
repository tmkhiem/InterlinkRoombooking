using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RoomBookingServer.Components;
using RoomBookingServer.Models.Db.Attendance;
using RoomBookingServer.Models.Db.Roombooking;
using RoomBookingServer.Options;
using RoomBookingServer.Services;

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
            builder.Services.AddScoped<IBookingDocumentStorage, BookingDocumentStorage>();
            builder.Services.AddHostedService<BookingDocumentCleanupHostedService>();
            builder.Services.Configure<DocumentUploadOptions>(
                builder.Configuration.GetSection(DocumentUploadOptions.SectionName));

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
                var employee = await attendanceDb.Employees
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.Id == employeeId && e.Password == password);

                if (employee is null)
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
            });

            app.MapGet("/auth/logout", async (HttpContext context) =>
            {
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return Results.Redirect("/login");
            });

            app.MapGet("/api/bookings/{bookingId:int}/documents", async (
                int bookingId,
                IDbContextFactory<RoombookingContext> roomFactory,
                CancellationToken cancellationToken) =>
            {
                await using var roomDb = await roomFactory.CreateDbContextAsync(cancellationToken);
                var documents = await roomDb.BookingDocuments
                    .Where(d => d.RoomBookingId == bookingId && d.DeletedAtUtc == null)
                    .OrderBy(d => d.UploadedAtUtc)
                    .Select(d => new
                    {
                        d.BookingDocumentId,
                        d.RoomBookingId,
                        d.OriginalFileName,
                        d.ContentType,
                        d.FileSizeBytes,
                        d.UploadedAtUtc,
                        d.UploadedBy
                    })
                    .ToListAsync(cancellationToken);

                return Results.Ok(documents);
            }).RequireAuthorization();

            app.MapGet("/api/bookings/{bookingId:int}/documents/{documentId:long}", async (
                HttpContext context,
                int bookingId,
                long documentId,
                IDbContextFactory<RoombookingContext> roomFactory,
                IBookingDocumentStorage documentStorage,
                CancellationToken cancellationToken) =>
            {
                await using var roomDb = await roomFactory.CreateDbContextAsync(cancellationToken);
                var document = await roomDb.BookingDocuments
                    .AsNoTracking()
                    .Where(d =>
                        d.RoomBookingId == bookingId &&
                        d.BookingDocumentId == documentId &&
                        d.DeletedAtUtc == null)
                    .Select(d => new
                    {
                        d.StoragePath,
                        d.OriginalFileName,
                        d.StoredFileName,
                        d.ContentType,
                        BookingCreator = d.RoomBooking.Creator
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                if (document is null)
                {
                    return Results.NotFound();
                }

                var employeeId = context.User.FindFirstValue("employee_id")
                                 ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrWhiteSpace(employeeId))
                {
                    return Results.Unauthorized();
                }

                var canDownload = string.Equals(document.BookingCreator, employeeId, StringComparison.OrdinalIgnoreCase);
                if (!canDownload)
                {
                    return Results.Forbid();
                }

                var stream = await documentStorage.OpenReadAsync(document.StoragePath, cancellationToken);
                if (stream is null)
                {
                    return Results.NotFound();
                }

                var fileName = Path.GetFileName(document.OriginalFileName);
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    fileName = document.StoredFileName;
                }

                return Results.File(stream, document.ContentType ?? "application/octet-stream", fileName);
            }).RequireAuthorization();

            app.MapPost("/api/bookings/{bookingId:int}/documents", async (
                HttpContext context,
                int bookingId,
                IDbContextFactory<RoombookingContext> roomFactory,
                IBookingDocumentStorage documentStorage,
                IOptions<DocumentUploadOptions> uploadOptions,
                CancellationToken cancellationToken) =>
            {
                var maxFileSizeBytes = uploadOptions.Value.MaxFileSizeBytes;
                var maxFilesPerRequest = uploadOptions.Value.MaxFileCount;

                var employeeId = context.User.FindFirstValue("employee_id")
                                 ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrWhiteSpace(employeeId))
                {
                    return Results.Unauthorized();
                }

                await using var roomDb = await roomFactory.CreateDbContextAsync(cancellationToken);
                var booking = await roomDb.Bookings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(b => b.RoomBookingId == bookingId, cancellationToken);

                if (booking is null)
                {
                    return Results.NotFound();
                }

                var isAdmin = context.User.IsInRole("Admin");
                var canUpload = isAdmin || string.Equals(booking.Creator, employeeId, StringComparison.OrdinalIgnoreCase);
                if (!canUpload)
                {
                    return Results.Forbid();
                }

                if (!context.Request.HasFormContentType)
                {
                    return Results.BadRequest("Dữ liệu tải lên không hợp lệ.");
                }

                var form = await context.Request.ReadFormAsync(cancellationToken);
                if (form.Files.Count == 0)
                {
                    return Results.BadRequest("Vui lòng chọn ít nhất một tập tin.");
                }

                if (form.Files.Count > maxFilesPerRequest)
                {
                    return Results.BadRequest($"Chỉ được tải tối đa {maxFilesPerRequest} tập tin mỗi lần.");
                }

                var uploadedDocuments = new List<object>();
                var storedPaths = new List<string>();

                try
                {
                    foreach (var file in form.Files)
                    {
                        if (file.Length <= 0)
                        {
                            continue;
                        }

                        if (file.Length > maxFileSizeBytes)
                        {
                            var safeFileName = Path.GetFileName(file.FileName);
                            if (string.IsNullOrWhiteSpace(safeFileName))
                            {
                                safeFileName = "tập tin";
                            }

                            if (safeFileName.Length > 100)
                            {
                                safeFileName = safeFileName[..100];
                            }

                            return Results.BadRequest($"Tập tin '{safeFileName}' vượt quá giới hạn {maxFileSizeBytes / 1024 / 1024} MB.");
                        }

                        await using var fileStream = file.OpenReadStream();
                        var stored = await documentStorage.SaveAsync(
                            fileStream,
                            file.FileName,
                            file.ContentType,
                            employeeId,
                            cancellationToken);
                        storedPaths.Add(stored.StoragePath);

                        var entity = new BookingDocument
                        {
                            RoomBookingId = bookingId,
                            UploadedBy = employeeId,
                            OriginalFileName = stored.OriginalFileName,
                            StoredFileName = stored.StoredFileName,
                            StoragePath = stored.StoragePath,
                            ContentType = stored.ContentType,
                            FileSizeBytes = stored.FileSizeBytes,
                            UploadedAtUtc = stored.UploadedAtUtc,
                            ExpiresAtUtc = stored.ExpiresAtUtc
                        };

                        roomDb.BookingDocuments.Add(entity);
                        uploadedDocuments.Add(new
                        {
                            entity.OriginalFileName,
                            entity.FileSizeBytes,
                            entity.ContentType,
                            entity.UploadedAtUtc
                        });
                    }

                    await roomDb.SaveChangesAsync(cancellationToken);
                    return Results.Ok(uploadedDocuments);
                }
                catch
                {
                    foreach (var path in storedPaths)
                    {
                        await documentStorage.DeleteAsync(path, cancellationToken);
                    }

                    throw;
                }
            }).RequireAuthorization();

            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            app.Run();
        }

        private static Task<bool> IsEmployeeAdminAsync(RoombookingContext db, Employee employee)
        {
            return db.Admins.AnyAsync(a => a.EmployeeId == employee.Id);
        }
    }
}
