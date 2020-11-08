CREATE TABLE [dbo].[JobVmRelation]
(
    [JobId] INT NOT NULL, 
    [VmId] NCHAR(50) NULL, 
    CONSTRAINT [FK_JobVmRelation_Jobs] FOREIGN KEY ([JobId]) REFERENCES [Jobs]([Id]), 
    CONSTRAINT [FK_JobVmRelation_VMs] FOREIGN KEY ([VmId]) REFERENCES [VMs]([Id])
)
