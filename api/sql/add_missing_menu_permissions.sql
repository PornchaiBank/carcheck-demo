SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @menus TABLE(id varchar(5),name nvarchar(200),url varchar(100),sort_order int);
    INSERT @menus VALUES
        ('018',N'รายการรับงาน','#jobs',18),
        ('019',N'คู่มือ / สำเนารถ','#vehiclecopy',19),
        ('020',N'บันทึกการมาทำงาน','#attendance',20);

    -- Stop if a reserved ID already belongs to a different menu.
    IF EXISTS(SELECT 1 FROM db_program p JOIN @menus m ON p.ID=m.id
              WHERE CONVERT(nvarchar(200),p.program_name)<>m.name
                AND COALESCE(CONVERT(varchar(100),p.program_url),'')<>m.url)
        THROW 50001,N'รหัสเมนู 018-020 มีการใช้งานแล้ว กรุณาตรวจสอบก่อนเพิ่มเมนู',1;

    INSERT db_program(ID,program_name,program_url,important)
    SELECT m.id,m.name,m.url,m.sort_order FROM @menus m
    WHERE NOT EXISTS(SELECT 1 FROM db_program p WITH(UPDLOCK,HOLDLOCK) WHERE p.ID=m.id);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT ID,program_name,program_url FROM db_program WHERE ID IN ('018','019','020') ORDER BY ID;
-- Assign these new menu permissions in the สิทธิ์การใช้งาน screen.
