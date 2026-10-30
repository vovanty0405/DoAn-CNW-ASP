using System.Text.Encodings.Web;
using System.Xml;
using System.Xml.Linq;
using System.Text.RegularExpressions;
namespace QL_DiemSV.Services;
public static class PortalRules
{
    public static readonly IReadOnlyDictionary<string,string> Categories=new Dictionary<string,string>{["ThongBaoChung"]="Thông báo chung",["HocPhi"]="Học phí",["NghiBu"]="Thông báo Nghỉ - Bù",["HuongDan"]="Hướng dẫn",["TuyenSinh"]="Tuyển sinh",["HocBong"]="Học bổng",["CTSV"]="CTSV",["TinTuc"]="Tin tức & Sự kiện"};
    public static DateTime Local(DateTime utc)=>TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc,DateTimeKind.Utc),TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"));
    public static bool IsNew(DateTime utc)=>utc<=DateTime.UtcNow && utc>=DateTime.UtcNow.AddDays(-3);
    public static string Size(long bytes)=>bytes>=1048576?$"{bytes/1048576d:0.0} MB":$"{Math.Ceiling(bytes/1024d):0} KB";
    public static bool SafeUrl(string? url,bool image=false)
    {
        if(string.IsNullOrWhiteSpace(url))return false;
        if(url.StartsWith('/')&&!url.StartsWith("//")&&!url.Contains('\\')&&!url.Any(char.IsControl)) return !image||url.StartsWith("/images/")||url.StartsWith("/uploads/");
        return Uri.TryCreate(url,UriKind.Absolute,out var uri)&& (uri.Scheme=="https"||(!image&&uri.Scheme=="http"));
    }
    private static readonly HashSet<string> Tags=new(StringComparer.OrdinalIgnoreCase){"p","div","br","strong","b","em","i","u","s","h2","h3","h4","ul","ol","li","blockquote","a","table","thead","tbody","tr","th","td","span","hr"};
    // Parse with DTD disabled, then rebuild an allowlist tree. No input markup is emitted verbatim.
    public static string SanitizeHtml(string? html)
    {
        html??="";
        try {
            html=Regex.Replace(html,@"<(br|hr)\s*/*>","<$1 />",RegexOptions.IgnoreCase).Replace("&nbsp;","&#160;");
            using var reader=XmlReader.Create(new StringReader("<root>"+html+"</root>"),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=200000});
            var root=XDocument.Load(reader).Root!;
            return string.Concat(root.Nodes().Select(Render));
        }catch(XmlException){return "<p>"+HtmlEncoder.Default.Encode(html)+"</p>";}
    }
    private static string Render(XNode node)
    {
        if(node is XText text)return HtmlEncoder.Default.Encode(text.Value);
        if(node is not XElement el)return "";
        var tag=el.Name.LocalName.ToLowerInvariant();
        if(el.Name.NamespaceName!=""||!Tags.Contains(tag))return "";
        var content=string.Concat(el.Nodes().Select(Render));
        if(tag=="br"||tag=="hr")return "<"+tag+">";
        string attributes="";
        if(tag=="a"){
            var href=el.Attribute("href")?.Value;
            if(SafeUrl(href))attributes=" href=\""+HtmlEncoder.Default.Encode(href!)+"\" rel=\"noopener noreferrer\"";
        }
        return "<"+tag+attributes+">"+content+"</"+tag+">";
    }
}
