//shows the settings form
var globalSettings = {};
var currentSettingsLanguage;
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
        title: languageStrings["system_settings_header"],
        html: "<div id='settingsPopUp'></div>",
        confirmButtonColor: '#3085d6',
        showCancelButton: true,
        allowOutsideClick: true,
        allowEscapeKey: true,
        confirmButtonText: languageStrings["apply_changes"],
        cancelButtonText: languageStrings["cancel"],
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
        globalSettings["language"] = $("#inputLanguage").val();

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
        }).done(function () {
            //reinit language if necessary
            if (globalSettings["language"] != currentSettingsLanguage) {
                loadLanguage();

                //reload page
                location.reload();
            }
        });

    });

    //load settings form
    $.ajax({
        url: "Templates/settingsForm"
    })
        .done(function (settingsForm) {

            //set language strings
            settingsForm = replaceLanguageMarkups(settingsForm);

            $("#settingsPopUp").html(settingsForm);

            //show settings
            $("#inputMountPath").val(globalSettings["mountpath"]);
            $("#inputMailServer").val(globalSettings["mailserver"]);
            $("#inputMailSSL").prop("checked", globalSettings["mailssl"] == "true" ? true : false);
            $("#inputMailUser").val(globalSettings["mailuser"]);
            $("#inputMailSender").val(globalSettings["mailsender"]);
            $("#inputMailRecipient").val(globalSettings["mailrecipient"]);
            $("#inputLanguage").val(globalSettings["language"]);
            currentSettingsLanguage = globalSettings["language"];

            //set toggle otp link
            if (!globalSettings["otpkey"]) {
                $("#toggleOTPLink a").text(languageStrings["activate_otp"]);
            } else {
                $("#toggleOTPLink a").text(languageStrings["disable_otp"]);
            }

            //handle otp toggle link click
            $("#toggleOTPLink a").click(function () {
                //show dialog box
                Swal.fire({
                    title: languageStrings["otp_header"],
                    html: languageStrings["otp_text"] + "<div id='otpqr'></div>",
                    confirmButtonColor: '#3085d6',
                    allowOutsideClick: true,
                    allowEscapeKey: true,
                    confirmButtonText: languageStrings["close"],
                });
            });

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

            //handle testmail link click
            $("#testmailLink").click(function () {
                //read given values
                var mailSettings = {};
                mailSettings["mailssl"] = $("#inputMailSSL").prop("checked");
                mailSettings["mailserver"] = $("#inputMailServer").val();
                mailSettings["mailuser"] = $("#inputMailUser").val();
                mailSettings["mailpassword"] = $("#inputMailPassword").val();
                mailSettings["mailsender"] = $("#inputMailSender").val();
                mailSettings["mailrecipient"] = $("#inputMailRecipient").val();

                //clear result div
                $("#testMailResult").html("");
                $("#testMailResult").css("color", "black");

                //send data to backend
                $.ajax({
                    url: "api/TestMail",
                    contentType: "application/json; charset=utf-8",
                    data: JSON.stringify(mailSettings),
                    type: 'POST'
                })
                    .done(function () {

                        //mail successfully sent
                        $("#testMailResult").css("color", "green");
                        $("#testMailResult").html(languageStrings["test_mail_success"]);                       

                    })
                    .fail(function (){
                        //error on sending mail
                        $("#testMailResult").css("color", "red");
                        $("#testMailResult").html(languageStrings["test_mail_error"]);
                    });
                    
            });

        });
}

//checks if a given path is valid
function checkForValidPath(path) {
    var result = path.match(/^(?:[a-z]:)?[\/\\]{0,2}(?:[.\/\\ ](?![.\/\\\n])|[^<>:"|?*.\/\\ \n])+$/gmi);
    return result != null;
}