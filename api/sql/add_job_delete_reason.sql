SET NOCOUNT ON;
SET XACT_ABORT ON;

IF COL_LENGTH(N'dbo.db_working',N'delete_reason') IS NULL
    ALTER TABLE dbo.db_working ADD delete_reason nvarchar(1000) NULL;
IF COL_LENGTH(N'dbo.db_working',N'deleted_at') IS NULL
    ALTER TABLE dbo.db_working ADD deleted_at datetime2(0) NULL;
IF COL_LENGTH(N'dbo.db_working',N'deleted_by') IS NULL
    ALTER TABLE dbo.db_working ADD deleted_by varchar(200) NULL;

-- Deletion continues to use flag='1'; existing rows are retained.
