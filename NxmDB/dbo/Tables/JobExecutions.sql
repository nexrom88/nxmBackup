CREATE TABLE [dbo].[JobExecutions]
(
	[Id] INT NOT NULL PRIMARY KEY IDENTITY, 
    [JobId] INT NOT NULL, 
    [StartStamp] DATETIME2 NOT NULL, 
    [StopTime] DATETIME2 NULL, 
    [IsRunning] BIT NOT NULL, 
    [TransferRate] INT NOT NULL, 
    [AlreadyRead] INT NOT NULL, 
    [AlreadyWritten] INT NOT NULL, 
    [Successful] BIT NOT NULL, 
    [Warnings] SMALLINT NOT NULL, 
    [Errors] SMALLINT NOT NULL, 
    [Type ] NCHAR(10) NOT NULL
)
