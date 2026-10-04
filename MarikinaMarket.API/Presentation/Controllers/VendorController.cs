using MarikinaMarket.API.Application.DTOs.User.Request;
using MarikinaMarket.API.Application.DTOs.Vendor.Internal;
using MarikinaMarket.API.Application.DTOs.Vendor.Request;
using MarikinaMarket.API.Application.DTOs.Vendor.Response;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace MarikinaMarket.API.Presentation.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class VendorController : ControllerBase
    {
        private readonly IVendorService _service;

        public VendorController(IVendorService service) => _service = service;

        [AllowAnonymous]
        [HttpPost("register")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<RegisterVendorResponse>> RegisterVendor([FromForm] RegisterVendorRequest request)
        {
            var vendorRegistry = await _service.CreateVendorRegistryAsync(request);
            return Accepted(vendorRegistry);
        }

        [HttpPost("approve-registry")]
        [Authorize(Roles = nameof(Role.Admin))]
        public async Task<ActionResult<RegistrationApprovalResponse>> ApproveRegistration(
            [FromBody] RegistrationApprovalRequest request)
        {
            if (!TryGetAdminId(out var adminId))
                return Unauthorized();

            var action = new RegistrationAdminAction
            {
                VendorRegistrationId = request.VendorRegistrationId,
                RequestStatus = RequestStatus.Approved,
                Version = request.Version,
                AdminId = adminId
            };

            return Ok(await _service.ApproveVendorRegistration(request.VendorRegistrationId, action));
        }

        [HttpPost("decline-registry")]
        [Authorize(Roles = nameof(Role.Admin))]
        public async Task<ActionResult<RegistrationDeclinedResponse>> DeclineRegistration(
            [FromBody] RegistrationDeclineRequest request)
        {
            if (!TryGetAdminId(out var adminId))
                return Unauthorized();

            var action = new RegistrationAdminAction
            {
                VendorRegistrationId = request.VendorRegistrationId,
                RequestStatus = RequestStatus.Declined,
                ReviewReason = request.ReviewReason,
                ReviewRemarks = request.ReviewRemarks,
                Version = request.Version,
                AdminId = adminId
            };

            return Ok(await _service.DeclineVendorRegistration(request.VendorRegistrationId, action));
        }

        [HttpPost("request-more-information")]
        [Authorize(Roles = nameof(Role.Admin))]
        public async Task<ActionResult<RegistrationDeclinedResponse>> RequestMoreInformation(
            [FromBody] RegistrationInformationRequest request)
        {
            if (!TryGetAdminId(out var adminId))
                return Unauthorized();

            var action = new RegistrationAdminAction
            {
                VendorRegistrationId = request.VendorRegistrationId,
                RequestStatus = RequestStatus.NeedsInformation,
                ReviewReason = request.ReviewReason,
                ReviewRemarks = request.ReviewRemarks,
                Version = request.Version,
                AdminId = adminId
            };

            return Ok(await _service.RequestMoreInformation(request.VendorRegistrationId, action));
        }

        [HttpGet("stall/{businessId}")]
        [Authorize(Roles = nameof(Role.Enforcer))]
        public async Task<ActionResult<List<GetVendorResponse>>> GetVendorByStallNumber([FromRoute] string businessId)
        {
            if (string.IsNullOrWhiteSpace(businessId))
                return BadRequest("Stall number must not be empty.");

            return Ok(await _service.GetVendorByBusinessIdAsync(businessId));
        }

        [HttpGet("code/{code}")]
        [Authorize(Roles = nameof(Role.Enforcer))]
        public async Task<ActionResult<GetVendorResponse>> GetVendorByQrCode([FromRoute] string code)
        {
            if(string.IsNullOrWhiteSpace(code))
                return BadRequest("Code must not be empty.");

            return Ok(await _service.GetVendorByQrCode(code));
        }

        private bool TryGetAdminId(out int adminId)
        {
            return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out adminId);
        }
    }
}
