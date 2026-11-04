using System.Data.Common;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using QL_DiemSV.Controllers;
using QL_DiemSV.Data;
using QL_DiemSV.Services;
using QL_DiemSV.ViewModels;

var config = JsonDocument.Parse(await File.ReadAllTextAsync("QL_DiemSV/appsettings.json"));
var connection = config.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString()!;
var capture = new Commands();
var options = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseSqlServer(connection, sql => sql.CommandTimeout(10)).AddInterceptors(capture).Options;
using var cache = new MemoryCache(new MemoryCacheOptions());
var jsonOptions = new JsonSerializerOptions { ReferenceHandler = ReferenceHandler.IgnoreCycles };
JsonElement Body(IActionResult result) => JsonSerializer.SerializeToElement(((OkObjectResult)result).Value, jsonOptions);
void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
async Task Run(string name, Func<Task> action)
{
    var start = capture.Sql.Count;
    var watch = Stopwatch.StartNew();
    await action();
    Console.WriteLine($"PASS {name}: {watch.ElapsedMilliseconds} ms, SQL commands={capture.Sql.Count - start}");
}
try
{
    await using var db = new ApplicationDbContext(options);
    Console.WriteLine($"Database={db.Database.GetDbConnection().Database}");
    var controller = new LopHocPhanController(db);
    JsonElement first = default;
    await Run("Course first page (cold)", async () =>
    {
        first = Body(await controller.GetAll(null, null, null, pageSize: 10));
        var rows = first.GetProperty("data").EnumerateArray().ToArray();
        Check(rows.Length <= 10, "Course page exceeds limit");
        var term = first.GetProperty("maHK").GetString();
        Check(rows.All(x => x.GetProperty("MaHK").GetString() == term), "Cross-semester row");
        Check(capture.Sql.Any(x => x.Contains("OFFSET") && x.Contains("FETCH")), "Missing SQL pagination");
        Check(!capture.Sql.Any(x => x.Contains("CauHinhDiem") || x.Contains("JOIN [Khoa]")), "Unnecessary columns/joins");
        var ids = rows.Select(x => x.GetProperty("MaLHP").GetString()!).ToArray();
        var expected = await db.KetQuaHocTaps.AsNoTracking().Where(x => ids.Contains(x.MaLHP!))
            .GroupBy(x => x.MaLHP!).Select(g => new { Id = g.Key, Total = g.Count(), Graded = g.Count(x => x.DiemTongKet != null) }).ToDictionaryAsync(x => x.Id);
        foreach (var row in rows)
        {
            expected.TryGetValue(row.GetProperty("MaLHP").GetString()!, out var stats);
            Check(row.GetProperty("SiSoHienTai").GetInt32() == (stats?.Total ?? 0), "Enrollment counter mismatch");
            Check(row.GetProperty("SoSVDaNhapDiem").GetInt32() == (stats?.Graded ?? 0), "Graded counter mismatch");
        }
    });
    var termId = first.GetProperty("maHK").GetString();
    await Run("Course first page (warm)", async () => { await controller.GetAll(termId, null, null, pageSize: 10); });
    await Run("Course page 2 + search", async () =>
    {
        var second = Body(await controller.GetAll(termId, null, null, page: 2, pageSize: 10));
        if (first.GetProperty("totalPages").GetInt32() > 1)
        {
            var ids = first.GetProperty("data").EnumerateArray().Select(x => x.GetProperty("MaLHP").GetString()).ToHashSet();
            Check(!second.GetProperty("data").EnumerateArray().Any(x => ids.Contains(x.GetProperty("MaLHP").GetString())), "Pages overlap");
        }
        var rows = first.GetProperty("data").EnumerateArray().ToArray();
        if (rows.Length > 0)
        {
            var code = rows[0].GetProperty("MaLHP").GetString();
            var found = Body(await controller.GetAll(termId, null, null, search: code));
            Check(found.GetProperty("data").EnumerateArray().Any(x => x.GetProperty("MaLHP").GetString() == code), "Search lost row");
        }
    });
    await Run("Historical term + direct editor link", async () =>
    {
        var oldTerm = await db.LopHocPhans.AsNoTracking().Where(x => x.MaHK != termId).Select(x => x.MaHK).FirstOrDefaultAsync();
        if (oldTerm != null)
        {
            var result = Body(await controller.GetAll(oldTerm, null, null, pageSize: 10));
            Check(result.GetProperty("data").EnumerateArray().All(x => x.GetProperty("MaHK").GetString() == oldTerm), "Historical filter ignored");
        }
        var last = await db.LopHocPhans.AsNoTracking().Where(x => x.MaHK == termId).OrderByDescending(x => x.MaLHP).Select(x => x.MaLHP).FirstOrDefaultAsync();
        if (last != null)
        {
            var result = (ViewResult)await new LichHocController(db).Index(last, null, pageSize: 10);
            var vm = (ScheduleEditorViewModel)result.Model!;
            Check(vm.Section.MaLHP == last && vm.Sections.Any(x => x.MaLHP == last), "Deep link outside first page is lost");
        }
    });
    await Run("Students pagination + total", async () =>
    {
        var start = capture.Sql.Count;
        var result = Body(await new SinhVienController(db).GetAll(null, page: 2, pageSize: 10));
        Check(result.GetProperty("total").GetInt32() == await db.SinhViens.CountAsync(), "Student total mismatch");
        Check(result.GetProperty("data").GetArrayLength() <= 10, "Student page exceeds limit");
        Check(!capture.Sql.Skip(start).Any(x => x.Contains("JOIN [Nganh]") || x.Contains("JOIN [Khoa]")), "Student list over-joins");
    });
    await Run("Course catalog cold/warm/invalidation", async () =>
    {
        var catalog = new CourseCatalogCache(cache, db);
        var subjects = await catalog.GetAsync();
        var commands = capture.Sql.Count;
        Check(ReferenceEquals(subjects, await catalog.GetAsync()), "Cache miss on second request");
        Check(commands == capture.Sql.Count, "Warm catalog issued SQL");
        Console.WriteLine($"Catalog: {subjects.Count} subjects, warm SQL=0");
        catalog.Invalidate();
        var refreshed = await catalog.GetAsync();
        Check(!ReferenceEquals(subjects, refreshed) && refreshed.Count == subjects.Count, "Invalidation failed");
    });
    await Run("Schedule editor page (cold)", async () =>
    {
        var result = (ViewResult)await new LichHocController(db).Index(null, null, termId, pageSize: 10);
        var vm = (ScheduleEditorViewModel)result.Model!;
        Check(vm.Sections.Count <= 10 && vm.Sections.All(x => x.MaHK == termId), "Editor not filtered/paged");
        Check(vm.Sections.All(x => x.KetQuaHocTaps.Count == 0), "Editor loads grade rows");
    });
    await Run("Schedule editor page (warm)", async () => { await new LichHocController(db).Index(null, null, termId, pageSize: 10); });
    await Run("Room matrix", async () =>
    {
        var start = capture.Sql.Count;
        var result = (ViewResult)await new LichHocController(db).Matrix(termId, 1, 2);
        var vm = (RoomMatrixViewModel)result.Model!;
        Check(!capture.Sql.Skip(start).Any(x => x.Contains("JOIN [KetQuaHocTap]")), "Matrix still joins grades");
        Check(vm.Schedules.All(x => x.LopHocPhan!.KetQuaHocTaps.Count == 0), "Matrix loaded grade entities");
    });
    Console.WriteLine("Read-only checks complete. Timings exclude browser/network and are not a latency guarantee.");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"FAIL {ex.GetType().Name}: {ex.Message}");
    Environment.ExitCode = 1;
}

sealed class Commands : DbCommandInterceptor
{
    public List<string> Sql { get; } = new();
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Sql.Add(command.CommandText);
        return ValueTask.FromResult(result);
    }
}
