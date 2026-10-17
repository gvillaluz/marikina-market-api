using System.Collections;
using System.Linq.Expressions;
using System.ComponentModel.DataAnnotations;
using MarikinaMarket.API.Application.DTOs.User.Request;
using MarikinaMarket.API.Application.DTOs.Vendor.Request;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Application.Services;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;
using MarikinaMarket.API.Infrastructure.Persistence;
using MarikinaMarket.API.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MarikinaMarket.Tests;

public class VendorRegistrationTests
{
    [Theory]
    [InlineData("0010", false)]
    [InlineData("0010", true)]
    [InlineData("9999", false)]
    public async Task Approved_and_admin_created_vendors_use_the_staff_username_format(string sequence, bool adminCreated)
    {
        using var fixture = new Fixture { Sequence = sequence };
        if (adminCreated)
            await fixture.Service.CreateAdminVendorRegistrationAsync(fixture.AdminRequest(), 7);
        else
        {
            var record = new ReviewFixture().Record;
            fixture.Registrations.Add(record);
            await fixture.Service.ApproveVendorRegistration(record.Id, new()
            {
                RequestStatus = RequestStatus.Approved, Version = record.Version, AdminId = 7
            });
        }
        var username = $"VEN{sequence}-{DateTime.UtcNow.Year}";
        Assert.Equal(username, Assert.Single(fixture.CreatedUsers).UserName);
        Assert.Contains(username, Assert.Single(fixture.EmailBodies));
    }

    [Fact]
    public async Task Vendor_username_never_exceeds_four_sequence_digits()
    {
        using var fixture = new Fixture { Sequence = "10000" };
        var record = new ReviewFixture().Record;
        fixture.Registrations.Add(record);
        await Assert.ThrowsAsync<MarikinaMarket.API.Application.InvalidRequestException>(() =>
            fixture.Service.ApproveVendorRegistration(record.Id, new()
            {
                RequestStatus = RequestStatus.Approved, Version = record.Version, AdminId = 7
            }));
        Assert.Empty(fixture.CreatedUsers);
        Assert.Empty(fixture.EmailBodies);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_existing_business_id_does_not_report_the_new_email_as_used(bool adminCreated)
    {
        using var fixture = new Fixture();
        var existing = new ReviewFixture().Record;
        fixture.Registrations.Add(existing);
        fixture.Request.Email = "new-vendor@example.invalid";
        fixture.Request.BusinessId = " mm-2026-0042 ";
        var error = await Assert.ThrowsAsync<MarikinaMarket.API.Application.InvalidRequestException>(async () =>
        {
            if (adminCreated) await fixture.Service.CreateAdminVendorRegistrationAsync(fixture.AdminRequest(), 7);
            else await fixture.Service.CreateVendorRegistryAsync(fixture.Request);
        });
        Assert.Equal("Business ID is already used by a vendor or registration request.", error.Message);
        Assert.Empty(fixture.CreatedUsers);
        Assert.Equal(0, fixture.Uploads);
    }

    [Theory]
    [InlineData(RequestStatus.Pending)]
    [InlineData(RequestStatus.NeedsInformation)]
    [InlineData(RequestStatus.Rejected)]
    public async Task Registration_email_conflicts_are_explained_even_without_a_user_account(RequestStatus status)
    {
        using var fixture = new Fixture();
        var existing = new ReviewFixture().Record;
        existing.Status = status;
        existing.Email = " Juan@Example.Invalid ";
        fixture.Registrations.Add(existing);
        fixture.Request.Email = " juan@example.invalid ";
        var error = await Assert.ThrowsAsync<MarikinaMarket.API.Application.InvalidRequestException>(() =>
            fixture.Service.CreateVendorRegistryAsync(fixture.Request));
        Assert.Equal("A vendor registration request already uses this email address. Check your existing request or contact the market administration office.", error.Message);
        Assert.Empty(fixture.CreatedUsers);
        Assert.Equal(0, fixture.Uploads);
    }

    [Fact]
    public async Task Existing_account_email_is_still_rejected()
    {
        using var fixture = new Fixture { AccountExists = true };
        var error = await Assert.ThrowsAsync<MarikinaMarket.API.Application.InvalidRequestException>(() =>
            fixture.Service.CreateVendorRegistryAsync(fixture.Request));
        Assert.Equal("Email is already registered.", error.Message);
        Assert.Empty(fixture.Registrations);
        Assert.Equal(0, fixture.Uploads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unique_registration_normalizes_email_and_business_id(bool adminCreated)
    {
        using var fixture = new Fixture();
        fixture.Request.Email = " Juan@Example.Invalid ";
        fixture.Request.BusinessId = " MM-2026-0010 ";
        if (adminCreated) await fixture.Service.CreateAdminVendorRegistrationAsync(fixture.AdminRequest(), 7);
        else await fixture.Service.CreateVendorRegistryAsync(fixture.Request);
        var record = Assert.Single(fixture.Registrations);
        Assert.Equal("Juan@Example.Invalid", record.Email);
        Assert.Equal("MM-2026-0010", record.BusinessId);
        Assert.Equal("Juan@Example.Invalid", fixture.LastEmailLookup);
    }

    [Fact]
    public async Task Confirmation_email_failure_returns_success_for_the_saved_registration()
    {
        using var fixture = new Fixture { FailEmail = true };
        var response = await fixture.Service.CreateVendorRegistryAsync(fixture.Request);
        Assert.Equal("Pending", response.Status);
        Assert.Contains("confirmation email", response.Message);
        Assert.Single(fixture.Registrations);
        Assert.Equal(1, fixture.Saves);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Empty_email_is_rejected_before_lookups_or_writes(bool adminCreated)
    {
        using var fixture = new Fixture();
        fixture.Request.Email = " ";
        await Assert.ThrowsAsync<ValidationException>(async () =>
        {
            if (adminCreated) await fixture.Service.CreateAdminVendorRegistrationAsync(fixture.AdminRequest(), 7);
            else await fixture.Service.CreateVendorRegistryAsync(fixture.Request);
        });
        Assert.Null(fixture.LastEmailLookup);
        Assert.Empty(fixture.Registrations);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Public_registration_requires_a_stall_number_before_lookups_or_uploads(string? stallNumber)
    {
        using var fixture = new Fixture();
        fixture.Request.StallNumber = stallNumber;

        var error = await Assert.ThrowsAsync<ValidationException>(() => fixture.Service.CreateVendorRegistryAsync(fixture.Request));

        Assert.Equal("Stall number is required for public vendors.", error.Message);
        Assert.Null(fixture.LastEmailLookup);
        Assert.Empty(fixture.Registrations);
        Assert.Equal(0, fixture.Uploads);
    }

    private sealed class Fixture : AppDbContext
    {
        public List<VendorRegistrationRequest> Registrations { get; } = [];
        public List<VendorProfile> Profiles { get; } = [];
        public List<User> CreatedUsers { get; } = [];
        public List<string> EmailBodies { get; } = [];
        public string Sequence = "0010";
        public string? LastEmailLookup;
        public bool FailEmail, AccountExists;
        public int Saves, Uploads;
        public VendorService Service { get; }
        public RegisterVendorRequest Request { get; } = new()
        {
            FirstName = "Juan", LastName = "Reyes", DateOfBirth = new(1990, 1, 1),
            HouseNumber = "12", Street = "Sampaguita", Barangay = "Santa Elena", City = "Marikina",
            PhoneNumber = "09171234567", Email = "juan@example.invalid", BusinessId = "MM-2026-0010",
            BusinessName = "Reyes Fresh Fish", NatureOfBusiness = "Fish retail", MarketSectionId = 1,
            StallNumber = "F-010",
            VendorType = VendorType.Public, GovernmentIdType = GovernmentIdType.NationalId,
            Password = "Registration-password-123", ConfirmPassword = "Registration-password-123",
            GovernmentIdPhoto = Photo("government.png"), BusinessDocumentPhoto = Photo("business.png")
        };

        public Fixture() : base(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused").Options)
        {
            VendorRegistrationRequests = new RegistrationDbSet<VendorRegistrationRequest>(Registrations);
            VendorProfiles = new RegistrationDbSet<VendorProfile>(Profiles);
            var users = ReviewBoundaryProxy.Create<IUserRepository>((method, args) => method.Name switch
            {
                "FindByEmailAsync" => FindEmail((string)args[0]!),
                "GetNextUserNameAsync" => Task.FromResult(Sequence),
                "CreateUserWithPassAsync" => CreateUser((User)args[0]!),
                "AddToRoleAsync" => Task.FromResult(IdentityResult.Success),
                _ => throw new InvalidOperationException(method.Name)
            });
            var section = new MarketSection { Id = 1, Name = "Fish and Seafood", Description = "Fish retail" };
            var unit = ReviewBoundaryProxy.Create<IUnitOfWork>((method, args) => method.Name == "SaveChangesAsync"
                ? SaveChangesAsync() : Task.CompletedTask);
            Service = new VendorService(users, new VendorRepository(this), unit,
                ReviewBoundaryProxy.Create<IMarketSectionRepository>((method, args) => Task.FromResult<MarketSection?>(section)),
                null!, ReviewBoundaryProxy.Create<IStorageService>((method, args) =>
                {
                    Uploads++;
                    return Task.FromResult((string)args[2]!);
                }), ReviewBoundaryProxy.Create<IEmailService>((method, args) =>
                {
                    EmailBodies.Add((string)args[2]!);
                    return FailEmail ? Task.FromException(new InvalidOperationException("Email unavailable")) : Task.CompletedTask;
                }), ReviewBoundaryProxy.Create<INotificationService>((method, args) => Task.FromResult(false)),
                NullLogger<VendorService>.Instance);
        }

        private Task<User?> FindEmail(string email)
        {
            LastEmailLookup = email;
            return Task.FromResult<User?>(AccountExists ? SampleUser() : null);
        }

        private static User SampleUser() => new()
        {
            FirstName = "Juan", LastName = "Reyes", DateOfBirth = new(1990, 1, 1),
            HouseNumber = "12", Street = "Sampaguita", Barangay = "Santa Elena", City = "Marikina"
        };

        private Task<IdentityResult> CreateUser(User user)
        {
            user.Id = 7;
            CreatedUsers.Add(user);
            return Task.FromResult(IdentityResult.Success);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            Saves++;
            foreach (var registration in Registrations)
            {
                if (registration.Id == 0) registration.Id = 42;
                if (registration.Version == 0) registration.Version = 1200;
            }
            return Task.FromResult(1);
        }

        public AdminRegisterVendorRequest AdminRequest() => new()
        {
            FirstName = Request.FirstName, LastName = Request.LastName, DateOfBirth = Request.DateOfBirth,
            HouseNumber = Request.HouseNumber, Street = Request.Street, Barangay = Request.Barangay, City = Request.City,
            PhoneNumber = Request.PhoneNumber, Email = Request.Email, BusinessId = Request.BusinessId,
            BusinessName = Request.BusinessName, NatureOfBusiness = Request.NatureOfBusiness,
            MarketSectionId = Request.MarketSectionId, VendorType = Request.VendorType,
            Password = Request.Password, ConfirmPassword = Request.ConfirmPassword
        };

        private static IFormFile Photo(string name) => new FormFile(new MemoryStream([137, 80, 78, 71]), 0, 4, "file", name)
        {
            Headers = new HeaderDictionary(), ContentType = "image/png"
        };
    }
}

internal sealed class RegistrationDbSet<T>(List<T> records) : DbSet<T>, IQueryable<T> where T : class
{
    private readonly IQueryable<T> _source = records.AsQueryable();
    public override Microsoft.EntityFrameworkCore.Metadata.IEntityType EntityType => throw new NotSupportedException();
    Type IQueryable.ElementType => typeof(T);
    Expression IQueryable.Expression => _source.Expression;
    IQueryProvider IQueryable.Provider => new VendorTicketQueryProvider(_source.Provider, new());
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => _source.GetEnumerator();
    public override ValueTask<EntityEntry<T>> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        records.Add(entity);
        return ValueTask.FromResult<EntityEntry<T>>(null!);
    }
}
