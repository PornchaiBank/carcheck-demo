SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.db_vehicle_copy',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.db_vehicle_copy
    (
        id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_db_vehicle_copy PRIMARY KEY,
        letter_category nvarchar(20) NOT NULL,
        registration_number nvarchar(30) NOT NULL,
        province nvarchar(100) NOT NULL,
        image_data varbinary(max) NULL,
        image_content_type varchar(50) NULL,
        center_id varchar(200) NOT NULL,
        created_by varchar(200) NOT NULL,
        created_at datetime2(0) NOT NULL CONSTRAINT DF_db_vehicle_copy_created_at DEFAULT SYSDATETIME(),
        modified_by varchar(200) NULL,
        modified_at datetime2(0) NULL
    );
    CREATE INDEX IX_db_vehicle_copy_center ON dbo.db_vehicle_copy(center_id,created_at DESC);
END;

IF COL_LENGTH(N'dbo.db_vehicle_copy',N'note') IS NULL
    ALTER TABLE dbo.db_vehicle_copy ADD note nvarchar(1000) NULL;

SELECT id,letter_category,registration_number,province,center_id,created_by,created_at,
       CASE WHEN image_data IS NULL THEN 0 ELSE 1 END AS has_image
FROM dbo.db_vehicle_copy ORDER BY created_at DESC,id DESC;
