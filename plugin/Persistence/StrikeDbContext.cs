using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.Plugins.Strike.Persistence;
public class StrikeDbContext : DbContext
{
	public StrikeDbContext()
	{
	}

	public StrikeDbContext(DbContextOptions<StrikeDbContext> builderOptions) : base(builderOptions)
	{
	}

	public DbSet<StrikeReceiveRequest> ReceiveRequests => Set<StrikeReceiveRequest>();
	public DbSet<StrikePayment> Payments => Set<StrikePayment>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);
		modelBuilder.HasDefaultSchema(StrikeDbContextFactory.Schema);
		modelBuilder.ApplyConfigurationsFromAssembly(typeof(StrikeDbContext).Assembly);
	}
}
