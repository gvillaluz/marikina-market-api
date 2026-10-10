using System.ComponentModel.DataAnnotations;
using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Audits.Request
{
    public class AuditLogFilter
    {
        [EnumDataType(typeof(AuditLogDateRange))]
        public AuditLogDateRange? DateRange { get; set; }
        public DateOnly? FromDate { get; set; }
        public DateOnly? ToDate { get; set; }
        [Range(1, int.MaxValue)]
        public int? UserId { get; set; }
        [EnumDataType(typeof(Role))]
        public Role? Role { get; set; }
        [EnumDataType(typeof(Module))]
        public Module? Module { get; set; }
        [MaxLength(10)]
        public Module[] Modules { get; set; } = [];
        [EnumDataType(typeof(LogResult))]
        public LogResult? Result { get; set; }
        [MaxLength(2)]
        public LogResult[] Results { get; set; } = [];
        [StringLength(200)]
        public string? Search { get; set; }
    }
}
