using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Data;
using QL_DiemSV.Models;
using QL_DiemSV.Services;
using QL_DiemSV.ViewModels;
namespace QL_DiemSV.Controllers;
[Authorize(Roles="Admin,GiangVien")]
[Route("bai-viet")]
public class ArticleController(ApplicationDbContext db,IWebHostEnvironment environment):Controller
{
    private int AuthorId=>int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),out int id)?id:0;
    private bool CanEdit(Article article)=>User.IsInRole("Admin")||article.NguoiDangId==AuthorId;
    [HttpGet("")]
    public async Task<IActionResult> Index(string? search,string? category,string? status,int page=1)
    {
        var q=db.Articles.AsNoTracking().AsQueryable();
        if(!User.IsInRole("Admin"))q=q.Where(x=>x.NguoiDangId==AuthorId);
        if(!string.IsNullOrWhiteSpace(search))q=q.Where(x=>x.TieuDe.Contains(search)||x.DonViDang.Contains(search));
        if(!string.IsNullOrWhiteSpace(category))q=q.Where(x=>x.DanhMuc==category);
        if(!string.IsNullOrWhiteSpace(status))q=q.Where(x=>x.TrangThai==status);
        int total=await q.CountAsync();page=Math.Clamp(page,1,Math.Max(1,(total+19)/20));
        return View(new ArticleListViewModel{Articles=await q.OrderByDescending(x=>x.GhimDauTrang).ThenByDescending(x=>x.NgayDang).Skip((page-1)*20).Take(20).ToListAsync(),Search=search,Category=category,Status=status,Page=page,Total=total});
    }
    [HttpGet("them")]
    public IActionResult Create()=>View("Edit",new ArticleEditViewModel{DonViDang=User.FindFirst("HoTen")?.Value??User.Identity?.Name??""});
    [HttpGet("{id:int}/sua")]
    public async Task<IActionResult> Edit(int id)
    {
        var a=await db.Articles.FindAsync(id);if(a==null)return NotFound();if(!CanEdit(a))return Forbid();
        return View(new ArticleEditViewModel{Id=a.Id,TieuDe=a.TieuDe,TomTat=a.TomTat,NoiDung=a.NoiDung,DanhMuc=a.DanhMuc,DonViDang=a.DonViDang,GhimDauTrang=a.GhimDauTrang,ThumbnailUrl=a.ThumbnailUrl,TrangThai=a.TrangThai,CurrentAttachment=a.TenFileDinhKem});
    }
    [HttpPost("luu"),ValidateAntiForgeryToken,RequestSizeLimit(6*1024*1024)]
    public async Task<IActionResult> Save(ArticleEditViewModel vm)
    {
        var a=vm.Id==0?new Article{NguoiDangId=AuthorId}:await db.Articles.FindAsync(vm.Id);
        if(a==null)return NotFound();if(!CanEdit(a))return Forbid();
        if(!await db.UserAccounts.AnyAsync(x=>x.UserID==AuthorId&&x.IsActive))return Forbid();
        if(!PortalRules.Categories.ContainsKey(vm.DanhMuc??""))ModelState.AddModelError(nameof(vm.DanhMuc),"Danh mục không hợp lệ.");
        if(vm.TrangThai!="BanNhap"&&vm.TrangThai!="DaXuatBan")ModelState.AddModelError(nameof(vm.TrangThai),"Trạng thái không hợp lệ.");
        if(!string.IsNullOrWhiteSpace(vm.ThumbnailUrl)&&!PortalRules.SafeUrl(vm.ThumbnailUrl,true))ModelState.AddModelError(nameof(vm.ThumbnailUrl),"Ảnh phải là URL HTTPS hoặc đường dẫn /images/.");
        var safe=PortalRules.SanitizeHtml(vm.NoiDung);
        if(string.IsNullOrWhiteSpace(System.Text.RegularExpressions.Regex.Replace(safe,"<[^>]*>","")))ModelState.AddModelError(nameof(vm.NoiDung),"Nội dung phải có văn bản.");
        byte[]? upload=null;string? extension=null;
        if(vm.File is {Length:>0}) {
            extension=Path.GetExtension(vm.File.FileName).ToLowerInvariant();
            if(Path.GetFileName(vm.File.FileName).Length>255)ModelState.AddModelError(nameof(vm.File),"Tên tệp tối đa 255 ký tự.");
            if(vm.File.Length>5*1024*1024||!new[]{".pdf",".docx",".xlsx",".txt",".png",".jpg",".jpeg"}.Contains(extension))ModelState.AddModelError(nameof(vm.File),"Tệp tối đa 5 MB: PDF, DOCX, XLSX, TXT, PNG, JPG.");
            else {
                using var buffer=new MemoryStream();await vm.File.CopyToAsync(buffer);upload=buffer.ToArray();
                bool signature=extension switch { ".pdf"=>upload.AsSpan().StartsWith("%PDF-"u8),".docx" or ".xlsx"=>upload.AsSpan().StartsWith("PK"u8),".png"=>upload.AsSpan().StartsWith(new byte[]{137,80,78,71,13,10,26,10}),".jpg" or ".jpeg"=>upload.Length>2&&upload[0]==255&&upload[1]==216&&upload[2]==255,_=>true };
                if(!signature)ModelState.AddModelError(nameof(vm.File),"Nội dung tệp không khớp định dạng.");
            }
        }
        vm.CurrentAttachment=a.TenFileDinhKem;
        if(!ModelState.IsValid)return View("Edit",vm);
        string? old=a.FileDinhKemUrl,newFile=null;
        if(upload!=null){
            var dir=Path.Combine(environment.ContentRootPath,"App_Data","PortalFiles");Directory.CreateDirectory(dir);
            newFile=Guid.NewGuid().ToString("N")+extension;await System.IO.File.WriteAllBytesAsync(Path.Combine(dir,newFile),upload);
            a.FileDinhKemUrl="/portal-files/"+newFile;a.TenFileDinhKem=Path.GetFileName(vm.File!.FileName);a.DungLuongFile=upload.LongLength;
        }else if(vm.RemoveAttachment){a.FileDinhKemUrl=null;a.TenFileDinhKem=null;a.DungLuongFile=null;}
        a.TieuDe=vm.TieuDe.Trim();a.TomTat=vm.TomTat?.Trim()??"";a.NoiDung=safe;a.DanhMuc=vm.DanhMuc!;a.DonViDang=vm.DonViDang.Trim();a.GhimDauTrang=vm.GhimDauTrang;a.ThumbnailUrl=string.IsNullOrWhiteSpace(vm.ThumbnailUrl)?null:vm.ThumbnailUrl.Trim();
        if(vm.TrangThai=="DaXuatBan"&&a.TrangThai!="DaXuatBan") { a.XuatBanLuc=DateTime.UtcNow;a.NgayDang=a.XuatBanLuc.Value; }
        a.TrangThai=vm.TrangThai;
        if(vm.Id==0)db.Articles.Add(a);
        try {await db.SaveChangesAsync();}catch {if(newFile!=null)DeleteStoredFile("/portal-files/"+newFile);throw;}
        if(old!=a.FileDinhKemUrl)DeleteStoredFile(old);
        TempData["Message"]="Đã lưu bài viết.";return RedirectToAction(nameof(Index));
    }
    [HttpPost("{id:int}/ghim"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Pin(int id)
    {
        var a=await db.Articles.FindAsync(id);if(a==null)return NotFound();if(!CanEdit(a))return Forbid();a.GhimDauTrang=!a.GhimDauTrang;await db.SaveChangesAsync();return RedirectToAction(nameof(Index));
    }
    [HttpPost("{id:int}/xoa"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var a=await db.Articles.FindAsync(id);if(a==null)return NotFound();if(!CanEdit(a))return Forbid();db.Articles.Remove(a);await db.SaveChangesAsync();DeleteStoredFile(a.FileDinhKemUrl);TempData["Message"]="Đã xóa bài viết.";return RedirectToAction(nameof(Index));
    }
    [AllowAnonymous,HttpGet("/portal/bai-viet/{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        var a=await db.Articles.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==id);
        if(a==null||(a.TrangThai!="DaXuatBan"&&!CanEdit(a)))return NotFound();return View(a);
    }
    [AllowAnonymous,HttpGet("/portal-files/{name}")]
    public async Task<IActionResult> Attachment(string name)
    {
        if(!ValidStorageName(name))return NotFound();
        var a=await db.Articles.AsNoTracking().FirstOrDefaultAsync(x=>x.FileDinhKemUrl=="/portal-files/"+name);
        if(a==null||(a.TrangThai!="DaXuatBan"&&!CanEdit(a)))return NotFound();
        var path=Path.Combine(environment.ContentRootPath,"App_Data","PortalFiles",name);
        if(!System.IO.File.Exists(path))return NotFound();Response.Headers["X-Content-Type-Options"]="nosniff";
        return PhysicalFile(path,"application/octet-stream",a.TenFileDinhKem??"tai-lieu");
    }
    private static bool ValidStorageName(string name)=>System.Text.RegularExpressions.Regex.IsMatch(name,@"\A[a-f0-9]{32}\.(pdf|docx|xlsx|txt|png|jpg|jpeg)\z");
    private void DeleteStoredFile(string? url)
    {
        if(url==null||!url.StartsWith("/portal-files/"))return;var name=url[14..];if(!ValidStorageName(name))return;
        var path=Path.Combine(environment.ContentRootPath,"App_Data","PortalFiles",name);
        try {System.IO.File.Delete(path);}catch(IOException) { /* Allow later cleanup if an active download holds the file. */ }
    }
}
