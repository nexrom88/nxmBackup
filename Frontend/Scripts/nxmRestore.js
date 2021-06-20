var selectedRestoreJob = {}; //the job selected for restore

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

  var restartStartDetails = {};
  restartStartDetails["basePath"] = selectedRestoreJob["BasePath"];
  restartStartDetails["vmName"] = $("#sbSourceVM option:selected").text() + "_restored";
  restartStartDetails["vmID"] = $("#sbSourceVM option:selected").data("vmid");
  restartStartDetails["destPath"] = "f:\\\\target";
  restartStartDetails["instanceID"] = instanceID;
  restartStartDetails["type"] = $("#sbRestoreType option:selected").data("type");
  restartStartDetails["jobID"] = selectedRestoreJob["DbId"];

  //do ajax call
  $.ajax({
    url: "api/BackupStart",
    contentType: "application/json; charset=utf-8",
    data: JSON.stringify(restartStartDetails),
    type: 'POST',
    cache: false,
  })
    .done(function (data) {
    });
  
}