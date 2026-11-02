using MeetSpace.Domain.Enums;

namespace MeetSpace.Domain.Entities;

public class User
{
    public Guid Id { get; private set; }

    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string? ProfilePhotoKey { get; private set; }
    public UserRole Role { get; private set; } = UserRole.User;

    public ICollection<Booking> Bookings { get; private set; } = new List<Booking>();
    public ICollection<RefreshToken> RefreshTokens { get; private set; } = new List<RefreshToken>();

    private User() { }

    public User(string firstName, string lastName, string email, string passwordHash, UserRole role = UserRole.User)
    {
        Id = Guid.NewGuid();
        SetFirstName(firstName);
        SetLastName(lastName);
        SetEmail(email);
        SetPasswordHash(passwordHash);
        Role = role;
    }

    public void SetFirstName(string firstName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        if (firstName.Length > Constants.DomainConstants.UserNameMaxLength)
            throw new ArgumentException($"FirstName must not exceed {Constants.DomainConstants.UserNameMaxLength} characters.");
        FirstName = firstName;
    }

    public void SetLastName(string lastName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);
        if (lastName.Length > Constants.DomainConstants.UserNameMaxLength)
            throw new ArgumentException($"LastName must not exceed {Constants.DomainConstants.UserNameMaxLength} characters.");
        LastName = lastName;
    }

    public void SetEmail(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        if (email.Length > Constants.DomainConstants.EmailMaxLength)
            throw new ArgumentException($"Email must not exceed {Constants.DomainConstants.EmailMaxLength} characters.");
        Email = email.ToLowerInvariant();
    }

    public void SetPasswordHash(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        PasswordHash = passwordHash;
    }

    public void SetProfilePhotoKey(string? profilePhotoKey)
    {
        ProfilePhotoKey = profilePhotoKey;
    }
}
