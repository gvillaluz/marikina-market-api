using System.Security.Claims;
using MarikinaMarket.API.Application.DTOs.Notification.Response;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarikinaMarket.API.Presentation.Controllers
{
    [ApiController]
    [Route("api/{controller}")]
    [Authorize]
    public class NotificationController : ControllerBase
    {
        private readonly INotificationService _service;

        public NotificationController(INotificationService notificationService)
            => _service = notificationService;

        [HttpGet]
        [Authorize(Roles = nameof(Role.Enforcer))]
        public async Task<ActionResult<List<GetNotificationsResponse>>> GetNotifications(
            [FromQuery] int offset = 0,
            [FromQuery] string filter = "All")
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdString == null || !int.TryParse(userIdString, out var userId))
            {
                throw new UnauthorizedAccessException("Invalid or missing identity token context.");
            }

            return Ok(await _service.GetNotificationsByEnforcerIdAsync(userId, offset, filter));
        }
    }
}