using Microsoft.EntityFrameworkCore;

namespace kuv_api.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
}