namespace MarikinaMarket.API.Application.DTOs.Vendor.Response
{
    public class VendorRegistrationDocumentResponse
    {
        public required string DocumentType { get; set; }
        public required string FileName { get; set; }
        public required string ContentType { get; set; }
        public long Size { get; set; }
        public required string Url { get; set; }
    }
}
