CREATE PROCEDURE [dbo].[spJobs_Update]  
    @Id INT,
    @Name NCHAR(30),
	@BasePath NCHAR(255), 
    @MaxElements INT, 
    @BlockSize INT, 
    @RotationTypeId INT, 
    @Day NCHAR(10), 
    @Hour INT, 
    @Minute INT, 
    @Interval NCHAR(10)
AS
begin

    set nocount on;

    update dbo.[Jobs]
    set [Name] = @Name, BasePath = @BasePath, MaxElements = @MaxElements, [BlockSize] = @BlockSize, RotationTypeId = @RotationTypeId, [Day] = @Day, [Hour] = @Hour, [Minute] = @Minute, Interval = @Interval
    where Id = @Id;

end
