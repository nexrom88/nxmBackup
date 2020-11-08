CREATE PROCEDURE [dbo].[spJobs_GetById]
	@Id int
AS
begin

	set nocount on;

	select [Id], [Name], [BasePath], [MaxElements], [BlockSize], [RotationTypeId], [Day], [Hour], [Minute], [Interval], [IsRunning], [Deleted]
	from dbo.Jobs
	where Id=@Id;

end
