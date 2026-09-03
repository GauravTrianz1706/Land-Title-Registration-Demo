-- =====================================================
-- Script: 001_Create_Nominees_Table.sql
-- Description: Creates the Nominees table to store nominee information
--              linked to TitleRegistrations via TitleRef foreign key.
--              Supports up to 2 nominees per land title registration.
-- =====================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Nominees' AND type = 'U')
BEGIN
    CREATE TABLE Nominees
    (
        NomineeId       INT IDENTITY(1,1) PRIMARY KEY,
        TitleRef        NVARCHAR(50) NOT NULL,
        NomineeName     NVARCHAR(200) NOT NULL,
        Relationship    NVARCHAR(100) NOT NULL,
        CreatedDate     DATETIMEOFFSET NOT NULL,

        CONSTRAINT FK_Nominees_TitleRegistrations
            FOREIGN KEY (TitleRef)
            REFERENCES TitleRegistrations(TitleRef)
            ON DELETE CASCADE
    );

    -- Create index on TitleRef for efficient lookup and counting
    CREATE NONCLUSTERED INDEX IX_Nominees_TitleRef
        ON Nominees(TitleRef);

    PRINT 'Nominees table created successfully.';
END
ELSE
BEGIN
    PRINT 'Nominees table already exists.';
END
GO
