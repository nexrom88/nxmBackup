//on main window load
var configuredJobs; //list of all active jobs

$(window).on('load', function () {


//load configured jobs
  $.ajax({
    url: "api/ConfiguredJobs"
  })
    .done(function (data) {
      configuredJobs = jQuery.parseJSON(data);
      buildJobsList();
    });
});


//builds the jobs sidebar
function buildJobsList() {
  //load html template first
  var jobTemplate;
  $.ajax({
    url: "Templates/navJobItem"
  })
    .done(function (data) {
      jobTemplate = data;

      //iterate through all jobs
      for (var i = 0; i < configuredJobs.length; i++) {

        //deep-copy job template
        var currentTemplate = jobTemplate.slice();

        //add job to sidebar
        $("#jobsList").append(Mustache.render(currentTemplate, { JobName: configuredJobs[i].Name, JobDbId: configuredJobs[i].DbId }));

      }

      //register job click handler
      $(".jobLink").click(function () {
        var JobDbId = $(this).data("jobdbid");

        //look for job
        for (var i = 0; i < configuredJobs.length; i++) {
          if (configuredJobs[i].DbId == JobDbId) {
            buildJobDetailsPanel(configuredJobs[i]);
          }
        }

      });

    });
}

//builds the vm list
function buildJobDetailsPanel(currentJob) {
 
      //load vm details table
      $.ajax({
        url: "Templates/jobDetailsTable"
      })
        .done(function (tableData) {
          $("#mainPanelHeader").html("Jobdetails (" + currentJob.Name + ")");
          var details = Mustache.render(tableData, { nextRun: currentJob.NextRun, lastRun: currentJob.LastRun, lastState: currentJob.Successful });
          $("#mainPanel").html(details);
        });

      
}