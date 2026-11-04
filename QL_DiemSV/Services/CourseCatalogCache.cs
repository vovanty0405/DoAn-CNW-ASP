using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QL_DiemSV.Data;
using QL_DiemSV.Models;

namespace QL_DiemSV.Services;

public sealed class CourseCatalogCache(IMemoryCache cache, ApplicationDbContext db)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static long version;
    // Versioned keys also prevent a concurrent old cache fill from undoing invalidation.
    public void Invalidate() => Interlocked.Increment(ref version);

    public async Task<IReadOnlyList<MonHoc>> GetAsync()
    {
        var connection = db.Database.GetDbConnection();
        var key = ("CourseCatalog", connection.DataSource, connection.Database, Volatile.Read(ref version));
        if (cache.TryGetValue(key, out IReadOnlyList<MonHoc>? hit)) return hit!;
        await Gate.WaitAsync();
        try
        {
            if (cache.TryGetValue(key, out hit)) return hit!;
            var subjects = await db.MonHocs.AsNoTracking().OrderBy(m => m.MaMon)
                .Select(m => new MonHoc
                {
                    MaMon = m.MaMon, TenMon = m.TenMon, SoTinChi = m.SoTinChi,
                    SoTietLyThuyet = m.SoTietLyThuyet, SoTietThucHanh = m.SoTietThucHanh, MaKhoa = m.MaKhoa,
                    Khoa = m.Khoa == null ? null : new Khoa { MaKhoa = m.Khoa.MaKhoa, TenKhoa = m.Khoa.TenKhoa }
                }).ToListAsync();
            var edges = await db.DieuKienMonHocs.AsNoTracking()
                .Select(d => new { d.ID, d.MaMon, d.MaMonTienQuyet }).ToListAsync();
            var lookup = subjects.ToDictionary(m => m.MaMon, StringComparer.OrdinalIgnoreCase);
            foreach (var edge in edges)
            {
                if (edge.MaMon == null || !lookup.TryGetValue(edge.MaMon, out var subject)) continue;
                lookup.TryGetValue(edge.MaMonTienQuyet ?? "", out var prerequisite);
                subject.DieuKienTienQuyets.Add(new DieuKienMonHoc
                {
                    ID = edge.ID, MaMon = edge.MaMon, MaMonTienQuyet = edge.MaMonTienQuyet,
                    MonHocTienQuyet = prerequisite == null ? null : new MonHoc
                    { MaMon = prerequisite.MaMon, TenMon = prerequisite.TenMon, SoTinChi = prerequisite.SoTinChi }
                });
            }
            IReadOnlyList<MonHoc> snapshot = subjects.AsReadOnly();
            cache.Set(key, snapshot, TimeSpan.FromMinutes(15));
            return snapshot;
        }
        finally { Gate.Release(); }
    }
}
