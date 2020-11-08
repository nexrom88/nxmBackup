using DataLibrary.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataLibrary.Data
{
    public interface IJobData
    {
        Task<int> CreateJob(JobModel job);
        Task<int> DeleteJob(int jobId);
        Task<JobModel> GetJobById(int jobId);
        Task<List<JobModel>> GetJobs();
        Task<int> UpdateJob(JobModel job);
    }
}