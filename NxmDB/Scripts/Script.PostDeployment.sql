/*
Vorlage für ein Skript nach der Bereitstellung							
--------------------------------------------------------------------------------------
 Diese Datei enthält SQL-Anweisungen, die an das Buildskript angefügt werden.		
 Schließen Sie mit der SQLCMD-Syntax eine Datei in das Skript nach der Bereitstellung ein.			
 Beispiel:   :r .\myfile.sql								
 Verwenden Sie die SQLCMD-Syntax, um auf eine Variable im Skript nach der Bereitstellung zu verweisen.		
 Beispiel:   :setvar TableName MyTable							
               SELECT * FROM [$(TableName)]					
--------------------------------------------------------------------------------------
*/


if not exists (select * from dbo.Jobs)
begin

    insert into dbo.Jobs([Name], BasePath, MaxElements, [BlockSize], RotationTypeId, [Day], [Hour], [Minute], Interval)
    values ('Testjob1', 'C:\Users\Administrator\Desktop', 2, 3, 2, 'Montag', 0, 5, 'weekly')  
end

begin
    insert into dbo.EventStatus(text)
    values ('warning'),  
           ('error'),
           ('inProgress'),
           ('successful'),
           ('info')
end

begin
    insert into dbo.RotationType(name)
    values ('blockrotation'),  
           ('merge')
end