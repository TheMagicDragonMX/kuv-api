using Microsoft.EntityFrameworkCore;
using kuv_api.Models;

namespace kuv_api.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
	public DbSet<User> Users => Set<User>();
}