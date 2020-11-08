CREATE PROCEDURE [dbo].[spJobs_All]
AS
begin
	
	set nocount on;

	select [Id], [Name], [BasePath], [MaxElements], [BlockSize], [RotationTypeId], [Day], [Hour], [Minute], [Interval], [IsRunning], [Deleted]
	from dbo.Jobs;


end
