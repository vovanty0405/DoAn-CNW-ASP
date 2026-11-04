-- Current application uses QuanLyDiemSV_Demo. For the original database, change ONLY this USE line.
USE [QuanLyDiemSV_Demo];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 10000;

-- Existing PK/unique/FK indexes with equivalent leading keys are reused.
-- No tables, columns or data are removed. Safe to run again.
DECLARE @indexes TABLE
(
    TableName sysname, IndexName sysname, Column1 sysname, Column2 sysname NULL,
    Column3 sysname NULL, Column4 sysname NULL, KeySql nvarchar(500)
);
INSERT @indexes VALUES
('LopHocPhan', 'IX_LopHocPhan_MaHK', 'MaHK', NULL, NULL, NULL, N'[MaHK]'),
('ClassSchedule', 'IX_ClassSchedule_MaLHP_Thu', 'MaLHP', 'Thu', NULL, NULL, N'[MaLHP], [Thu]'),
('KetQuaHocTap', 'IX_KetQuaHocTap_MaSV_MaLHP', 'MaSV', 'MaLHP', NULL, NULL, N'[MaSV], [MaLHP]'),
('KetQuaHocTap', 'IX_KetQuaHocTap_MaLHP_DiemTongKet', 'MaLHP', 'DiemTongKet', NULL, NULL, N'[MaLHP], [DiemTongKet]'),
('KhungChuongTrinh', 'IX_KhungChuongTrinh_MaNganh', 'MaNganh', NULL, NULL, NULL, N'[MaNganh]'),
('Article', 'IX_Article_TrangThai_GhimDauTrang_NgayDang_Id', 'TrangThai', 'GhimDauTrang', 'NgayDang', 'Id',
 N'[TrangThai], [GhimDauTrang] DESC, [NgayDang] DESC, [Id] DESC');

DECLARE @table sysname, @index sysname, @c1 sysname, @c2 sysname, @c3 sysname, @c4 sysname,
        @keys nvarchar(500), @object int, @sql nvarchar(max);
DECLARE index_cursor CURSOR LOCAL FAST_FORWARD FOR SELECT * FROM @indexes;
BEGIN TRY
    BEGIN TRANSACTION;
    OPEN index_cursor;
    FETCH NEXT FROM index_cursor INTO @table,@index,@c1,@c2,@c3,@c4,@keys;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @object = OBJECT_ID(N'dbo.' + @table, N'U');
        IF @object IS NULL THROW 51000, 'Missing required table. Check target database.', 1;
        IF NOT EXISTS
        (
            SELECT 1 FROM sys.indexes i
            WHERE i.object_id=@object AND i.type IN (1,2) AND i.is_disabled=0 AND i.is_hypothetical=0 AND i.has_filter=0
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
                WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=1 AND c.name=@c1)
            AND (@c2 IS NULL OR EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
                WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=2 AND c.name=@c2))
            AND (@c3 IS NULL OR EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
                WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=3 AND c.name=@c3))
            AND (@c4 IS NULL OR EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
                WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=4 AND c.name=@c4))
            AND (@table <> 'Article' OR EXISTS (SELECT 1 FROM sys.index_columns ic
                WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=2 AND ic.is_descending_key=1)
                AND EXISTS (SELECT 1 FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=3 AND ic.is_descending_key=1)
                AND EXISTS (SELECT 1 FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=4 AND ic.is_descending_key=1))
        )
        BEGIN
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=@object AND name=@index)
                THROW 51001, 'Index name exists with a different definition. Review manually.', 1;
            SET @sql = N'CREATE NONCLUSTERED INDEX ' + QUOTENAME(@index) + N' ON dbo.' + QUOTENAME(@table) + N' (' + @keys + N');';
            EXEC sys.sp_executesql @sql;
            PRINT N'Created: ' + @index;
        END
        ELSE PRINT N'Reused existing index: ' + @table + N' (' + @keys + N')';
        FETCH NEXT FROM index_cursor INTO @table,@index,@c1,@c2,@c3,@c4,@keys;
    END;
    CLOSE index_cursor;
    DEALLOCATE index_cursor;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF CURSOR_STATUS('local','index_cursor') >= 0 CLOSE index_cursor;
    IF CURSOR_STATUS('local','index_cursor') >= -1 DEALLOCATE index_cursor;
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
