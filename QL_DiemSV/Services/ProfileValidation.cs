using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Data;

namespace QL_DiemSV.Services;

internal static partial class ProfileValidation
{
    public static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex(@"\A[A-Za-z0-9_-]{1,20}\z")]
    internal static partial Regex Code();

    [GeneratedRegex(@"\A[0-9]{12}\z")]
    private static partial Regex IdentityNumber();

    [GeneratedRegex(@"\A[0-9]{10,11}\z")]
    private static partial Regex Phone();

    public static string? FormatError(DateTime? birthDate, string? gender, string? identity, string? phone,
        string? email, string? address = null)
    {
        if (birthDate?.Date > DateTime.UtcNow.AddHours(7).Date) return "Ngày sinh không được nằm trong tương lai.";
        if (gender is not (null or "Nam" or "Nữ" or "Khác")) return "Giới tính không hợp lệ.";
        if (identity != null && !IdentityNumber().IsMatch(identity)) return "CCCD phải gồm đúng 12 chữ số.";
        if (phone != null && !Phone().IsMatch(phone)) return "Số điện thoại phải gồm 10 hoặc 11 chữ số.";
        if (email != null && (email.Length > 255 || !new EmailAddressAttribute().IsValid(email))) return "Email không hợp lệ.";
        return address?.Length > 255 ? "Địa chỉ tối đa 255 ký tự." : null;
    }

    public static async Task<string?> DuplicateErrorAsync(ApplicationDbContext db, string? identity,
        string? phone, string? studentId = null, string? lecturerId = null)
    {
        var matches = await FindContactsAsync(db, [identity], [phone], studentId, lecturerId);
        return matches.DuplicateError(identity, phone);
    }

    public static async Task<ContactMatches> FindContactsAsync(ApplicationDbContext db, IEnumerable<string?> identityValues,
        IEnumerable<string?> phoneValues, string? studentId = null, string? lecturerId = null)
    {
        var identities = identityValues.Select(Optional).OfType<string>().Where(value => IdentityNumber().IsMatch(value)).Distinct().ToArray();
        var phones = phoneValues.Select(Optional).OfType<string>().Where(value => Phone().IsMatch(value)).Distinct().ToArray();
        if (identities.Length == 0 && phones.Length == 0) return new([], []);

        var students = db.SinhViens.Where(x => studentId == null || x.MaSV != studentId)
            .Select(x => new { x.CCCD, x.SDT });
        var lecturers = db.GiangViens.Where(x => lecturerId == null || x.MaGV != lecturerId)
            .Select(x => new { x.CCCD, x.SDT });
        var matches = await students.Concat(lecturers)
            .Where(x => identities.Contains(x.CCCD) || phones.Contains(x.SDT))
            .Distinct().ToListAsync();

        return new(matches.Select(x => Optional(x.CCCD)).OfType<string>().ToHashSet(StringComparer.Ordinal),
            matches.Select(x => Optional(x.SDT)).OfType<string>().ToHashSet(StringComparer.Ordinal));
    }

    internal sealed record ContactMatches(HashSet<string> Identities, HashSet<string> Phones)
    {
        public string? DuplicateError(string? identity, string? phone)
        {
            if (identity != null && Identities.Contains(identity)) return "CCCD đã được sử dụng trong một hồ sơ khác.";
            return phone != null && Phones.Contains(phone) ? "Số điện thoại đã được sử dụng trong một hồ sơ khác." : null;
        }
    }
}
