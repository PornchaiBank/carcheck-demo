SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Applies to Admin and User. NULL preserves access to the default center.
-- Additional selected centers are stored as a JSON array in allowed_center_ids.
-- Id_center remains the default and is always included by the API.
IF COL_LENGTH(N'dbo.db_staff',N'allowed_center_ids') IS NULL
    ALTER TABLE dbo.db_staff ADD allowed_center_ids nvarchar(max) NULL;

SELECT User_id,User_name,Id_center,allowed_center_ids FROM dbo.db_staff WHERE flag='0';
