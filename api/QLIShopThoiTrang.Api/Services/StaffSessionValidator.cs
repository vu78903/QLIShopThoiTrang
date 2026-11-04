using QLIShopThoiTrang.Api.Data;
using QLIShopThoiTrang.Api.Models;

namespace QLIShopThoiTrang.Api.Services;

public interface IStaffSessionValidator
{
    Task<bool> IsCurrentAsync(LoginResultDto user);
}

public sealed class DatabaseStaffSessionValidator(StoreRepository store):IStaffSessionValidator
{
    public async Task<bool> IsCurrentAsync(LoginResultDto user)
    {
        var current=await store.GetActiveStaffAsync(user.EmployeeId);
        return current is not null && current.Role==user.Role && current.PermissionCode==user.PermissionCode && current.SessionVersion==user.SessionVersion;
    }
}
