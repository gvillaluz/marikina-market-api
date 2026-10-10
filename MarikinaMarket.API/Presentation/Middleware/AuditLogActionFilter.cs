using System.ComponentModel.DataAnnotations;
using System.Globalization;
using MarikinaMarket.API.Application;
using MarikinaMarket.API.Application.DTOs.Audits.Internal;
using MarikinaMarket.API.Application.DTOs.Auth.Response;
using MarikinaMarket.API.Application.DTOs.Backups.Response;
using MarikinaMarket.API.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MarikinaMarket.API.Presentation.Middleware
{
    public class AuditLogActionFilter : IAsyncActionFilter, IOrderedFilter
    {
        private readonly AuditLogContext _audit;
        public AuditLogActionFilter(AuditLogContext audit) => _audit = audit;
        public int Order => int.MinValue;

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (_audit.Entry is not null)
            {
                AuditLogMiddleware.CaptureActor(context.HttpContext, _audit);
                if ((_audit.Entry.Module == Module.Users || _audit.Entry.Action is "ChangePassword" or "MandatoryChangePassword") &&
                    _audit.Entry.TargetId is null)
                    _audit.Entry.TargetId = _audit.Entry.UserId?.ToString(CultureInfo.InvariantCulture);
                if (_audit.Entry.Module == Module.Vendors)
                {
                    foreach (var argument in context.ActionArguments.Values)
                        if (argument?.GetType().GetProperty("VendorRegistrationId")?.GetValue(argument) is int id && id > 0)
                            _audit.Entry.TargetId = id.ToString(CultureInfo.InvariantCulture);
                }
            }

            var executed = await next();
            if (!_audit.IsSecurityFailure && executed.Exception is (ValidationException or InvalidRequestException))
                _audit.IsValidationFailure = true;
            else if (executed.Exception is not null)
                _audit.IsBusinessFailure = true;

            if (executed.Result is not ObjectResult result) return;
            if (result.Value is ValidationProblemDetails)
                _audit.IsValidationFailure = true;
            if (result.Value is ResetPasswordResponse reset && !reset.Success ||
                result.Value is VerifyCodeResponse verify && !verify.Success ||
                result.Value is BackupResponse backup && backup.Status != BackupStatus.Completed ||
                result.Value?.GetType().GetProperty("code")?.GetValue(result.Value) is "PASSWORD_CHANGE_FAILED")
                _audit.IsBusinessFailure = true;

            if (_audit.Entry is null || _audit.Stored || result.Value is null) return;
            var names = _audit.Entry.Module == Module.Vendors ? new[] { "RegistrationId", "Id", "VendorId" } :
                _audit.Entry.Module == Module.Users ? new[] { "UserId", "Id" } : new[] { "Id", "TicketId" };
            foreach (var name in names)
                if (result.Value.GetType().GetProperty(name)?.GetValue(result.Value) is int id && id > 0)
                {
                    _audit.Entry.TargetId = id.ToString(CultureInfo.InvariantCulture);
                    break;
                }
        }
    }
}
