using MarikinaMarket.API.Application.DTOs.User.Request;
using MarikinaMarket.API.Application.DTOs.Tickets.Response;
using MarikinaMarket.API.Application.DTOs.Vendor.Internal;
using MarikinaMarket.API.Application.DTOs.Vendor.Request;
using MarikinaMarket.API.Application.DTOs.Vendor.Response;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.Text.Encodings.Web;
using System.Security.Cryptography;

namespace MarikinaMarket.API.Application.Services
{
    public class VendorService : IVendorService
    {
        private readonly IUserRepository _userRepository;
        private readonly IVendorRepository _vendorRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMarketSectionRepository _marketSectionRepository;
        private readonly IVendorComplianceScoreService _complianceScoreService;
        private readonly IStorageService _storageService;
        private readonly IEmailService _emailService;
        private readonly ILogger<VendorService> _logger;

        public VendorService(
            IUserRepository userRepository, 
            IVendorRepository vendorRepository, 
            IUnitOfWork unitOfWork,
            IMarketSectionRepository marketSectionRepository,
            IVendorComplianceScoreService complianceScoreService,
            IStorageService storageService,
            IEmailService emailService,
            ILogger<VendorService> logger)
        {
            _userRepository = userRepository;
            _vendorRepository = vendorRepository;
            _unitOfWork = unitOfWork;
            _marketSectionRepository = marketSectionRepository;
            _complianceScoreService = complianceScoreService;
            _storageService = storageService;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<RegisterVendorResponse> CreateVendorRegistryAsync(RegisterVendorRequest request)
        {
            ValidateDocumentFile(request.GovernmentIdPhoto, "Government ID photo");
            ValidateDocumentFile(request.BusinessDocumentPhoto, "Business document photo");

            var dateOfBirth = request.DateOfBirth
                ?? throw new ValidationException("Birth date is required.");
            var governmentIdType = request.GovernmentIdType
                ?? throw new ValidationException("Government ID type is required.");
            var vendorType = request.VendorType;

            if (dateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow))
                throw new ValidationException("Date of birth cannot be in the future.");

            var user = await _userRepository.FindByEmailAsync(request.Email);

            if (user is not null)
                throw new InvalidRequestException("Email is already registered.");

            if (await _vendorRepository.RegistrationExistsAsync(request.Email, request.BusinessId))
                throw new InvalidRequestException("A registration request already exists for this email address or business ID.");

            var marketSection = await _marketSectionRepository.GetById(request.MarketSectionId);

            if (marketSection is null)
                throw new RecordNotFoundException("Selected market section was not found.");

            var hasher = new PasswordHasher<User>();

            var tempUser = new User
            { 
                FirstName = request.FirstName,
                MiddleName = request.MiddleName,
                LastName = request.LastName,
                DateOfBirth = dateOfBirth,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                HouseNumber = request.HouseNumber,
                Street = request.Street,
                Barangay = request.Barangay,
                City = request.City
            };

            var hashedPassword = hasher.HashPassword(tempUser, request.Password);
            var governmentIdPhotoKey = CreateRegistrationDocumentKey(request.GovernmentIdPhoto);
            var businessDocumentPhotoKey = CreateRegistrationDocumentKey(request.BusinessDocumentPhoto);

            await _storageService.UploadFileAsync(
                B2BucketType.Documents,
                request.GovernmentIdPhoto,
                governmentIdPhotoKey);
            await _storageService.UploadFileAsync(
                B2BucketType.Documents,
                request.BusinessDocumentPhoto,
                businessDocumentPhotoKey);

            var vendorRegistry = new VendorRegistrationRequest
            {
                GovernmentIdType = governmentIdType,
                GovernmentIdNumber = request.BusinessId,
                GovernmentIdPhotoUrl = governmentIdPhotoKey,
                BusinessDocumentPhotoUrl = businessDocumentPhotoKey,
                Type = vendorType,
                BusinessId = request.BusinessId,
                BusinessName = request.BusinessName,
                NatureOfBusiness = request.NatureOfBusiness,
                FirstName = request.FirstName,
                LastName = request.LastName,
                MiddleName = request.MiddleName,
                DateOfBirth = dateOfBirth,
                Age = CalculateAge(dateOfBirth),
                HouseNumber = request.HouseNumber,
                Street = request.Street,
                Barangay = request.Barangay,
                City = request.City,
                PhoneNumber = request.PhoneNumber,
                Email = request.Email,
                Password = hashedPassword,
                StallNumber = request.StallNumber,
                MarketSectionId = request.MarketSectionId,
                Status = RequestStatus.Pending
            };

            var savedVendorRegistry = await _vendorRepository.AddVendorRegistryAsync(vendorRegistry);
            await _vendorRepository.SaveChangesAsync();

            var encodedName = HtmlEncoder.Default.Encode(savedVendorRegistry.FirstName);
            var encodedBusinessName = HtmlEncoder.Default.Encode(savedVendorRegistry.BusinessName);
            var emailBody = $@"
                <div style=""font-family:Arial,sans-serif;max-width:600px;margin:0 auto;padding:24px;color:#333;"">
                    <h2 style=""color:#0F3D7A;"">Registration request received</h2>
                    <p>Hello {encodedName},</p>
                    <p>We received the vendor registration request for <strong>{encodedBusinessName}</strong>.</p>
                    <p>Your request is pending review. We will email you after an administrator reaches a decision.</p>
                    <p style=""color:#777;font-size:12px;margin-top:28px;"">This is an automated message. Please do not reply.</p>
                </div>";

            await _emailService.SendEmailAsync(
                savedVendorRegistry.Email,
                "Vendor Registration Request Received",
                emailBody);

            return new RegisterVendorResponse
            {
                FirstName = savedVendorRegistry.FirstName,
                LastName = savedVendorRegistry.LastName,
                BusinessName = savedVendorRegistry.BusinessName,
                EmailAddress = savedVendorRegistry.Email,
                Status = savedVendorRegistry.Status.ToString(),
                Message = "Registration successful! Your account has been submitted for review. You'll be able to sign in once an administrator approves your account."
            };
        }

        public async Task<RegistrationApprovalResponse> CreateAdminVendorRegistrationAsync(
            AdminRegisterVendorRequest request,
            int adminId)
        {
            var dateOfBirth = request.DateOfBirth
                ?? throw new ValidationException("Birth date is required.");
            var vendorType = request.VendorType
                ?? throw new ValidationException("Vendor type is required.");

            if (dateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow))
                throw new ValidationException("Date of birth cannot be in the future.");

            if (await _userRepository.FindByEmailAsync(request.Email) is not null)
                throw new InvalidRequestException("Email is already registered.");

            if (await _vendorRepository.RegistrationExistsAsync(request.Email, request.BusinessId))
                throw new InvalidRequestException("A registration request already exists for this email address or business ID.");

            if (await _marketSectionRepository.GetById(request.MarketSectionId) is null)
                throw new RecordNotFoundException("Selected market section was not found.");

            var hasher = new PasswordHasher<User>();
            var tempUser = new User
            {
                FirstName = request.FirstName,
                MiddleName = request.MiddleName,
                LastName = request.LastName,
                DateOfBirth = dateOfBirth,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                HouseNumber = request.HouseNumber,
                Street = request.Street,
                Barangay = request.Barangay,
                City = request.City
            };

            var registration = new VendorRegistrationRequest
            {
                GovernmentIdNumber = "",
                GovernmentIdPhotoUrl = "",
                BusinessDocumentPhotoUrl = "",
                Type = vendorType,
                BusinessId = request.BusinessId,
                BusinessName = request.BusinessName,
                NatureOfBusiness = request.NatureOfBusiness,
                FirstName = request.FirstName,
                MiddleName = request.MiddleName,
                LastName = request.LastName,
                DateOfBirth = dateOfBirth,
                Age = CalculateAge(dateOfBirth),
                HouseNumber = request.HouseNumber,
                Street = request.Street,
                Barangay = request.Barangay,
                City = request.City,
                PhoneNumber = request.PhoneNumber,
                Email = request.Email,
                Password = hasher.HashPassword(tempUser, request.Password),
                StallNumber = request.StallNumber,
                MarketSectionId = request.MarketSectionId,
                Status = RequestStatus.Pending,
                ReviewedBy = adminId
            };

            await _unitOfWork.BeginTransactionAsync();

            try
            {
                await _vendorRepository.AddVendorRegistryAsync(registration);
                await _unitOfWork.SaveChangesAsync();

                return await ApproveVendorRegistration(registration.Id, new RegistrationAdminAction
                {
                    VendorRegistrationId = registration.Id,
                    RequestStatus = RequestStatus.Approved,
                    Version = registration.Version,
                    AdminId = adminId
                });
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }

        public async Task<RegistrationApprovalResponse> ApproveVendorRegistration(int registrationId, RegistrationAdminAction request)
        {
            await _unitOfWork.BeginTransactionAsync();
            var transactionCommitted = false;

            try
            {
                if (request.RequestStatus is not RequestStatus.Approved)
                    throw new InvalidRequestException("RequestStatus must be Approved."); 

                var registration = await _vendorRepository.GetRegistrationById(registrationId);

                if (registration is null)
                    throw new RecordNotFoundException("Registration request record not found.");

                if (registration.Status != RequestStatus.Pending)
                    throw new AlreadyProcessedException("This request has already been processed.");

                _vendorRepository.SetOriginalVersion(registration, request.Version);

                registration.Status = request.RequestStatus;
                registration.ReviewedAt = DateTime.UtcNow;
                registration.ReviewedBy = request.AdminId;

                var newUser = new User
                {
                    FirstName = registration.FirstName,
                    MiddleName = registration.MiddleName,
                    LastName = registration.LastName,
                    DateOfBirth = registration.DateOfBirth,
                    Email = registration.Email,
                    UserName = await _userRepository.GetNextUserNameAsync(),
                    PasswordHash = registration.Password,
                    Status = AccountStatus.Active,
                    PhoneNumber = registration.PhoneNumber,
                    HouseNumber = registration.HouseNumber,
                    Street = registration.Street,
                    Barangay = registration.Barangay,
                    City = registration.City
                };

                var userResult = await _userRepository.CreateUserWithPassAsync(newUser);

                if (!userResult.Succeeded)
                    throw new ResourceCreationFailedException("Failed to create user account.");

                var roleResult = await _userRepository.AddToRoleAsync(newUser, nameof(Role.MarketVendor));

                if (!roleResult.Succeeded)
                    throw new ResourceCreationFailedException("Failed to assign the vendor role to the new account.");

                var marketSection = await _marketSectionRepository.GetById(registration.MarketSectionId);

                if (marketSection is null)
                    throw new RecordNotFoundException("Failed to find selected market section.");

                var vendor = new VendorProfile
                {
                    UserId = newUser.Id,
                    MarketSectionId = marketSection.Id,
                    BusinessId = registration.BusinessId,
                    BusinessName = registration.BusinessName,
                    Type = registration.Type,
                    StallNumber = registration.StallNumber,
                    ComplianceScore = 100,
                    ScoreUpdatedAt = DateTime.UtcNow,
                    Status = VendorStatus.Active,
                    QrCodeValue = GenerateUniqueQrToken()
                };

                var vendorProfile = await _vendorRepository.CreateVendorAsync(vendor);

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();
                transactionCommitted = true;

                var emailBody = $@"
                    <div style=""font-family:Arial,sans-serif;max-width:600px;margin:0 auto;padding:24px;color:#333;"">
                        <h2 style=""color:#0F3D7A;"">Your vendor registration was approved</h2>
                        <p>Hello {HtmlEncoder.Default.Encode(registration.FirstName)},</p>
                        <p>Your registration for <strong>{HtmlEncoder.Default.Encode(registration.BusinessName)}</strong> has been approved.</p>
                        <p>You can now sign in using the email address and password you provided during registration.</p>
                        <p style=""color:#777;font-size:12px;margin-top:28px;"">This is an automated message. Please do not reply.</p>
                    </div>";

                var emailSent = await SendReviewEmailAsync(
                    registration, "Vendor Registration Approved", emailBody);

                return new RegistrationApprovalResponse
                {
                    RegistrationId = registration.Id,
                    UserId = newUser.Id,
                    VendorId = vendorProfile.Id,
                    FirstName = newUser.FirstName,
                    MiddleName = newUser.MiddleName ?? "",
                    LastName = newUser.LastName,
                    BusinessName = vendorProfile.BusinessName,
                    MarketSectionId = vendorProfile.MarketSectionId,
                    MarketSectionName = marketSection.Name,
                    AdminId = request.AdminId,
                    Status = registration.Status,
                    VendorStatus = vendorProfile.Status,
                    ReviewedAt = registration.ReviewedAt.Value,
                    EmailSent = emailSent,
                    CreatedAt = newUser.CreatedAt,
                    Version = registration.Version
                };
            }
            catch (ConcurrencyConflictException)
            {
                if (!transactionCommitted)
                    await _unitOfWork.RollbackAsync();
                throw new ConcurrencyConflictException("This registration was already reviewed by another admin. Please refresh and try again.");
            }
            catch (Exception)
            {
                if (!transactionCommitted)
                    await _unitOfWork.RollbackAsync();
                throw;
            }
        }

        public async Task<RegistrationDeclinedResponse> DeclineVendorRegistration(int registrationId, RegistrationAdminAction request)
        {
            await _unitOfWork.BeginTransactionAsync();
            var transactionCommitted = false;

            try
            {
                if (request.RequestStatus is not RequestStatus.Rejected)
                    throw new InvalidRequestException("RequestStatus must be Rejected.");

                var registration = await _vendorRepository.GetRegistrationById(registrationId);

                if (registration is null)
                    throw new RecordNotFoundException("Registration request record not found.");

                if (registration.Status != RequestStatus.Pending)
                    throw new AlreadyProcessedException("This request has already been processed.");

                if (string.IsNullOrWhiteSpace(request.ReviewReason))
                    throw new InvalidRequestException("A review reason is required when declining a registration.");

                if (string.IsNullOrWhiteSpace(request.ReviewRemarks))
                    throw new InvalidRequestException("Review remarks are required when declining a registration.");

                _vendorRepository.SetOriginalVersion(registration, request.Version);

                registration.Status = request.RequestStatus;
                registration.ReviewedAt = DateTime.UtcNow;
                registration.ReviewedBy = request.AdminId;
                registration.ReviewReason = request.ReviewReason;
                registration.ReviewRemarks = request.ReviewRemarks;
                registration.RemarksOrReason = request.ReviewRemarks;

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();
                transactionCommitted = true;

                var emailBody = $@"
                    <div style=""font-family:Arial,sans-serif;max-width:600px;margin:0 auto;padding:24px;color:#333;"">
                        <h2 style=""color:#0F3D7A;"">Update on your vendor registration</h2>
                        <p>Hello {HtmlEncoder.Default.Encode(registration.FirstName)},</p>
                        <p>We are unable to approve the registration for <strong>{HtmlEncoder.Default.Encode(registration.BusinessName)}</strong> at this time.</p>
                        <p><strong>Reason:</strong> {HtmlEncoder.Default.Encode(registration.ReviewReason)}</p>
                        <p>{HtmlEncoder.Default.Encode(registration.ReviewRemarks)}</p>
                        <p>If you have questions, please contact the market administration office.</p>
                        <p style=""color:#777;font-size:12px;margin-top:28px;"">This is an automated message. Please do not reply.</p>
                    </div>";

                var emailSent = await SendReviewEmailAsync(
                    registration, "Vendor Registration Update", emailBody);

                return new RegistrationDeclinedResponse
                {
                    RegistrationId = registration.Id,
                    Status = registration.Status,
                    EmailSent = emailSent,
                    FirstName = registration.FirstName,
                    MiddleName = registration.MiddleName,
                    LastName = registration.LastName,
                    BusinessName = registration.BusinessName,
                    ReviewReason = registration.ReviewReason!,
                    ReviewRemarks = registration.ReviewRemarks!,
                    AdminId = request.AdminId,
                    ReviewedAt = registration.ReviewedAt.Value,
                    Version = registration.Version
                };
            }
            catch (ConcurrencyConflictException)
            {
                if (!transactionCommitted)
                    await _unitOfWork.RollbackAsync();
                throw new ConcurrencyConflictException("This registration was already reviewed by another admin. Please refresh and try again.");
            }
            catch (Exception)
            {
                if (!transactionCommitted)
                    await _unitOfWork.RollbackAsync();
                throw;
            }
        }

        public async Task<RegistrationDeclinedResponse> RequestMoreInformation(
            int registrationId,
            RegistrationAdminAction request)
        {
            await _unitOfWork.BeginTransactionAsync();
            var transactionCommitted = false;

            try
            {
                if (request.RequestStatus is not RequestStatus.NeedsInformation)
                    throw new InvalidRequestException("RequestStatus must be NeedsInformation.");

                var registration = await _vendorRepository.GetRegistrationById(registrationId);

                if (registration is null)
                    throw new RecordNotFoundException("Registration request record not found.");

                if (registration.Status != RequestStatus.Pending)
                    throw new AlreadyProcessedException("This request has already been processed.");

                if (string.IsNullOrWhiteSpace(request.ReviewReason))
                    throw new InvalidRequestException("A review reason is required when requesting information.");

                if (string.IsNullOrWhiteSpace(request.ReviewRemarks))
                    throw new InvalidRequestException("Review remarks are required when requesting information.");

                _vendorRepository.SetOriginalVersion(registration, request.Version);

                registration.Status = request.RequestStatus;
                registration.ReviewedAt = DateTime.UtcNow;
                registration.ReviewedBy = request.AdminId;
                registration.ReviewReason = request.ReviewReason;
                registration.ReviewRemarks = request.ReviewRemarks;
                registration.RemarksOrReason = request.ReviewRemarks;

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();
                transactionCommitted = true;

                var emailBody = $@"
                    <div style=""font-family:Arial,sans-serif;max-width:600px;margin:0 auto;padding:24px;color:#333;"">
                        <h2 style=""color:#0F3D7A;"">More information is required</h2>
                        <p>Hello {HtmlEncoder.Default.Encode(registration.FirstName)},</p>
                        <p>More information is required before your registration for <strong>{HtmlEncoder.Default.Encode(registration.BusinessName)}</strong> can continue.</p>
                        <p><strong>Reason:</strong> {HtmlEncoder.Default.Encode(registration.ReviewReason)}</p>
                        <p>{HtmlEncoder.Default.Encode(registration.ReviewRemarks)}</p>
                        <p style=""color:#777;font-size:12px;margin-top:28px;"">This is an automated message. Please do not reply.</p>
                    </div>";

                var emailSent = await SendReviewEmailAsync(
                    registration, "More Information Required for Vendor Registration", emailBody);

                return new RegistrationDeclinedResponse
                {
                    RegistrationId = registration.Id,
                    Status = registration.Status,
                    EmailSent = emailSent,
                    FirstName = registration.FirstName,
                    MiddleName = registration.MiddleName,
                    LastName = registration.LastName,
                    BusinessName = registration.BusinessName,
                    ReviewReason = registration.ReviewReason,
                    ReviewRemarks = registration.ReviewRemarks,
                    AdminId = request.AdminId,
                    ReviewedAt = registration.ReviewedAt.Value,
                    Version = registration.Version
                };
            }
            catch (ConcurrencyConflictException)
            {
                if (!transactionCommitted)
                    await _unitOfWork.RollbackAsync();
                throw new ConcurrencyConflictException("This registration was already reviewed by another admin. Please refresh and try again.");
            }
            catch (Exception)
            {
                if (!transactionCommitted)
                    await _unitOfWork.RollbackAsync();
                throw;
            }
        }

        private async Task<bool> SendReviewEmailAsync(
            VendorRegistrationRequest registration, string subject, string body)
        {
            try
            {
                await _emailService.SendEmailAsync(registration.Email, subject, body);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "Review email failed for vendor registration {RegistrationId}. Error type: {ErrorType}",
                    registration.Id, ex.GetType().Name);
                return false;
            }
        }

        private static int CalculateAge(DateOnly dateOfBirth)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var age = today.Year - dateOfBirth.Year;

            if (dateOfBirth > today.AddYears(-age))
                age--;

            return age;
        }

        private static void ValidateDocumentFile(IFormFile file, string fieldName)
        {
            const long maximumFileSize = 5 * 1024 * 1024;
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var allowedTypes = new Dictionary<string, string[]>
            {
                [".jpg"] = ["image/jpeg", "image/jpg"],
                [".jpeg"] = ["image/jpeg", "image/jpg"],
                [".png"] = ["image/png"],
                [".pdf"] = ["application/pdf"]
            };

            if (file.Length <= 0)
                throw new ValidationException($"{fieldName} cannot be empty.");

            if (file.Length > maximumFileSize)
                throw new ValidationException($"{fieldName} must not exceed 5 MB.");

            if (!allowedTypes.TryGetValue(extension, out var contentTypes) ||
                !contentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            {
                throw new ValidationException($"{fieldName} must be a JPG, PNG, or PDF file.");
            }
        }

        private static string CreateRegistrationDocumentKey(IFormFile file)
        {
            var extension = Path.GetExtension(file.FileName);
            var datePath = DateTime.UtcNow.ToString("yyyy/MM");
            return $"vendor-registrations/{datePath}/{Guid.NewGuid()}{extension}";
        }

        private string GenerateUniqueQrToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(16);
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        public async Task<List<GetVendorResponse>> GetVendorByBusinessIdAsync(string businessId)
        {
            var vendors = await _vendorRepository.GetVendorByBusinessId(businessId);

            return vendors.Select(v => new GetVendorResponse
            {
                VendorId = v.VendorId,
                Username = v.Username,
                Type = v.Type,
                BusinessId = v.BusinessId,
                StallNumber = v.StallNumber,
                TradeName = v.TradeName,
                LastName = v.LastName,
                FirstName = v.FirstName,
                MiddleName = v.MiddleName,
                Address = v.Address ?? "",
                MarketSectionId = v.MarketSectionId,
                MarketSectionName = v.MarketSectionName
            }).ToList();
        }

        public async Task<GetVendorResponse> GetVendorByQrCode(string qrCode)
        {
            var vendor = await _vendorRepository.GetVendorByQrCode(qrCode);

            if (vendor == null)
                throw new RecordNotFoundException("Vendor not found.");

            return new GetVendorResponse
            {
                VendorId = vendor.VendorId,
                Username = vendor.Username,
                Type = vendor.Type,
                BusinessId = vendor.BusinessId,
                StallNumber = vendor.StallNumber,
                TradeName = vendor.TradeName,
                LastName = vendor.LastName,
                FirstName = vendor.FirstName,
                MiddleName = vendor.MiddleName,
                Address = "",
                MarketSectionId = vendor.MarketSectionId,
                MarketSectionName = vendor.MarketSectionName
            };
        }

        public async Task<PageResponse<AdminVendorSummaryResponse>> GetAdminVendorSummariesAsync(
            int offset,
            AdminVendorSummaryFilter filters)
        {
            const int pageSize = 10;
            List<AdminVendorSummary> vendors;
            int total;
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                await _complianceScoreService.RefreshAllAsync(DateTime.UtcNow);
                vendors = await _vendorRepository.GetAdminVendorSummariesAsync(offset, pageSize, filters);
                total = await _vendorRepository.GetAdminVendorSummaryCountAsync(filters);
                await _unitOfWork.CommitAsync();
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }

            return new PageResponse<AdminVendorSummaryResponse>
            {
                Items = vendors.Select(v => new AdminVendorSummaryResponse
                {
                    VendorId = v.VendorId,
                    BusinessId = v.BusinessId,
                    VendorName = v.VendorName,
                    Type = v.VendorType,
                    MarketSectionName = v.MarketSectionName,
                    ComplianceScore = v.ComplianceScore,
                    WarningCount = v.WarningCount,
                    TicketCount = v.TicketCount
                }).ToList(),
                HasMore = offset + vendors.Count < total,
                Total = total
            };
        }

        public async Task<VendorRegistrationStatusCountsResponse> GetVendorRegistrationStatusCountsAsync()
        {
            var counts = await _vendorRepository.GetVendorRegistrationStatusCountsAsync();

            return new VendorRegistrationStatusCountsResponse
            {
                PendingReview = counts.PendingReview,
                NeedsInformation = counts.NeedsInformation,
                Approved = counts.Approved,
                Rejected = counts.Rejected
            };
        }

        public async Task<PageResponse<VendorRegistrationSummaryResponse>> GetVendorRegistrationSummariesAsync(
            int offset,
            VendorRegistrationRequestFilters filters)
        {
            const int pageSize = 10;
            var requests = await _vendorRepository.GetVendorRegistrationSummariesAsync(
                offset,
                pageSize,
                filters);
            var total = await _vendorRepository.GetVendorRegistrationSummaryCountAsync(filters);

            return new PageResponse<VendorRegistrationSummaryResponse>
            {
                Items = requests.Select(request => new VendorRegistrationSummaryResponse
                {
                    RegistrationId = request.RegistrationId,
                    BusinessId = request.BusinessId,
                    BusinessName = request.BusinessName,
                    VendorName = request.VendorName,
                    VendorType = request.VendorType,
                    MarketSectionName = request.MarketSectionName,
                    StallNumber = request.StallNumber,
                    RequestedAt = request.RequestedAt,
                    Status = request.Status
                }).ToList(),
                HasMore = offset + requests.Count < total,
                Total = total
            };
        }

        public async Task<VendorRegistrationDetailsResponse> GetVendorRegistrationDetailsAsync(int registrationId)
        {
            var registration = await GetRegistrationAsync(registrationId);
            var documents = await GetRegistrationDocumentsAsync(registration);

            return new VendorRegistrationDetailsResponse
            {
                RegistrationId = registration.Id,
                BusinessId = registration.BusinessId,
                BusinessName = registration.BusinessName,
                NatureOfBusiness = registration.NatureOfBusiness,
                VendorType = registration.Type,
                MarketSectionName = registration.MarketSection?.Name ?? "",
                StallNumber = registration.StallNumber,
                FirstName = registration.FirstName,
                MiddleName = registration.MiddleName,
                LastName = registration.LastName,
                DateOfBirth = registration.DateOfBirth,
                Age = registration.Age,
                GovernmentIdType = registration.GovernmentIdType,
                GovernmentIdNumber = registration.GovernmentIdNumber,
                PhoneNumber = registration.PhoneNumber,
                Email = registration.Email,
                HouseNumber = registration.HouseNumber,
                Street = registration.Street,
                Barangay = registration.Barangay,
                City = registration.City,
                Status = registration.Status,
                RequestedAt = registration.RequestedAt,
                ReviewedAt = registration.ReviewedAt,
                ReviewerName = registration.ReviewedByUser is null
                    ? null
                    : FormatVendorName(
                        registration.ReviewedByUser.FirstName,
                        registration.ReviewedByUser.MiddleName,
                        registration.ReviewedByUser.LastName),
                RemarksOrReason = registration.RemarksOrReason,
                ReviewReason = registration.ReviewReason,
                ReviewRemarks = registration.ReviewRemarks,
                Documents = documents
            };
        }

        public async Task<List<VendorRegistrationDocumentResponse>> GetVendorRegistrationDocumentsAsync(
            int registrationId)
        {
            var registration = await GetRegistrationAsync(registrationId);
            return await GetRegistrationDocumentsAsync(registration);
        }

        private async Task<VendorRegistrationRequest> GetRegistrationAsync(int registrationId)
        {
            var registration = await _vendorRepository.GetRegistrationById(registrationId);

            if (registration is null)
                throw new RecordNotFoundException("Vendor registration request not found.");

            return registration;
        }

        private async Task<List<VendorRegistrationDocumentResponse>> GetRegistrationDocumentsAsync(
            VendorRegistrationRequest registration)
        {
            var documents = new[]
            {
                ("Government ID", registration.GovernmentIdPhotoUrl),
                ("Business document", registration.BusinessDocumentPhotoUrl)
            };

            var result = new List<VendorRegistrationDocumentResponse>();

            foreach (var (documentType, key) in documents)
            {
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                var metadata = await _storageService.GetFileMetadataAsync(B2BucketType.Documents, key);
                if (metadata is null)
                    continue;

                var url = await _storageService.GetPresignedUrlAsync(B2BucketType.Documents, key);

                result.Add(new VendorRegistrationDocumentResponse
                {
                    DocumentType = documentType,
                    FileName = Path.GetFileName(key),
                    ContentType = metadata.ContentType,
                    Size = metadata.Size,
                    Url = url
                });
            }

            return result;
        }

        public async Task<AdminVendorComplianceOverviewResponse> GetAdminVendorComplianceOverviewAsync()
        {
            var today = DateTime.UtcNow.Date;
            var daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
            var weekStart = today.AddDays(-daysSinceMonday);

            var vendorsWithWarnings = await _vendorRepository.GetVendorWarningCountThisWeekAsync(weekStart);
            var vendorsWithTickets = await _vendorRepository.GetVendorTicketCountThisWeekAsync(weekStart);
            var activities = await _vendorRepository.GetRecentVendorActivitiesAsync(5);

            return new AdminVendorComplianceOverviewResponse
            {
                VendorsWithWarningsThisWeek = vendorsWithWarnings,
                VendorsWithTicketsThisWeek = vendorsWithTickets,
                Activities = activities.Select(a => new AdminVendorActivityResponse
                {
                    TicketId = a.TicketId,
                    ControlNumber = a.ControlNumber,
                    BusinessId = a.BusinessId,
                    VendorName = FormatVendorName(a.FirstName, a.MiddleName, a.LastName),
                    Type = a.Type,
                    IssuedAt = a.IssuedAt
                }).ToList()
            };
        }

        private static string FormatVendorName(string firstName, string? middleName, string lastName)
        {
            var middleInitial = string.IsNullOrWhiteSpace(middleName)
                ? ""
                : $" {middleName.Trim()[0]}.";

            return $"{lastName}, {firstName}{middleInitial}";
        }

        public async Task<VendorProfileResponse> GetVendorProfileAsync(int vendorId)
        {
            var vendor = await _vendorRepository.GetVendorProfileDetailsAsync(vendorId);

            if (vendor is null)
                throw new RecordNotFoundException("Vendor not found.");

            return new VendorProfileResponse
            {
                Name = FormatVendorName(vendor.FirstName, vendor.MiddleName, vendor.LastName),
                Username = vendor.Username,
                PhoneNumber = vendor.PhoneNumber,
                Email = vendor.Email,
                AccountCreatedAt = vendor.AccountCreatedAt,
                Role = Role.MarketVendor,
                LastViolationIssuedAt = vendor.LastViolationIssuedAt
            };
        }

        public async Task<VendorComplianceScoreResponse> GetVendorComplianceScoreAsync(int vendorId)
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var response = await _complianceScoreService.RefreshAsync(vendorId, DateTime.UtcNow);
                await _unitOfWork.CommitAsync();
                return response;
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }

        public async Task<PageResponse<AdminPendingTicketSettlementResponse>> GetPendingTicketSettlementsAsync(int offset)
        {
            const int pageSize = 5;
            var settlements = await _vendorRepository.GetPendingTicketSettlementsAsync(offset, pageSize);
            var total = await _vendorRepository.GetPendingTicketSettlementCountAsync();

            return new PageResponse<AdminPendingTicketSettlementResponse>
            {
                Items = settlements.Select(s => new AdminPendingTicketSettlementResponse
                {
                    TicketId = s.TicketId,
                    ControlNumber = s.ControlNumber,
                    BusinessId = s.BusinessId,
                    VendorName = s.VendorName,
                    MarketSectionName = s.MarketSectionName,
                    Status = s.Status,
                    PenaltyType = s.PenaltyType,
                    TotalPaymentAmount = s.TotalPaymentAmount,
                    CommunityServiceHours = s.CommunityServiceHours,
                    IssuedAt = s.IssuedAt,
                    DueDate = s.DueDate
                }).ToList(),
                HasMore = offset + settlements.Count < total,
                Total = total
            };
        }
    }
}
