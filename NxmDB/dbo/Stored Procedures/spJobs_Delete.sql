CREATE PROCEDURE [dbo].[spJobs_Delete]
	@Id int
AS
begin

	set nocount on;

	delete
	from dbo.Jobs
	where Id = @Id;


end
