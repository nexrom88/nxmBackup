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
  var selectedVM = $("#sbSourceVM option:selected").text();
  restoreDetails["vmName"] = selectedVM;


}