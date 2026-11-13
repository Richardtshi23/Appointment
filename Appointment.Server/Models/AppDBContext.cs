using Microsoft.EntityFrameworkCore;

namespace Appointment.Server.Models
{
    public class AppDBContext : DbContext
    {
        // Fixed so the seed rows don't change every time the model is built
        private static readonly DateTime SeedDate = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public AppDBContext(DbContextOptions<AppDBContext> options) : base(options) { }


        public DbSet<Branch> Branches => Set<Branch>();
        public DbSet<Service> Services => Set<Service>();
        public DbSet<BranchService> BranchServices => Set<BranchService>();
        public DbSet<Appointment> Appointments => Set<Appointment>();

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
                entity.Property(b => b.Capacity).HasDefaultValue(1);
            });

            modelBuilder.Entity<Service>(entity =>
            {
                entity.Property(s => s.Name).IsRequired().HasMaxLength(100);
                entity.Property(s => s.Description).HasMaxLength(500);
            });

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

                entity.HasIndex(a => new { a.BranchId, a.StartTime });

                entity.HasOne(a => a.Branch)
                    .WithMany(b => b.Appointments)
                    .HasForeignKey(a => a.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(a => a.Service)
                    .WithMany(s => s.Appointments)
                    .HasForeignKey(a => a.ServiceId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

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

            var branch1Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var branch2Id = Guid.Parse("11111111-1111-1111-1111-111111111112");
            var branch3Id = Guid.Parse("11111111-1111-1111-1111-111111111113");

            var service1Id = Guid.Parse("22222222-2222-2222-2222-222222222221");
            var service2Id = Guid.Parse("22222222-2222-2222-2222-222222222222");
            var service3Id = Guid.Parse("22222222-2222-2222-2222-222222222223");

            modelBuilder.Entity<Branch>().HasData(
                new Branch
                {
                    Id = branch1Id,
                    Name = "Downtown Branch",
                    Address = "123 Main Street",
                    PhoneNumber = "555-0100",
                    Capacity = 3,
                    CreatedAt = SeedDate
                },
                new Branch
                {
                    Id = branch2Id,
                    Name = "Suburban Plaza",
                    Address = "456 Commerce Blvd",
                    PhoneNumber = "555-0200",
                    Capacity = 1,
                    CreatedAt = SeedDate
                },
                new Branch
                {
                    Id = branch3Id,
                    Name = "Westside Health Center",
                    Address = "789 Park Avenue",
                    PhoneNumber = "555-0300",
                    Capacity = 2,
                    CreatedAt = SeedDate
                }
            );

            modelBuilder.Entity<Service>().HasData(
                new Service
                {
                    Id = service1Id,
                    Name = "General Consultation",
                    Description = "Standard 30-minute appointment slot for routine checkups",
                    DurationMinutes = 30,
                    CreatedAt = SeedDate
                },
                new Service
                {
                    Id = service2Id,
                    Name = "Specialized Health Assessment",
                    Description = "Comprehensive 60-minute in-depth review",
                    DurationMinutes = 60,
                    CreatedAt = SeedDate
                },
                new Service
                {
                    Id = service3Id,
                    Name = "Express Check-in & Lab",
                    Description = "Quick 15-minute diagnostic drop-off or quick consult",
                    DurationMinutes = 15,
                    CreatedAt = SeedDate
                }
            );

            modelBuilder.Entity<BranchService>().HasData(
                new BranchService { BranchId = branch1Id, ServiceId = service1Id },
                new BranchService { BranchId = branch1Id, ServiceId = service2Id },
                new BranchService { BranchId = branch1Id, ServiceId = service3Id },
                new BranchService { BranchId = branch2Id, ServiceId = service1Id },
                new BranchService { BranchId = branch2Id, ServiceId = service3Id },
                new BranchService { BranchId = branch3Id, ServiceId = service1Id },
                new BranchService { BranchId = branch3Id, ServiceId = service2Id }
            );
        }
    }
}
