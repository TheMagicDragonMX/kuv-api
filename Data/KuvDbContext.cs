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
	/// Comments and replies on creations.
	/// </summary>
	public DbSet<Comment> Comments => Set<Comment>();

	/// <summary>
	/// Likes on comments.
	/// </summary>
	public DbSet<CommentLike> CommentLikes => Set<CommentLike>();

	/// <summary>
	/// Moderation reports on animations, users, and comments.
	/// </summary>
	public DbSet<Report> Reports => Set<Report>();

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

		foreach (var entry in ChangeTracker.Entries())
		{
			if (entry.State == EntityState.Added && entry.Entity is ICreatedAt created)
			{
				created.CreatedAt = now;
			}

			if (entry.State is EntityState.Added or EntityState.Modified && entry.Entity is IUpdatedAt updated)
			{
				updated.UpdatedAt = now;
			}
		}
	}

	/// <summary>
	/// Sets up the Google id of the users and the key and relations of the follows, creations, comments, likes, and reports.
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
			entity.HasIndex(creation => creation.UserId);
			entity.HasOne(creation => creation.User).WithMany().HasForeignKey(creation => creation.UserId).OnDelete(DeleteBehavior.Cascade);
		});

		modelBuilder.Entity<Comment>(entity =>
		{
			entity.HasIndex(comment => comment.CreationId);
			entity.HasIndex(comment => comment.ParentCommentId);
			entity.HasOne(comment => comment.Creation).WithMany().HasForeignKey(comment => comment.CreationId).OnDelete(DeleteBehavior.Cascade);
			entity.HasOne(comment => comment.User).WithMany().HasForeignKey(comment => comment.UserId).OnDelete(DeleteBehavior.Cascade);
			entity.HasOne(comment => comment.ParentComment).WithMany().HasForeignKey(comment => comment.ParentCommentId).OnDelete(DeleteBehavior.Cascade);
		});

		modelBuilder.Entity<CommentLike>(entity =>
		{
			entity.HasKey(like => new { like.CommentId, like.UserId });
			entity.HasIndex(like => like.UserId);
			entity.HasOne(like => like.Comment).WithMany().HasForeignKey(like => like.CommentId).OnDelete(DeleteBehavior.Cascade);
			entity.HasOne(like => like.User).WithMany().HasForeignKey(like => like.UserId).OnDelete(DeleteBehavior.Cascade);
		});

		modelBuilder.Entity<Report>(entity =>
		{
			entity.HasIndex(report => new { report.TargetType, report.TargetId });
			entity.HasIndex(report => new { report.ReportingUserId, report.TargetType, report.TargetId });
			entity.HasOne(report => report.ReportingUser).WithMany().HasForeignKey(report => report.ReportingUserId).OnDelete(DeleteBehavior.Cascade);
			entity.HasOne(report => report.ReportedUser).WithMany().HasForeignKey(report => report.ReportedUserId).OnDelete(DeleteBehavior.Cascade);
			entity.ToTable(table => table.HasCheckConstraint("CK_Reports_NotSelf", "\"ReportingUserId\" <> \"ReportedUserId\""));
		});
	}
}
