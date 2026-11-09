using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Models;
using QL_DiemSV.Services;

namespace QL_DiemSV.Data;

public static class RoleSetup
{
    public static bool IsSystemRole(string name) =>
        new[] { "Admin", "GiangVien", "SinhVien", "KhaoThi", "GiaoVuKhoa", "DaoTao" }
            .Contains(name, StringComparer.OrdinalIgnoreCase);

    public static void Initialize(ApplicationDbContext db)
    {
        using var tx = db.Database.BeginTransaction(System.Data.IsolationLevel.Serializable);
        db.Database.ExecuteSqlRaw("""
            DECLARE @result int;
            EXEC @result = sp_getapplock @Resource=N'AcademicLedger.RoleSetup', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000;
            IF @result < 0 THROW 51000, 'Cannot lock role initialization.', 1;
            """);
        var training = db.Roles.SingleOrDefault(r => r.RoleName == "DaoTao");
        bool newTraining = training == null;
        if (training == null)
        {
            training = new Role { RoleName = "DaoTao", MoTa = "Đào tạo · Quản lý bài viết, thông báo và chương trình đào tạo" };
            db.Roles.Add(training);
        }
        var facultyStaff = db.Roles.SingleOrDefault(r => r.RoleName == "GiaoVuKhoa");
        bool newFacultyStaff = facultyStaff == null;
        if (facultyStaff == null)
        {
            facultyStaff = new Role { RoleName = "GiaoVuKhoa", MoTa = "Giáo vụ khoa · Tra cứu sinh viên thuộc khoa được phân công" };
            db.Roles.Add(facultyStaff);
        }
        db.SaveChanges();
        var adminId = db.Roles.Where(r => r.RoleName == "Admin").Select(r => r.RoleID).Single();
        bool installing = false;
        foreach (var (code, title) in new[] {
            (ContentAccess.Articles, "Bài viết và thông báo"),
            (ContentAccess.Curriculums, "Quản lý chương trình đào tạo")
        })
        {
            bool newModule = !db.ChucNangs.Any(c => c.MaChucNang == code);
            if (newModule)
            {
                installing = true;
                db.ChucNangs.Add(new ChucNang { MaChucNang = code, TenChucNang = title });
                db.SaveChanges();
            }
            if (!newModule && !newTraining) continue;
            foreach (var action in new[] { "VIEW", "CREATE", "EDIT", "DELETE" })
            {
                Grant(training.RoleID, code, action);
                Grant(adminId, code, action);
            }
        }
        if ((installing || newFacultyStaff) && db.ChucNangs.Any(c => c.MaChucNang == "TRA_CUU_DIEM"))
        {
            Grant(facultyStaff.RoleID, "TRA_CUU_DIEM", "VIEW");
            foreach (var id in db.Roles.Where(r => r.RoleName == "GiangVien").Select(r => r.RoleID).ToList())
                Grant(id, "TRA_CUU_DIEM", "VIEW");
        }
        db.SaveChanges();
        tx.Commit();

        void Grant(int role, string module, string action)
        {
            if (!db.HanhDongs.Any(a => a.MaHanhDong == action)) return;
            if (db.PhanQuyens.Find(role, module, action) == null)
                db.PhanQuyens.Add(new PhanQuyen { RoleID = role, MaChucNang = module, MaHanhDong = action });
        }
    }
}
