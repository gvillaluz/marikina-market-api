using MarikinaMarket.API.Application.DTOs.Enforcers.Request;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;
using MarikinaMarket.API.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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

        public async Task<IdentityResult> CreateUserAsync(User user, string password)
        {
            return await _userManager.CreateAsync(user, password);
        }

        public async Task<IdentityResult> CreateUserWithPassAsync(User user)
        {
            return await _userManager.CreateAsync(user);
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
            return await _userManager.AddToRoleAsync(user, role);
        }

        public async Task<Role?> GetRoleAsync(User user)
        {
            var roleName = (await _userManager.GetRolesAsync(user)).FirstOrDefault();

            return Enum.TryParse<Role>(roleName, out var role)
                ? role
                : null;
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

        private IQueryable<User> ApplyEnforcerFilters(IQueryable<User> query, EnforcerSummaryFilter filters)
        {
            query = query.Where(u => _context.UserRoles
                .Any(ur => ur.UserId == u.Id && _context.Roles
                    .Any(r => r.Id == ur.RoleId && r.Name == nameof(Role.Enforcer))));

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
    }
}
