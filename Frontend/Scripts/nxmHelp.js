//shows the help form
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
                $.ajax({
                    url: "api/SupportPackage",
                    error: function (result) {
                        Swal.fire(
                            'Fehlgeschlagen',
                            'Das Support Paket konnte nicht generiert werden',
                            'error'
                        );
                    }
                });
            });
        });
}