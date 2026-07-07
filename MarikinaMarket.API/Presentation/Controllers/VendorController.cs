using MarikinaMarket.API.Application.DTOs.User.Request;
using MarikinaMarket.API.Application.DTOs.Vendor.Request;
using MarikinaMarket.API.Application.DTOs.Vendor.Response;
using MarikinaMarket.API.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarikinaMarket.API.Presentation.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class VendorController : ControllerBase
    {
        private readonly IVendorService _service;

        public VendorController(IVendorService service)
        {
            _service = service;
        }

        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<ActionResult<RegisterVendorResponse>> RegisterVendor([FromBody] RegisterVendorRequest request)
        {
            try
            {
                if (request is null)
                    return BadRequest("Request body must not be empty.");

                var vendorRegistry = await _service.CreateVendorRegistryAsync(request);

                if (vendorRegistry is null)
                    return Unauthorized("Vendor registration request failed.");

                return vendorRegistry;
            }
            catch (Exception ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpPost("approve-registry")]
        public async Task<IActionResult> ApproveRegistration([FromBody] RegistrationApprovalRequest request)
        {
            return Ok();
        }
    }
}
