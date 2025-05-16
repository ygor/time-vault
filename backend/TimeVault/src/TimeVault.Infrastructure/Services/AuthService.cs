using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using TimeVault.Core.Services.Interfaces;
using TimeVault.Domain.Entities;
using TimeVault.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace TimeVault.Infrastructure.Services
{
    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            ApplicationDbContext context, 
            IConfiguration configuration,
            ILogger<AuthService> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<(bool Success, string Token, User? User, string Error)> LoginAsync(string email, string password)
        {
            _logger.LogInformation("Login attempt for email: {Email}", email);
            
            // Get all users (since there should be few, this is a simple approach that avoids 
            // PostgreSQL case sensitivity issues)
            var allUsers = await _context.Users.ToListAsync();
            
            // Perform case-insensitive email check in memory
            var user = allUsers.FirstOrDefault(u => 
                string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));

            if (user == null)
            {
                _logger.LogWarning("Login failed: User not found for email: {Email}", email);
                return (false, string.Empty, null, "User not found");
            }

            if (!VerifyPasswordHash(password, user.PasswordHash))
            {
                _logger.LogWarning("Login failed: Invalid password for email: {Email}", email);
                return (false, string.Empty, null, "Invalid password");
            }

            // Update last login
            user.LastLogin = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var token = GenerateJwtToken(user);
            _logger.LogInformation("Login successful for user: {Email}", email);

            return (true, token, user, string.Empty);
        }

        public async Task<(bool Success, string Token, User? User, string Error)> RegisterAsync(string email, string password)
        {
            _logger.LogInformation("Registration attempt for email: {Email}", email);
            
            // Get all users to check for email match in memory
            var allUsers = await _context.Users.ToListAsync();
            bool emailExists = allUsers.Any(u => 
                string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));
                
            if (emailExists)
            {
                _logger.LogWarning("Registration failed: Email already registered: {Email}", email);
                return (false, string.Empty, null, "Email already registered");
            }

            var now = DateTime.UtcNow;
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = HashPassword(password),
                FirstName = "",
                LastName = "",
                IsAdmin = false,
                CreatedAt = now,
                UpdatedAt = now,
                LastLogin = now
            };

            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();

            var token = GenerateJwtToken(user);
            _logger.LogInformation("Registration successful for new user: {Email}, ID: {UserId}", email, user.Id);

            return (true, token, user, string.Empty);
        }

        public Task<(bool Success, string Token, string Error)> RefreshTokenAsync(string token)
        {
            _logger.LogDebug("Token refresh requested");
            
            // This is a simplified implementation - in a real-world app, 
            // you would verify the token and check a refresh token stored in a database
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT key is not configured"));
                
                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _configuration["Jwt:Issuer"] ?? "TimeVault",
                    ValidateAudience = true,
                    ValidAudience = _configuration["Jwt:Audience"] ?? "TimeVaultUsers",
                    ClockSkew = TimeSpan.Zero
                }, out SecurityToken validatedToken);

                var jwtToken = (JwtSecurityToken)validatedToken;
                var userId = Guid.Parse(jwtToken.Claims.First(x => x.Type == "id").Value);

                var user = _context.Users.Find(userId);
                if (user == null)
                {
                    _logger.LogWarning("Token refresh failed: User not found for ID: {UserId}", userId);
                    return Task.FromResult((false, string.Empty, "User not found"));
                }

                var newToken = GenerateJwtToken(user);
                _logger.LogInformation("Token refresh successful for user: {Email}", user.Email);

                return Task.FromResult((true, newToken, string.Empty));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Token refresh failed: Invalid token");
                return Task.FromResult((false, string.Empty, "Invalid token"));
            }
        }

        public async Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
        {
            _logger.LogInformation("Password change requested for user ID: {UserId}", userId);
            
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("Password change failed: User not found for ID: {UserId}", userId);
                return false;
            }

            if (!VerifyPasswordHash(currentPassword, user.PasswordHash))
            {
                _logger.LogWarning("Password change failed: Invalid current password for user: {Email}", user.Email);
                return false;
            }

            user.PasswordHash = HashPassword(newPassword);
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Password changed successfully for user: {Email}", user.Email);
            return true;
        }

        private string GenerateJwtToken(User user)
        {
            _logger.LogDebug("Generating JWT token for user: {Email}", user.Email);
            
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtKey = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT key is not configured");
            var key = Encoding.ASCII.GetBytes(jwtKey);
            
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim("id", user.Id.ToString()),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim("isAdmin", user.IsAdmin.ToString().ToLower())
                }),
                Expires = DateTime.UtcNow.AddDays(7),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key), 
                    SecurityAlgorithms.HmacSha256Signature),
                Issuer = _configuration["Jwt:Issuer"] ?? "TimeVault",
                Audience = _configuration["Jwt:Audience"] ?? "TimeVaultUsers"
            };
            
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private string HashPassword(string password)
        {
            _logger.LogDebug("Hashing password");
            
            using (var hmac = new HMACSHA512())
            {
                var salt = hmac.Key;
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
                
                return Convert.ToBase64String(salt) + ":" + Convert.ToBase64String(hash);
            }
        }

        private bool VerifyPasswordHash(string password, string storedHash)
        {
            _logger.LogDebug("Verifying password hash");
            
            var parts = storedHash.Split(':');
            if (parts.Length != 2)
            {
                _logger.LogWarning("Invalid stored hash format");
                return false;
            }

            var salt = Convert.FromBase64String(parts[0]);
            var hash = Convert.FromBase64String(parts[1]);

            using (var hmac = new HMACSHA512(salt))
            {
                var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
                
                for (int i = 0; i < computedHash.Length; i++)
                {
                    if (computedHash[i] != hash[i])
                        return false;
                }
            }

            return true;
        }

        public async Task<bool> CreateAdminUserIfNotExists(string email, string password)
        {
            _logger.LogInformation("Checking if admin user exists: {Email}", email);
            
            // Get all users to check for email match in memory
            var allUsers = await _context.Users.ToListAsync();
            bool emailExists = allUsers.Any(u => 
                string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));
                
            if (emailExists)
            {
                _logger.LogInformation("Admin user already exists: {Email}", email);
                return false;  // User already exists
            }

            var now = DateTime.UtcNow;
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = HashPassword(password),
                FirstName = "Admin",
                LastName = "User",
                IsAdmin = true,
                CreatedAt = now,
                UpdatedAt = now,
                LastLogin = now
            };

            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Admin user created successfully: {Email}", email);
            return true;
        }
    }
} 