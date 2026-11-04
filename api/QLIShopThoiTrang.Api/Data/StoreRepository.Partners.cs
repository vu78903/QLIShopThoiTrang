using System.Globalization;
using System.Text.RegularExpressions;
using Npgsql;
using QLIShopThoiTrang.Api.Models;

namespace QLIShopThoiTrang.Api.Data;

public sealed partial class StoreRepository
{
    public async Task<LoginResultDto?> GetActiveStaffAsync(string employeeId)
    {
        await using var connection=db.CreateConnection();await connection.OpenAsync();
        await using var command=NewCommand(connection,null,"""
            SELECT nv.MaNV,nv.HoTen,nv.MaQuyen,nv.phienbanbaomat::text || ':' || pq.phienbanbaomat::text FROM NhanVien nv
            JOIN PhanQuyen pq ON pq.MaQuyen=nv.MaQuyen
            WHERE nv.MaNV=@id AND nv.TrangThai=TRUE AND pq.TrangThai=TRUE
            """,("@id",employeeId.Trim()));
        await using var reader=await command.ExecuteReaderAsync();if(!await reader.ReadAsync())return null;
        var permission=reader.GetString(2);
        var role=permission switch {"CHT"=>"CuaHangTruong","TC"=>"TruongCa","KHO"=>"NhanVienKho",_=>"ThuNgan"};
        return new(reader.GetString(0),reader.GetString(1),role,permission,reader.GetString(3));
    }

    public Task<bool> SavePartnerAsync(PartnerUpsertDto input,bool createOnly=false)
        => SavePartnerCoreAsync(input,createOnly,null,null);

    public async Task ImportPartnersAsync(List<PartnerUpsertDto> rows)
    {
        if(rows.Count is <1 or >500)throw new InvalidOperationException("Mỗi lần nhập từ 1 đến 500 đối tác.");
        if(rows.Any(p=>p.Category is not ("Nhân viên" or "Nhà cung cấp" or "Khách hàng")))
            throw new InvalidOperationException("File đối tác chỉ nhận Nhân viên, Nhà cung cấp hoặc Khách hàng.");
        await using var connection=db.CreateConnection();await connection.OpenAsync();
        await using var tx=await connection.BeginTransactionAsync();
        for(var i=0;i<rows.Count;i++)
        {
            try
            {
                if(rows[i].Category=="Nhân viên" && (string.IsNullOrWhiteSpace(rows[i].Username)||string.IsNullOrWhiteSpace(rows[i].Password)))
                    throw new InvalidOperationException("Nhập nhân viên cần đủ hai cột Tài khoản và Mật khẩu; không dùng thông tin đăng nhập mặc định.");
                if(!await SavePartnerCoreAsync(rows[i],true,connection,tx))
                    throw new InvalidOperationException("Mã đối tác đã tồn tại; nhập file không cập nhật dữ liệu cũ.");
            }
            catch(PostgresException ex) when(ex.SqlState is "23505" or "23503" or "22001")
            {throw new InvalidOperationException($"Dòng {i+2}: mã/tài khoản/số điện thoại bị trùng, tham chiếu không hợp lệ hoặc dữ liệu quá dài. Chưa lưu dòng nào.",ex);}
            catch(InvalidOperationException ex)
            {throw new InvalidOperationException($"Dòng {i+2}: {ex.Message} Chưa lưu dòng nào.",ex);}
        }
        await tx.CommitAsync();
    }

    private async Task<bool> SavePartnerCoreAsync(PartnerUpsertDto input,bool createOnly,NpgsqlConnection? connection,NpgsqlTransaction? tx)
    {
        if(string.IsNullOrWhiteSpace(input.Id)||string.IsNullOrWhiteSpace(input.Name)||string.IsNullOrWhiteSpace(input.Phone))
            throw new InvalidOperationException("Mã, tên và số điện thoại là bắt buộc.");
        if(!Regex.IsMatch(input.Phone.Trim(),@"^0\d{9,10}$"))
            throw new InvalidOperationException("Số điện thoại phải bắt đầu bằng 0 và gồm 10 đến 11 chữ số.");
        if(string.IsNullOrWhiteSpace(input.Email)||!Regex.IsMatch(input.Email.Trim(),@"^[^@\s]+@[^@\s]+\.com$",RegexOptions.IgnoreCase))
            throw new InvalidOperationException("Email phải đúng định dạng và kết thúc bằng .com.");
        if(input.Username is not null&&(string.IsNullOrWhiteSpace(input.Username)||input.Username.Length>50||input.Username.Any(char.IsWhiteSpace)))
            throw new InvalidOperationException("Tên đăng nhập phải từ 1 đến 50 ký tự và không có khoảng trắng.");
        if(input.Password is not null&&(input.Password.Length==0||input.Password.Length>200))
            throw new InvalidOperationException("Mật khẩu phải từ 1 đến 200 ký tự.");
        DateOnly? birthDate=null;
        if(!string.IsNullOrWhiteSpace(input.BirthDate))
        {
            if(!DateOnly.TryParseExact(input.BirthDate,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var parsed)||parsed>DateOnly.FromDateTime(DateTime.Today))
                throw new InvalidOperationException("Ngày sinh không hợp lệ hoặc nằm trong tương lai.");
            birthDate=parsed;
        }
        if(input.Gender is not null&&input.Gender is not ("Nam" or "Nữ"))throw new InvalidOperationException("Giới tính phải là Nam hoặc Nữ.");
        Task<int> Write(string sql,params (string Name,object? Value)[] values)=>connection is null
            ? ExecuteStandaloneAsync(sql,values):ExecuteAsync(connection,tx,sql,values);
        var credentials=CommonPartnerParams(input).Concat(new (string Name,object? Value)[]{("@username",DbValue(input.Username)),("@password",input.Password is null?DBNull.Value:input.Password),("@gender",DbValue(input.Gender)),("@birthdate",birthDate.HasValue?birthDate.Value:DBNull.Value)}).ToArray();
        if(input.Category=="Khách hàng")
        {
            var conflict=createOnly?"ON CONFLICT (MaKH) DO NOTHING":"""
                ON CONFLICT (MaKH) DO UPDATE SET HoTen=EXCLUDED.HoTen,SoDienThoai=EXCLUDED.SoDienThoai,Email=EXCLUDED.Email,
                DiaChi=EXCLUDED.DiaChi,TrangThai=TRUE,TenDangNhap=COALESCE(@username,KhachHang.TenDangNhap),MatKhau=COALESCE(@password,KhachHang.MatKhau),
                GioiTinh=COALESCE(@gender,KhachHang.GioiTinh),NgaySinh=COALESCE(@birthdate,KhachHang.NgaySinh)
                """;
            return await Write("""
                INSERT INTO KhachHang(MaKH,SoDienThoai,HoTen,Email,DiaChi,TenDangNhap,MatKhau,GioiTinh,NgaySinh)
                VALUES(@id,@phone,@name,@email,@address,@username,@password,@gender,@birthdate)
                """+conflict,credentials)>0;
        }
        if(input.Category=="Nhà cung cấp")
        {
            var conflict=createOnly?"ON CONFLICT (MaNSX) DO NOTHING":"""
                ON CONFLICT (MaNSX) DO UPDATE SET TenNSX=EXCLUDED.TenNSX,DiaChi=EXCLUDED.DiaChi,MaSoThue=EXCLUDED.MaSoThue,
                SoDienThoai=EXCLUDED.SoDienThoai,Email=EXCLUDED.Email,TrangThai=TRUE
                """;
            return await Write("INSERT INTO NhaSanXuat(MaNSX,TenNSX,DiaChi,MaSoThue,SoDienThoai,Email) VALUES(@id,@name,@address,@role,@phone,@email) "+conflict,CommonPartnerParams(input))>0;
        }
        if(input.Category=="Nhân viên")
        {
            var conflict=createOnly?"ON CONFLICT (MaNV) DO NOTHING":"""
                ON CONFLICT (MaNV) DO UPDATE SET HoTen=EXCLUDED.HoTen,ChucVu=EXCLUDED.ChucVu,SoDienThoai=EXCLUDED.SoDienThoai,
                Email=EXCLUDED.Email,DiaChi=EXCLUDED.DiaChi,MaQuyen=EXCLUDED.MaQuyen,TrangThai=TRUE,
                TenDangNhap=COALESCE(@username,NhanVien.TenDangNhap),MatKhau=COALESCE(@password,NhanVien.MatKhau)
                """;
            return await Write("""
                INSERT INTO NhanVien(MaNV,TenDangNhap,HoTen,ChucVu,SoDienThoai,Email,DiaChi,MaQuyen,MatKhau)
                VALUES(@id,COALESCE(@username,LOWER(REPLACE(@id,'-',''))),@name,@role,@phone,@email,@address,@permission,COALESCE(@password,'123'))
                """+conflict,credentials.Append(("@permission",(object?)MapPermission(input.Role))).ToArray())>0;
        }
        throw new InvalidOperationException("Loại đối tác không hợp lệ.");
    }
}
