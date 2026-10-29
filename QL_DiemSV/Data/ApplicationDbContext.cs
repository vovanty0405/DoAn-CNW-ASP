using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Models;

namespace QL_DiemSV.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Role> Roles { get; set; }
    public DbSet<UserAccount> UserAccounts { get; set; }
    public DbSet<Session> Sessions { get; set; }
    public DbSet<ChucNang> ChucNangs { get; set; }
    public DbSet<HanhDong> HanhDongs { get; set; }
    public DbSet<PhanQuyen> PhanQuyens { get; set; }
    public DbSet<PhanQuyenPhamVi> PhanQuyenPhamVis { get; set; }
    public DbSet<Khoa> Khoas { get; set; }
    public DbSet<Nganh> Nganhs { get; set; }
    public DbSet<QuyDinhTotNghiep> QuyDinhTotNghieps { get; set; }
    public DbSet<KhungChuongTrinh> KhungChuongTrinhs { get; set; }
    public DbSet<MonHoc> MonHocs { get; set; }
    public DbSet<DieuKienMonHoc> DieuKienMonHocs { get; set; }
    public DbSet<GiangVien> GiangViens { get; set; }
    public DbSet<HocKy> HocKys { get; set; }
    public DbSet<LopHanhChinh> LopHanhChinhs { get; set; }
    public DbSet<SinhVien> SinhViens { get; set; }
    public DbSet<LopHocPhan> LopHocPhans { get; set; }
    public DbSet<KetQuaHocTap> KetQuaHocTaps { get; set; }
    public DbSet<DonKhieuNai> DonKhieuNais { get; set; }
    public DbSet<ThongBao> ThongBaos { get; set; }
    public DbSet<NhatKyHoatDong> NhatKyHoatDongs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // UserAccount unique username
        modelBuilder.Entity<UserAccount>()
            .HasIndex(u => u.Username)
            .IsUnique();

        // PhanQuyen composite PK
        modelBuilder.Entity<PhanQuyen>()
            .HasKey(pq => new { pq.RoleID, pq.MaChucNang, pq.MaHanhDong });

        modelBuilder.Entity<PhanQuyen>()
            .HasOne(pq => pq.Role)
            .WithMany(r => r.PhanQuyens)
            .HasForeignKey(pq => pq.RoleID)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PhanQuyen>()
            .HasOne(pq => pq.ChucNang)
            .WithMany(c => c.PhanQuyens)
            .HasForeignKey(pq => pq.MaChucNang)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PhanQuyen>()
            .HasOne(pq => pq.HanhDong)
            .WithMany(h => h.PhanQuyens)
            .HasForeignKey(pq => pq.MaHanhDong)
            .OnDelete(DeleteBehavior.Cascade);

        // PhanQuyenPhamVi composite PK
        modelBuilder.Entity<PhanQuyenPhamVi>()
            .HasKey(p => new { p.RoleID, p.MaPhamVi });

        modelBuilder.Entity<PhanQuyenPhamVi>()
            .HasOne(p => p.Role)
            .WithMany(r => r.PhanQuyenPhamVis)
            .HasForeignKey(p => p.RoleID)
            .OnDelete(DeleteBehavior.Cascade);

        // 1-1 Nganh <-> QuyDinhTotNghiep
        modelBuilder.Entity<Nganh>()
            .HasOne(n => n.QuyDinhTotNghiep)
            .WithOne(q => q.Nganh)
            .HasForeignKey<QuyDinhTotNghiep>(q => q.MaNganh)
            .OnDelete(DeleteBehavior.Cascade);

        // KhungChuongTrinh unique index
        modelBuilder.Entity<KhungChuongTrinh>()
            .HasIndex(k => new { k.MaNganh, k.MaMon })
            .IsUnique();

        modelBuilder.Entity<KhungChuongTrinh>()
            .HasOne(k => k.Nganh)
            .WithMany(n => n.KhungChuongTrinhs)
            .HasForeignKey(k => k.MaNganh)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<KhungChuongTrinh>()
            .HasOne(k => k.MonHoc)
            .WithMany(m => m.KhungChuongTrinhs)
            .HasForeignKey(k => k.MaMon)
            .OnDelete(DeleteBehavior.Cascade);

        // DieuKienMonHoc self-referencing
        modelBuilder.Entity<DieuKienMonHoc>()
            .HasOne(d => d.MonHocChinh)
            .WithMany(m => m.DieuKienTienQuyets)
            .HasForeignKey(d => d.MaMon)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DieuKienMonHoc>()
            .HasOne(d => d.MonHocTienQuyet)
            .WithMany(m => m.LaTienQuyetCua)
            .HasForeignKey(d => d.MaMonTienQuyet)
            .OnDelete(DeleteBehavior.Restrict);

        // UserAccount 1-1 GiangVien & SinhVien
        modelBuilder.Entity<UserAccount>()
            .HasOne(u => u.GiangVien)
            .WithOne(g => g.UserAccount)
            .HasForeignKey<GiangVien>(g => g.UserID)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<UserAccount>()
            .HasOne(u => u.SinhVien)
            .WithOne(s => s.UserAccount)
            .HasForeignKey<SinhVien>(s => s.UserID)
            .OnDelete(DeleteBehavior.SetNull);

        // Prevent multiple cascade paths in SQL Server
        modelBuilder.Entity<LopHanhChinh>()
            .HasOne(l => l.GiangVien)
            .WithMany(g => g.LopHanhChinhs)
            .HasForeignKey(l => l.MaGVCN)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<LopHocPhan>()
            .HasOne(l => l.GiangVien)
            .WithMany(g => g.LopHocPhans)
            .HasForeignKey(l => l.MaGV)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<KetQuaHocTap>()
            .HasOne(k => k.SinhVien)
            .WithMany(s => s.KetQuaHocTaps)
            .HasForeignKey(k => k.MaSV)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<KetQuaHocTap>()
            .HasOne(k => k.LopHocPhan)
            .WithMany(l => l.KetQuaHocTaps)
            .HasForeignKey(k => k.MaLHP)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DonKhieuNai>()
            .HasOne(d => d.SinhVien)
            .WithMany(s => s.DonKhieuNais)
            .HasForeignKey(d => d.MaSV)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DonKhieuNai>()
            .HasOne(d => d.LopHocPhan)
            .WithMany(l => l.DonKhieuNais)
            .HasForeignKey(d => d.MaLHP)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
