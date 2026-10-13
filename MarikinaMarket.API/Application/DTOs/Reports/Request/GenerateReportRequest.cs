using System.ComponentModel.DataAnnotations;

namespace MarikinaMarket.API.Application.DTOs.Reports.Request
{
    public class GenerateReportRequest
    {
        [Required]
        public string? From { get; set; }
        [Required]
        public string? To { get; set; }
        [Required]
        public string? Type { get; set; }
    }
}
