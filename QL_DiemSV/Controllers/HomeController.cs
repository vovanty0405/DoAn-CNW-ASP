using Microsoft.AspNetCore.Mvc;
using QL_DiemSV.Models;
using System.Diagnostics;

namespace QL_DiemSV.Controllers;

public class HomeController : Controller
{
    [HttpGet("/")]
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated != true || User.IsInRole("SinhVien") || User.IsInRole("GiangVien")) return Redirect("/portal");
        ViewData["Title"] = "Trang chủ & Tổng quan";
        return View("Index");
    }

    [HttpGet("/login")]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true) return Redirect("/");
        ViewData["Title"] = "Đăng nhập hệ thống";
        return View("Login");
    }

    [HttpGet("/thong-ke")]
    public IActionResult ThongKe()
    {
        if (User.Identity?.IsAuthenticated != true) return Redirect("/login");
        ViewData["Title"] = "Thống kê & Báo cáo học tập";
        return View("ThongKe");
    }

    [HttpGet("/sinh-vien")]
    public IActionResult SinhVien()
    {
        if (User.Identity?.IsAuthenticated != true) return Redirect("/login");
        ViewData["Title"] = "Quản lý Sinh viên";
        return View("SinhVien");
    }

    [HttpGet("/giang-vien")]
    public IActionResult GiangVien()
    {
        if (User.Identity?.IsAuthenticated != true) return Redirect("/login");
        ViewData["Title"] = "Quản lý Giảng viên & Khoa";
        return View("GiangVien");
    }

    [HttpGet("/mon-hoc")]
    public IActionResult MonHoc()
    {
        if (User.Identity?.IsAuthenticated != true) return Redirect("/login");
        ViewData["Title"] = "Quản lý Môn học";
        return View("MonHoc");
    }

    [HttpGet("/khung-dao-tao")]
    public IActionResult KhungDaoTao()
    {
        if (User.Identity?.IsAuthenticated != true) return Redirect("/login");
        ViewData["Title"] = "Khung Chương trình Đào tạo";
        return View("KhungDaoTao");
    }

    [HttpGet("/lop-hoc-phan")]
    public IActionResult LopHocPhan()
    {
        if (User.Identity?.IsAuthenticated != true) return Redirect("/login");
        ViewData["Title"] = "Quản lý Lớp học phần & Xếp lớp";
        return View("LopHocPhan");
    }

    [HttpGet("/lop-hanh-chinh")]
    public IActionResult LopHanhChinh()
    {
        if (User.Identity?.IsAuthenticated != true) return Redirect("/login");
        ViewData["Title"] = "Quản lý Lớp hành chính & Ngành";
        return View("LopHanhChinh");
    }

    [HttpGet("/diem-so")]
    public IActionResult DiemSo()
    {
        if (User.Identity?.IsAuthenticated != true) return Redirect("/login");
        ViewData["Title"] = "Quản lý Điểm số";
        return View("DiemSo");
    }

    [HttpGet("/tra-cuu-diem")]
    public IActionResult TraCuuDiem()
    {
        if (User.Identity?.IsAuthenticated != true) return Redirect("/login");
        ViewData["Title"] = "Tra cứu Điểm & Xét tốt nghiệp";
        return View("TraCuuDiem");
    }

    [HttpGet("/khieu-nai")]
    public IActionResult KhieuNai()
    {
        if (User.Identity?.IsAuthenticated != true) return Redirect("/login");
        ViewData["Title"] = "Quản lý Khiếu nại & Phúc khảo";
        return View("KhieuNai");
    }

    [HttpGet("/tai-khoan")]
    public IActionResult TaiKhoan()
    {
        if (User.Identity?.IsAuthenticated != true) return Redirect("/login");
        ViewData["Title"] = "Quản lý Tài khoản & Phân quyền";
        return View("TaiKhoan");
    }

    [HttpGet("/phan-quyen-chi-tiet/{id?}")]
    public IActionResult PhanQuyenChiTiet(int? id)
    {
        if (User.Identity?.IsAuthenticated != true) return Redirect("/login");
        ViewData["Title"] = "Ma trận Phân quyền chi tiết";
        ViewData["SelectedRoleId"] = id ?? 1;
        return View("PhanQuyenChiTiet");
    }

    [HttpGet("/nhat-ky-he-thong")]
    public IActionResult NhatKyHeThong()
    {
        if (User.Identity?.IsAuthenticated != true) return Redirect("/login");
        ViewData["Title"] = "Nhật ký Hoạt động Hệ thống";
        return View("NhatKyHeThong");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
