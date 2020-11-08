CREATE PROCEDURE [dbo].[spJobs_Insert]
	@Name NCHAR(30),
	@BasePath NCHAR(255), 
    @MaxElements INT, 
    @BlockSize INT, 
    @RotationTypeId INT, 
    @Day NCHAR(10), 
    @Hour INT, 
    @Minute INT, 
    @Interval NCHAR(10), 
	@Id int output
AS
begin

    set nocount on;

    insert into dbo.[Jobs]([Name], BasePath, MaxElements, [BlockSize], RotationTypeId, [Day], [Hour], [Minute], Interval)
    values (@Name, @BasePath, @MaxElements, @BlockSize, @RotationTypeId, @Day, @Hour, @Minute, @Interval);

    set @Id = SCOPE_IDENTITY();

end
