using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web.Resource;
using RoomBookingBackend.Models;
using RoomBookingBackend.Utilities;
using System.ComponentModel;
using System.Globalization;

namespace RoomBookingBackend.Controllers
{
    [ApiController]
    [Route("/api")]
    [Authorize]
    public class MainController : ControllerBase
    {
        private readonly ILogger<MainController> _logger;
        private readonly RoombookingContext dbContext;
        private readonly TelegramBotService _telegramBotService;

        public MainController(ILogger<MainController> logger, RoombookingContext dbContext, TelegramBotService telegramBotService)
        {
            _logger = logger;
            this.dbContext = dbContext;
            _telegramBotService = telegramBotService;
        }

        [HttpGet("authenticate")]
        [AllowAnonymous]
        public IActionResult LoginPage()
        {
            var content = System.IO.File.ReadAllText("login.html");
            return Content(content, "text/html");
        }

        [HttpGet("headers")]
        [AllowAnonymous]
        public IActionResult GetHeaders()
        {
            var headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString());
            return Ok(headers);
        }

        [HttpGet("claims")]
        public IActionResult Test()
        {
            var name = User.Claims.FirstOrDefault(c => c.Type == "name")?.Value;
            var email = User.Claims.FirstOrDefault(c => c.Type == "preferred_username")?.Value;

            // Returns all user claims
            var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();
            return Ok($"name: {name}, email: {email}, claims: {claims}");
            //5return Ok(claims);
        }


        [HttpGet("challenge-ms")]
        [AllowAnonymous]
        public IActionResult ChallengeMicrosoft()
        {
            return Challenge(new AuthenticationProperties { RedirectUri = "/" }, "AzureAD");
        }


        [HttpGet("challenge-ya")]
        [AllowAnonymous]
        public IActionResult ChallengeYandex()
        {
            return Challenge(new AuthenticationProperties { RedirectUri = "/" }, "Yandex");
        }

        // Returns all schedules for a week containing the specified day
        [HttpGet("schedules")]
        public IActionResult GetSchedules(string date)
        {
            // Try parse YYYY-MM-DD dateformat
            if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", null, DateTimeStyles.None, out DateOnly parsedDate))
            {
                return BadRequest("Invalid date format");
            }

            // If `parsedDate` is a monday, then the week starts on `parsedDate`
            // Otherwise, the week starts on the previous monday
            var weekStart = parsedDate.DayOfWeek == DayOfWeek.Monday ? parsedDate : parsedDate.AddDays(-(int)parsedDate.DayOfWeek + 1);
            var weekEnd = weekStart.AddDays(6);

            var schedules = dbContext.Bookings
                .Where(s => s.Date >= weekStart && s.Date <= weekEnd);

            return Ok(schedules);
        }

        // Add a new schedule
        [HttpPost("schedule")]
        public async Task<IActionResult> AddSchedule([FromBody] Booking newBooking)
        {
            var (name, email) = GetNameAndEmail();

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(name))
                return Unauthorized("User is not authenticated");

            // Set the Id to default so that EF generates a new Id
            newBooking.Id = default;

            newBooking.Creator = email; // Set the creator to the email of the user
            newBooking.Name = name; // Set the name to the name of the user

            // Get all bookings for the same room and date
            var bookings = dbContext.Bookings
                .Where(s => s.Room == newBooking.Room && s.Date == newBooking.Date).AsAsyncEnumerable();

            // Check if the room is already booked
            await foreach (var booking in bookings)
                if (newBooking.IsOverlapping(booking))
                    return BadRequest("Booking overlapped");

            if (newBooking.Room == 1)
                newBooking.State = Booking.ConfirmationWait; // confirmation wait
            else
                newBooking.State = Booking.Booked; // booked

            dbContext.Bookings.Add(newBooking);

            dbContext.SaveChanges();

            // Send a message to the telegram bot
            var message = $"New booking:\n{newBooking.ToTelegramMessage()}";
            await _telegramBotService.SendMessageAsync(message); // Replace with your chat ID

            return Ok();
        }

        // Update an existing schedule
        //[HttpPut("schedule")]
        //public IActionResult UpdateSchedule([FromBody] Booking schedule)
        //{
        //    // Find existing schedule
        //    var existingSchedule = dbContext.Bookings.FirstOrDefault(b => b.Id == schedule.Id);

        //    if (existingSchedule == null)
        //        return NotFound();

        //    // Get all bookings for the same room and date
        //    var sameDayBookings = dbContext.Bookings
        //        .Where(s => s.Room == schedule.Room && s.Date == schedule.Date && s.Id != schedule.Id);

        //    // Check for overlapping bookings
        //    foreach (var booking in sameDayBookings)
        //        if (schedule.IsOverlapping(booking))
        //            return BadRequest("Booking overlapped");

        //    // If the room is room 1, set the state to confirmation wait
        //    if (schedule.Room == 1)
        //        schedule.State = Booking.ConfirmationWait; // confirmation wait
        //    else
        //        schedule.State = Booking.Booked; // booked                                 

        //    dbContext.Bookings.Update(schedule);
        //    dbContext.SaveChanges();
        //    return Ok();
        //}

        // Delete an existing schedule

        [HttpPut("confirm/{id}")]
        public async Task<IActionResult> ConfirmSchedule(int id)
        {
            var (name, email) = GetNameAndEmail();

            var admin = await IsAdmin(email);

            if (!admin)
                return Unauthorized();

            var schedule = await dbContext.Bookings.FirstOrDefaultAsync(b => b.Id == id);

            if (schedule == null)
                return NotFound();

            if (schedule.State != Booking.ConfirmationWait)
                return BadRequest("Booking is not in confirmation wait state");

            if (schedule.Room != 1)
                return BadRequest("Booking is not for room 1");

            schedule.State = Booking.Booked; // booked

            dbContext.SaveChanges();

            // Send a message to the telegram bot
            var message = $"Confirmed by {name} ({email}):\n{schedule.ToTelegramMessage()}";
            await _telegramBotService.SendMessageAsync(message); // Replace with your chat ID

            return Ok();
        }


        [HttpDelete("schedule/{id}")]
        public async Task<IActionResult> DeleteSchedule(int id)
        {
            var schedule = await dbContext.Bookings.FirstOrDefaultAsync(b => b.Id == id);

            if (schedule == null)
                return NotFound();

            var (name, email) = GetNameAndEmail();

            if (schedule.Creator != email && !await IsAdmin(email))
                return Unauthorized("You are not admin or the creator of this booking");

            dbContext.Bookings.Remove(schedule);
            dbContext.SaveChanges();

            // Send a message to the telegram bot
            var message = $"Deleted by {name} ({email}):\n{schedule.ToTelegramMessage()}";
            await _telegramBotService.SendMessageAsync(message); // Replace with your chat ID

            return Ok();
        }

        // Check privilleges
        [HttpGet("self")]
        public async Task<IActionResult> GetSelf()
        {
            var (name, email) = GetNameAndEmail();

            var isAdmin = await IsAdmin(email);

            return Ok(new
            {
                name = name,
                email = email,
                isAdmin = isAdmin
            });
        }

        [AllowAnonymous]
        [HttpGet("signin")]
        public IActionResult SignIn()
        {
            // This is a placeholder. The actual sign-in process is handled by the authentication middleware.
            return Redirect("/api/authenticate");
        }

        [HttpGet("signout")]
        public async Task<IActionResult> SignOut()
        {
            // Sign out from your primary cookie scheme
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // If you also want to sign out from external providers (e.g., Azure AD for a clean logout from the IdP)
            // You might need to handle this more explicitly if you want to redirect to the IdP's logout endpoint.
            // For now, this just clears your app's cookie.
            return Redirect("/api/authenticate");
        }

        private async Task<bool> IsAdmin(string? email)
        {
            if (string.IsNullOrEmpty(email))
                return false;
            return await dbContext.Admins.AnyAsync(a => a.Email == email);
        }

        private (string, string) GetNameAndEmail()
        {
            var yandexSurname = User.Claims.FirstOrDefault(c => c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname")?.Value;
            var yandexGivenname = User.Claims.FirstOrDefault(c => c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname")?.Value;

            var yandexEmail = User.Claims.FirstOrDefault(c => c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Value;

            var yandexName = yandexSurname?.Trim() ?? "" + " " + yandexGivenname?.Trim() ?? "";

            if (!string.IsNullOrWhiteSpace(yandexName) && !string.IsNullOrWhiteSpace(yandexEmail))
                return (yandexName, yandexEmail);


            var name = User.Claims.FirstOrDefault(c => c.Type == "name")?.Value.Trim();
            var email = User.Claims.FirstOrDefault(c => c.Type == "preferred_username")?.Value.Trim();

            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(email))
            {
                return (name, email);
            }

            // Get all claims into a dictionary
            var claims = User.Claims.ToDictionary(c => c.Type, c => c.Value);
            var claimsString = string.Join(Environment.NewLine, claims.Select(c => $"[{c.Key}]: [{c.Value}]"));

            throw new Exception("User has no info: " + claimsString);
        }
    }
}