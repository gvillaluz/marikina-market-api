using System.ComponentModel.DataAnnotations;
using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Backups.Request
{
    public class UpdateBackupScheduleRequest
    {
        [Required] public bool? Enabled { get; set; }
        [Required] public BackupFrequency? Frequency { get; set; }
        public DayOfWeek? DayOfWeek { get; set; }
        [Required] public string? Time { get; set; }
        [Required] public int? RetentionDays { get; set; }
    }
}
