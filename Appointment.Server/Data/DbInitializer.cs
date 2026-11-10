using Appointment.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Appointment.Server.Data
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDBContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");

            // Migrations target SQL Server; other providers (e.g. SQLite in tests) build the schema from the model.
            if (db.Database.IsSqlServer())
                await db.Database.MigrateAsync();
            else
                await db.Database.EnsureCreatedAsync();

            if (await db.Branches.AnyAsync())
                return;

            logger.LogInformation("Seeding reference data");

            var branches = new[]
            {
                new Branch { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "Cape Town CBD", Address = "12 Long Street, Cape Town", PhoneNumber = "+27 21 555 0101" },
                new Branch { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Name = "Johannesburg Sandton", Address = "88 Rivonia Road, Sandton", PhoneNumber = "+27 11 555 0202" },
                new Branch { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), Name = "Durban Umhlanga", Address = "5 Lighthouse Road, Umhlanga", PhoneNumber = "+27 31 555 0303" }
            };

            var catalog = new[]
            {
                new Service { Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), Name = "Account Opening", Description = "Open a new personal or business account.", DurationMinutes = 60 },
                new Service { Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), Name = "Financial Advice", Description = "One-on-one session with an advisor.", DurationMinutes = 60 },
                new Service { Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), Name = "Card Replacement", Description = "Replace a lost or damaged card.", DurationMinutes = 30 },
                new Service { Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"), Name = "General Enquiry", Description = "Quick questions about your account.", DurationMinutes = 30 }
            };

            db.Branches.AddRange(branches);
            db.Services.AddRange(catalog);

            // Cape Town and Sandton offer everything; Umhlanga offers a subset.
            foreach (var b in branches.Take(2))
                foreach (var s in catalog)
                    db.BranchServices.Add(new BranchService { BranchId = b.Id, ServiceId = s.Id });

            foreach (var s in catalog.Skip(2))
                db.BranchServices.Add(new BranchService { BranchId = branches[2].Id, ServiceId = s.Id });

            await db.SaveChangesAsync();
        }
    }
}
