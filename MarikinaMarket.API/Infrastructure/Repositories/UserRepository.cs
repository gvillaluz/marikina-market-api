using MarikinaMarket.API.Application.DTOs.Enforcers.Request;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.DTOs.User.Request;
using MarikinaMarket.API.Application.DTOs.User.Response;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;
using MarikinaMarket.API.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MarikinaMarket.API.Application;

namespace MarikinaMarket.API.Infrastructure.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly AppDbContext _context;

        public UserRepository(
            UserManager<User> userManager, 
            SignInManager<User> signInManager,
            AppDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
        }

        public async Task<AccountCountsResponse> GetAccountCountsAsync()
        {
            var users = _context.Users.AsNoTracking();
            return new AccountCountsResponse
            {
                TotalStaffUsers = await users.CountAsync(u => _context.UserRoles.Any(ur =>
                    ur.UserId == u.Id && _context.Roles.Any(r => r.Id == ur.RoleId &&
                        (r.Name == nameof(Role.HeadAdmin) || r.Name == nameof(Role.AdminOfficer) ||
                         r.Name == nameof(Role.MarketEnforcer))))),
                TotalMarketVendorUsers = await users.CountAsync(u => _context.UserRoles.Any(ur =>
                    ur.UserId == u.Id && _context.Roles.Any(r => r.Id == ur.RoleId &&
                        r.Name == nameof(Role.MarketVendor)))),
                TotalAdministrators = await users.CountAsync(u => _context.UserRoles.Any(ur =>
                    ur.UserId == u.Id && _context.Roles.Any(r => r.Id == ur.RoleId &&
                        (r.Name == nameof(Role.HeadAdmin) || r.Name == nameof(Role.AdminOfficer))))),
                TotalActiveAccounts = await users.CountAsync(u => u.Status == AccountStatus.Active)
            };
        }

        public async Task<List<UserSummaryResponse>> GetUserSummariesAsync(int offset, int limit, UserSummaryFilter filters)
        {
            return await ApplyUserSummaryFilters(_context.Users.AsNoTracking(), filters)
                .OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ThenBy(u => u.Id)
                .Skip(offset)
                .Take(limit + 1)
                .Select(u => new UserSummaryResponse
                {
                    Id = u.Id,
                    Role = (from ur in _context.UserRoles
                            join r in _context.Roles on ur.RoleId equals r.Id
                            where ur.UserId == u.Id
                            orderby r.Name == nameof(Role.HeadAdmin) ? 0 : r.Id
                            select r.Name == nameof(Role.HeadAdmin) ? (Role?)Role.HeadAdmin :
                                   r.Name == nameof(Role.AdminOfficer) ? (Role?)Role.AdminOfficer :
                                   r.Name == nameof(Role.MarketEnforcer) ? (Role?)Role.MarketEnforcer :
                                   r.Name == nameof(Role.MarketVendor) ? (Role?)Role.MarketVendor : null)
                           .FirstOrDefault(),
                    Username = u.UserName ?? "",
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    MiddleName = u.MiddleName,
                    Email = u.Email ?? "",
                    PhoneNumber = u.PhoneNumber ?? "",
                    ProfileUrl = u.ProfilePictureUrl,
                    Status = u.Status
                })
                .ToListAsync();
        }

        public Task<int> GetUserSummariesCountAsync(UserSummaryFilter filters)
            => ApplyUserSummaryFilters(_context.Users.AsNoTracking(), filters).CountAsync();

        private IQueryable<User> ApplyUserSummaryFilters(IQueryable<User> query, UserSummaryFilter filters)
        {
            if (!string.IsNullOrEmpty(filters.Role) &&
                !string.Equals(filters.Role, "All", StringComparison.OrdinalIgnoreCase))
            {
                var roleName = Enum.Parse<Role>(filters.Role, ignoreCase: true).ToString();
                query = query.Where(u => _context.UserRoles.Any(ur => ur.UserId == u.Id &&
                    _context.Roles.Any(r => r.Id == ur.RoleId && r.Name == roleName)));
            }

            if (!string.IsNullOrWhiteSpace(filters.Search))
            {
                var term = filters.Search.Trim().ToLower();
                query = query.Where(u => u.FirstName.ToLower().Contains(term) ||
                    u.LastName.ToLower().Contains(term) ||
                    (u.MiddleName != null && u.MiddleName.ToLower().Contains(term)) ||
                    (u.UserName != null && u.UserName.ToLower().Contains(term)) ||
                    (u.Email != null && u.Email.ToLower().Contains(term)) ||
                    (u.PhoneNumber != null && u.PhoneNumber.Contains(term)));
            }

            return query;
        }

        public async Task<IdentityResult> CreateUserAsync(User user, string password)
        {
            return await _userManager.CreateAsync(user, password);
        }

        public async Task<IdentityResult> CreateUserWithPassAsync(User user)
        {
            try
            {
                return await _userManager.CreateAsync(user);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ConcurrencyConflictException("The record was changed by another request. Refresh and try again.");
            }
        }

        public async Task<User?> FindByUserNameAsync(string username)
        {
            return await _userManager.FindByNameAsync(username);
        }

        public async Task<User?> FindByEmailAsync(string email)
        {
            return await _userManager.FindByEmailAsync(email);
        }

        public async Task<SignInResult> CheckPasswordAsync(User user, string password)
        {
            return await _signInManager.CheckPasswordSignInAsync(user, password, true);
        }

        public async Task<IdentityResult> AddToRoleAsync(User user, string role)
        {
            try
            {
                return await _userManager.AddToRoleAsync(user, role);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ConcurrencyConflictException("The record was changed by another request. Refresh and try again.");
            }
        }

        public async Task<Role?> GetRoleAsync(User user)
        {
            var roleName = (await _userManager.GetRolesAsync(user)).FirstOrDefault();

            return Enum.TryParse<Role>(roleName, out var role)
                ? role
                : null;
        }

        public Task<bool> RoleExistsAsync(Role role)
        {
            var normalizedName = role.ToString().ToUpperInvariant();
            return _context.Roles.AsNoTracking().AnyAsync(r => r.NormalizedName == normalizedName);
        }

        public async Task<RefreshToken> AddRefreshTokenAsync(RefreshToken refreshToken)
        {
            await _context.RefreshTokens.AddAsync(refreshToken);
            return refreshToken;
        }

        public async Task<RefreshToken?> GetRefreshTokenAsync(string refreshToken)
        {
            return await _context.RefreshTokens
                .Include(t => t.User)
                .FirstOrDefaultAsync(r => r.Token == refreshToken);
        }

        public async Task RevokeRefreshTokensAsync(int userId)
        {
            var tokens = await _context.RefreshTokens
                .Where(t => t.UserId == userId && !t.IsRevoked)
                .ToListAsync();

            foreach (var token in tokens)
                token.IsRevoked = true;

            await _context.SaveChangesAsync();
        }

        public async Task SaveChangesAsync()
        {
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                Console.WriteLine(ex.Message);
                throw new Exception("A database error occured while saving the changes.");
            }
        }

        public async Task<string> GetNextUserNameAsync()
        {
            var rawNextValue = await _context.Database
            .SqlQueryRaw<int>(@"SELECT nextval('shared.""UserSequence""') AS ""Value""")
            .SingleAsync();

            return rawNextValue.ToString("D4");
        }

        public async Task<User?> GetUserAsync(int userId) => await _userManager.FindByIdAsync(userId.ToString());

        public async Task<Dictionary<int, UserNamesResponse>> GetNamesByIdsAsync(IEnumerable<int> userIds,
            CancellationToken cancellationToken = default)
        {
            var ids = userIds.Distinct().ToList();
            if (ids.Count == 0) return new Dictionary<int, UserNamesResponse>();

            return await _context.Users.AsNoTracking()
                .Where(user => ids.Contains(user.Id))
                .Select(user => new UserNamesResponse
                {
                    Id = user.Id, FirstName = user.FirstName, LastName = user.LastName
                })
                .ToDictionaryAsync(user => user.Id, cancellationToken);
        }

        public async Task<IdentityResult> UpdateUserAsync(User user) => await _userManager.UpdateAsync(user);

        public async Task<IdentityResult> ChangePasswordAsync(User user, string oldPassword, string newPassword)
            => await _userManager.ChangePasswordAsync(user, oldPassword, newPassword);

        public async Task<UserDeviceToken?> GetDeviceTokenByValueAsync(string deviceToken)
            =>  await _context.UserDeviceTokens.FirstOrDefaultAsync(d => d.DeviceToken == deviceToken);

        public async Task<List<string>> GetDeviceTokensByIdAsync(int userId)
        {
            return await _context.UserDeviceTokens
                .Where(d => d.UserId == userId)
                .Select(d => d.DeviceToken)
                .ToListAsync();
        }

        public async Task<List<string>> GetAdminDeviceTokensAsync()
        {
            var adminRoles = new[] { nameof(Role.HeadAdmin), nameof(Role.AdminOfficer) };

            return await _context.UserDeviceTokens
                .AsNoTracking()
                .Where(token => _context.Users.Any(user => user.Id == token.UserId &&
                    user.Status == AccountStatus.Active &&
                    _context.UserRoles.Any(userRole => userRole.UserId == user.Id &&
                        _context.Roles.Any(role => role.Id == userRole.RoleId && adminRoles.Contains(role.Name!)))))
                .Select(token => token.DeviceToken)
                .Distinct()
                .ToListAsync();
        }

        public async Task AddDeviceTokenAsync(UserDeviceToken userDeviceToken)
        {
            await _context.UserDeviceTokens.AddAsync(userDeviceToken);
        }

        public async Task<List<User>> GetEnforcersAsync(int offset, int limit, EnforcerSummaryFilter filters)
        {
            var enforcers = ApplyEnforcerFilters(_userManager.Users, filters);

            var descending = string.Equals(filters.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);
            enforcers = filters.SortBy switch
            {
                "tickets" => descending
                    ? enforcers.OrderByDescending(e => e.IssuedTickets.Count(t => t.Type == ViolationType.Ticket))
                    : enforcers.OrderBy(e => e.IssuedTickets.Count(t => t.Type == ViolationType.Ticket)),

                "warnings" => descending
                    ? enforcers.OrderByDescending(e => e.IssuedTickets.Count(t => t.Type == ViolationType.Warning))
                    : enforcers.OrderBy(e => e.IssuedTickets.Count(t => t.Type == ViolationType.Warning)),

                _ => descending
                    ? enforcers.OrderByDescending(e => e.LastName).ThenByDescending(e => e.FirstName)
                    : enforcers.OrderBy(e => e.LastName).ThenBy(e => e.FirstName),
            };

            return await enforcers
                .Skip(offset)
                .Take(limit + 1)
                .ToListAsync();
        }

        public async Task<int> GetEnforcersCountAsync(EnforcerSummaryFilter filters)
        {
            var query = ApplyEnforcerFilters(_userManager.Users, filters);
            return await query.CountAsync();
        }

        public Task<IdentityResult> ResetPasswordByUsernameAsync(User user, string resetToken, string newPassword)
        {
            return _userManager.ResetPasswordAsync(user, resetToken, newPassword);
        }

        private IQueryable<User> ApplyEnforcerFilters(IQueryable<User> query, EnforcerSummaryFilter filters)
        {
            query = query.Where(u => _context.UserRoles
                .Any(ur => ur.UserId == u.Id && _context.Roles
                    .Any(r => r.Id == ur.RoleId && r.Name == nameof(Role.MarketEnforcer))));

            if (filters.Status.HasValue)
            {
                query = query.Where(u => u.Status == filters.Status.Value);
            }

            if (!string.IsNullOrWhiteSpace(filters.Search))
            {
                var term = filters.Search.Trim();
                query = query.Where(u =>
                    u.FirstName.ToLower().Contains(term.ToLower()) ||
                    u.LastName.ToLower().Contains(term.ToLower()) ||
                    (u.UserName != null && u.UserName.ToLower().Contains(term.ToLower())));
            }

            return query;
        }

        public async Task<string> GenerateResetPassTokenAsync(User user)
        {
            return await _userManager.GeneratePasswordResetTokenAsync(user);
        }
    }
}
