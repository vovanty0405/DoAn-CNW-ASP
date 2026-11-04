using QL_DiemSV.Services;
void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);}
var table=PortalRules.SanitizeHtml("<table><tbody><tr><td rowspan=\"2\" colspan=\"30\" style=\"mso-font-size:12pt;background-color:#ffff00;border:1px solid #999;padding:8px\">A</td><td>B</td></tr><tr><td>C</td></tr></tbody></table>");
Check(table.Contains("rowspan=\"2\"")&&table.Contains("colspan=\"30\""),"server preserves table merge structure");
Check(!table.Contains("mso-")&&table.Contains("background-color")&&table.Contains("padding"),"Word CSS removed, cell shading and padding retained");
var text=PortalRules.SanitizeHtml("<p style=\"font-family:Times New Roman;font-size:18pt;line-height:1.5;text-align:justify\"><sub>x</sub><sup>2</sup></p>");
Check(text.Contains("18pt")&&text.Contains("line-height")&&text.Contains("<sub>")&&text.Contains("<sup>"),"font, paragraph and script formatting survives save");
var bad=PortalRules.SanitizeHtml("<p><a href='javascript:alert(1)'>broken");
Check(!bad.Contains("<a href="),"malformed HTML cannot bypass sanitizer");
var unsafeHtml=PortalRules.SanitizeHtml("<table><tr><td onclick=\"bad()\" style=\"background:url(javascript:bad)\">A</td></tr></table>");
Check(!unsafeHtml.Contains("onclick")&&!unsafeHtml.Contains("javascript"),"event handlers and active CSS rejected");
Check(PortalRules.SanitizeHtml(table).Contains("rowspan=\"2\""),"saved tables remain stable on subsequent render");
