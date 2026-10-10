using System.Globalization;
using System.Security.Claims;
using MarikinaMarket.API.Application.DTOs.Audits.Internal;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace MarikinaMarket.API.Presentation.Middleware
{
    public class AuditLogMiddleware
    {
        private readonly RequestDelegate _next;
        public AuditLogMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context, AuditLogContext audit, IAuditLogService service)
        {
            var action = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
            if (action is null) { await _next(context); return; }

            if (action.ControllerName == "Auth" &&
                action.ActionName is "RefreshTokens" or "RefreshAccessToken")
            {
                await _next(context);
                return;
            }

            var module = ModuleFor(action.ControllerName);
            if (action.ControllerName == "Auth" && action.ActionName == "Register") module = Module.Users;
            var description = DescriptionFor(action);
            audit.IsMutation = !HttpMethods.IsGet(context.Request.Method) &&
                !HttpMethods.IsHead(context.Request.Method) && !HttpMethods.IsOptions(context.Request.Method) &&
                action.ActionName is not ("GetLoggedInUserInfo" or "GetTicketFineSummary" or
                    "GetWarningOrdinances" or "FindAccount");
            audit.Entry = new AuditLog
            {
                Action = action.ActionName, Module = module, Result = LogResult.Success,
                TargetId = RouteTarget(context),
                Details = $"{description} completed successfully."
            };
            // These endpoints commit their business mutation once, at the end of the action.
            audit.SaveWithTransaction = audit.IsMutation &&
                (action.ControllerName == "User" && action.ActionName == "EditUserInformation" ||
                 action.ControllerName == "Ordinance" && action.ActionName is "Create" or "Update" ||
                 action.ControllerName == "AdminUser" && action.ActionName == "CreateUser" ||
                 action.ControllerName == "Vendor" && action.ActionName is "ApproveRegistration" or "DeclineRegistration" or "RequestMoreInformation" ||
                 action.ControllerName == "EnforcerTicket" && action.ActionName is "SaveNewInspection" or "SubmitTicketReceiptProof" or "LogCommunityServiceHours" ||
                 action.ControllerName == "AdminTicket" && action.ActionName == "UpdateTicketStatus" ||
                 action.ControllerName == "Auth" && action.ActionName is "Register" or "ChangePassword");

            await _next(context);
            var status = context.Response.StatusCode;
            if (status is StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden)
            {
                CaptureActor(context, audit);
                audit.Entry.Action = "AuthorizationFailed";
                audit.Entry.Module = Module.Security;
                audit.Entry.Result = LogResult.Failed;
                audit.Entry.Details = $"{(audit.Entry.UserId.HasValue ? "" : "Anonymous: ")}{description} was denied (HTTP {status}).";
                await service.RecordAsync(audit.Entry);
                return;
            }

            if (!audit.IsMutation || audit.IsValidationFailure ||
                (status == StatusCodes.Status400BadRequest && !audit.IsBusinessFailure)) return;
            if (audit.Stored && status < 400 && !audit.IsBusinessFailure) return;

            CaptureActor(context, audit);
            audit.Entry.Result = status >= 400 || audit.IsBusinessFailure ? LogResult.Failed : LogResult.Success;
            if (audit.Stored)
            {
                audit.Entry.Action = "RequestFailedAfterCommit";
                audit.Entry.Details = $"{description} committed, but request completion failed (HTTP {status}).";
                await service.RecordAsync(audit.Entry);
                return;
            }
            audit.Entry.Details = (audit.Entry.UserId.HasValue ? "" : "Anonymous: ") + $"{description} " +
                (audit.Entry.Result == LogResult.Success ? "completed successfully." : $"failed (HTTP {status}).");
            await service.RecordAsync(audit.Entry);
        }

        internal static void CaptureActor(HttpContext context, AuditLogContext audit)
        {
            // Never attribute invalid bearer tokens or request-body IDs to an actor.
            if (audit.ActorResolved || audit.Entry is null || context.User.Identity?.IsAuthenticated != true) return;
            var log = audit.Entry;
            if (int.TryParse(context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) && id > 0)
                log.UserId = id;
            if (Enum.TryParse<Role>(context.User.FindFirst("role")?.Value, out var role) && Enum.IsDefined(role))
                log.Role = role;
        }

        private static string? RouteTarget(HttpContext context)
        {
            foreach (var key in new[] { "id", "ticketId", "notificationId", "registrationId", "vendorId", "marketSectionId" })
                if (context.Request.RouteValues.TryGetValue(key, out var value) &&
                    int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var id) && id > 0)
                    return id.ToString(CultureInfo.InvariantCulture);
            return null;
        }

        private static Module ModuleFor(string controller) => controller switch
        {
            "Auth" => Module.Security,
            "User" or "AdminUser" or "AdminAccount" or "AdminEnforcer" => Module.Users,
            "Vendor" or "AdminVendor" => Module.Vendors,
            "Ticket" or "AdminTicket" or "EnforcerTicket" => Module.Tickets,
            "Ordinance" => Module.Ordinances,
            "MarketSection" => Module.MarketSections,
            "Notification" => Module.Notifications,
            "AdminBackup" => Module.Backups,
            "AdminAuditLog" => Module.AuditLogs,
            _ => Module.Reports
        };

        private static string DescriptionFor(ControllerActionDescriptor action)
            => $"{action.ControllerName}.{action.ActionName}" switch
            {
                "User.EditUserInformation" => "Profile update",
                "User.UpdateProfilePicture" => "Profile photo replacement",
                "User.RemoveProfilePhoto" => "Profile photo removal",
                "User.RegisterDeviceToken" => "Notification device registration",
                "Auth.Login" or "Auth.LoginMobile" => "Sign-in verification code request",
                "Auth.VerifyLogin" or "Auth.VerifyLoginMobile" => "User sign-in",
                "Auth.ChangePassword" or "Auth.MandatoryChangePassword" => "Password change",
                "Auth.SendOtpCode" => "Password-reset code request",
                "Auth.VerifyCode" => "Password-reset code verification",
                "Auth.ResetPassword" => "Password reset",
                "Auth.Register" or "AdminUser.CreateUser" => "User account creation",
                "Vendor.RegisterVendor" or "AdminVendor.RegisterVendor" => "Vendor registration",
                "Vendor.ApproveRegistration" => "Vendor registration approval",
                "Vendor.DeclineRegistration" => "Vendor registration rejection",
                "Vendor.RequestMoreInformation" => "Additional vendor information request",
                "EnforcerTicket.SaveNewInspection" => "Inspection creation",
                "EnforcerTicket.SubmitTicketReceiptProof" => "Ticket receipt submission",
                "EnforcerTicket.LogCommunityServiceHours" => "Community-service hours recording",
                "AdminTicket.UpdateTicketStatus" => "Ticket status update",
                "Ordinance.Create" => "Ordinance creation",
                "Ordinance.Update" => "Ordinance update",
                "MarketSection.Create" => "Market section creation",
                "MarketSection.Update" => "Market section update",
                "MarketSection.UpdateActiveStatus" => "Market section status update",
                "Notification.MarkAsRead" => "Notification marked as read",
                "AdminBackup.Create" => "Manual backup creation",
                "AdminBackup.UpdateSchedule" => "Backup schedule update",
                "AdminAuditLog.GetSummaries" or "AdminAuditLog.GetDetails" or "AdminAuditLog.GetCounts" => "Audit log access",
                _ => "The requested operation"
            };
    }
}
