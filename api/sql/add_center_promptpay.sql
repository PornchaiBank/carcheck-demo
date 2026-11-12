SET NOCOUNT ON;
SET XACT_ABORT ON;

IF COL_LENGTH(N'dbo.db_center',N'promptpay_type') IS NULL
    ALTER TABLE dbo.db_center ADD promptpay_type varchar(20) NULL;

IF COL_LENGTH(N'dbo.db_center',N'promptpay_number') IS NULL
    ALTER TABLE dbo.db_center ADD promptpay_number varchar(20) NULL;

IF COL_LENGTH(N'dbo.db_center',N'payment_qr_mode') IS NULL
    ALTER TABLE dbo.db_center ADD payment_qr_mode varchar(20) NULL;
IF COL_LENGTH(N'dbo.db_center',N'payment_qr_image') IS NULL
    ALTER TABLE dbo.db_center ADD payment_qr_image varbinary(max) NULL;

SELECT Id_center,Name_center,promptpay_type,promptpay_number
FROM dbo.db_center
WHERE flag='0'
ORDER BY Id_center;
