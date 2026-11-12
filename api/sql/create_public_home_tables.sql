SET NOCOUNT ON;
SET XACT_ABORT ON;

/*
  SmileHost imported the legacy tables under schema carchecktor_car, while
  the API uses dbo. A synonym keeps both names on the same rows and images.
*/

IF OBJECT_ID(N'dbo.db_declare_text') IS NULL
BEGIN
    IF OBJECT_ID(N'carchecktor_car.db_declare_text',N'U') IS NOT NULL
        EXEC(N'CREATE SYNONYM dbo.db_declare_text FOR carchecktor_car.db_declare_text;');
    ELSE
        CREATE TABLE dbo.db_declare_text
        (
            id int NOT NULL,
            data varchar(max) NULL,
            CONSTRAINT PK_db_declare_text PRIMARY KEY CLUSTERED(id)
        );
END;

IF OBJECT_ID(N'dbo.db_visitcustomer') IS NULL
BEGIN
    IF OBJECT_ID(N'carchecktor_car.db_visitcustomer',N'U') IS NOT NULL
        EXEC(N'CREATE SYNONYM dbo.db_visitcustomer FOR carchecktor_car.db_visitcustomer;');
    ELSE
        CREATE TABLE dbo.db_visitcustomer
        (
            id int NOT NULL,
            idurl varchar(100) NULL,
            name varchar(100) NULL,
            type varchar(100) NULL,
            path varchar(200) NULL,
            url varchar(max) NULL,
            CONSTRAINT PK_db_visitcustomer PRIMARY KEY CLUSTERED(id)
        );
END;

IF OBJECT_ID(N'dbo.db_visitcustomer_head') IS NULL
BEGIN
    IF OBJECT_ID(N'carchecktor_car.db_visitcustomer_head',N'U') IS NOT NULL
        EXEC(N'CREATE SYNONYM dbo.db_visitcustomer_head FOR carchecktor_car.db_visitcustomer_head;');
    ELSE
        CREATE TABLE dbo.db_visitcustomer_head
        (
            id varchar(20) NOT NULL,
            image_head varbinary(max) NULL,
            CONSTRAINT PK_db_visitcustomer_head PRIMARY KEY CLUSTERED(id)
        );
END;

IF OBJECT_ID(N'dbo.db_visitwebsite') IS NULL
BEGIN
    IF OBJECT_ID(N'carchecktor_car.db_visitwebsite',N'U') IS NOT NULL
        EXEC(N'CREATE SYNONYM dbo.db_visitwebsite FOR carchecktor_car.db_visitwebsite;');
    ELSE
        CREATE TABLE dbo.db_visitwebsite
        (
            visitwebsite_id varchar(50) NOT NULL,
            visitwebsite_image varbinary(max) NULL,
            visitwebsite_type varchar(50) NULL,
            CONSTRAINT PK_db_visitwebsite PRIMARY KEY CLUSTERED(visitwebsite_id)
        );
END;

SELECT
    SCHEMA_NAME(object.schema_id) AS schema_name,
    object.name,
    object.type_desc
FROM sys.objects AS object
WHERE object.object_id IN
(
    OBJECT_ID(N'dbo.db_declare_text'),
    OBJECT_ID(N'dbo.db_visitcustomer'),
    OBJECT_ID(N'dbo.db_visitcustomer_head'),
    OBJECT_ID(N'dbo.db_visitwebsite')
)
ORDER BY object.name;
