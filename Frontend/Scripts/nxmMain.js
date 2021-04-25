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
        $("#jobsList").append(Mustache.render(currentTemplate, { JobName: configuredJobs[i].Name }));
      }

    });



}