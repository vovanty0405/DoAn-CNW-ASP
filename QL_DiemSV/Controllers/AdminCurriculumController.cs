using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Data;
using QL_DiemSV.Models;
using QL_DiemSV.Services;
using QL_DiemSV.ViewModels;
namespace QL_DiemSV.Controllers;
[Authorize(Roles="Admin"),Route("admin/curriculums")]
public class AdminCurriculumController(ApplicationDbContext db):Controller {
 [HttpGet("")]
 public async Task<IActionResult> Index(string? system, int? year, string? major, string? program, bool? active, int page = 1, int pageSize = 20)
 {
     var query = db.Curriculums.AsNoTracking();
     // Dropdown metadata never includes the large HTML contents of the eleven sections.
     var options = await query.OrderByDescending(x => x.KhoaHoc).ThenBy(x => x.Id)
         .Select(x => new Curriculum { Id = x.Id, HeDaoTao = x.HeDaoTao, KhoaHoc = x.KhoaHoc,
             NganhHocId = x.NganhHocId, TenChuongTrinh = x.TenChuongTrinh,
             NganhHoc = x.NganhHoc == null ? null : new Nganh { MaNganh = x.NganhHoc.MaNganh, TenNganh = x.NganhHoc.TenNganh } })
         .ToListAsync();
     if (!string.IsNullOrWhiteSpace(system)) query = query.Where(x => x.HeDaoTao == system);
     if (year.HasValue) query = query.Where(x => x.KhoaHoc == year);
     if (!string.IsNullOrWhiteSpace(major)) query = query.Where(x => x.NganhHocId == major);
     if (!string.IsNullOrWhiteSpace(program)) query = query.Where(x => x.TenChuongTrinh == program);
     if (active.HasValue) query = query.Where(x => x.TrangThai == active);
     var total = await query.CountAsync();
     pageSize = Math.Clamp(pageSize, 1, 50);
     page = Math.Clamp(page, 1, Math.Max(1, (total + pageSize - 1) / pageSize));
     var rows = await query.OrderByDescending(x => x.KhoaHoc).ThenBy(x => x.Id)
         .Skip((page - 1) * pageSize).Take(pageSize)
         .Select(x => new { Curriculum = new Curriculum { Id = x.Id, HeDaoTao = x.HeDaoTao, KhoaHoc = x.KhoaHoc,
             NganhHocId = x.NganhHocId, TenChuongTrinh = x.TenChuongTrinh, TrangThai = x.TrangThai,
             NganhHoc = x.NganhHoc == null ? null : new Nganh { MaNganh = x.NganhHoc.MaNganh, TenNganh = x.NganhHoc.TenNganh } },
             SectionCount = x.Sections.Count() }).ToListAsync();
     return View(new CurriculumCatalogViewModel { All = options, Visible = rows.Select(x => x.Curriculum).ToList(),
         SectionCounts = rows.ToDictionary(x => x.Curriculum.Id, x => x.SectionCount),
         System = system, Year = year, Major = major, Program = program, Active = active,
         Page = page, PageSize = pageSize, Total = total });
 }
 [HttpGet("edit/{id:int?}")] public async Task<IActionResult> Edit(int? id) {
 var vm=new CurriculumEditViewModel();if(id.HasValue){var c=await db.Curriculums.AsNoTracking().AsSplitQuery().Include(x=>x.Sections).FirstOrDefaultAsync(x=>x.Id==id);if(c==null)return NotFound();vm.Id=c.Id;vm.HeDaoTao=c.HeDaoTao;vm.KhoaHoc=c.KhoaHoc;vm.NganhHocId=c.NganhHocId;vm.TenChuongTrinh=c.TenChuongTrinh;vm.TrangThai=c.TrangThai;vm.Contents=Enumerable.Range(1,11).Select(n=>c.Sections.FirstOrDefault(s=>s.ThuTu==n)?.NoiDung??"").ToList();}vm.Majors=await db.Nganhs.AsNoTracking().OrderBy(x=>x.TenNganh).Select(x=>new Nganh { MaNganh=x.MaNganh, TenNganh=x.TenNganh }).ToListAsync();return View(vm); }
 [HttpPost("save"),ValidateAntiForgeryToken,RequestSizeLimit(268435456),RequestFormLimits(ValueLengthLimit=8388608)] public async Task<IActionResult> Save(CurriculumEditViewModel vm) {
 var c=vm.Id==0?new Curriculum():await db.Curriculums.Include(x=>x.Sections).FirstOrDefaultAsync(x=>x.Id==vm.Id);if(c==null)return NotFound();
 if(vm.Id>0){vm.HeDaoTao=c.HeDaoTao;vm.KhoaHoc=c.KhoaHoc;vm.NganhHocId=c.NganhHocId;vm.TenChuongTrinh=c.TenChuongTrinh;ModelState.Remove(nameof(vm.HeDaoTao));ModelState.Remove(nameof(vm.KhoaHoc));ModelState.Remove(nameof(vm.NganhHocId));ModelState.Remove(nameof(vm.TenChuongTrinh));}
 vm.HeDaoTao=(vm.HeDaoTao??"").Trim();vm.TenChuongTrinh=(vm.TenChuongTrinh??"").Trim();
 if(string.IsNullOrWhiteSpace(vm.HeDaoTao)||string.IsNullOrWhiteSpace(vm.TenChuongTrinh))ModelState.AddModelError("","Nhập hệ và tên chương trình.");
 vm.Contents ??= new();
 if(vm.Contents.Count>11)ModelState.AddModelError("","Chương trình hỗ trợ tối đa 11 mục nội dung.");
 for(int n=0;n<vm.Contents.Count;n++)if((vm.Contents[n]?.Length??0)>2000000)ModelState.AddModelError($"Contents[{n}]",$"Mục {n+1} vượt quá 2.000.000 ký tự. Các mục khác có thể để trống.");
 if(!await db.Nganhs.AnyAsync(x=>x.MaNganh==vm.NganhHocId))ModelState.AddModelError("NganhHocId","Ngành không tồn tại.");
 if(await db.Curriculums.AnyAsync(x=>x.Id!=vm.Id&&x.HeDaoTao==vm.HeDaoTao&&x.KhoaHoc==vm.KhoaHoc&&x.NganhHocId==vm.NganhHocId&&x.TenChuongTrinh==vm.TenChuongTrinh))ModelState.AddModelError("","Chương trình đã tồn tại trong hệ/khóa/ngành này.");
 if(!ModelState.IsValid){vm.Majors=await db.Nganhs.OrderBy(x=>x.TenNganh).ToListAsync();vm.Contents=(vm.Contents??new()).Take(11).Concat(Enumerable.Repeat("",11)).Take(11).ToList();return View("Edit",vm);}
 if(vm.Id==0){c.HeDaoTao=vm.HeDaoTao;c.KhoaHoc=vm.KhoaHoc;c.NganhHocId=vm.NganhHocId;c.TenChuongTrinh=vm.TenChuongTrinh;}
 c.TrangThai=vm.TrangThai;c.NgayCapNhat=DateTime.UtcNow;
 for(int i=1;i<=11;i++){var section=c.Sections.FirstOrDefault(x=>x.ThuTu==i);if(section==null){section=new CurriculumSection{ThuTu=i};c.Sections.Add(section);}section.TieuDeMuc=CurriculumRules.Titles[i-1];if(i<=vm.Contents.Count)section.NoiDung=PortalRules.SanitizeHtml(vm.Contents[i-1]);}
 if(vm.Id==0)db.Curriculums.Add(c);try{await db.SaveChangesAsync();}catch(DbUpdateException){ModelState.AddModelError("","Không thể lưu; kiểm tra trùng hệ/khóa/ngành/tên chương trình.");vm.Majors=await db.Nganhs.ToListAsync();return View("Edit",vm);}TempData["Message"]="Đã lưu CTĐT.";return RedirectToAction(nameof(Index)); }
 [HttpPost("delete/{id:int}"),ValidateAntiForgeryToken] public async Task<IActionResult> Delete(int id){var c=await db.Curriculums.FindAsync(id);if(c==null)return NotFound();db.Curriculums.Remove(c);await db.SaveChangesAsync();return RedirectToAction(nameof(Index));}
 [HttpPost("clone/{id:int}"),ValidateAntiForgeryToken] public async Task<IActionResult> Clone(int id,int year){var c=await db.Curriculums.AsNoTracking().Include(x=>x.Sections).FirstOrDefaultAsync(x=>x.Id==id);if(c==null)return NotFound();if(year<1900||year>2200||await db.Curriculums.AnyAsync(x=>x.HeDaoTao==c.HeDaoTao&&x.NganhHocId==c.NganhHocId&&x.TenChuongTrinh==c.TenChuongTrinh&&x.KhoaHoc==year)){TempData["Message"]="Khóa không hợp lệ hoặc CTĐT đã tồn tại.";return RedirectToAction(nameof(Index));}
 var copy=new Curriculum{HeDaoTao=c.HeDaoTao,KhoaHoc=year,NganhHocId=c.NganhHocId,TenChuongTrinh=c.TenChuongTrinh,TrangThai=false,Sections=Enumerable.Range(1,11).Select(n=>new CurriculumSection{ThuTu=n,TieuDeMuc=CurriculumRules.Titles[n-1],NoiDung=PortalRules.SanitizeHtml(c.Sections.FirstOrDefault(s=>s.ThuTu==n)?.NoiDung)}).ToList()};db.Curriculums.Add(copy);try{await db.SaveChangesAsync();}catch(DbUpdateException){TempData["Message"]="CTĐT khóa đích đã tồn tại.";return RedirectToAction(nameof(Index));}return RedirectToAction(nameof(Edit),new{id=copy.Id}); }
}
