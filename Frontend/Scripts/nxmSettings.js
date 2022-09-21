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
        cancelButtonText: 'Abbrechen',
        preConfirm: (result) => {
            //check if given mount path is valid
            if (!checkForValidPath($("#inputMountPath").val())) {
                $("#inputMountPath").css("background-color", "red");
                return false;
            }
        },
    }).then((result) => { //gets called when done
        //return when not confirm clicked
        if (!result.isConfirmed) {
            return;
        }

        //set settings object to new values
        globalSettings["mountpath"] = $("#inputMountPath").val();
        globalSettings["mailssl"] = $("#inputMailSSL").prop("checked");
        globalSettings["mailserver"] = $("#inputMailServer").val();
        globalSettings["mailuser"] = $("#inputMailUser").val();
        globalSettings["mailpassword"] = $("#inputMailPassword").val();
        globalSettings["mailsender"] = $("#inputMailSender").val();
        globalSettings["mailrecipient"] = $("#inputMailRecipient").val();

        //remove last backslash from mountpath
        if (globalSettings["mountpath"].endsWith("\\")) {
            globalSettings["mountpath"] = globalSettings["mountpath"].substring(0, globalSettings["mountpath"].length - 1);
        }



        //send settings object to backend
        $.ajax({
            url: 'api/Settings',
            contentType: "application/json; charset=utf-8",
            data: JSON.stringify(globalSettings),
            type: 'POST'
        });

    });

    //load settings form
    $.ajax({
        url: "Templates/settingsForm"
    })
        .done(function (settingsForm) {
            $("#settingsPopUp").html(settingsForm);

            //show settings
            $("#inputMountPath").val(globalSettings["mountpath"]);
            $("#inputMailServer").val(globalSettings["mailserver"]);
            $("#inputMailSSL").prop("checked",globalSettings["mailssl"] == "true" ? true:false);
            $("#inputMailUser").val(globalSettings["mailuser"]);
            $("#inputMailSender").val(globalSettings["mailsender"]);
            $("#inputMailRecipient").val(globalSettings["mailrecipient"]);

            //handle reset link click
            $("#resetLink").click(function () {
                //wipe db
                $.ajax({
                    url: "api/wipeDB"
                })
                    .done(function (data) {
                        location.reload();
                });
            });

        });
}

//checks if a given path is valid
function checkForValidPath(path) {
    var result = path.match(/^(?:[a-z]:)?[\/\\]{0,2}(?:[.\/\\ ](?![.\/\\\n])|[^<>:"|?*.\/\\ \n])+$/gmi);
    return result != null;
}