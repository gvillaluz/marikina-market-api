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

        public async Task AddToRoleAsync(User user, string role)
        {
            await _userManager.AddToRoleAsync(user, role);
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
            return await _context.RefreshTokens.FirstOrDefaultAsync(r => r.Token == refreshToken);
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
    }
}
