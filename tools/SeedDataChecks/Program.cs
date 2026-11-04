using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;
using QL_DiemSV.Data;
using QL_DiemSV.Services;
var root=Environment.CurrentDirectory;var file=Path.Combine(root,"SeedData_QuanLyDiemSV.sql");
var seed=await File.ReadAllTextAsync(file);seed=seed.Replace("'admin123'","'"+BCrypt.Net.BCrypt.HashPassword("admin123")+"'").Replace("'123456'","'"+BCrypt.Net.BCrypt.HashPassword("123456")+"'");await File.WriteAllTextAsync(file,seed,new System.Text.UTF8Encoding(true));
if(!args.Contains("--verify")){Console.WriteLine("Seed passwords converted to BCrypt hashes.");return;}
var name="AcademicLedger_SeedCheck_"+Guid.NewGuid().ToString("N");
await using var master=new SqlConnection("Server=.\\SQLEXPRESS01;Database=master;Trusted_Connection=True;TrustServerCertificate=True");await master.OpenAsync();
async Task Master(string sql){using var cmd=new SqlCommand(sql,master);await cmd.ExecuteNonQueryAsync();}
try {
 await Master($"CREATE DATABASE [{name}]");
 await using var db=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer($"Server=.\\SQLEXPRESS01;Database={name};Trusted_Connection=True;TrustServerCertificate=True").Options);
 string Clean(string sql)=>Regex.Replace(Regex.Replace(sql,@"^\s*GO\s*$","",RegexOptions.Multiline|RegexOptions.IgnoreCase),@"^USE \[QuanLyDiemSV\];\s*$","",RegexOptions.Multiline|RegexOptions.IgnoreCase);
 await db.Database.ExecuteSqlRawAsync(Clean(db.Database.GenerateCreateScript()));
 await db.Database.OpenConnectionAsync();using(var seedCommand=db.Database.GetDbConnection().CreateCommand()){seedCommand.CommandText=Clean(seed);seedCommand.CommandTimeout=180;await seedCommand.ExecuteNonQueryAsync();}
 void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
 Check(await db.SinhViens.CountAsync()==450&&await db.LopHanhChinhs.CountAsync()==30,"450 students, 30 classes");
 Check(await db.UserAccounts.CountAsync(x=>x.Role!.RoleName=="SinhVien")==450,"450 student accounts");
 db.Database.SetCommandTimeout(180);
 var grades=await db.KetQuaHocTaps.AsNoTracking().Where(x=>x.DiemTongKet!=null).Select(x=>new{x.MaSV,MaMon=x.LopHocPhan!.MaMon,TC=x.LopHocPhan.MonHoc!.SoTinChi,x.DiemTongKet}).ToListAsync();
 var framework=await db.KhungChuongTrinhs.ToListAsync();var students=await db.SinhViens.Include(x=>x.LopHanhChinh).ToListAsync();int passed=0,failed=0;
 foreach(var s in students.Where(x=>x.MaSV.Substring(3,2) is "22" or "23")){
 var records=grades.Where(x=>x.MaSV==s.MaSV).GroupBy(x=>x.MaMon).Select(g=>g.MaxBy(x=>x.DiemTongKet)!).ToList();
 int req=0,opt=0,attempt=0;double points=0;bool debt=false;
 foreach(var k in records){var tc=k.TC;attempt+=tc;points+=GradeHelper.QuyDoiHe4(k.DiemTongKet!.Value)*tc;if(k.DiemTongKet<4){debt=true;continue;}if(framework.Single(x=>x.MaNganh==s.LopHanhChinh!.MaNganh&&x.MaMon==k.MaMon).LoaiMon=="Tự chọn")opt+=tc;else req+=tc;}
 bool eligible=req>=112&&opt>=28&&req+opt>=140&&!debt&&points/attempt>=2;bool expected=int.Parse(s.MaSV[^4..])<=5810;Check(eligible==expected,"graduation "+s.MaSV);if(eligible)passed++;else failed++;
 }
 Check(passed==120&&failed==60,"120 graduate cases and 60 ineligible final-year cases");
 Check(BCrypt.Net.BCrypt.Verify("123456",(await db.UserAccounts.FirstAsync(x=>x.Username=="DTH235801")).PasswordHash),"demo student password hash");
 Console.WriteLine($"Grade records: {await db.KetQuaHocTaps.CountAsync()}");
} finally {SqlConnection.ClearAllPools();if(!name.StartsWith("AcademicLedger_SeedCheck_"))throw new Exception("Unsafe cleanup");await Master($"IF DB_ID(N'{name}') IS NOT NULL BEGIN ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;DROP DATABASE [{name}];END");Console.WriteLine("Temporary database removed.");}
