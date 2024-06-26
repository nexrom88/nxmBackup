﻿//shows the settings form
var globalSettings = {};
var configuredHosts = {};
var currentSettingsLanguage;
var ignoreIP = false;
function showSettings() {

    //get settings from BD
    $.ajax({
        url: "api/Settings"
    })
        .done(function (data) {
            globalSettings = JSON.parse(data);

            //now read configured hyperv hosts
            $.ajax({
                url: "api/HyperVHosts"
            })
                .done(function (data) {
                    configuredHosts = JSON.parse(data);
                    showSettingsPopUp();
                });

            
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

            //set hyperv hosts list
            settingsForm = Mustache.render(settingsForm, { hosts: configuredHosts });

            //set language strings
            settingsForm = replaceLanguageMarkups(settingsForm);


            $("#settingsPopUp").html(settingsForm);

            //disable edit/delete button for localhost
            $(".hostsListItem").find("[data-hostid='1']").attr("disabled", true);

            //show settings
            if (globalSettings) {
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
            }

            //handle host delete button click
            $(".deleteHostButton").click(function () {
                var hostID = $(this).data("hostid");
                $.ajax({
                    url: 'api/HyperVHosts',
                    type: 'DELETE',
                    contentType: "application/json; charset=utf-8",
                    data: String(hostID),
                    error: function (request, error) {
                        Swal.fire(
                            languageStrings["error"],
                            languageStrings["host_remove_error"],
                            'error'
                        );
                    },
                    success: function () {
                        $(".hostsListItem[data-hostid='" + hostID + "']").remove();

                        //look for host in list and remove it
                        for (var i = 0; i < configuredHosts.length; i++) {
                            if (configuredHosts[i]["id"] == hostID) {
                                configuredHosts.splice(i, 1);
                                break;
                            }
                        }
                    }
                });
            });

            //handle host edit button click
            //handle host delete button click
            $(".editHostButton").click(function () {
                var hostID = $(this).data("hostid");
                showAddHyperVHostForm(hostID);
            });

            //handle otp toggle link click
            $("#toggleOTPLink a").click(function () {

                //show qr code
                if (!globalSettings["otpkey"]) {

                    //load otr register template
                    $.ajax({
                        url: "Templates/otpRegisterForm"
                    })
                        .done(function (otpForm) {
                            //translate language markups
                            otpForm = replaceLanguageMarkups(otpForm);

                            //show dialog box
                            Swal.fire({
                                title: languageStrings["otp_header"],
                                html: otpForm,
                                confirmButtonColor: '#3085d6',
                                showCancelButton: true,
                                allowOutsideClick: true,
                                allowEscapeKey: true,
                                confirmButtonText: languageStrings["apply"],
                                cancelButtonText: languageStrings["cancel"]
                            }).then((result) => {
                                if (result.isConfirmed) {
                                    //register otp
                                    registerOTP();
                                }
                            });

                            //load qr image
                            $("#otpimg").attr("src", "api/MFA");


                        });
                } else {
                    //disable otp
                    $.ajax({
                        url: 'api/MFA',
                        type: 'DELETE'
                    }).done(function () {
                        Swal.fire(
                            '2-FA',
                            languageStrings["2fa_disabled"],
                            'info'
                        );
                    });
                }

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

            //handle click on add hyperv host
            $("#addHyperVHost").click(function () {
                showAddHyperVHostForm(-1);           
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


//shows the add/edit hyperv host form
function showAddHyperVHostForm(editID) {
    ignoreIP = false;
    Swal.close();
    $("#newHostOverlay").css("display", "block");

    //set loading spinner to unvisible
    $("#saveHostLoadingSpinner").hide();

    //delay setting focus, because swal.close would take focus back 
    setTimeout(() => $("#inputDescription").focus(), 500)

    //register close button handler
    $(".overlayClose").click(function () {
        $("#newHostOverlay").css("display", "none");
        $("#inputDescription").val("");
        $("#inputHost").val("");
        $("#inputUser").val("");
        $("#inputPass").val("");
    });

    $("#saveAddHostButton").click(function () {
        //set input color to default first
        $("#inputDescription").css("background-color", "initial");
        $("#inputHost").css("background-color", "initial");
        $("#inputUser").css("background-color", "initial");
        $("#inputPass").css("background-color", "initial");

        //disable button
        $('#saveAddHostButton').css("display", "none")

        //set loading spinner to visible
        $("#saveHostLoadingSpinner").show();


        //first check that every input is filled
        if ($("#inputDescription").val() == "") {
            $("#inputDescription").css("background-color", "rgb(255,77,77)");
            return;
        }
        if ($("#inputHost").val() == "") {
            $("#inputHost").css("background-color", "rgb(255,77,77)");
            return;
        }
        if ($("#inputUser").val() == "") {
            $("#inputUser").css("background-color", "rgb(255,77,77)");
            return;
        }
        if ($("#inputPass").val() == "" && editID == -1) {
            $("#inputPass").css("background-color", "rgb(255,77,77)");
            return;
        }

        //listen on input change
        $("#inputPass, #inputUser, #inputHost").on("input", hypervInputChanged);

        //get data from user input
        var newHost = {};
        newHost["editID"] = editID;
        newHost["description"] = $("#inputDescription").val();
        newHost["host"] = $("#inputHost").val();
        newHost["user"] = $("#inputUser").val();
        newHost["password"] = $("#inputPass").val();

        //send new host to server
        sendHostToServer(newHost);

    });

    

    //set edit id
    $("#hostEditID").html(editID);

    //show host details when editing a given host
    if (editID != -1) {
        //look for host
        for (var i = 0; i < configuredHosts.length; i++) {
            if (configuredHosts[i]["id"] == editID) {
                //host found
                $("#inputDescription").val(configuredHosts[i]["description"]);
                $("#inputHost").val(configuredHosts[i]["host"]);
                $("#inputUser").val(configuredHosts[i]["user"]);

                //add hint
                $("#inputPass").attr("placeholder", languageStrings["change_pw_ph"]);

                break;
            }
        }
    }
}

//on new hyperv host input change
function hypervInputChanged() {
    ignoreIP = false;
}

//send a given host to the server and tries to translate ip address
function sendHostToServer(newHost) {
    //when ip has to be ignored -> continue
    if (ignoreIP == true) {
        saveHost(newHost);
        ignoreIP = false;
        return;
    }

    $.ajax({
        url: "api/TranslateIP",
        contentType: "application/json; charset=utf-8",
        data: JSON.stringify(newHost),
        type: 'POST',
        success: function (result) {
            //no ip address found -> go on without translation
            saveHost(newHost);
        },
        error: function (result) {
            if (result["status"] == 302) {
                //translation successful
                newHost["host"] = result["responseText"];
                $("#inputHost").val(newHost["host"]);

                //show message after ip translation
                Swal.fire(
                    languageStrings["hint"],
                    languageStrings["ip_translated"],
                    'info'
                );

                //show button
                $('#saveAddHostButton').css('display', "inline-block")
                //set loading spinner to unvisible
                $("#saveHostLoadingSpinner").hide();

            } else if (result["status"] == 404) {
                //no translation possible
                ignoreIP = true;
                Swal.fire(
                    languageStrings["hint"],
                    languageStrings["ip_not_translated"],
                    'info'
                );
                //show button
                $('#saveAddHostButton').css('display', "inline-block")
                //set loading spinner to unvisible
                $("#saveHostLoadingSpinner").hide();
            }
        }
    });
}

//saves to given host
function saveHost(newHost) {
    $.ajax({
        url: "api/HyperVHosts",
        contentType: "application/json; charset=utf-8",
        data: JSON.stringify(newHost),
        type: 'POST',
        success: function (result) {
            $("#newHostOverlay").css("display", "none");
            //empty inputs
            $("#inputDescription").val("");
            $("#inputHost").val("");
            $("#inputUser").val("");
            $("#inputPass").val("");

            Swal.fire(
                languageStrings["successful_capital"],
                languageStrings["host_added_success"],
                'success'
            );
            //show button
            $('#saveAddHostButton').css('display', "inline-block")
            //set loading spinner to unvisible
            $("#saveHostLoadingSpinner").hide();
        },
        error: function (result) {
            $("#newHostOverlay").css("display", "none");

            //empty inputs
            $("#inputDescription").val("");
            $("#inputHost").val("");
            $("#inputUser").val("");
            $("#inputPass").val("");

            Swal.fire(
                languageStrings["failed_capital"],
                languageStrings["host_added_error"],
                'error'
            );
            //show button
            $('#saveAddHostButton').css('display', "inline-block")
            //set loading spinner to unvisible
            $("#saveHostLoadingSpinner").hide();
        }
    });
}

//registers the otp
function registerOTP() {
    var currentOTP = $("#otpConfirmInput").val();

    //build object to send to server
    var otpObj = {};
    otpObj["otp"] = currentOTP;

    //send otp to server
    $.ajax({
        url: 'api/MFA',
        contentType: "application/json; charset=utf-8",
        data: JSON.stringify(otpObj),
        type: 'PUT',
        success: function (result) {
            Swal.fire(
                languageStrings["success"],
                languageStrings["2fa_activate"],
                'success'
            ).then((result) => {
                location.reload();
            });
        },
        error: function (jqXHR, exception) {
            Swal.fire(
                languageStrings["error"],
                languageStrings["2fa_activate_error"],
                'error'
            );
        }
    });
}

//checks if a given path is valid
function checkForValidPath(path) {
    var result = path.match(/^(?:[a-z]:)?[\/\\]{0,2}(?:[.\/\\ ](?![.\/\\\n])|[^<>:"|?*.\/\\ \n])+$/gmi);
    return result != null;
}