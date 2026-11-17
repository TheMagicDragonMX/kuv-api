using Microsoft.EntityFrameworkCore;
using kuv_api.Models.Entities;
using kuv_api.Models.Entities.Base;

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
	/// Saves the changes, stamping the creation and update times first.
	/// </summary>
	/// <returns>
	/// The number of rows written.
	/// </returns>
	public override int SaveChanges(bool acceptAllChangesOnSuccess)
	{
		StampTimes();

		return base.SaveChanges(acceptAllChangesOnSuccess);
	}

	/// <summary>
	/// Saves the changes, stamping the creation and update times first.
	/// </summary>
	/// <returns>
	/// The number of rows written.
	/// </returns>
	public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
	{
		StampTimes();

		return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
	}

	/// <summary>
	/// Sets CreatedAt on the entities about to be inserted and UpdatedAt on the ones about to be inserted or changed.
	/// </summary>
	private void StampTimes()
	{
		var now = DateTime.UtcNow;

		foreach (var entry in ChangeTracker.Entries<TimestampedEntity>())
		{
			if (entry.State == EntityState.Added)
			{
				entry.Entity.CreatedAt = now;
			}

			if (entry.Entity is UpdatableEntity updatable && entry.State is EntityState.Added or EntityState.Modified)
			{
				updatable.UpdatedAt = now;
			}
		}
	}

	/// <summary>
	/// Sets up the Google id of the users and the key and relations of the follows and creations.
	/// </summary>
	/// <param name="modelBuilder">
	/// Builder of the model.
	/// </param>
	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		// Two users can never share a Google account (accounts without one all hold null, which a unique index allows)
		modelBuilder.Entity<User>(entity => entity.HasIndex(user => user.GoogleId).IsUnique());

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
