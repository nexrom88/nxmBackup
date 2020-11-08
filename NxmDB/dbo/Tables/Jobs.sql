CREATE TABLE [dbo].[Jobs]
(
	[Id] INT NOT NULL PRIMARY KEY IDENTITY, 
    [Name] NCHAR(30) NOT NULL, 
    [BasePath] NCHAR(255) NOT NULL, 
    [MaxElements] INT NOT NULL, 
    [BlockSize] INT NOT NULL, 
    [RotationTypeId] INT NOT NULL, 
    [Day] NCHAR(10) NULL, 
    [Hour] INT NULL, 
    [Minute] INT NOT NULL, 
    [Interval] NCHAR(10) NOT NULL , 
    [IsRunning] BIT NOT NULL DEFAULT 0, 
    [Deleted] BIT NOT NULL DEFAULT 0, 
    [LiveBackup] BIT NOT NULL DEFAULT 0

)
