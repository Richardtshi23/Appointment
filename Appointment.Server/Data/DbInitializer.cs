using Appointment.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Appointment.Server.Data
{
    public static class DbInitializer
    {
        // Reference data (branches and services) is seeded by the migrations through HasData in AppDBContext
        public static async Task InitializeAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDBContext>();

            if (db.Database.IsSqlServer())
                await db.Database.MigrateAsync();
            else
                await db.Database.EnsureCreatedAsync();
        }
    }
}
