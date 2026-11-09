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

	/// <summary>
	/// Who follows whom.
	/// </summary>
	public DbSet<Follow> Follows => Set<Follow>();

	/// <summary>
	/// User animations and creations.
	/// </summary>
	public DbSet<Creation> Creations => Set<Creation>();

	/// <summary>
	/// Sets up the key and relations of the follows and creations.
	/// </summary>
	/// <param name="modelBuilder">
	/// Builder of the model.
	/// </param>
	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<Follow>(entity =>
		{
			entity.HasKey(follow => new { follow.FollowerId, follow.FollowedId });
			entity.HasIndex(follow => follow.FollowedId);
			entity.HasOne(follow => follow.Follower).WithMany().HasForeignKey(follow => follow.FollowerId).OnDelete(DeleteBehavior.Cascade);
			entity.HasOne(follow => follow.Followed).WithMany().HasForeignKey(follow => follow.FollowedId).OnDelete(DeleteBehavior.Cascade);
			entity.ToTable(table => table.HasCheckConstraint("CK_Follows_NotSelf", "\"FollowerId\" <> \"FollowedId\""));
		});

		modelBuilder.Entity<Creation>(entity =>
		{
			entity.HasIndex(c => c.UserId);
			entity.HasOne(c => c.User).WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
		});
	}
}
