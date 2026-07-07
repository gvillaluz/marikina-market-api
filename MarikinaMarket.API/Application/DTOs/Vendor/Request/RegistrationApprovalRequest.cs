using MarikinaMarket.API.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace MarikinaMarket.API.Application.DTOs.Vendor.Request
{
    public class RegistrationApprovalRequest
    {
        public required int AdminId { get; set; }
        public required RequestStatus RequestStatus { get; set; }
        public required string RowVersion { get; set; }
    }
}
