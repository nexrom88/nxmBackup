﻿//shows the help form
function showHelpForm() {
    //show dialog box
    Swal.fire({
        title: 'Hilfe',
        html: "<div id='helpForm'></div>",
        confirmButtonColor: '#3085d6',
        allowOutsideClick: true,
        allowEscapeKey: true,
        confirmButtonText: 'OK'

    });

    //load help form
    $.ajax({
        url: "Templates/helpForm"
    })
        .done(function (settingsForm) {
            $("#helpForm").html(settingsForm);
            
            $("#createSupportPackage").click(function () {
                window.location = "api/SupportPackage";
            });
        });
}