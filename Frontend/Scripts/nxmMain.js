//on main window load
var configuredJobs; //list of all active jobs
var selectedJob; //the id of the currently selected job
var selectedJobObj; //the obj with the currently selected job
var selectedVM; //the selected vm id within main panel
var eventRefreshTimer; //timer for refreshing vm events
var maxNodeID; //counter for treeview nodes
var selectedDirectory; //the currently selected directory within folder browser
var newJobObj; //new job object
var dbState; //current db state (values: init, error, success)
var jobStateTableTemplate; //template for job state table
var eventsListItemTemplate; //template for event list item
var lastJobStateData; //last data for job state to decide whether to refresh jobStateTable or not

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
  dbState = "init";
  //check DB availability
  $.ajax({
    url: "api/DBConnectTest",
    error: function (jqXHR, exception) {
      $("#welcomeText").html("Es besteht ein Problem mit der Datenbank!");
      $("#welcomeText").addClass("welcomeTextError");
      dbState = "error";
    },
    success: function (jqXHR, exception) {
      dbState = "success";
    }
  });

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

  //register settings handler
  $("#settings").click(function () {
    showSettings();
  });

  //register "add Job" Button handler
  $("#addJobButton").click(function () {

    if (dbState == "success") {
      startNewJobProcess(null);
    }

  });
});


//starts a job editing/creating process
function startNewJobProcess(selectedEditJob) {
  $("#newJobOverlay").css("display", "block");

  newJobObj = {};

  //set updated job ID if necessary
  if (selectedEditJob) {
    newJobObj["updatedJob"] = selectedEditJob["DbId"];
  }

  showNewJobPage(1, selectedEditJob);

  //register close button handler
  $(".overlayClose").click(function () {
    $("#newJobOverlay").css("display", "none");
  });

  //register esc key press handler
  $(document).on('keydown', function (event) {
    if (event.key == "Escape") {
      $("#newJobOverlay").css("display", "none");
    }
  });
}

//shows a given page number when adding a new job
function showNewJobPage(pageNumber, selectedEditJob) {
  //load page
  $.ajax({
    url: "Templates/newJobPage" + pageNumber
  })
    .done(function (data) {
      switch (pageNumber) {
        case 1:
          $("#newJobPage").html(data);
          registerNextPageClickHandler(pageNumber, selectedEditJob);
          //click handler for encryption checkBox
          $("#cbEncryption").click(function () {
            if ($("#cbEncryption").prop("checked")) {
              $("#txtEncryptionPassword").css("display", "block");
            } else {
              $("#txtEncryptionPassword").css("display", "none");
            }
          });

          //show current settings when editing a job
          if (selectedEditJob) {
            showCurrentSettings(pageNumber, selectedEditJob);
          }

          //set focus to txtJobName
          $("#txtJobName").focus();

          break;
        case 2:
          //load vms
          $.ajax({
            url: "api/vms"
          })
            .done(function (vmdata) {
              var parsedJSON = jQuery.parseJSON(vmdata)
              var renderedData = Mustache.render(data, { vms: parsedJSON });
              $("#newJobPage").html(renderedData);
              registerNextPageClickHandler(pageNumber, selectedEditJob);

              //vm click handler
              $(".availablevm").click(function (event) {
                $(this).toggleClass("active");

                //enable next-button
                if ($(".availablevm.active").length > 0) {
                  $("#newJobNextButton").removeAttr("disabled");
                } else {
                  $("#newJobNextButton").attr("disabled", "disabled");
                }

              });

              //set next-button to disabled
              $("#newJobNextButton").attr("disabled", "disabled");

              //show current settings when editing a job
              if (selectedEditJob) {
                showCurrentSettings(pageNumber, selectedEditJob);

                //activate next button when vm is selected
                if ($(".availablevm.active")) {
                  $("#newJobNextButton").attr("disabled", false);
                }
              }
            });

          break;
        case 3:
          $("#newJobPage").html(data);
          registerNextPageClickHandler(pageNumber, selectedEditJob);

          //enable input number spinner
          $("input[type='number']").inputSpinner();

          //set interval select change event handler
          $("#sbJobInterval").on("change", function (event) {
            var interval = $(this).children("option:selected").data("interval");

            //disable/enable controls
            switch (interval) {
              case "hourly":
                $("#spJobIntervalHour").prop("disabled", true);
                $("#sbJobDay").prop("disabled", true);
                break;
              case "daily":
                $("#spJobIntervalHour").removeAttr("disabled");
                $("#sbJobDay").prop("disabled", true);
                break;
              case "weekly":
                $("#spJobIntervalHour").removeAttr("disabled");
                $("#sbJobDay").removeAttr("disabled");
                break;
            }
          });

          //show current settings when editing a job
          if (selectedEditJob) {
            showCurrentSettings(pageNumber, selectedEditJob);
          }

          break;
        case 4:
          $("#newJobPage").html(data);
          registerNextPageClickHandler(pageNumber, selectedEditJob);

          //disable options for non-incremental jobs
          if (!newJobObj["incremental"]) {
            $("#incrementalOptions").css("display", "none");
          }

          //enable input number spinner
          $("input[type='number']").inputSpinner();

          //set "rotation type" select event handler
          $("#sbRotationType").on("change", function (event) {
            var rotationType = $(this).children("option:selected").data("rotationtype");

            switch (rotationType) {
              case "merge":
                $("#lblMaxElements").html("Anzahl aufzubewahrender Backups");
                break;
              case "blockrotation":
                $("#lblMaxElements").html("Anzahl aufzubewahrender Blöcke");
                break;
            }

          });

          //show current settings when editing a job
          if (selectedEditJob) {
            showCurrentSettings(pageNumber, selectedEditJob);
          }
          break;

        case 5:
          $("#newJobPage").html(data);
          registerNextPageClickHandler(pageNumber, selectedEditJob);
          buildFolderBrowser();

          //register change on target type selection
          $("#sbTargetType").on("change", function (event) {
            var targetType = $(this).children("option:selected").data("targettype");

            switch (targetType) {
              case "local":
                buildFolderBrowser();
                break;
              case "smb":
                $('#folderBrowser').jstree("destroy");
                buildSMBForm();
                break;
            }
          });

          //show current settings when editing a job
          if (selectedEditJob) {
            showCurrentSettings(pageNumber, selectedEditJob);
          }

          break;
      }



    });

}

//builds the form fpr smb credentials
function buildSMBForm() {
  $.ajax({
    url: "Templates/smbCredentials"
  })
    .done(function (data) {
      $("#folderBrowser").html(data);
    });
}

//builds the folder browser elements on creating a new job
function buildFolderBrowser() {
  $('#folderBrowser').jstree({
    'core': {
      'check_callback': true,
      'data': null
    },
    types: {
      "drive": {
        "icon": "fa fa-hdd-o"
      },
      "folder": {
        "icon": "fa fa-folder-open-o"
      },
      "default": {
      }
    }, plugins: ["types"]
  });

  //init treeview
  maxNodeID = 0;
  navigateToDirectory("/", "drive", "#");
  selectedDirectory = "";

  //node select handler
  $("#folderBrowser").on("select_node.jstree", function (e, data) {
    var selectedPath = data.instance.get_path(data.node, '\\');
    selectedDirectory = selectedPath;
    navigateToDirectory(selectedPath, "folder", data.node.id);
  });
}

//shows the current settings on a given page when editing a job
function showCurrentSettings(pageNumber, selectedEditJob) {
  switch (pageNumber) {
    case 1:
      $("#txtJobName").val(selectedEditJob["Name"]);
      $("#cbIncremental").prop("checked", selectedEditJob["Incremental"]);
      $("#cbLiveBackup").prop("checked", selectedEditJob["LiveBackup"]);
      $("#cbDedupe").prop("checked", selectedEditJob["UsingDedupe"]);
      $("#cbEncryption").prop("checked", selectedEditJob["UseEncryption"]);
      $("#cbEncryption").prop("disabled", true); //encrpytion setting not changeable
      break;

    case 2:
      for (var i = 0; i < selectedEditJob["JobVMs"].length; i++) {
        $(".availablevmvm").each(function () {
          if ($(this).data("vmid") == selectedEditJob["JobVMs"][i]["vmID"]) {
            $(this).addClass("active");
          }
        });
      }
      break;
    case 3:
      //set interval base
      switch (selectedEditJob["Interval"]["intervalBase"]) {
        case 0: //hourly
          $('option[data-interval="hourly"]').prop("selected", true);
          $("#sbJobInterval").change(); //trigger change-event manually
          $("#spJobIntervalMinute").val(selectedEditJob["Interval"]["minute"]);
          break;
        case 1: //daily
          $('option[data-interval="daily"]').prop("selected", true);
          $("#sbJobInterval").change(); //trigger change-event manually
          $("#spJobIntervalMinute").val(selectedEditJob["Interval"]["minute"]);
          $("#spJobIntervalHour").val(selectedEditJob["Interval"]["hour"]);
          break;
        case 2: //weekly
          $('option[data-interval="weekly"]').prop("selected", true);
          $("#sbJobInterval").change(); //trigger change-event manually
          $("#spJobIntervalMinute").val(selectedEditJob["Interval"]["minute"]);
          $("#spJobIntervalHour").val(selectedEditJob["Interval"]["hour"]);
          $('option[data-day="' + selectedEditJob["Interval"]["day"] + '"]').prop("selected", true);
          break;
      }
      break;

    case 4:
      $("#spBlockSize").val(selectedEditJob["BlockSize"]);

      //set rotation type
      if (selectedEditJob["Rotation"]["type"] == 0) { //merge
        $('option[data-rotationtype="merge"]').prop("selected", true);
        $("#lblMaxElements").html("Anzahl aufzubewahrender Backups");
      } else if (selectedEditJob["Rotation"]["type"] == 1) { //blockrotation
        $('option[data-rotationtype="blockrotation"]').prop("selected", true);
        $("#lblMaxElements").html("Anzahl aufzubewahrender Blöcke");
      }

      $("#spMaxElements").val(selectedEditJob["Rotation"]["maxElementCount"]);
      break;

    case 5:

      break;

  }
}

//folder browser: navigate to directory
function navigateToDirectory(directory, nodeType, currentNodeID) {

  //when data already loaded, do not load again
  if (currentNodeID != "#") {
    if ($("#" + currentNodeID).data("loaded")) {
      return;
    }
  }

  $.ajax({
    url: 'api/Directory',
    contentType: "application/json; charset=utf-8",
    data: JSON.stringify({ path: directory }),
    type: 'POST',
    cache: false,
    success: function (result) {
      var directories = JSON.parse(result);
      for (var i = 0; i < directories.length; i++) {
        $('#folderBrowser').jstree().create_node(currentNodeID, { id: "dirnode" + (i + maxNodeID), text: directories[i], type: nodeType }, "last", false, false);
      }
      maxNodeID += directories.length;



      //set loaded attrib to current node
      if (currentNodeID != "#") {
        $("#" + currentNodeID).data("loaded", true);
      }

      //open current node
      if (currentNodeID != "#") {
        $("#folderBrowser").jstree("open_node", $("#" + currentNodeID));
      }

    }
  });
}

//click handler for nextPageButton
function registerNextPageClickHandler(currentPage, selectedEditJob) {
  $("#newJobNextButton").click(function () {

    //parse inputs
    switch (currentPage) {
      case 1:
        var jobName = $("#txtJobName").val();

        //no valid jobname given
        if (!jobName) {
          $("#txtJobName").css("background-color", "#ff4d4d");
          return;
        } else { //valid jobname given
          $("#txtJobName").css("background-color", "initial");
          newJobObj["name"] = jobName;
        }

        //using encryption but no password given?
        if (!selectedEditJob && $("#cbEncryption").prop("checked") && !$("#txtEncryptionPassword").val()) {
          $("#txtEncryptionPassword").css("background-color", "#ff4d4d");
          return;
        } else {
          $("#txtEncryptionPassword").css("background-color", "initial");
        }

        //use dedupe?
        newJobObj["usingdedupe"] = $("#cbDedupe").prop("checked");

        //use live backup?
        newJobObj["livebackup"] = $("#cbLiveBackup").prop("checked");

        //use encryption?
        newJobObj["useencryption"] = $("#cbEncryption").prop("checked");
        newJobObj["encpassword"] = $("#txtEncryptionPassword").val();

        //use incremental backups?
        newJobObj["incremental"] = $("#cbIncremental").prop("checked");

        break;
      case 2:
        var selectedVMs = $(".availablevm.active");
        newJobObj["vms"] = [];

        //add vm IDs to newJob object
        for (var i = 0; i < selectedVMs.length; i++) {
          var vm = {};
          vm.id = $(selectedVMs[i]).data("vmid");
          vm.name = $(selectedVMs[i]).data("vmname");
          newJobObj["vms"].push(vm);
        }

        break;

      case 3:
        //get job interval
        newJobObj["interval"] = $("#sbJobInterval option:selected").data("interval");

        //get minute
        newJobObj["minute"] = $("#spJobIntervalMinute").val();

        //get hour
        newJobObj["hour"] = $("#spJobIntervalHour").val();

        //get day
        newJobObj["day"] = $("#sbJobDay option:selected").data("day");

        break;

      case 4:
        //get block-size
        newJobObj["blocksize"] = $("#spBlockSize").val();

        //get rotation-type
        newJobObj["rotationtype"] = $("#sbRotationType option:selected").data("rotationtype");

        //get max-elements
        newJobObj["maxelements"] = $("#spMaxElements").val();

        //valid input?
        if (newJobObj["rotationtype"] == "merge" && newJobObj["blocksize"] > newJobObj["maxelements"]) {
          Swal.fire(
            'Eingabefehler',
            'Ein Vollbackup würde nie durchgeführt werden, da die "Anzahl aufzubewahrender Backups" zu klein ist',
            'error'
          );
          return;
        }

        break;

      case 5:
        var targetType = $("#sbTargetType").children("option:selected").data("targettype");

        //set target type
        newJobObj["targetType"] = targetType;

        if (targetType == "smb") {
          //get and set smb path, username and password
          var username = $("#inputUsername").val();
          var password = $("#inputPassword").val();
          var smbDirectory = $("#smbPath").val();

          newJobObj["targetUsername"] = username;
          newJobObj["targetPassword"] = password;
          newJobObj["targetPath"] = smbDirectory;


        } else if (targetType == "local") {

          //get and set selected node
          newJobObj["targetPath"] = selectedDirectory;
        }


        

        //done creating new job, send to server
        saveNewJob();
        $("#newJobOverlay").css("display", "none");
        return;
        break;
    }


    currentPage += 1;
    showNewJobPage(currentPage, selectedEditJob);
  });
}

//sends the new job data to server
function saveNewJob() {
  $.ajax({
    url: 'api/JobCreate',
    contentType: "application/json; charset=utf-8",
    data: JSON.stringify(newJobObj),
    type: 'POST',
    cache: false,
    success: function (result) {
      location.reload();
    }
  });
}

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

  //clear the content first
  $("#jobsList").html("");

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
        $("#jobsList").append(Mustache.render(currentTemplate, { JobName: configuredJobs[i].Name, JobDbId: configuredJobs[i].DbId, JobEnabled: configuredJobs[i].Enabled}));

      }

      //register job click handler
      $(".jobLink").click(function () {
        var JobDbId = $(this).data("jobdbid");
        selectedJob = JobDbId;

        //look for job
        for (var i = 0; i < configuredJobs.length; i++) {
          if (configuredJobs[i].DbId == JobDbId) {
            lastJobStateData = "";
            selectedJobObj = configuredJobs[i];
            buildJobDetailsPanel();
          }
        }

      });

    });
}

//builds the vm list
function buildJobDetailsPanel() {

  //load vm details table
  $.ajax({
    url: "Templates/jobDetailsPanel"
  })
    .done(function (tableData) {
      var jobHeaderString;
      jobHeaderString = "Jobdetails (" + selectedJobObj.Name + ")";
      if (!selectedJobObj.Enabled) {
        jobHeaderString += " - deaktiviert";
      }

      $("#mainPanelHeader").html(jobHeaderString);
      

      //set details panel and vms list
      var vms = [];
      for (var i = 0; i < selectedJobObj.JobVMs.length; i++) {
        vms[i] = { vmid: selectedJobObj.JobVMs[i].vmID, name: selectedJobObj.JobVMs[i].vmName };
      }

      

      var details = Mustache.render(tableData, { vms: vms });
      $("#mainPanel").html(details);

      //set vm click handler
      $(".vm").click(vmClickHandler);

      //set start job button click handler
      $("#startJobButton").click(startJobHandler);

      //set delete job button click handler
      $("#deleteJobButton").click(deleteJobHandler);

      //set enable job button click handler
      $("#enableJobButton").click(enableJobHandler);

      //set edit job button click handler
      $("#editJobButton").click(editJobHandler);

      //set restore button click handler
      $("#restoreButton").click(startRestoreHandler); //startRestoreHandler function is defined within nxmRestore.js

      //edit enableJobButton caption
      $("#enableJobButtonCaption").html(selectedJobObj["Enabled"] ? "Job deaktivieren" : "Job aktivieren");

      //select first vm
      $(".vm").first().click();

    });

}

//changes a given int to two digits
function buildTwoDigitsInt(input) {
  return input.toLocaleString('en-US', { minimumIntegerDigits: 2, useGrouping: false });
}


//click handler for editing job
function editJobHandler(event) {

  var selectedEditJob = [];
  //look for job
  for (var i = 0; i < configuredJobs.length; i++) {
    if (configuredJobs[i].DbId == selectedJob) {
      selectedEditJob = configuredJobs[i];
    }
  }

  startNewJobProcess(selectedEditJob);
}

//click handler for enabling/disabling job
function enableJobHandler(event) {
  var postObj = {};

  postObj["jobID"] = selectedJob;
  postObj["enabled"] = selectedJobObj["Enabled"] ? "false" : "true";

  $.ajax({
    url: 'api/JobEnable',
    contentType: "application/json; charset=utf-8",
    data: JSON.stringify(postObj),
    type: 'POST',
    cache: false,
    success: function (result) {
      selectedJobObj["Enabled"] = !selectedJobObj["Enabled"];

      //edit enableJobButton caption
      $("#enableJobButtonCaption").html(selectedJobObj["Enabled"] ? "Job deaktivieren" : "Job aktivieren");

      //refresh the details panel header
      var jobHeaderString;
      jobHeaderString = "Jobdetails (" + selectedJobObj.Name + ")";
      if (!selectedJobObj.Enabled) {
        jobHeaderString += " - deaktiviert";
      }
      $("#mainPanelHeader").html(jobHeaderString);

      //refresh job state table
      renderJobStateTable();

      //build jobs list panel
      buildJobsList();
    }
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
            'Der ausgewählte Job wurde gelöscht',
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

  //remove active-class from all vms
  $(".vm").removeClass("active");

  //set current element in GUI
  $(this).addClass("active");

  //load required templates
  loadJobTemplates();

  showCurrentEvents();

  //start refresh timer
  eventRefreshTimer = setInterval(showCurrentEvents, 4000);
}

//loads the required templates
function loadJobTemplates() {
  $.ajax({
    url: "Templates/eventsListItem"
  })
    .done(function (data) {
      eventsListItemTemplate = data;
    });

  $.ajax({
    url: "Templates/jobStateTable"
  })
    .done(function (data) {
      jobStateTableTemplate = data;
    });

}

//refresh handler for clicking vm in main panel
function showCurrentEvents() {

  //api call
  $.ajax({
    url: "api/BackupJobEvent?id=" + selectedJob + "&jobType=backup"
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
      $("#jobEventList").html(Mustache.render(eventsListItemTemplate, { events: eventsList }));


    });

  //step two: refresh backup job state panel
  renderJobStateTable();
}


//render job state table
function renderJobStateTable() {
  
  $.ajax({
    url: "api/BackupJobState?jobId=" + selectedJob
  })
    .done(function (data) {

      //nothing to refresh?
      if (lastJobStateData == data) {
        return;
      } else {
        lastJobStateData = data;
      }

      data = JSON.parse(data);

      //build next run string
      var intervalString;

      if (selectedJobObj["Enabled"]) {
        switch (data["Interval"]["intervalBase"]) {
          case 0: //stündlich
            intervalString = "Stündlich bei Minute " + buildTwoDigitsInt(data["Interval"]["minute"]);
            break;
          case 1: //täglich
            intervalString = "Täglich um " + buildTwoDigitsInt(data["Interval"]["hour"]) + ":" + buildTwoDigitsInt(data["Interval"]["minute"]);
            break;
          case 2: //wöchentlich
            var dayForGui;
            switch (data["Interval"]["day"]) {
              case "monday":
                dayForGui = "Montag";
                break;
              case "tuesday":
                dayForGui = "Dienstag";
                break;
              case "wednesday":
                dayForGui = "Mittwoch";
                break;
              case "thursday":
                dayForGui = "Donnerstag";
                break;
              case "friday":
                dayForGui = "Freitag";
                break;
              case "saturday":
                dayForGui = "Samstag";
                break;
              case "sunday":
                dayForGui = "Sonntag";
                break;
            }

            intervalString = dayForGui + "s " + " um " + buildTwoDigitsInt(data["Interval"]["hour"]) + ":" + buildTwoDigitsInt(data["Interval"]["minute"]);
            break;
        }
      } else {
        //job is disabled
        intervalString = "Job ist deaktiviert";
      }

      var lastRunString = data["LastRun"];
      if (data["IsRunning"]) {
        lastRunString = "Job wird gerade ausgeführt...";
      }

      var successString = data["Successful"];
      if (data["LastRun"] == "") {
        successString = "Nicht zutreffend";
      }

      var currentTransferrateString = prettyPrintBytes(data["CurrentTransferrate"]) + "/s";

      $("#jobStateTable").html(Mustache.render(jobStateTableTemplate, { running: data["IsRunning"], interval: intervalString, lastRun: lastRunString, lastState: successString, lastTransferrate: currentTransferrateString}));


      //register cick handler for clicking link to show last execution details
      $("#lastExecutionDetailsLink").click(showLastExecutionDetails);

      //set state color
      if (data["LastRun"] == "" && !data["IsRunning"]) {
        $("#jobDetailsRow").css("background-color", "#e6e6e6");
        $("#jobDetailsRow").removeClass("detailsRowRunning");
      } else {
        if (data["Successful"] == "erfolgreich") {
          $("#jobDetailsRow").css("background-color", "#ccffcc");
          $("#jobDetailsRow").removeClass("detailsRowRunning");
        } else {
          $("#jobDetailsRow").css("background-color", "#ffb3b3");
          $("#jobDetailsRow").removeClass("detailsRowRunning");
        }

        if (data.IsRunning) {
          $("#jobDetailsRow").addClass("detailsRowRunning");
        }
      }

    });
}

//click handler for showing stats for last execution
function showLastExecutionDetails() {
  //show dialog box
  Swal.fire({
    title: 'Details der letzten Ausführung',
    html: "<div id='executionDetailsPopUp'></div>",
    confirmButtonColor: '#3085d6',
    allowOutsideClick: true,
    allowEscapeKey: true,
    confirmButtonText: 'Schließen',
  });

  //load form
  $.ajax({
    url: "Templates/JobExecutionDetailsForm"
  })
    .done(function (detailsForm) {
      //parse job data
      var jobData = JSON.parse(lastJobStateData);

      var bytesTransfered = prettyPrintBytes(jobData["LastBytesTransfered"]);
      var bytesProcessed = prettyPrintBytes(jobData["LastBytesProcessed"]);

      if (jobData["LastStop"]) {
        //calculate compression efficiency
        var compressionEfficiency = parseFloat(((jobData["LastBytesProcessed"] - jobData["LastBytesTransfered"]) / jobData["LastBytesProcessed"]) * 100).toFixed(2);

        //calculate execution duration
        var startDate = moment(jobData["LastRun"], "DD.MM.YYYY HH:mm:ss");
        var endDate = moment(jobData["LastStop"], "DD.MM.YYYY HH:mm:ss");

        var duration = prettyPrintDuration(endDate.diff(startDate));
        duration = duration.replace(".", ",");

        var renderedDetailsForm = Mustache.render(detailsForm, { bytesTransfered: bytesTransfered, bytesProcessed: bytesProcessed, compressionEfficiency: compressionEfficiency, duration: duration });

        $("#executionDetailsPopUp").html(renderedDetailsForm)

      } else { //job not started yet
        var renderedDetailsForm = Mustache.render(detailsForm, { bytesTransfered: bytesTransfered, bytesProcessed: bytesProcessed, compressionEfficiency: "0", duration: "-" });

        $("#executionDetailsPopUp").html(renderedDetailsForm)
      }



    });
}

//pretty print milliseconds
function prettyPrintDuration(ms) {
    let seconds = (ms / 1000).toFixed(1);
    let minutes = (ms / (1000 * 60)).toFixed(1);
    let hours = (ms / (1000 * 60 * 60)).toFixed(1);
    let days = (ms / (1000 * 60 * 60 * 24)).toFixed(1);
    if (seconds < 60) return seconds + " Sekunden";
    else if (minutes < 60) return minutes + " Minuten";
    else if (hours < 24) return hours + " Stunden";
    else return days + " Tage"
}

//pretty prints file size
function prettyPrintBytes(bytes, si = true, dp = 2) {
  const thresh = si ? 1000 : 1024;

  if (Math.abs(bytes) < thresh) {
    return bytes + ' B';
  }

  const units = si
    ? ['kB', 'MB', 'GB', 'TB', 'PB', 'EB', 'ZB', 'YB']
    : ['KiB', 'MiB', 'GiB', 'TiB', 'PiB', 'EiB', 'ZiB', 'YiB'];
  let u = -1;
  const r = 10 ** dp;

  do {
    bytes /= thresh;
    ++u;
  } while (Math.round(Math.abs(bytes) * r) / r >= thresh && u < units.length - 1);


  return bytes.toFixed(dp) + ' ' + units[u];
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

//async function for login ajax call
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