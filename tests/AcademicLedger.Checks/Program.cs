using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Data;
using QL_DiemSV.Models;
using QL_DiemSV.Services;
int count = 0;
void Check(bool result, string name) { if (!result) throw new Exception(name); Console.WriteLine("PASS " + name); count++; }
ClassSchedule Lesson(int start=1,int length=5,int day=2,string weeks="1-20") => new() { TietBatDau=start,SoTiet=length,Thu=day,ChuoiTuanHoc=weeks };
Check(ScheduleRules.Weeks("1-3,6,8-12").SetEquals(new[]{1,2,3,6,8,9,10,11,12}),"Week ranges and lists");
foreach (var invalid in new[]{"", "0", "21", "8-3", "-----6-89012", "a", "1-2-3"}) {
 bool rejected=false;try {ScheduleRules.Weeks(invalid,20);}catch(ArgumentException){rejected=true;}Check(rejected,"Reject malformed weeks: "+invalid);
}
Check(!ScheduleRules.Overlaps(Lesson(),Lesson(6)),"Adjacent periods do not overlap");
Check(ScheduleRules.Overlaps(Lesson(),Lesson(5)),"One-period overlap detected");
Check(!ScheduleRules.Overlaps(Lesson(),Lesson(day:3)),"Different weekdays do not overlap");
Check(!ScheduleRules.Overlaps(Lesson(weeks:"1-5"),Lesson(weeks:"6-10")),"Disjoint weeks do not overlap");
Check(ScheduleRules.Overlaps(Lesson(weeks:"1,8,12"),Lesson(weeks:"6-8")),"Intersecting week detected");
Check(!ScheduleRules.IsOpen(new HocKy()),"Unconfigured registration is closed");
Check(!ScheduleRules.IsOpen(new HocKy{BatDauDangKy=DateTime.MinValue,KetThucDangKy=DateTime.MinValue}),"Expired registration is closed");
Check(ScheduleRules.IsOpen(new HocKy{BatDauDangKy=DateTime.MinValue,KetThucDangKy=DateTime.MaxValue}),"Open registration window");
Check(ScheduleRules.HasGrades(new KetQuaHocTap{DiemGK=0}),"A zero grade still blocks cancellation");
Check(ScheduleRules.HasGrades(new KetQuaHocTap{ChiTietDiemQT="{}"}),"Process grades block cancellation");
Check(!ScheduleRules.HasGrades(new KetQuaHocTap()),"Ungraded enrollment can be cancelled");
// Mimic Razor rowspans, proving each weekday covers exactly 12 cells without extra TDs.
var lessons = new[]{Lesson(1,5),Lesson(6,5),Lesson(2,3,3),Lesson(12,1,8)};
for(int day=2;day<=8;day++) {
 int spanTotal=0, rendered=0;
 for(int period=1;period<=12;period++) {
  var start=lessons.FirstOrDefault(x=>x.Thu==day&&x.TietBatDau==period);
  bool covered=lessons.Any(x=>x.Thu==day&&x.TietBatDau<period&&period<x.TietBatDau+x.SoTiet);
  if(start!=null&&!covered){spanTotal+=start.SoTiet;rendered++;}else if(!covered){spanTotal++;rendered++;}
 }
 Check(spanTotal==12,"Rowspan coverage day "+day);
 if(day==2) Check(rendered==4,"Two 5-period blocks render 4 cells, including 2 empty cells");
}
using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer("Server=(local);Database=ModelCheck;Trusted_Connection=True;TrustServerCertificate=True").Options);
Check(db.Model.FindEntityType(typeof(ClassSchedule))!.GetForeignKeys().Count()==3,"Schedule foreign keys: section, teacher, room");
Check(db.Model.FindEntityType(typeof(KetQuaHocTap))!.FindProperty("HocPhiDangKy")!.GetColumnType()=="decimal(18,2)","Tuition uses decimal precision");
Console.WriteLine($"{count} checks passed.");

for (int period=1;period<=12;period++) {
 var times=RoomMatrixRules.PeriodTime(period).Split(" - ");
 Check((TimeOnly.Parse(times[1])-TimeOnly.Parse(times[0])).TotalMinutes==50,"Period duration 50 minutes: "+period);
}
Check(RoomMatrixRules.PeriodTime(3)=="08:50 - 09:40","Morning break preserved");
Check(RoomMatrixRules.PeriodTime(6)=="13:00 - 13:50","Afternoon session starts at 13:00");
Check(RoomMatrixRules.AcademicDay(new DateTime(2026,11,1))==8,"Sunday maps to day 8");
var matrixBlocks=RoomMatrixRules.Blocks(new[]{Lesson(1,5),Lesson(6,5)});
Check(matrixBlocks.Count==2 && matrixBlocks.Sum(x=>x.Span)==10,"Adjacent lessons remain separate colspan blocks");
var collisions=RoomMatrixRules.Blocks(new[]{Lesson(1,5),Lesson(4,4),Lesson(7,3)});
Check(collisions.Count==1 && collisions[0].Span==9 && collisions[0].Lessons.Count==3,"Overlapping legacy bookings remain visible in one interval");
for(int period=1,coverage=0;period<=12;period++) {
 var block=matrixBlocks.FirstOrDefault(x=>x.Start==period);
 if(block!=null)coverage+=block.Span;else if(!matrixBlocks.Any(x=>x.Start<period&&period<x.Start+x.Span))coverage++;
 if(period==12)Check(coverage==12,"Colspan row always covers 12 periods");
}
Console.WriteLine("Room matrix rule checks passed.");
var today=new DateTime(2026,10,30);
var currentTerm=new HocKy{MaHK="2026",NgayBatDauTuan1=new DateTime(2026,8,10),SoTuan=20};
var oldTerm=new HocKy{MaHK="2023",NgayBatDauTuan1=new DateTime(2023,8,7),SoTuan=20};
var terms=new[]{oldTerm,currentTerm};
var calendar=RoomMatrixRules.ResolveCalendar(terms,null,null,null,null,today);
Check(calendar.Term==currentTerm && calendar.Week==12 && calendar.Day==6 && calendar.Date==today,"Initial matrix selects current semester, week 12, Friday and today");
calendar=RoomMatrixRules.ResolveCalendar(terms,null,null,null,new DateTime(2026,11,1),today);
Check(calendar.Week==12 && calendar.Day==8,"Sunday remains in the current week");
calendar=RoomMatrixRules.ResolveCalendar(terms,null,null,null,new DateTime(2026,11,2),today);
Check(calendar.Week==13 && calendar.Day==2,"Monday advances to the next week");
calendar=RoomMatrixRules.ResolveCalendar(terms,"2026",12,2,null,today);
Check(calendar.Date==new DateTime(2026,10,26),"Explicit week and weekday override default today");
calendar=RoomMatrixRules.ResolveCalendar(terms,"2026",1,2,today,today);
Check(calendar.Week==12 && calendar.Day==6,"Datepicker wins over stale week and weekday query values");
calendar=RoomMatrixRules.ResolveCalendar(terms,"2023",null,null,null,today);
Check(calendar.Term==oldTerm && calendar.OutsideTerm,"Explicit semester remains selected and reports outside date");
calendar=RoomMatrixRules.ResolveCalendar(new[]{oldTerm},null,null,null,null,today);
Check(calendar.Term==oldTerm && calendar.OutsideTerm && calendar.Date==today,"No current semester does not silently display an old week");
calendar=RoomMatrixRules.ResolveCalendar(Array.Empty<HocKy>(),null,null,null,null,today);
Check(calendar.Term==null && calendar.Date==today,"Empty semester list preserves today's date");
Check(!RoomMatrixRules.ContainsDate(currentTerm,currentTerm.NgayBatDauTuan1!.Value.AddDays(140)),"Semester end boundary is exclusive");
var overlappingTerm=new HocKy{MaHK="LATE",NgayBatDauTuan1=new DateTime(2026,10,5),SoTuan=20};
calendar=RoomMatrixRules.ResolveCalendar(terms.Append(overlappingTerm),null,null,null,null,today);
Check(calendar.Term==overlappingTerm,"Latest start resolves overlapping semester dates deterministically");
var compactVm=new QL_DiemSV.ViewModels.RoomMatrixViewModel{ViewMode="compact",Schedules=new(){Lesson(3,3),Lesson(8,2)}};
Check(compactVm.VisiblePeriods.SequenceEqual(Enumerable.Range(3,7)),"Compact matrix trims only leading/trailing empty periods");
Check(new QL_DiemSV.ViewModels.RoomMatrixViewModel{ViewMode="day",Schedules=new(){Lesson(3,3)}}.VisiblePeriods.Count==12,"Daily mode retains all twelve periods");
Console.WriteLine($"{count} total rule checks passed.");
Check(PortalRules.SanitizeHtml("<p>Hello <strong>world</strong></p>")=="<p>Hello <strong>world</strong></p>","Rich text keeps supported formatting");
Check(!PortalRules.SanitizeHtml("<p onclick=\"bad()\">ok<script>alert(1)</script></p>").Contains("script"),"Rich text removes script elements");
Check(!PortalRules.SanitizeHtml("<a href=\"javascript:alert(1)\">click</a>").Contains("href"),"Rich text removes javascript links");
Check(!PortalRules.SanitizeHtml("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>bad()</script></svg>").Contains("svg"),"Rich text drops namespaced elements");
Check(PortalRules.SanitizeHtml("<p>broken").Contains("&lt;"),"Malformed HTML is displayed as escaped text");
Check(!PortalRules.SafeUrl("//evil.example")&&!PortalRules.SafeUrl("/\\evil.example")&&!PortalRules.SafeUrl("javascript:alert(1)"),"Unsafe URL schemes and protocol-relative paths rejected");
Check(PortalRules.SafeUrl("/images/portal-news.svg",true)&&PortalRules.SafeUrl("https://example.com/image.png",true),"Thumbnail allows local images and HTTPS");
Check(!PortalRules.IsNew(DateTime.UtcNow.AddDays(-4))&&PortalRules.IsNew(DateTime.UtcNow.AddDays(-1)),"New badge uses publication within last three days");
var baseLesson=Lesson(1,3,weeks:"1-3");
Check(TimetablePresentation.Status(baseLesson,new[]{baseLesson},false,1)=="normal","A lesson never conflicts with itself");
var partial=Lesson(3,3,weeks:"1-3");
Check(TimetablePresentation.Status(baseLesson,new[]{baseLesson,partial},false,1)=="partial-overlap","One shared period is a partial overlap");
var full=Lesson(1,3,weeks:"1-3");
Check(TimetablePresentation.Status(baseLesson,new[]{baseLesson,full},false,1)=="full-overlap","All periods shared is a full overlap");
var otherWeeks=Lesson(1,3,weeks:"4-6");
Check(TimetablePresentation.Status(baseLesson,new[]{baseLesson,otherWeeks},false,null)=="normal","Whole-semester conflict detection respects disjoint weeks");
full.TuanBaoNghi="1";
Check(TimetablePresentation.Status(baseLesson,new[]{baseLesson,full},false,1)=="normal","Cancelled session does not create a conflict");
Check(TimetablePresentation.Status(full,new[]{baseLesson,full},false,1)=="cancelled","Absence is shown only for the recorded week");
Check(TimetablePresentation.Status(full,new[]{full},false,2)=="normal","Other weeks of same session are not cancelled");
Check(TimetablePresentation.Status(baseLesson,new[]{baseLesson},true,1)=="historical","Past semester without conflicts uses historical color");
Check(TimetablePresentation.Status(full,new[]{full},true,1)=="cancelled","Absence takes precedence over historical color");
Check(TimetablePresentation.Status(baseLesson,new[]{baseLesson,full},false,null)=="partial-overlap","Full overlap in only some weeks is partial for whole semester");
full.TuanBaoNghi="1-3";
Check(TimetablePresentation.Status(full,new[]{full},false,null)=="cancelled","Whole semester cancellation requires all scheduled weeks");
Check(TimetablePresentation.WeekLabel(currentTerm,12)=="Tuần 12 [Từ 26/10/2026 -- Đến 01/11/2026]","Week labels show complete date ranges");
Check(!TimetablePresentation.IsHistorical(new HocKy(),today),"Unconfigured semester is never guessed to be historical");
Console.WriteLine($"{count} total rule checks including timetable presentation passed.");
if (args.Contains("--database")) await DatabaseChecks.Run();
