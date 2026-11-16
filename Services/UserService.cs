using kuv_api.Data;
using kuv_api.Models.Exceptions;
using kuv_api.Models.Entities;
using kuv_api.Models.Requests;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace kuv_api.Services;

/// <summary>
/// Handles user-related logic, such as creating new user accounts and telling whether a username or an email
/// is still free. Both ways of signing up (password and Google) use it.
/// </summary>
public sealed class UserService(
    KuvDbContext dbContext,
    IPasswordHasher<User> passwordHasher
)
{
    /// <summary>
    /// Shortest username, in characters.
    /// </summary>
    public const int MinUsernameLength = 3;

    /// <summary>
    /// Longest username, in characters.
    /// </summary>
    public const int MaxUsernameLength = 50;

    /// <summary>
    /// Smallest age accepted.
    /// </summary>
    public const int MinAge = 0;

    /// <summary>
    /// Largest age accepted.
    /// </summary>
    public const int MaxAge = 150;

    /// <summary>
    /// Registers a user with a hashed password.
    /// </summary>
    /// <returns>
    /// The persisted user.
    /// </returns>
    /// <exception cref="BadRequestException">
    /// The age is out of range, or the username is too short or too long.
    /// </exception>
    /// <exception cref="ConflictException">
    /// The email or the username is already registered; the message says which.
    /// </exception>
    public async Task<User> Register(RegisterRequest request)
    {
        var username = request.Username.Trim();
        var email = request.Email.Trim();

        CheckAge(request.Age);

        if (!await IsEmailAvailable(email))
        {
            throw new ConflictException("Ese correo ya está registrado.");
        }

        if (!await IsUsernameAvailable(username))
        {
            throw new ConflictException("Ese nombre de usuario ya está en uso.");
        }

        var user = new User
        {
            Username = username,
            Email = email,
            Age = request.Age,
            Hashword = passwordHasher.HashPassword(null!, request.Password)
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        return user;
    }

    /// <summary>
    /// Whether nobody has a username yet, ignoring case and the spaces around it.
    /// </summary>
    /// <param name="username">
    /// The username to look for.
    /// </param>
    /// <returns>
    /// True when the username is free.
    /// </returns>
    /// <exception cref="BadRequestException">
    /// The username is too short or too long.
    /// </exception>
    public async Task<bool> IsUsernameAvailable(string username)
    {
        var name = username.Trim();

        CheckUsernameLength(name);

        return !await dbContext.Users.AnyAsync(user => user.Username.ToLower() == name.ToLower());
    }

    /// <summary>
    /// Whether no account uses an email yet, ignoring case and the spaces around it. Accounts made with Google
    /// count too.
    /// </summary>
    /// <param name="email">
    /// The email to look for.
    /// </param>
    /// <returns>
    /// True when the email is free.
    /// </returns>
    /// <exception cref="BadRequestException">
    /// The email is blank.
    /// </exception>
    public async Task<bool> IsEmailAvailable(string email)
    {
        var address = email.Trim();

        if (address.Length == 0)
        {
            throw new BadRequestException("Escribe un correo.");
        }

        return !await dbContext.Users.AnyAsync(user => user.Email.ToLower() == address.ToLower());
    }

    /// <summary>
    /// Checks that an age is within the accepted range.
    /// </summary>
    /// <param name="age">
    /// The age to check.
    /// </param>
    /// <exception cref="BadRequestException">
    /// The age is out of range.
    /// </exception>
    public static void CheckAge(int age)
    {
        if (age < MinAge || age > MaxAge)
        {
            throw new BadRequestException($"La edad debe estar entre {MinAge} y {MaxAge}.");
        }
    }

    /// <summary>
    /// Checks that a username is not too short or too long.
    /// </summary>
    /// <param name="username">
    /// The username, already trimmed.
    /// </param>
    /// <exception cref="BadRequestException">
    /// The username is too short or too long.
    /// </exception>
    public static void CheckUsernameLength(string username)
    {
        if (username.Length < MinUsernameLength || username.Length > MaxUsernameLength)
        {
            throw new BadRequestException($"El nombre de usuario debe tener entre {MinUsernameLength} y {MaxUsernameLength} caracteres.");
        }
    }
}
