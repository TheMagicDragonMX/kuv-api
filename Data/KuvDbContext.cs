using Microsoft.EntityFrameworkCore;
using kuv_api.Models;

namespace kuv_api.Data;

/// <summary>
/// Entity Framework Core database context of the application.
/// </summary>
public sealed class KuvDbContext(DbContextOptions<KuvDbContext> options) : DbContext(options)
{
	/// <summary>
	/// Registered users.
	/// </summary>
	public DbSet<User> Users => Set<User>();
}