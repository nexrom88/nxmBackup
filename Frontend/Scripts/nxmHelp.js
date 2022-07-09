//shows the help form
function showHelpForm() {
    //show dialog box
    Swal.fire({
        title: 'Hilfe',
        html: "<div id='helpForm'></div>",
        confirmButtonColor: '#3085d6',
        allowOutsideClick: true,
        allowEscapeKey: true,
        confirmButtonText: 'ok'

    });

    //load settings form
    $.ajax({
        url: "Templates/helpForm"
    })
        .done(function (settingsForm) {
            $("#settingsPopUp").html(settingsForm);

            //show settings
            $("#inputMountPath").val(globalSettings["mountpath"]);
        });
}