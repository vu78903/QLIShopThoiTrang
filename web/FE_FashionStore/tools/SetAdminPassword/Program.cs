using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System.Text;

if (args.Length != 1 || string.IsNullOrWhiteSpace(args[0]))
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/SetAdminPassword -- WEBADMIN001");
    return 2;
}
var employeeId = args[0].Trim();
var connection = Environment.GetEnvironmentVariable("FASHIONSTORE_DEV_CONNECTION");
var roleCode = Environment.GetEnvironmentVariable("FASHIONSTORE_ADMIN_ROLE_CODE");
if (string.IsNullOrWhiteSpace(connection) || string.IsNullOrWhiteSpace(roleCode))
{
    Console.Error.WriteLine("Set FASHIONSTORE_DEV_CONNECTION and FASHIONSTORE_ADMIN_ROLE_CODE before running.");
    return 2;
}
SqlConnectionStringBuilder builder;
try { builder = new SqlConnectionStringBuilder(connection); }
catch (ArgumentException) { Console.Error.WriteLine("Connection string invalid."); return 2; }
if (!string.Equals(builder.InitialCatalog, "FashionStoreDB_Dev", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine("REFUSED: this utility only accepts FashionStoreDB_Dev.");
    return 2;
}
if (Console.IsInputRedirected || Console.IsOutputRedirected)
{
    Console.Error.WriteLine("REFUSED: run interactively to avoid exposing passwords in scripts and logs.");
    return 2;
}
Console.Write("New admin password (12–128 characters): ");
var password = ReadSecret();
Console.Write("Confirm new password: ");
var confirm = ReadSecret();
if (password.Length is < 12 or > 128 || !string.Equals(password, confirm, StringComparison.Ordinal))
{
    Console.Error.WriteLine("Password too short/long or confirmation does not match.");
    return 2;
}
try
{
    await using var sql = new SqlConnection(connection);
    await sql.OpenAsync();
    await using (var guard = new SqlCommand("SELECT DB_NAME()", sql))
    {
        var actual = (string?)await guard.ExecuteScalarAsync();
        if (!string.Equals(actual, "FashionStoreDB_Dev", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("REFUSED: connection resolved to an unexpected database.");
            return 2;
        }
    }
    var employee = new BootstrapEmployee();
    var hasher = new PasswordHasher<BootstrapEmployee>(Options.Create(new PasswordHasherOptions { IterationCount = 210000 }));
    var hashed = hasher.HashPassword(employee, password);
    await using var command = new SqlCommand(@"
UPDATE dbo.NhanVien
SET MatKhauHash = @hash
WHERE MaNV = @employeeId AND MaQuyen = @roleCode AND TrangThai = 1
  AND EXISTS (SELECT 1 FROM dbo.PhanQuyen q
              WHERE q.MaQuyen = @roleCode AND q.TrangThai = 1);
SELECT @@ROWCOUNT;", sql);
    command.Parameters.AddWithValue("@hash", hashed);
    command.Parameters.AddWithValue("@employeeId", employeeId);
    command.Parameters.AddWithValue("@roleCode", roleCode);
    var affected = Convert.ToInt32(await command.ExecuteScalarAsync());
    if (affected != 1)
    {
        Console.Error.WriteLine("No matching active employee/role in DEV. No password was changed.");
        return 1;
    }
    Console.WriteLine("Password hash updated for DEV employee " + employeeId + ". No password was printed or stored in a script.");
    return 0;
}
catch (SqlException e)
{
    Console.Error.WriteLine("SQL operation failed (error " + e.Number + "). Check the DEV connection and permissions.");
    return 1;
}
finally
{
    password = string.Empty;
    confirm = string.Empty;
}
static string ReadSecret()
{
    var characters = new StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); break; }
        if (key.Key == ConsoleKey.Backspace) { if (characters.Length > 0) characters.Length--; continue; }
        if (key.KeyChar != '\0' && !char.IsControl(key.KeyChar) && characters.Length < 128) characters.Append(key.KeyChar);
    }
    return characters.ToString();
}
sealed class BootstrapEmployee { }
