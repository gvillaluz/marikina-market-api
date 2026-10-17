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
using Microsoft.EntityFrameworkCore;
using Npgsql;

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

        public async Task<AdminAccountDetailsResponse> GetAccountAsync(int userId, int adminId)
        {
            ValidateAccountTarget(userId, adminId);
            var user = await _repository.GetUserAsync(userId) ?? throw new RecordNotFoundException("User not found.");
            return await AccountDetailsAsync(user);
        }

        public async Task<AdminAccountDetailsResponse> UpdateAccountAsync(int userId, UpdateAdminUserRequest request, int adminId)
        {
            ValidateAccountTarget(userId, adminId);
            Validator.ValidateObject(request, new ValidationContext(request), validateAllProperties: true);
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Asia/Manila"));
            if (request.DateOfBirth is null || request.DateOfBirth == DateOnly.MinValue || request.DateOfBirth > today)
                throw new ValidationException("Enter a valid birth date that is not in the future.");

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var user = await _repository.GetUserAsync(userId) ?? throw new RecordNotFoundException("User not found.");
                if (user.Version != request.Version)
                    throw new ConcurrencyConflictException("This account was updated by another user. Reload the account before saving again.");
                var email = request.Email.Trim();
                var owner = await _repository.FindByEmailAsync(email);
                if (owner is not null && owner.Id != userId)
                    throw new ValidationException("Email is already in use.");

                if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)) user.EmailConfirmed = false;
                if (!string.Equals(user.PhoneNumber, request.PhoneNumber, StringComparison.Ordinal)) user.PhoneNumberConfirmed = false;
                user.FirstName = request.FirstName.Trim();
                user.MiddleName = string.IsNullOrWhiteSpace(request.MiddleName) ? null : request.MiddleName.Trim();
                user.LastName = request.LastName.Trim();
                user.DateOfBirth = request.DateOfBirth.Value;
                user.Email = email;
                user.PhoneNumber = request.PhoneNumber;
                user.HouseNumber = request.HouseNumber.Trim();
                user.Street = request.Street.Trim();
                user.Barangay = request.Barangay.Trim();
                user.City = request.City.Trim();
                var result = await _repository.UpdateUserAsync(user);
                if (!result.Succeeded)
                {
                    if (result.Errors.Any(error => error.Code == "ConcurrencyFailure"))
                        throw new ConcurrencyConflictException("This account was updated by another user. Reload the account before saving again.");
                    throw new ValidationException(result.Errors.Any(error => error.Code == "DuplicateEmail")
                        ? "Email is already in use." : "Could not update this account.");
                }
                if (_audit?.Entry is not null) _audit.Entry.TargetId = userId.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (_auditService is not null) await _auditService.StageCurrentAsync();
                var response = await AccountDetailsAsync(user);
                await _unitOfWork.CommitAsync();
                return response;
            }
            catch (Exception exception) when (exception is DbUpdateConcurrencyException
                or PostgresException { SqlState: PostgresErrorCodes.SerializationFailure }
                or DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.SerializationFailure } })
            {
                await _unitOfWork.RollbackAsync();
                throw new ConcurrencyConflictException("This account was updated by another user. Reload the account before saving again.");
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }

        private static void ValidateAccountTarget(int userId, int adminId)
        {
            if (adminId <= 0) throw new UnauthorizedAccessException("Invalid admin identity.");
            if (userId <= 0) throw new ValidationException("User ID must be greater than zero.");
            if (userId == adminId) throw new UnauthorizedAccessException("Your signed-in account cannot be managed from the account directory.");
        }

        private async Task<AdminAccountDetailsResponse> AccountDetailsAsync(User user) => new()
        {
            Id = user.Id, Username = user.UserName ?? "", Role = await _repository.GetRoleAsync(user), Status = user.Status,
            FirstName = user.FirstName, MiddleName = user.MiddleName, LastName = user.LastName,
            DateOfBirth = user.DateOfBirth, Email = user.Email ?? "", PhoneNumber = user.PhoneNumber ?? "",
            HouseNumber = user.HouseNumber, Street = user.Street, Barangay = user.Barangay, City = user.City, Version = user.Version
        };

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
            var body = $@"<p style=""margin:0 0 14px;color:#4C8B5A;font-size:11px;letter-spacing:1px;font-weight:bold;"">ACCOUNT CREATED</p>
                <h1 style=""margin:0 0 22px;color:#0B2D5B;font-size:26px;line-height:1.3;"">Your account is ready</h1>
                <p style=""margin:0 0 14px;"">Hello {firstName},</p>
                <div style=""background-color:#E9F0F8;border:1px solid #D9E1EC;border-radius:8px;padding:18px;margin:20px 0;""><p style=""margin:0;font-size:13px;"">Username</p><p style=""margin:4px 0 0;color:#0B2D5B;font-weight:bold;"">{username}</p></div>
                <p style=""margin:0 0 14px;"">Your initial password is the same as your username. Change it after signing in.</p>
                <p style=""margin:0 0 14px;"">An email verification code is required each time you sign in.</p>";
            try
            {
                await _emailService.SendEmailAsync(user.Email!, "Marikina Market account created", body, "#4C8B5A");
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
