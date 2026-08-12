namespace MarikinaMarket.API.Application.DTOs.Vendor.Internal
{
    public class CheckWarningList
    {
        public required int VendorId { get; set; }
        public required bool HasActiveWarning { get; set; }
    }
}