CREATE TABLE [dbo].[JobExecutionEvents]
(
	[Id] INT NOT NULL PRIMARY KEY IDENTITY, 
    [JobExecutionId] INT NOT NULL, 
    [VmId] NCHAR(50) NULL, 
    [TimeStamp] DATETIME2 NOT NULL, 
    [Status] INT NOT NULL, 
    [Info] NCHAR(50) NULL, 
    CONSTRAINT [FK_JobExecutionEvents_JobExecution] FOREIGN KEY ([JobExecutionId]) REFERENCES [JobExecutions]([Id]), 
    CONSTRAINT [FK_JobExecutionEvents_VmId] FOREIGN KEY ([VmId]) REFERENCES [VMs]([Id]), 
    CONSTRAINT [FK_JobExecutionEvents_EventStatus] FOREIGN KEY ([Status]) REFERENCES [EventStatus]([Id])
)
