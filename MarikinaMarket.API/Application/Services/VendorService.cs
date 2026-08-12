using MarikinaMarket.API.Application.DTOs.User.Request;
using MarikinaMarket.API.Application.DTOs.Vendor.Internal;
using MarikinaMarket.API.Application.DTOs.Vendor.Request;
using MarikinaMarket.API.Application.DTOs.Vendor.Response;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;
using Microsoft.AspNetCore.Identity;
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
                LastName = request.LastName
            };

            var hashedPassword = hasher.HashPassword(tempUser, request.Password);

            var vendorRegistry = new VendorRegistrationRequest
            {
                GovernmentIdType = request.GovernmentIdType,
                GovernmentIdNumber = request.GovernmentIdNumber,
                GovernmentIdPhotoUrl = request.GovernmentIdPhotoUrl,
                BusinessDocumentPhotoUrl = request.BusinessDocumentPhotoUrl,
                BusinessName = request.BusinessName,
                FirstName = request.FirstName,
                LastName = request.LastName,
                MiddleName = request.MiddleName,
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
                var registration = await _vendorRepository.GetRegistrationById(registrationId);

                if (registration is null)
                    throw new ArgumentNullException("Registration request record not found.");

                if (registration.Status != RequestStatus.Pending)
                    throw new Exception("This request has already been processed.");

                registration.Status = RequestStatus.Approved;
                registration.ReviewedAt = DateTime.UtcNow;
                registration.ReviewedBy = request.AdminId;

                var newUser = new User
                {
                    FirstName = registration.FirstName,
                    MiddleName = registration.MiddleName,
                    LastName = registration.LastName,
                    Email = registration.Email,
                    PasswordHash = registration.Password,
                    MustChangedPassword = false,
                    Status = AccountStatus.Active
                };

                var userResult = await _userRepository.CreateUserWithPassAsync(newUser);

                if (!userResult.Succeeded)
                    throw new Exception("Failed to create user account.");

                /*
                    Update the response properties when the web app wireframe is created.

                    Fix the RowVersion checking or querying.

                    July 7, 2026
                 */

                var marketSection = await _marketSectionRepository.GetById(registration.MarketSectionId);

                if (marketSection is null)
                    throw new Exception("Failed to find selected market section.");

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
                    RowVersion = registration.RowVersion.ToString() ?? ""
                };
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
