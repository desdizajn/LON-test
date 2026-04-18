namespace LON.Application.Common.Interfaces;

/// <summary>
/// Abstracts password hashing so Application-layer handlers can create users
/// without depending on the Infrastructure AuthService. Implemented by
/// <c>LON.Infrastructure.Services.AuthService</c>.
/// </summary>
public interface IPasswordHasher
{
    string HashPassword(string password);
}
