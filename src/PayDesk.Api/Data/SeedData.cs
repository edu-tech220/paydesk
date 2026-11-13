using PayDesk.Api.Domain.Entities;
using PayDesk.Api.Domain.Enums;
using PayDesk.Api.Security;

namespace PayDesk.Api.Data;

/// <summary>
/// Seeds a demo admin/manager/employee plus a progressive tax table so the API and
/// frontend are usable immediately after the first run, without manual setup.
/// </summary>
public static class SeedData
{
    public static async Task SeedAsync(PayDeskDbContext db)
    {
        if (!db.TaxBrackets.Any())
        {
            db.TaxBrackets.AddRange(
                new TaxBracket { MinAmount = 0m, MaxAmount = 1500m, Rate = 0.05m },
                new TaxBracket { MinAmount = 1500m, MaxAmount = 4500m, Rate = 0.12m },
                new TaxBracket { MinAmount = 4500m, MaxAmount = 10000m, Rate = 0.20m },
                new TaxBracket { MinAmount = 10000m, MaxAmount = null, Rate = 0.30m });
        }

        if (!db.Employees.Any())
        {
            var (adminHash, adminSalt) = PasswordHasher.Hash("Admin123!");
            var admin = new Employee
            {
                EmployeeCode = "ADMIN-001",
                FullName = "Alex Reyes",
                Email = "admin@paydesk.local",
                PasswordHash = adminHash,
                PasswordSalt = adminSalt,
                Role = EmployeeRole.Admin,
                Department = "Payroll Administration",
                HourlyRate = 0m,
                HireDate = new DateOnly(2022, 1, 10),
                IsActive = true
            };

            var (managerHash, managerSalt) = PasswordHasher.Hash("Manager123!");
            var manager = new Employee
            {
                EmployeeCode = "MGR-001",
                FullName = "Jane Morales",
                Email = "manager@paydesk.local",
                PasswordHash = managerHash,
                PasswordSalt = managerSalt,
                Role = EmployeeRole.Manager,
                Department = "Operations",
                HourlyRate = 28.50m,
                HireDate = new DateOnly(2022, 3, 1),
                IsActive = true
            };

            db.Employees.AddRange(admin, manager);
            await db.SaveChangesAsync();

            var (employeeHash, employeeSalt) = PasswordHasher.Hash("Employee123!");
            var employee = new Employee
            {
                EmployeeCode = "EMP-001",
                FullName = "John Castillo",
                Email = "employee@paydesk.local",
                PasswordHash = employeeHash,
                PasswordSalt = employeeSalt,
                Role = EmployeeRole.Employee,
                Department = "Operations",
                HourlyRate = 18.75m,
                ManagerId = manager.Id,
                HireDate = new DateOnly(2023, 6, 15),
                IsActive = true
            };

            db.Employees.Add(employee);
        }

        await db.SaveChangesAsync();
    }
}
