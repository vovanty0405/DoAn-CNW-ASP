using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace QL_DiemSV.Services;

public static class PortalRules
{
    public static readonly IReadOnlyDictionary<string, string> Categories = new Dictionary<string, string>
    {
        ["ThongBaoChung"] = "Thông báo chung",
        ["HocPhi"] = "Học phí",
        ["NghiBu"] = "Thông báo Nghỉ - Bù",
        ["HuongDan"] = "Hướng dẫn",
        ["TuyenSinh"] = "Tuyển sinh",
        ["HopDong"] = "Hợp đồng",
        ["CTSV"] = "CTSV",
        ["HocBong"] = "Học bổng",
        ["TinTuc"] = "Tin tức & sự kiện"
    };

    public static readonly IReadOnlyList<string> HomeTabKeys = new[]
    {
        "ThongBaoChung",
        "HocPhi",
        "NghiBu",
        "HuongDan",
        "TuyenSinh",
        "HopDong",
        "CTSV",
        "HocBong"
    };

    public static readonly IReadOnlyList<string> TrainingPrograms = new[]
    {
        "Đại học chính quy",
        "Chương trình Chất lượng cao",
        "Đào tạo Từ xa",
        "Sau đại học",
        "Vừa làm vừa học"
    };

    public static readonly IReadOnlyList<string> DefaultDepartments = new[]
    {
        "Phòng Đào tạo Đại học",
        "Phòng Công tác Sinh viên",
        "Phòng Kế hoạch - Tài chính",
        "Phòng Khảo thí & Đảm bảo Chất lượng",
        "Khoa Công nghệ Thông tin",
        "Khoa Kỹ thuật Máy tính",
        "Khoa Kinh tế & Quản trị",
        "Khoa Điện tử Viễn thông",
        "Khoa Ngoại ngữ"
    };

    public static DateTime Local(DateTime utc)
    {
        var kindUtc = utc.Kind == DateTimeKind.Local ? utc.ToUniversalTime() : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(kindUtc, TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"));
    }

    public static bool IsNew(DateTime utc)
    {
        var localNow = Local(DateTime.UtcNow);
        var localArticle = Local(utc);
        return localArticle <= localNow.AddHours(1) && localArticle >= localNow.AddDays(-3);
    }

    public static string FormatVietnameseDateWithDay(DateTime utc)
    {
        var d = Local(utc);
        var dayName = d.DayOfWeek switch
        {
            DayOfWeek.Monday => "Thứ Hai",
            DayOfWeek.Tuesday => "Thứ Ba",
            DayOfWeek.Wednesday => "Thứ Tư",
            DayOfWeek.Thursday => "Thứ Năm",
            DayOfWeek.Friday => "Thứ Sáu",
            DayOfWeek.Saturday => "Thứ Bảy",
            DayOfWeek.Sunday => "Chủ nhật",
            _ => ""
        };
        return $"{dayName}, {d:dd/MM/yyyy}";
    }

    public static string CategoryBannerTitle(string? category) => category switch
    {
        "NghiBu" => "Thông báo nghỉ lớp",
        "HocPhi" => "Thông báo Học phí",
        "HuongDan" => "Hướng dẫn sinh viên",
        "TuyenSinh" => "Thông tin Tuyển sinh",
        "HopDong" => "Thông báo Hợp đồng",
        "CTSV" => "Công tác Sinh viên",
        "HocBong" => "Thông tin Học bổng",
        "TinTuc" => "Tin tức & Sự kiện",
        _ => "Thông báo chung"
    };

    public static string Size(long bytes) =>
        bytes >= 1048576 ? $"{bytes / 1048576d:0.0} MB" : $"{Math.Max(1, Math.Ceiling(bytes / 1024d)):0} KB";

    public static bool SafeUrl(string? url, bool image = false)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (url.StartsWith('/') && !url.StartsWith("//") && !url.Contains('\\') && !url.Any(char.IsControl))
            return !image || url.StartsWith("/images/") || url.StartsWith("/uploads/") || url.StartsWith("/portal-files/");
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == "https" || (!image && uri.Scheme == "http"));
    }

    private static readonly HashSet<string> AllowedTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "p", "div", "span", "br", "hr",
        "strong", "b", "em", "i", "u", "s", "sub", "sup", "font",
        "h1", "h2", "h3", "h4", "h5", "h6",
        "ul", "ol", "li", "blockquote", "pre", "code",
        "a", "img",
        "table", "caption", "colgroup", "col", "thead", "tbody", "tfoot", "tr", "th", "td"
    };

    private static readonly HashSet<string> SafeStyleProps = new(StringComparer.OrdinalIgnoreCase)
    {
        "color", "background", "background-color",
        "text-align", "font-size", "font-family", "font-weight", "font-style", "text-decoration",
        "line-height", "letter-spacing",
        "border", "border-top", "border-bottom", "border-left", "border-right", "border-collapse", "border-color", "border-radius",
        "padding", "padding-top", "padding-bottom", "padding-left", "padding-right",
        "margin", "margin-top", "margin-bottom", "margin-left", "margin-right",
        "width", "max-width", "min-width", "vertical-align", "list-style-type"
    };

    public static string SanitizeStyle(string? rawStyle)
    {
        if (string.IsNullOrWhiteSpace(rawStyle)) return "";
        var cleanDecls = new List<string>();
        foreach (var part in rawStyle.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = part.IndexOf(':');
            if (idx <= 0) continue;
            var prop = part[..idx].Trim().ToLowerInvariant();
            var val = part[(idx + 1)..].Trim();
            if (!SafeStyleProps.Contains(prop)) continue;
            if (val.Contains("url(", StringComparison.OrdinalIgnoreCase) ||
                val.Contains("expression", StringComparison.OrdinalIgnoreCase) ||
                val.Contains("javascript", StringComparison.OrdinalIgnoreCase) ||
                val.Any(char.IsControl))
                continue;
            cleanDecls.Add($"{prop}: {val}");
        }
        return string.Join("; ", cleanDecls);
    }

    public static string SanitizeHtml(string? html)
    {
        html ??= "";
        if (string.IsNullOrWhiteSpace(html)) return "";
        try
        {
            var normalized = Regex.Replace(html, @"<(br|hr|img)(\s[^>]*)?\s*/*>", m =>
            {
                var tag = m.Groups[1].Value.ToLowerInvariant();
                var attrs = m.Groups[2].Value;
                if (attrs.EndsWith("/")) attrs = attrs[..^1];
                return $"<{tag}{attrs} />";
            }, RegexOptions.IgnoreCase);

            normalized = normalized
                .Replace("&nbsp;", "&#160;")
                .Replace("&ndash;", "&#8211;")
                .Replace("&mdash;", "&#8212;")
                .Replace("&bull;", "&#8226;");

            using var reader = XmlReader.Create(
                new StringReader("<root>" + normalized + "</root>"),
                new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersInDocument = 64000000
                });
            var root = XDocument.Load(reader).Root!;
            return string.Concat(root.Nodes().Select(Render));
        }
        catch (XmlException)
        {
            // Malformed HTML must never bypass the structural allowlist.
            return "<p>" + HtmlEncoder.Default.Encode(html) + "</p>";
        }
    }

    private static string Render(XNode node)
    {
        if (node is XText text) return HtmlEncoder.Default.Encode(text.Value);
        if (node is not XElement el) return "";
        var tag = el.Name.LocalName.ToLowerInvariant();
        if (el.Name.NamespaceName != "" || !AllowedTags.Contains(tag)) return "";

        var attrs = new List<string>();

        var styleVal = SanitizeStyle(el.Attribute("style")?.Value);
        if (!string.IsNullOrEmpty(styleVal))
            attrs.Add($"style=\"{HtmlEncoder.Default.Encode(styleVal)}\"");

        var alignVal = el.Attribute("align")?.Value;
        if (!string.IsNullOrEmpty(alignVal) && new[] { "left", "center", "right", "justify" }.Contains(alignVal.ToLowerInvariant()))
            attrs.Add($"align=\"{HtmlEncoder.Default.Encode(alignVal.ToLowerInvariant())}\"");

        if (tag == "a")
        {
            var href = el.Attribute("href")?.Value;
            if (SafeUrl(href))
            {
                attrs.Add($"href=\"{HtmlEncoder.Default.Encode(href!)}\"");
                attrs.Add("target=\"_blank\"");
                attrs.Add("rel=\"noopener noreferrer\"");
            }
        }
        else if (tag == "img")
        {
            var src = el.Attribute("src")?.Value;
            if (!SafeUrl(src, true)) return "";
            attrs.Add($"src=\"{HtmlEncoder.Default.Encode(src!)}\"");
            var alt = el.Attribute("alt")?.Value ?? "";
            attrs.Add($"alt=\"{HtmlEncoder.Default.Encode(alt)}\"");
            return $"<img {(attrs.Count > 0 ? string.Join(" ", attrs) : "")} />";
        }
        else if (tag == "font")
        {
            foreach (var fa in new[] { "color", "face", "size" })
            {
                var fv = el.Attribute(fa)?.Value;
                if (!string.IsNullOrWhiteSpace(fv) && !fv.Any(char.IsControl))
                    attrs.Add($"{fa}=\"{HtmlEncoder.Default.Encode(fv)}\"");
            }
        }
        else if (tag is "td" or "th")
        {
            foreach (var ta in new[] { "colspan", "rowspan" })
            {
                if (int.TryParse(el.Attribute(ta)?.Value, out int span) && span is >= 1 and <= 1000)
                    attrs.Add($"{ta}=\"{span}\"");
            }
        }
        else if (tag == "table")
        {
            attrs.Add("border=\"1\"");
            attrs.Add("cellpadding=\"6\"");
        }

        if (tag is "br" or "hr" or "col")
            return $"<{tag}{(attrs.Count > 0 ? " " + string.Join(" ", attrs) : "")} />";

        var content = string.Concat(el.Nodes().Select(Render));
        var attrStr = attrs.Count > 0 ? " " + string.Join(" ", attrs) : "";
        return $"<{tag}{attrStr}>{content}</{tag}>";
    }
}
