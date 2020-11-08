using Dapper;
using DataLibrary.Db;
using DataLibrary.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLibrary.Data
{
    public class JobData : IJobData
    {
        private readonly IDataAccess dataAccess;
        private readonly ConnectionStringData connectionString;

        public JobData(IDataAccess dataAccess, ConnectionStringData connectionString)
        {
            this.dataAccess = dataAccess;
            this.connectionString = connectionString;
        }

        public Task<List<JobModel>> GetJobs()
        {
            return dataAccess.LoadData<JobModel, dynamic>("dbo.spJobs_All",
                                                          new { },
                                                          connectionString.SqlConnectionName);
        }

        public async Task<int> CreateJob(JobModel job)
        {
            DynamicParameters p = new DynamicParameters();

            p.Add("Name", job.Name);
            p.Add("BasePath", job.BasePath);
            p.Add("MaxElements", job.MaxElements);
            p.Add("BlockSize", job.BlockSize);
            p.Add("RotationTypeId", job.RotationTypeId);
            p.Add("Day", job.Day);
            p.Add("Hour", job.Hour);
            p.Add("Minute", job.Minute);
            p.Add("Interval", job.Interval);
            p.Add("Id", DbType.Int32, direction: ParameterDirection.Output);

            await dataAccess.SaveData("dbo.spJobs_Insert", p, connectionString.SqlConnectionName);

            return p.Get<int>("Id");
        }

        public Task<int> DeleteJob(int jobId)
        {
            return dataAccess.SaveData("dbo.spJobs_Delete",
                                       new { Id = jobId },
                                       connectionString.SqlConnectionName);
        }

        public async Task<JobModel> GetJobById(int jobId)
        {
            var recs = await dataAccess.LoadData<JobModel, dynamic>("dbo.spJobs_GetById",
                                                                    new { Id = jobId },
                                                                    connectionString.SqlConnectionName);
            return recs.FirstOrDefault();

        }

        public Task<int> UpdateJob(JobModel job)
        {
            return dataAccess.SaveData("dbo.spJobs_Update",
                                       new
                                       {
                                           Id = job.Id,
                                           Name = job.Name,
                                           BasePath = job.BasePath,
                                           MaxElements = job.MaxElements,
                                           BlockSize = job.BlockSize,
                                           RotationTypeId = job.RotationTypeId,
                                           Day = job.Day,
                                           Hour = job.Hour,
                                           Minute = job.Minute,
                                           Interval = job.Interval
                                       },
                                       connectionString.SqlConnectionName);
        }
    }
}
