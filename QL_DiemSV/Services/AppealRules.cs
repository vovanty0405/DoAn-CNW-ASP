namespace QL_DiemSV.Services;

public static class AppealRules
{
    public static string Status(string? status) => status?.Trim().ToLowerInvariant() switch
    {
        "pending" or "chờ xử lý" => "pending",
        "approved" or "đã duyệt" => "approved",
        "rejected" or "từ chối" => "rejected",
        _ => "unknown"
    };

    public static string? GradeType(string? type) => type?.Trim().ToLowerInvariant() switch
    {
        "quatrinh" or "diemqt" or "diemck" => "quatrinh",
        "thilan1" or "diemthilan1" or "cuoiky" or "diemthi" => "thiLan1",
        "thilan2" or "diemthilan2" => "thiLan2",
        _ => null
    };

    public static bool HasGrade(string type, double? coursework, double? exam1, double? exam2) =>
        type switch
        {
            "quatrinh" => coursework.HasValue,
            "thiLan1" => exam1.HasValue,
            "thiLan2" => exam2.HasValue,
            _ => false
        };
}
