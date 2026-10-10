using System.ComponentModel.DataAnnotations;
using System.Text.Encodings.Web;
using MarikinaMarket.API.Application.DTOs.User.Request;
using MarikinaMarket.API.Application.DTOs.User.Response;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using MarikinaMarket.API.Application.DTOs.Audits.Internal;

namespace MarikinaMarket.API.Application.Services
{
    public class AdminUserService : IAdminUserService
    {
        private readonly IUserRepository _repository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailService _emailService;
        private readonly ILogger<AdminUserService> _logger;
        private readonly AuditLogContext? _audit;
        private readonly IAuditLogService? _auditService;

        public AdminUserService(
            IUserRepository repository,
            IUnitOfWork unitOfWork,
            IEmailService emailService,
            ILogger<AdminUserService> logger, AuditLogContext? audit = null, IAuditLogService? auditService = null)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
            _emailService = emailService;
            _logger = logger;
            _audit = audit;
            _auditService = auditService;
        }

        public async Task<AdminUserResponse> CreateUserAsync(CreateAdminUserRequest request, int adminId)
        {
            if (adminId <= 0)
                throw new UnauthorizedAccessException("Invalid admin identity.");

            Validator.ValidateObject(request, new ValidationContext(request), validateAllProperties: true);
            var role = request.Role ?? throw new ValidationException("Role is required.");
            if (role is not (Role.HeadAdmin or Role.AdminOfficer or Role.MarketEnforcer))
                throw new ValidationException("Select a staff role.");

            var dateOfBirth = request.DateOfBirth ?? throw new ValidationException("Birth date is required.");
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Asia/Manila"));
            if (dateOfBirth == DateOnly.MinValue || dateOfBirth > today)
                throw new ValidationException("Enter a valid birth date.");

            var email = request.Email.Trim();
            User user;
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                if (await _repository.FindByEmailAsync(email) is not null)
                    throw new ValidationException("Email is already registered.");

                if (!await _repository.RoleExistsAsync(role))
                    throw new InvalidRequestException("Selected role is unavailable.");

                var prefix = role switch
                {
                    Role.HeadAdmin => "HAD",
                    Role.AdminOfficer => "ADM",
                    _ => "ENF"
                };
                var sequence = await _repository.GetNextUserNameAsync();
                var now = DateTime.UtcNow;
                var username = $"{prefix}{sequence}-{now.Year}";
                user = new User
                {
                    UserName = username,
                    FirstName = request.FirstName.Trim(),
                    MiddleName = string.IsNullOrWhiteSpace(request.MiddleName) ? null : request.MiddleName.Trim(),
                    LastName = request.LastName.Trim(),
                    DateOfBirth = dateOfBirth,
                    Email = email,
                    PhoneNumber = request.PhoneNumber,
                    HouseNumber = request.HouseNumber.Trim(),
                    Street = request.Street.Trim(),
                    Barangay = request.Barangay.Trim(),
                    City = request.City.Trim(),
                    Status = AccountStatus.Active,
                    MustChangePassword = true,
                    CreatedAt = now
                };

                var result = await _repository.CreateUserAsync(user, username);
                if (!result.Succeeded)
                    throw new ValidationException(GetCreationError(result));

                var roleResult = await _repository.AddToRoleAsync(user, role.ToString());
                if (!roleResult.Succeeded)
                    throw new InvalidRequestException("Could not assign role.");

                if (_audit?.Entry is not null) _audit.Entry.TargetId = user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (_auditService is not null) await _auditService.StageCurrentAsync();
                await _unitOfWork.CommitAsync();
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }

            var emailSent = await SendAccountEmailAsync(user, adminId);
            return new AdminUserResponse
            {
                Id = user.Id,
                Username = user.UserName!,
                Role = role,
                Email = user.Email!,
                Status = user.Status,
                MustChangePassword = user.MustChangePassword,
                EmailSent = emailSent,
                Message = "User created."
            };
        }

        private static string GetCreationError(IdentityResult result)
        {
            if (result.Errors.Any(e => e.Code == "DuplicateEmail"))
                return "Email is already registered.";
            if (result.Errors.Any(e => e.Code == "DuplicateUserName"))
                return "Username already exists. Try again.";

            return "Could not create user.";
        }

        private async Task<bool> SendAccountEmailAsync(User user, int adminId)
        {
            var username = HtmlEncoder.Default.Encode(user.UserName!);
            var firstName = HtmlEncoder.Default.Encode(user.FirstName);
            var body = $"<h2>Your account is ready</h2><p>Hello {firstName},</p>" +
                $"<p>Your username is <strong>{username}</strong>.</p>" +
                "<p>Your initial password is the same as your username. Change it after signing in.</p>" +
                "<p>An email verification code is required each time you sign in.</p>";
            try
            {
                await _emailService.SendEmailAsync(user.Email!, "Marikina Market account created", body);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "Account email failed for user {UserId}, created by admin {AdminId}. Error type: {ErrorType}",
                    user.Id, adminId, ex.GetType().Name);
                return false;
            }
        }
    }
}
