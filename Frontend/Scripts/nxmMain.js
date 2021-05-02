//on main window load
var configuredJobs; //list of all active jobs
var selectedJob; //the id of the currently selected job
var selectedVM; //the selected vm id within main panel
var eventRefreshTimer; //timer for refreshing vm events

//global handler for http status 401 (login required)
$.ajaxSetup({
  statusCode: {
    401: function (jqxhr, textStatus, errorThrown) {
      //stop possible running eventRefreshTimer
      clearInterval(eventRefreshTimer);

      document.body.innerHTML = "";

      //show login form
      showLoginForm();
    }
  }
});

$(window).on('load', function () {


//load configured jobs
  $.ajax({
    url: "api/ConfiguredJobs"
  })
    .done(function (data) {
      configuredJobs = jQuery.parseJSON(data);
      buildJobsList();
    });

  //register logout handler
  $("#logout").click(function () {
    logOut();
  });

  //register "add Job" Button handler
  $("#addJobButton").click(function () {
    $("#newJobOverlay").css("display", "block");
  });
});


//performs logout
function logOut() {
  $.ajax({
    url: "api/Logout"
  })
    .done(function (data) {
      $.removeCookie("session_id");
      location.reload();
    });
}

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

          //set start job button click handler
          $("#startJobButton").click(startJobHandler);

          //set delete job button click handler
          $("#deleteJobButton").click(deleteJobHandler);

          //set state color
          if (currentJob.Successful == "erfolgreich") {
            $("#jobDetailsRow").css("background-color", "#ccffcc");
          } else {
            $("#jobDetailsRow").css("background-color", "#ffb3b3");
          }

          if (currentJob.IsRunning) {
            $("#jobDetailsRow").css("background-color", "#ffffb3");
          }

          //select first vm
          $(".vm").first().click();

        });
              
}


//click handler for deleting job
function deleteJobHandler(event) {
  //api call
  Swal.fire({
    title: 'Job löschen?',
    text: "Soll der aktuelle Job wirklich gelöscht werden?",
    icon: 'question',
    showCancelButton: true,
    confirmButtonColor: '#3085d6',
    cancelButtonColor: '#d33',
    confirmButtonText: 'Löschen',
    cancelButtonText: 'Abbrechen'
  }).then((result) => {
    if (result.isConfirmed) {
      $.ajax({
        url: 'api/JobDelete',
        contentType: "application/json; charset=utf-8",
        data: String(selectedJob),
        type: 'POST',
        cache: false,
        success: function (result) {
          Swal.fire(
            'Job gelöscht',
            'Der ausgewählte Job wurde glöscht',
            'success'
          );
          location.reload();
        }
      });

      
    }
  });
}

//click handler for starting job manually
function startJobHandler(event) {
  //api call
  $.ajax({
    url: "api/JobStart/" + selectedJob
  })
    .done(function (data) {
      
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
  eventRefreshTimer = setInterval(showCurrentEvents, 4000);
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

//show login form
function showLoginForm(showError) {
  Swal.fire({
    title: 'Login',
    html: `<input type="text" id="loginText" class="swal2-input" placeholder="Username">
  <input type="password" id="passwordText" class="swal2-input" placeholder="Password">`,
    showLoaderOnConfirm: true,
    confirmButtonText: "Anmelden",
    allowEnterKey: true,
    preConfirm: () => {
      const login = Swal.getPopup().querySelector('#loginText').value;
      const password = Swal.getPopup().querySelector('#passwordText').value;

      if (!login || !password) {
        Swal.showValidationMessage(`Anmeldung ist fehlgeschlagen`);
      }
      var encodedLogin = String(btoa(login + ":" + password));


      return encodedLogin;
    }
  }).then((result) => {
    Swal.fire({
      title: 'Anmeldung',
      text: 'Anmeldung wird ausgeführt...',
      allowOutsideClick: false,
      allowEscapeKey: false,
      allowEnterKey: false,
      onOpen: () => {
        swal.showLoading()
      }
    })
    ajaxLogin(result.value);
  });

  $("#loginText").focus();

  //register enter handler
  $(".swal2-popup").on("keypress", function (event) {
    if (event.which == 13) {
      $(".swal2-confirm").click();
    }
  });

  if (showError) {
    Swal.showValidationMessage(`Anmeldung ist fehlgeschlagen`);
  }
  
}

//async function for loagin ajax call
function ajaxLogin(encodedLogin) {
  try {
    $.ajax({
      url: 'api/Login',
      contentType: "application/json",
      data: "'" + encodedLogin + "'",
      type: 'POST',
      cache: false,
      success: function (result) {
        location.reload();
      },
      error: function (jqXHR, exception) {
        showLoginForm(true);
      }
    });
  } catch (error) {
    console.error(error);
  }
}