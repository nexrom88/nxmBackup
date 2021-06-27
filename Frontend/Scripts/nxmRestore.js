const { json } = require("modernizr");

var selectedRestoreJob = {}; //the job selected for restore
var selectedRestoreHDD = ""; //the selected hdd for restore

//starts the restore process
function startRestoreHandler() {
  var restoreJobID = selectedJob;

  var jobObj = {};
  //look for the job object
  for (var i = 0; i < configuredJobs.length; i++) {
    if (configuredJobs[i].DbId == restoreJobID) {
      jobObj = configuredJobs[i];
      selectedRestoreJob = jobObj;
      break;
    }
  }

  //show overlay
  $("#restoreOverlay").css("display", "block");

  //load content for overlay
  $.ajax({
    url: "Templates/restoreOptions"
  }).done(function (data) {

    //load job vms
    var vms = [];
    for (var i = 0; i < jobObj.JobVMs.length; i++) {
      vms[i] = { vmid: jobObj.JobVMs[i].vmID, name: jobObj.JobVMs[i].vmName };
    }

    var vmsHTML = Mustache.render(data, { vms: vms });

    $("#restoreOptions").html(vmsHTML);

    //on start restore handler
    $("#startRestoreButton").click(function () {
      startRestore();
    });

    //on select event handler
    $(".sbSourceVM").change(function () {
      loadRestorePoints();
    });

    loadRestorePoints();
  });

}

//load restore points
function loadRestorePoints() {

  //build object to query restore points
  var restoreDetails = {};
  restoreDetails["jobName"] = selectedRestoreJob["Name"];
  restoreDetails["basePath"] = selectedRestoreJob["BasePath"];

  //get selected vm
  var selectedVM = $("#sbSourceVM option:selected").data("vmid");
  restoreDetails["vmName"] = selectedVM;

  //send ajax request
  $.ajax({
    url: 'api/BackupChain',
    contentType: "application/json; charset=utf-8",
    data: JSON.stringify(restoreDetails),
    type: 'POST',
    cache: false,
    success: function (result) {
      result = JSON.parse(result);

      //pretty print backup properties
      convertBackupProperties(result);

      //load table template
      $.ajax({
        url: "Templates/restorePointsTable"
      })
        .done(function (data) {
          var renderedData = Mustache.render(data, { restorePoints: result });
          $("#restorePointTable").html(renderedData);


          //set backup select event handler
          $('#restorePointTable tr').on('click', function (event) {
            $(this).addClass('active').siblings().removeClass('active');
          });

        });

    }
  });


}

//converts backup properties to be GUI friendly
function convertBackupProperties(properties) {

  for (var i = 0; i < properties.length; i++) {

      //convert backup type
    switch (properties[i].type) {
      case "full":
        properties[i].type = "Vollsicherung";
        break;
      case "rct":
        properties[i].type = "Inkrement";
        break;
      case "lb":
        properties[i].type = "LiveBackup";
        break;
    }

    //parse timestamp
    var parsedDate = moment(properties[i].timeStamp, "YYYYMMDDhhmmss").format("DD.MM.YYYY hh:mm");
    properties[i].timeStamp = parsedDate;

  }


}

//starts the restore process
function startRestore() {
  //check whether backup is selected
  var instanceID = $('#restorePointTable tr.active').data("instanceid");
  if (!instanceID) {
    Swal.fire({
      title: 'Fehler',
      text: 'Es wurde kein Wiederherstellungspunkt ausgewählt!',
      icon: 'error'
    });
    return;
  }

  //disable start restore button
  $("#startRestoreButton").prop("disabled", true);

  var restartStartDetails = {};
  restartStartDetails["basePath"] = selectedRestoreJob["BasePath"];
  restartStartDetails["vmName"] = $("#sbSourceVM option:selected").text() + "_restored";
  restartStartDetails["vmID"] = $("#sbSourceVM option:selected").data("vmid");
  restartStartDetails["destPath"] = "f:\\\\target";
  restartStartDetails["instanceID"] = instanceID;
  restartStartDetails["type"] = $("#sbRestoreType option:selected").data("type");
  restartStartDetails["jobID"] = selectedRestoreJob["DbId"];
  restartStartDetails["selectedHDD"] = selectedRestoreHDD;

  //reset selected hdd
  selectedRestoreHDD = "";

  //show loading screen
  Swal.fire({
    title: 'Initialisierung',
    html: 'Wiederherstellung wird gestartet...',
    allowEscapeKey: false,
    allowOutsideClick: false,
    didOpen: () => {
      Swal.showLoading()
    }
  });

  //do ajax call
  $.ajax({
    url: "api/Restore",
    contentType: "application/json; charset=utf-8",
    data: JSON.stringify(restartStartDetails),
    type: 'POST',
    cache: false,
  })
    .done(function (data) {
      //close loading screen
      Swal.close();

      //re-enable start restore button
      $("#startRestoreButton").prop("disabled", false);

      switch (restartStartDetails["type"]) {
        case "lr":
          handleRunningLiveRestore();
          break;
        case "flr":

          //build volumes array
          var volumes = [];

          try {
            volumes = JSON.parse(data);
          } catch (e) {
            volumes = [];
          }
          handleRunningFLR(volumes);
          break;
      }
    })
    .fail(function (data) {
      //close loading screen
      swal.close();

      //re-enable start restore button
      $("#startRestoreButton").prop("disabled", false);

      //http error code 400: job already running
      if (data["status"] == 400) {
        Swal.fire({
          icon: 'error',
          title: 'Fehler',
          text: 'Die Wiederherstellung kann nicht gestartet werden, da ein anderer Job bereits läuft',
        })
      };

      //http error code 500: backup could not be mounted
      if (data["status"] == 500) {
        Swal.fire({
          icon: 'error',
          title: 'Fehler',
          text: 'Die Wiederherstellung kann aufgrund eines Serverfehlers nicht gestartet werden',
        })
      };

      //http error code 409: hdd select on flr
      if (data["status"] == 409) {
        var hddOptions = JSON.parse(data["responseText"]);
        var hddOptionsMinified = [];

        //minify hdd options
        for (var i = 0; i < hddOptions.length; i++) {
          var oneHDDElements = hddOptions[i].split("\\");
          hddOptionsMinified[i] = oneHDDElements[oneHDDElements.length - 1];
        }

        Swal.fire({
          input: 'select',
          inputOptions: hddOptionsMinified,
          title: 'Festplatte auswählen',
          text: 'Bitte wählen Sie hier eine virtuelle Festplatte aus, auf die die Wiederherstellung gestartet wird',
        }).then(function (value) {
          selectedRestoreHDD = hddOptions[value];

          //restart restore
          startRestore();
        });
      };

    });
  
}

//handles a currently running flr
function handleRunningFLR(volumes) {
  var restoreWebWorker = new Worker("Scripts/restoreHeartbeatWebWorker.js");

  //show dialog box
  Swal.fire({
    title: 'Einzeldatei-Wiederherstellung',
    html: "<div id='flrBrowserContainer'></div>",
    confirmButtonColor: '#3085d6',
    allowOutsideClick: false,
    allowEscapeKey: false,
    confirmButtonText: 'Wiederherstellung beenden'
  }).then((result) => { //gets called when done
    

    restoreWebWorker.terminate();
    restoreWebWorker = null;
  });

  //load file browser container
  $.ajax({
    url: "Templates/flrBrowser"
  })
    .done(function (data) {
      $("#flrBrowserContainer").html(Mustache.render(data, { volumes: volumes }));

      //get selected volume
      var newPath = $("#sbDriveSelect option:selected").data("path");

      //build jsTree
      $('#flrBrowser').jstree({
        'core': {
          'check_callback': true,
          'data': null
        },
        types: {
          "directory": {
            "icon": "fa fa-hdd-o"
          },
          "file": {
            "icon": "fa fa-folder-open-o"
          },
          "default": {
          }
        }, plugins: ["types"]
      });

      //flr browser node select handler
      $("#flrBrowser").on("select_node.jstree", function (e, data) {
        flrDoNavigate(data.node.id, data.node.id);
      });

      //navigate
      flrDoNavigate(newPath, "#");
    });
}


//navigates to a given path within flr
function flrDoNavigate(path, parentNode) {
  //do ajax call
  $.ajax({
    url: "api/FLRBrowser",
    contentType: "application/json; charset=utf-8",
    data: JSON.stringify({ path: path }),
    type: 'POST',
    cache: false,
  })
    .done(function (data) {
      var fsEntries = JSON.parse(data);

      for (var i = 0; i < fsEntries.length; i++) {
        //get last path element
        var buffer = fsEntries[i]["path"].split("\\");
        $('#flrBrowser').jstree().create_node(parentNode, { id: fsEntries[i]["path"], text: buffer[buffer.length - 1], type: fsEntries[i]["type"] }, "last", false, false);
      }
    });
}

//handles a currently running live restore
function handleRunningLiveRestore() {

 var restoreWebWorker = new Worker("Scripts/restoreHeartbeatWebWorker.js")


  //show dialog box
  Swal.fire({
    title: 'LiveRestore läuft',
    text: "Der LiveRestore läuft. Schließen Sie dieses Hinweisfenster um den LiveRestore wieder zu beenden",
    icon: 'warning',
    confirmButtonColor: '#3085d6',
    allowOutsideClick: false,
    allowEscapeKey: false,
    confirmButtonText: 'LiveRestore beenden'
  }).then((result) => {
    //send delete request to stop job
    $.ajax({
      url: 'api/Restore',
      type: 'DELETE'
    });

    restoreWebWorker.terminate();
    restoreWebWorker = null;
  });

}