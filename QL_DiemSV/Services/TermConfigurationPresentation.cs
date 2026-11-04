using QL_DiemSV.Models;
namespace QL_DiemSV.Services;
public static class TermConfigurationPresentation
{
    public static (string Label,string Css) Status(HocKy term,DateTime today)
    {
        if(term.NgayBatDauTuan1 is not DateTime first)return ("Chưa cấu hình","neutral");
        if(today.Date<first.Date)return ("Sắp tới","upcoming");
        return (today.Date-first.Date).TotalDays<term.SoTuan*7?("Hiện tại","active"):("Đã kết thúc","ended");
    }
}
