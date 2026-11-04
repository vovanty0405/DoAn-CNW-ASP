using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using QL_DiemSV.Data;
var workspace=Path.GetFullPath(args.FirstOrDefault(x=>!x.StartsWith("--"))??Environment.CurrentDirectory);
var options=new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer("Server=.\\SQLEXPRESS01;Database=QuanLyDiemSV;Trusted_Connection=True;TrustServerCertificate=True").Options;
await using var db=new ApplicationDbContext(options);
var ddl=db.Database.GenerateCreateScript();
var body=Regex.Replace(ddl,@"^\s*GO\s*$","",RegexOptions.Multiline|RegexOptions.IgnoreCase).Trim();
var tables=db.Model.GetEntityTypes().Select(x=>"dbo."+x.GetTableName()).Distinct().Order().ToList();
var foreignKeys=db.Model.GetEntityTypes().Sum(x=>x.GetForeignKeys().Count());
var header="""
-- Academic Ledger: complete SQL Server schema, generated from ApplicationDbContext.
-- Includes tables, columns, identity keys, primary/foreign keys and indexes.
-- No sample records, passwords, or existing user data are exported.
-- Run in SQL Server Management Studio. This script only accepts an empty database.
-- To use another database name, change QuanLyDiemSV in CREATE DATABASE and USE below.
-- Existing populated databases: use the application's upgrade mechanism instead.
USE [master];
GO
IF DB_ID(N'QuanLyDiemSV') IS NULL
    EXEC(N'CREATE DATABASE [QuanLyDiemSV]');
GO
USE [QuanLyDiemSV];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF EXISTS (SELECT 1 FROM sys.tables WHERE is_ms_shipped = 0)
BEGIN
    THROW 51000, N'Database already contains tables. Select an empty database; existing data has not been deleted.', 1;
END
ELSE
BEGIN
    BEGIN TRY
        BEGIN TRANSACTION;
""";
var footer="""
        COMMIT TRANSACTION;
        PRINT N'Academic Ledger schema created successfully.';
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO
SELECT s.name AS [Schema], t.name AS [Table]
FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE t.is_ms_shipped = 0 ORDER BY s.name, t.name;
GO
""";
var output=Path.Combine(workspace,"Database_QLDiemSV_Full.sql");
await File.WriteAllTextAsync(output,header+Environment.NewLine+"-- "+tables.Count+" tables: "+string.Join(", ",tables)+Environment.NewLine+body+Environment.NewLine+footer,new UTF8Encoding(true));
Console.WriteLine($"Exported {tables.Count} tables and {foreignKeys} foreign keys to {output}");
if(args.Contains("--verify")) {
 var name="AcademicLedger_SchemaCheck_"+Guid.NewGuid().ToString("N");
 await using var master=new SqlConnection("Server=.\\SQLEXPRESS01;Database=master;Trusted_Connection=True;TrustServerCertificate=True");await master.OpenAsync();
 async Task Master(string sql){using var cmd=new SqlCommand(sql,master);await cmd.ExecuteNonQueryAsync();}
 try {
  await Master($"CREATE DATABASE [{name}]");
  await using var temp=new SqlConnection($"Server=.\\SQLEXPRESS01;Database={name};Trusted_Connection=True;TrustServerCertificate=True");await temp.OpenAsync();
  using(var cmd=new SqlCommand(body,temp)){cmd.CommandTimeout=60;await cmd.ExecuteNonQueryAsync();}
  using(var cmd=new SqlCommand("SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0",temp)){var count=Convert.ToInt32(await cmd.ExecuteScalarAsync());if(count!=tables.Count)throw new Exception("Table count mismatch");Console.WriteLine($"PASS: {count} tables created by exported DDL");}
  using(var cmd=new SqlCommand("SELECT COUNT(*) FROM sys.foreign_keys",temp)){var count=Convert.ToInt32(await cmd.ExecuteScalarAsync());if(count!=foreignKeys)throw new Exception("Foreign key count mismatch");Console.WriteLine($"PASS: {count} foreign keys match EF model");}
 } finally {SqlConnection.ClearAllPools();if(!name.StartsWith("AcademicLedger_SchemaCheck_"))throw new Exception("Unsafe cleanup");await Master($"IF DB_ID(N'{name}') IS NOT NULL BEGIN ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]; END");Console.WriteLine("Temporary verification database removed.");}
}
