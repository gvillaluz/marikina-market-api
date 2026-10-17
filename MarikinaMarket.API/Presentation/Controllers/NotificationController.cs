using System.Security.Claims;
using MarikinaMarket.API.Application.DTOs.Notification.Response;
using MarikinaMarket.API.Application.DTOs.Tickets.Response;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarikinaMarket.API.Presentation.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationController : ControllerBase
    {
        private readonly INotificationService _service;

        public NotificationController(INotificationService notificationService)
            => _service = notificationService;

        [HttpGet]
        [Authorize(Roles = nameof(Role.MarketEnforcer))]
        public async Task<ActionResult<PageResponse<GetNotificationsResponse>>> GetNotifications(
            [FromQuery] int offset = 0,
            [FromQuery] string filter = "All")
        {
            return Ok(await _service.GetNotificationsByEnforcerIdAsync(GetUserIdFromClaims(), offset, filter));
        }

        [HttpPatch("{notificationId}/read")]
        [Authorize(Roles = nameof(Role.MarketEnforcer))]
        public async Task<ActionResult> MarkAsRead([FromRoute] int notificationId)
        {
            if (notificationId <= 0) 
                return BadRequest("Notification identification must not be invalid.");
            
            int userId = GetUserIdFromClaims();

            await _service.MarkAsReadNotificationAsync(notificationId, userId);

            return NoContent();
        }

        [HttpGet("vendor")]
        [Authorize(Roles = nameof(Role.MarketVendor))]
        public async Task<ActionResult<PageResponse<GetVendorNotificationsResponse>>> GetVendorNotifications(
            [FromQuery] int offset = 0, [FromQuery] string filter = "All")
            => Ok(await _service.GetNotificationsByVendorUserIdAsync(GetUserIdFromClaims(), offset, filter));

        [HttpPatch("vendor/{notificationId:int}/read")]
        [Authorize(Roles = nameof(Role.MarketVendor))]
        public async Task<ActionResult> MarkVendorNotificationAsRead([FromRoute] int notificationId)
        {
            await _service.MarkVendorNotificationAsReadAsync(notificationId, GetUserIdFromClaims());
            return NoContent();
        }

        private int GetUserIdFromClaims()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdString == null || !int.TryParse(userIdString, out var userId) || userId <= 0)
            {
                throw new UnauthorizedAccessException("Invalid or missing identity token context.");
            }
            return userId;
        }
    }
}
