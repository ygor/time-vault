namespace TimeVault.Domain.Enums
{
    /// <summary>
    /// Defines the roles available in the application
    /// </summary>
    public enum UserRole
    {
        /// <summary>
        /// Regular user role with standard permissions
        /// </summary>
        User = 0,
        
        /// <summary>
        /// Admin role with elevated permissions
        /// </summary>
        Admin = 1
    }
} 