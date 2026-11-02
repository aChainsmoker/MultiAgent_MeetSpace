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

    public User(
        string firstName,
        string lastName,
        string email,
        string passwordHash,
        int nameMaxLength,
        int emailMaxLength,
        UserRole role = UserRole.User)
    {
        Id = Guid.NewGuid();
        SetFirstName(firstName, nameMaxLength);
        SetLastName(lastName, nameMaxLength);
        SetEmail(email, emailMaxLength);
        SetPasswordHash(passwordHash);
        Role = role;
    }

    public void SetFirstName(string firstName, int maxLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        if (firstName.Length > maxLength)
            throw new ArgumentException($"FirstName must not exceed {maxLength} characters.");
        FirstName = firstName;
    }

    public void SetLastName(string lastName, int maxLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);
        if (lastName.Length > maxLength)
            throw new ArgumentException($"LastName must not exceed {maxLength} characters.");
        LastName = lastName;
    }

    public void SetEmail(string email, int maxLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        if (email.Length > maxLength)
            throw new ArgumentException($"Email must not exceed {maxLength} characters.");
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
