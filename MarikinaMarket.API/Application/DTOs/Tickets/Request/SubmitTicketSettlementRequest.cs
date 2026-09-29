using Microsoft.AspNetCore.Mvc;

namespace MarikinaMarket.API.Application.DTOs.Tickets.Request
{
    public class SubmitTicketSettlementRequest
    {
        [FromForm(Name = "proof_file")]
        public IFormFile? ProofFile { get; set; }

        [FromForm(Name = "service_date")]
        public DateTime? ServiceDate { get; set; }

        [FromForm(Name = "hours_worked")]
        public decimal? HoursWorked { get; set; }
    }
}
