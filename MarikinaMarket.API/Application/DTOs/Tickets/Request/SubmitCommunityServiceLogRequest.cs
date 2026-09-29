using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace MarikinaMarket.API.Application.DTOs.Tickets.Request
{
    public class SubmitCommunityServiceLogRequest
    {
        [FromForm(Name = "proof_file")]
        [Required(ErrorMessage = "Community service proof is required.")]
        public IFormFile? ProofFile { get; set; }

        [FromForm(Name = "service_date")]
        [Required(ErrorMessage = "Service date is required.")]
        public DateTime? ServiceDate { get; set; }

        [FromForm(Name = "hours_worked")]
        [Required(ErrorMessage = "Hours worked is required.")]
        [Range(typeof(decimal), "0.01", "999.99", ErrorMessage = "Hours worked must be between 0.01 and 999.99.")]
        public decimal? HoursWorked { get; set; }
    }
}
