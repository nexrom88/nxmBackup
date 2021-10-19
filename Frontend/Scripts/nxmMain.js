//on main window load
var configuredJobs; //list of all active jobs
var selectedJob; //the id of the currently selected job
var selectedVM; //the selected vm id within main panel
var eventRefreshTimer; //timer for refreshing vm events
var maxNodeID; //counter for treeview nodes
var selectedDirectory; //the currently selected directory within folder browser
var newJobObj; //new job object
var dbState; //current db state (values: init, error, success)

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
    showNewJobPage(1, selectedEditJob);

    //register close button handler
    $(".overlayCloseButton").click(function () {
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
                $("#txtEncryptionPassword").css("display", "inline-block");
            } else {
                $("#txtEncryptionPassword").css("display", "none");
            }
            });

            //show current settings when editing a job
            if (selectedJob) {
                showCurrentSettings(pageNumber, selectedEditJob);
            }
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
                $(".vm").click(function (event) {
                $(this).toggleClass("active");

                //enable next-button
                if ($(".vm.active").length > 0) {
                    $("#newJobNextButton").removeAttr("disabled");
                } else {
                    $("#newJobNextButton").attr("disabled", "disabled");
                }

                });

                //set next-button to disabled
                $("#newJobNextButton").attr("disabled", "disabled");

                //show current settings when editing a job
                if (selectedJob) {
                    showCurrentSettings(pageNumber, selectedEditJob);

                    //activate next button when vm is selected
                    if ($(".vm.active")) {
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
          if (selectedJob) {
            showCurrentSettings(pageNumber, selectedEditJob);

            //activate next button when vm is selected
            if ($(".vm.active")) {
              $("#newJobNextButton").attr("disabled", false);
            }
          }

          break;
        case 4:
          $("#newJobPage").html(data);
              registerNextPageClickHandler(pageNumber, selectedEditJob);

          //disable options for non-incremental jobs
          if (!newJobObj["incremental"]) {
            $("#incrementalOptions").css("display","none");
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

          break;

        case 5:
          $("#newJobPage").html(data);
              registerNextPageClickHandler(pageNumber, selectedEditJob);
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

          break;
      }

        

    });
    
}

//shows the current settings on a given page when editing a job
function showCurrentSettings(pageNumber, selectedEditJob) {
    switch (pageNumber) {
        case 1:
            $("#txtJobName").val(selectedEditJob["Name"]);
            $("#cbIncremental").prop("checked", selectedEditJob["Incremental"]);
            $("#cbLiveBackup").prop("checked", selectedEditJob["LiveBackup"]);
            $("#cbEncryption").prop("checked", selectedEditJob["UseEncryption"]);
            $("#cbEncryption").prop("disabled", true); //encrpytion setting not changeable
            break;

      case 2:
        for (var i = 0; i < selectedEditJob["JobVMs"].length; i++) {
          $(".vm").each(function () {
            if ($(this).data("vmid") == selectedEditJob["JobVMs"][i]["vmID"]) {
              $(this).addClass("active");
            }
          });
        }
        break;
      case 3:
        //set interval base
        switch (selectedEditJob["Interval"]["IntervalBase"]) {
          case 0: //hourly
            $('option[data-interval="hourly"]').prop("selected", true);
        }

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
        if ($("#cbEncryption").prop("checked") && !$("#txtEncryptionPassword").val()) {
          $("#txtEncryptionPassword").css("background-color", "#ff4d4d");
          return;
        } else {
          $("#txtEncryptionPassword").css("background-color", "initial");
        }

        //use live backup?
        newJobObj["livebackup"] = $("#cbLiveBackup").prop("checked");

        //use encryption?
        newJobObj["useencryption"] = $("#cbEncryption").prop("checked");
        newJobObj["encpassword"] = $("#txtEncryptionPassword").val();

        //use incremental backups?
        newJobObj["incremental"] = $("#cbIncremental").prop("checked");

        break;
      case 2:
        var selectedVMs = $(".vm.active");
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

        break;

      case 5:
        //get selected node
        newJobObj["target"] = selectedDirectory;

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
      Swal.fire(
        'Job erstellt',
        'Der neue Backupjob wurde erfolgreich erstellt',
        'success'
      );
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

            //set edit job button click handler
            $("#editJobButton").click(editJobHandler);

          //set restore button click handler
          $("#restoreButton").click(startRestoreHandler); //startRestoreHandler function is defined within nxmRestore.js

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

  showCurrentEvents();

  //start refresh timer
  eventRefreshTimer = setInterval(showCurrentEvents, 4000);
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