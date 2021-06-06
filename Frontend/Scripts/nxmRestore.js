
//starts the restore process
function startRestoreHandler() {
  var restoreJobID = selectedJob;

  //show overlay
  $("#restoreOverlay").css("display", "block");

  //load content for overlay
  $.ajax({
    url: "Templates/restoreOptions"
  }).done(function (data) {
    $("#restoreOptions").html(data);
  });

}