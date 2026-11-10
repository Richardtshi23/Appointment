using Microsoft.EntityFrameworkCore;

namespace Appointment.Server.Models
{
    public class AppDBContext : DbContext
    {
        public AppDBContext(DbContextOptions<AppDBContext> options) : base(options) { }



        public DbSet<Branch> Branches => Set<Branch>();
        public DbSet<Service> Services => Set<Service>();
        public DbSet<BranchService> BranchServices => Set<BranchService>();
        public DbSet<Appointment> Appointments => Set<Appointment>();

        // SQLite drops DateTime.Kind; always read values back as UTC so JSON carries the "Z" suffix.
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Properties<DateTime>()
                .HaveConversion<UtcDateTimeConverter>();
            configurationBuilder.Properties<DateTime?>()
                .HaveConversion<NullableUtcDateTimeConverter>();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Branch>(entity =>
            {
                entity.Property(b => b.Name).IsRequired().HasMaxLength(100);
                entity.Property(b => b.Address).IsRequired().HasMaxLength(250);
                entity.Property(b => b.PhoneNumber).IsRequired().HasMaxLength(30);
            });

            modelBuilder.Entity<Service>(entity =>
            {
                entity.Property(s => s.Name).IsRequired().HasMaxLength(100);
                entity.Property(s => s.Description).HasMaxLength(500);
            });

            // Appointment Configuration
            modelBuilder.Entity<Appointment>(entity =>
            {
                entity.HasKey(a => a.Id);

                entity.Property(a => a.CustomerName)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(a => a.CustomerEmail)
                    .IsRequired()
                    .HasMaxLength(150);

                entity.Property(a => a.CustomerPhone)
                    .IsRequired()
                    .HasMaxLength(30);

                entity.Property(a => a.ConfirmationCode)
                    .IsRequired()
                    .HasMaxLength(20);

                entity.HasIndex(a => a.ConfirmationCode).IsUnique();

                entity.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);

                // Performance Index for slot lookup optimization
                entity.HasIndex(a => new { a.BranchId, a.StartTime });

                // Relationships & Restrict Delete Behavior
                entity.HasOne(a => a.Branch)
                    .WithMany(b => b.Appointments)
                    .HasForeignKey(a => a.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(a => a.Service)
                    .WithMany(s => s.Appointments)
                    .HasForeignKey(a => a.ServiceId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // BranchService (Many-to-Many Junction Table) Configuration
            modelBuilder.Entity<BranchService>(entity =>
            {
                entity.HasKey(bs => new { bs.BranchId, bs.ServiceId });

                entity.HasOne(bs => bs.Branch)
                    .WithMany(b => b.BranchServices)
                    .HasForeignKey(bs => bs.BranchId);

                entity.HasOne(bs => bs.Service)
                    .WithMany(s => s.BranchServices)
                    .HasForeignKey(bs => bs.ServiceId);
            });
        }
    }
}
