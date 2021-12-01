//shows the settings form
var globalSettings = {};
function showSettings() {

  //get settings from BD
  $.ajax({
    url: "api/Settings"
  })
    .done(function (data) {
      globalSettings = JSON.parse(data);
      showSettingsPopUp();
    });
}

//shows the settings popup
function showSettingsPopUp() {
  //show dialog box
  Swal.fire({
    title: 'System-Einstellungen',
    html: "<div id='settingsPopUp'></div>",
    confirmButtonColor: '#3085d6',
    showCancelButton: true,
    allowOutsideClick: true,
    allowEscapeKey: true,
    confirmButtonText: 'Änderungen übernehmen',
    cancelButtonText: 'Abbrechen'
  }).then((result) => { //gets called when done

    
  });

  //load settings form
  $.ajax({
    url: "Templates/settingsForm"
  })
    .done(function (settingsForm) {
      $("#settingsPopUp").html(settingsForm);

      //show settings
      $("#inputMountPath").val(globalSettings["mountpath"]);
    });
}