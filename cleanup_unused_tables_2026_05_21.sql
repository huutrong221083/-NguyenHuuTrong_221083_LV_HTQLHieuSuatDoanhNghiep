USE [LV2026];
GO

/*
    Cleanup script for likely-unused tables:
    - dbo.AI_HITL_ACTION
    - dbo.THONGBAO_DOITUONG

    Safety features:
    1) Archive data into dbo._ARCHIVE_* tables (if source exists).
    2) Drop FK/check/default constraints on target tables (if present).
    3) Drop indexes that are not PK/unique constraints.
    4) Drop table.
    5) Wrap in transaction.

    Run mode:
    - Set @dry_run = 1 to print planned actions only.
    - Set @dry_run = 0 to execute.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @dry_run bit = 1; -- Change to 0 to execute.

BEGIN TRY
    BEGIN TRAN;

    DECLARE @targets TABLE (TableName sysname NOT NULL PRIMARY KEY);
    INSERT INTO @targets (TableName)
    VALUES (N'AI_HITL_ACTION'), (N'THONGBAO_DOITUONG');

    DECLARE @table sysname;
    DECLARE target_cursor CURSOR FAST_FORWARD FOR
        SELECT TableName FROM @targets ORDER BY TableName;

    OPEN target_cursor;
    FETCH NEXT FROM target_cursor INTO @table;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        DECLARE @source_full nvarchar(300) = N'dbo.' + QUOTENAME(@table);
        DECLARE @archive_name sysname = N'_ARCHIVE_' + @table + N'_20260521';
        DECLARE @archive_full nvarchar(300) = N'dbo.' + QUOTENAME(@archive_name);

        IF OBJECT_ID(@source_full, N'U') IS NOT NULL
        BEGIN
            DECLARE @sql nvarchar(max);

            -- 1) Archive table structure + data.
            IF OBJECT_ID(@archive_full, N'U') IS NULL
            BEGIN
                SET @sql = N'SELECT * INTO ' + @archive_full + N' FROM ' + @source_full + N';';
                IF @dry_run = 1 PRINT N'[DRY-RUN] ' + @sql ELSE EXEC sp_executesql @sql;
            END
            ELSE
            BEGIN
                SET @sql = N'INSERT INTO ' + @archive_full + N' SELECT * FROM ' + @source_full + N';';
                IF @dry_run = 1 PRINT N'[DRY-RUN] ' + @sql ELSE EXEC sp_executesql @sql;
            END

            -- 2) Drop FK constraints defined on this table.
            ;WITH fk_on_table AS
            (
                SELECT fk.name AS ConstraintName
                FROM sys.foreign_keys fk
                WHERE fk.parent_object_id = OBJECT_ID(@source_full, N'U')
            )
            SELECT @sql = STRING_AGG(
                N'ALTER TABLE ' + @source_full + N' DROP CONSTRAINT ' + QUOTENAME(ConstraintName) + N';',
                CHAR(10)
            )
            FROM fk_on_table;
            IF @sql IS NOT NULL
            BEGIN
                IF @dry_run = 1 PRINT N'[DRY-RUN] ' + @sql ELSE EXEC sp_executesql @sql;
            END

            -- 3) Drop check constraints on this table.
            ;WITH ck_on_table AS
            (
                SELECT cc.name AS ConstraintName
                FROM sys.check_constraints cc
                WHERE cc.parent_object_id = OBJECT_ID(@source_full, N'U')
            )
            SELECT @sql = STRING_AGG(
                N'ALTER TABLE ' + @source_full + N' DROP CONSTRAINT ' + QUOTENAME(ConstraintName) + N';',
                CHAR(10)
            )
            FROM ck_on_table;
            IF @sql IS NOT NULL
            BEGIN
                IF @dry_run = 1 PRINT N'[DRY-RUN] ' + @sql ELSE EXEC sp_executesql @sql;
            END

            -- 4) Drop default constraints on this table.
            ;WITH df_on_table AS
            (
                SELECT dc.name AS ConstraintName
                FROM sys.default_constraints dc
                WHERE dc.parent_object_id = OBJECT_ID(@source_full, N'U')
            )
            SELECT @sql = STRING_AGG(
                N'ALTER TABLE ' + @source_full + N' DROP CONSTRAINT ' + QUOTENAME(ConstraintName) + N';',
                CHAR(10)
            )
            FROM df_on_table;
            IF @sql IS NOT NULL
            BEGIN
                IF @dry_run = 1 PRINT N'[DRY-RUN] ' + @sql ELSE EXEC sp_executesql @sql;
            END

            -- 5) Drop non-PK, non-unique-constraint indexes.
            ;WITH ix_on_table AS
            (
                SELECT i.name AS IndexName
                FROM sys.indexes i
                WHERE i.object_id = OBJECT_ID(@source_full, N'U')
                  AND i.index_id > 0
                  AND i.is_primary_key = 0
                  AND i.is_unique_constraint = 0
                  AND i.name IS NOT NULL
            )
            SELECT @sql = STRING_AGG(
                N'DROP INDEX ' + QUOTENAME(IndexName) + N' ON ' + @source_full + N';',
                CHAR(10)
            )
            FROM ix_on_table;
            IF @sql IS NOT NULL
            BEGIN
                IF @dry_run = 1 PRINT N'[DRY-RUN] ' + @sql ELSE EXEC sp_executesql @sql;
            END

            -- 6) Drop table.
            SET @sql = N'DROP TABLE ' + @source_full + N';';
            IF @dry_run = 1 PRINT N'[DRY-RUN] ' + @sql ELSE EXEC sp_executesql @sql;
        END
        ELSE
        BEGIN
            PRINT N'[SKIP] Table not found: ' + @source_full;
        END

        FETCH NEXT FROM target_cursor INTO @table;
    END

    CLOSE target_cursor;
    DEALLOCATE target_cursor;

    IF @dry_run = 1
    BEGIN
        PRINT N'[DRY-RUN] Transaction rolled back.';
        ROLLBACK TRAN;
    END
    ELSE
    BEGIN
        COMMIT TRAN;
        PRINT N'[DONE] Cleanup committed.';
    END
END TRY
BEGIN CATCH
    IF CURSOR_STATUS('global', 'target_cursor') >= -1
    BEGIN
        CLOSE target_cursor;
        DEALLOCATE target_cursor;
    END

    IF XACT_STATE() <> 0
        ROLLBACK TRAN;

    DECLARE @err nvarchar(4000) = ERROR_MESSAGE();
    DECLARE @line int = ERROR_LINE();
    DECLARE @num int = ERROR_NUMBER();
    DECLARE @throwMessage nvarchar(2048);
    SET @throwMessage = N'Cleanup failed at line '
        + CONVERT(nvarchar(20), @line)
        + N' ('
        + CONVERT(nvarchar(20), @num)
        + N'): '
        + ISNULL(@err, N'Unknown error');
    THROW 51000, @throwMessage, 1;
END CATCH;
GO
