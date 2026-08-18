using MarikinaMarket.API.Application.DTOs.User.Request;
using MarikinaMarket.API.Application.DTOs.Vendor.Internal;
using MarikinaMarket.API.Application.DTOs.Vendor.Request;
using MarikinaMarket.API.Application.DTOs.Vendor.Response;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using System.Security.Cryptography;

namespace MarikinaMarket.API.Application.Services
{
    public class VendorService : IVendorService
    {
        private readonly IUserRepository _userRepository;
        private readonly IVendorRepository _vendorRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMarketSectionRepository _marketSectionRepository;

        public VendorService(
            IUserRepository userRepository, 
            IVendorRepository vendorRepository, 
            IUnitOfWork unitOfWork,
            IMarketSectionRepository marketSectionRepository)
        {
            _userRepository = userRepository;
            _vendorRepository = vendorRepository;
            _unitOfWork = unitOfWork;
            _marketSectionRepository = marketSectionRepository;
        }

        public async Task<RegisterVendorResponse> CreateVendorRegistryAsync(RegisterVendorRequest request)
        {
            var user = await _userRepository.FindByEmailAsync(request.Email);

            if (user is not null)
                throw new Exception("Email is already registered.");

            var hasher = new PasswordHasher<User>();

            var tempUser = new User
            { 
                FirstName = request.FirstName,
                MiddleName = request.MiddleName,
                LastName = request.LastName,
                DateOfBirth = request.DateOfBirth,
                Email = request.Email,
                PhoneNumber = request.MobileNumber,
                HouseNumber = request.HouseNumber,
                Street = request.Street,
                Barangay = request.Barangay,
                City = request.City
            };

            var hashedPassword = hasher.HashPassword(tempUser, request.Password);

            var vendorRegistry = new VendorRegistrationRequest
            {
                GovernmentIdType = request.GovernmentIdType,
                GovernmentIdNumber = request.GovernmentIdNumber,
                GovernmentIdPhotoUrl = request.GovernmentIdPhotoUrl,
                BusinessDocumentPhotoUrl = request.BusinessDocumentPhotoUrl,
                BusinessName = request.BusinessName,
                NatureOfBusiness = request.NatureOfBusiness,
                FirstName = request.FirstName,
                LastName = request.LastName,
                MiddleName = request.MiddleName,
                DateOfBirth = request.DateOfBirth,
                Age = request.Age,
                HouseNumber = request.HouseNumber,
                Street = request.Street,
                Barangay = request.Barangay,
                City = request.City,
                PhoneNumber = request.MobileNumber,
                Email = request.Email,
                Password = hashedPassword,
                StallNumber = request.StallNumber,
                MarketSectionId = request.MarketSectionId,
                Status = RequestStatus.Pending
            };

            var savedVendorRegistry = await _vendorRepository.AddVendorRegistryAsync(vendorRegistry);
            await _vendorRepository.SaveChangesAsync();

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

        public async Task<RegistrationApprovalResponse> ApproveVendorRegistration(int registrationId, RegistrationAdminAction request)
        {
            await _unitOfWork.BeginTransactionAsync();

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
                    PhoneNumber = registration.PhoneNumber,
                    HouseNumber = registration.HouseNumber,
                    Street = registration.Street,
                    Barangay = registration.Barangay,
                    City = registration.City
                };

                var userResult = await _userRepository.CreateUserWithPassAsync(newUser);

                if (!userResult.Succeeded)
                    throw new ResourceCreationFailedException("Failed to create user account.");

                var marketSection = await _marketSectionRepository.GetById(registration.MarketSectionId);

                if (marketSection is null)
                    throw new RecordNotFoundException("Failed to find selected market section.");

                var vendor = new VendorProfile
                {
                    UserId = newUser.Id,
                    MarketSectionId = marketSection.Id,
                    BusinessName = registration.BusinessName,
                    StallNumber = registration.StallNumber,
                    QrCodeValue = GenerateUniqueQrToken()
                };

                var vendorProfile = await _vendorRepository.CreateVendorAsync(vendor);

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();

                return new RegistrationApprovalResponse
                {
                    UserId = newUser.Id,
                    VendorId = vendorProfile.Id,
                    FirstName = newUser.FirstName,
                    MiddleName = newUser.MiddleName ?? "",
                    LastName = newUser.LastName,
                    BusinessName = vendorProfile.BusinessName,
                    MarketSectionId = vendorProfile.MarketSectionId,
                    MarketSectionName = marketSection.Name,
                    AdminId = request.AdminId,
                    Status = VendorStatus.Active,
                    CreatedAt = newUser.CreatedAt,
                    Version = registration.Version
                };
            }
            catch (DbUpdateConcurrencyException)
            {
                await _unitOfWork.RollbackAsync();
                throw new ConcurrencyConflictException("This registration was already reviewed by another admin. Please refresh and try again.");
            }
            catch (Exception)
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }

        public async Task<RegistrationDeclinedResponse> DeclineVendorRegistration(int registrationId, RegistrationAdminAction request)
        {
            await _unitOfWork.BeginTransactionAsync();

            try
            {
                if (request.RequestStatus is not RequestStatus.Declined)
                    throw new InvalidRequestException("RequestStatus must be Declined.");

                var registration = await _vendorRepository.GetRegistrationById(registrationId);

                if (registration is null)
                    throw new RecordNotFoundException("Registration request record not found.");

                if (registration.Status != RequestStatus.Pending)
                    throw new AlreadyProcessedException("This request has already been processed.");

                if (string.IsNullOrWhiteSpace(request.RemarksOrReason))
                    throw new InvalidRequestException("A reason is required when declining a registration.");

                _vendorRepository.SetOriginalVersion(registration, request.Version);

                registration.Status = request.RequestStatus;
                registration.ReviewedAt = DateTime.UtcNow;
                registration.ReviewedBy = request.AdminId;
                registration.RemarksOrReason = request.RemarksOrReason;

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();

                return new RegistrationDeclinedResponse
                {
                    FirstName = registration.FirstName,
                    MiddleName = registration.MiddleName,
                    LastName = registration.LastName,
                    BusinessName = registration.BusinessName,
                    RemarksOrReason = registration.RemarksOrReason,
                };
            }
            catch (DbUpdateConcurrencyException)
            {
                await _unitOfWork.RollbackAsync();
                throw new ConcurrencyConflictException("This registration was already reviewed by another admin. Please refresh and try again.");
            }
            catch (Exception)
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }

        private string GenerateUniqueQrToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(16);
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        public async Task<List<GetVendorResponse>> GetVendorByStallNumberAsync(string stallNumber)
        {
            var vendors = await _vendorRepository.GetVendorByStallNumber(stallNumber);

            var vendorIdsWithWarning = await _vendorRepository.CheckHasWarningList(vendors.Select(v => v.VendorId).ToList());
            var warningLookup = vendorIdsWithWarning.ToDictionary(w => w.VendorId);

            return vendors
                .Select(v => {
                    var warningCheck = warningLookup[v.VendorId];
                    return new GetVendorResponse
                    {
                        
                        VendorId = v.VendorId,
                        Username = v.Username,
                        StallNumber = v.StallNumber,
                        TradeName = v.TradeName,
                        LastName = v.LastName,
                        FirstName = v.FirstName,
                        MiddleName = v.MiddleName,
                        Address = "",
                        MarketSectionId = v.MarketSectionId,
                        MarketSectionName = v.MarketSectionName,
                        CanIssueWarning = warningCheck.CanIssueWarning,
                        ActiveWarningIssuedAt = warningCheck.ActiveWarningIssuedAt
                    };
                }).ToList();
        }

        public async Task<GetVendorResponse> GetVendorByQrCode(string qrCode)
        {
            var vendor = await _vendorRepository.GetVendorByQrCode(qrCode);

            if (vendor == null)
                throw new RecordNotFoundException("Vendor not found.");

            var warningCheck = await _vendorRepository.CheckHasWarning(vendor.VendorId);

            return new GetVendorResponse
            {
                VendorId = vendor.VendorId,
                Username = vendor.Username,
                StallNumber = vendor.StallNumber,
                TradeName = vendor.TradeName,
                LastName = vendor.LastName,
                FirstName = vendor.FirstName,
                MiddleName = vendor.MiddleName,
                Address = "",
                MarketSectionId = vendor.MarketSectionId,
                MarketSectionName = vendor.MarketSectionName,
                CanIssueWarning = warningCheck.CanIssueWarning,
                ActiveWarningIssuedAt = warningCheck.ActiveWarningIssuedAt
            };
        }
    }
}
