namespace MarikinaMarket.API.Application.DTOs.Vendor.Internal
{
    public class VendorRegistrationStatusCounts
    {
        public int PendingReview { get; set; }
        public int NeedsInformation { get; set; }
        public int Approved { get; set; }
        public int Rejected { get; set; }
    }
}
