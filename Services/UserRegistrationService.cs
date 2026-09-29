using kuv_api.Data;
using kuv_api.Models;
using kuv_api.Requests;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace kuv_api.Services;

public sealed class UserRegistrationService(
    KuvDbContext dbContext,
    IPasswordHasher<User> passwordHasher
)
{
    public async Task<User?> RegisterAsync(RegisterRequest request)
    {
        var username = request.Username.Trim();
        var email = request.Email.Trim();
        var alreadyExists = await dbContext.Users.AnyAsync(
            item => item.Username.ToLower() == username.ToLower() || item.Email.ToLower() == email.ToLower());

        if (alreadyExists)
        {
            return null;
        }

        var user = new User
        {
            Username = username,
            Email = email,
            Age = request.Age
        };
        user.Hashword = passwordHasher.HashPassword(user, request.Password);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        return user;
    }
}