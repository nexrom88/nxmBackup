//on main window load
var configuredJobs; //list of all active jobs
var slectedJob; //the id of the currently selected job
var selectedVM; //the selected vm id within main panel
var eventRefreshTimer; //timer for refreshing vm events

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
        selectedJob = JobDbId;

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
        url: "Templates/jobDetailsPanel"
      })
        .done(function (tableData) {
          $("#mainPanelHeader").html("Jobdetails (" + currentJob.Name + ")");

          //build interval string
          var interval;
          switch (currentJob.Interval.intervalBase) {
            case 0:
              interval = "stündlich";
              break;
            case 1:
              interval = "täglich";
              break;
            case 2:
              interval = "wöchentlich";
              break;
            default:
              interval = "manuell";
          }

          //set details panel and vms list
          var vms= [];
          for (var i = 0; i < currentJob.JobVMs.length; i++) {
            vms[i] = { vmid: currentJob.JobVMs[i].vmID, name: currentJob.JobVMs[i].vmName };
          }

          var details = Mustache.render(tableData, {vms: vms, running: currentJob.IsRunning, nextRun: currentJob.NextRun, interval: interval, lastRun: currentJob.LastRun, lastState: currentJob.Successful });
          $("#mainPanel").html(details);

          //set vm click handler
          $(".vm").click(vmClickHandler);

          //set state color
          if (currentJob.Successful == "erfolgreich") {
            $("#jobDetailsRow").css("background-color", "#ccffcc");
          } else {
            $("#jobDetailsRow").css("background-color", "#ffb3b3");
          }

          if (currentJob.IsRunning) {
            $("#jobDetailsRow").css("background-color", "#ffffb3");
          }

        });
              
}

//click handler for clicking a vm within main panel
function vmClickHandler(event) {
  //stop refresh timer
  clearInterval(eventRefreshTimer);

  selectedVM = $(this).data("vmid");

  //set current element in GUI
  $(this).addClass("active");

  showCurrentEvents();

  //start refresh timer
  eventRefreshTime = setInterval(showCurrentEvents, 4000);
}

//refresh handler for clicking vm in main panel
function showCurrentEvents() {
  
  //api call
  $.ajax({
    url: "api/BackupJobEvent/" + selectedJob
  })
    .done(function (data) {
      data = jQuery.parseJSON(data);

      //iterate through all events
      var eventsList = [];
      for (var i = 0; i < data.length; i++) {
        //ignore events if wrong vmid
        if (data[i]["vmid"] != selectedVM) {
          continue;
        }

        //build event object
        var oneEvent = {};
        oneEvent.text = data[i].info;

        switch (data[i].status) {
          case "successful":
            oneEvent.successful = true;
            break;
          case "inProgress":
            oneEvent.inProgress = true;
            break;
          case "error":
            oneEvent.error = true;
            break;
          case "warning":
            oneEvent.warning = true;
            break;
          case "info":
            oneEvent.info = true;
            break;
        }

        //add event to eventsList
        eventsList.unshift(oneEvent);

      }

      //display events
      $.ajax({
        url: "Templates/eventsListItem"
      })
        .done(function (data) {
          $("#jobEventList").html(Mustache.render(data, { events: eventsList }));
        });

    });
}