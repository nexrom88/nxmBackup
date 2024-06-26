﻿//on main window load
var configuredJobs; //list of all active jobs
var selectedJob; //the id of the currently selected job
var selectedJobObj; //the obj with the currently selected job
var selectedVM; //the selected vm id within main panel
var eventRefreshTimer; //timer for refreshing vm events
var maxNodeID; //counter for treeview nodes
var selectedDirectory; //the currently selected directory within folder browser
var newJobObj; //new job object
var dbState; //current db state (values: init, error, success)
var jobStateTableTemplate; //template for job state table
var eventsListItemTemplate; //template for event list item
var lastJobStateData; //last data for job state to decide whether to refresh jobStateTable or not
var ratesChart; //var to hold the chart for displaying transferrates
var transferrates = []; //var to hold the current transfer rates
var processrates = []; //var to hold the current process rates
var versionControl = {} //object to hold version and update information
var languageStrings = {}; //object to hold all language strings
var currentLanguage; //string object to hold current selected language

//global handler for http status 401 (login required)
$.ajaxSetup({
    statusCode: {
        401: function (jqxhr, textStatus, errorThrown) {
            //stop possible running eventRefreshTimer
            clearInterval(eventRefreshTimer);

            document.body.innerHTML = "";

            //show login form
            initLoginForm();
        }
    }
});

//loads a given string from language db. When furtherInit is set, MainWindow template gets loaded afterwards
function loadLanguage() {
    $.ajax({
        url: "api/LoadLanguage",
        async:false
    })
        .done(function (data) {
            //parse language object
            languageStrings = JSON.parse(data);
        });
}

//loads and initializes the main window template
function loadMainWindowTemplate() {
    $.ajax({
        url: "Templates/mainWindow"
    })
        .done(function (data) {
            //replace lang markups
            data = replaceLanguageMarkups(data);

            //set main window to viewport
            $("#mainWindow").html(data);

            //init everything else
            init();
        });
}

//replaces language markups with strings from loaded language strings
function replaceLanguageMarkups(text) {

    //just replace markups when language strings are available
    if (languageStrings) {
        var markups = text.match(/\$\$[\w]+\$\$/g);

        //iterate through all found markups
        for (var i = 0; markups != null && i < markups.length; i++) {
            //remove $$ $$
            var currentMarkup = markups[i].substring(2, markups[i].length - 2);

            //look for lang string
            var currentText = languageStrings[currentMarkup];

            //set default value when text cannot be found
            if (!currentText) {
                currentText = currentMarkup;
            }

            //replace original markup
            text = text.replace(markups[i], currentText);
        }
    }

    return text;
}


//init jobs post-login
function init() {

    //check DB availability
    $.ajax({
        url: "api/DBConnectTest",
        error: function (jqXHR, exception) {
            $("#welcomeText").html(languageStrings["db_error"]);
            $("#welcomeText").addClass("welcomeTextError");
            dbState = "error";
        },
        success: function (jqXHR, exception) {
            dbState = "success";
        }
    });

    //check for update
    $.ajax({
        url: "api/Update",
        success: function (jqXHR, exception) {
            versionControl = JSON.parse(jqXHR);

            //disable update notification when no update is available
            if (!versionControl.UpdateAvailable) {
                $("#versionInfo").css("display", "none");
            }
        }
    });

    //load configured jobs
    $.ajax({
        url: "api/ConfiguredJobs"
    })
        .done(function (data) {
            configuredJobs = jQuery.parseJSON(data);
            buildJobsList();
        });

    //register logout handler
    $("#logout").click(function () {
        logOut();
    });

    //register settings handler
    $("#settings").click(function () {
        showSettings();
    });

    //register help handler
    $("#help").click(function () {
        showHelpForm();
    });

    //register settings handler
    $("#versionInfo").click(function () {
        Swal.fire(
            'Update',
            languageStrings["update_available"],
            'info'
        );
    });

    //register "add Job" Button handler
    $("#addJobButton").click(function () {

        if (dbState == "success") {
            startNewJobProcess(null);
        }

    });

    //register tooltips
    registerTooltips();

}

$(window).on('load', function () {
    dbState = "init";

    //load language
    loadLanguage();

    //check if user is authenticated
    $.ajax({
        url: "api/CheckAuth",
        success: function () {
            //user is authenticated

            //now load main window template
            loadMainWindowTemplate();
       
        }
    })
    
});

//registers all tooltips
function registerTooltips() {
    $('[data-toggle="tooltip"]').tooltip({ 'delay': { show: 500, hide: 500 } });
}


//starts a job editing/creating process
function startNewJobProcess(selectedEditJob) {
    $("#newJobOverlay").css("display", "block");

    newJobObj = {};

    //set updated job ID if necessary
    if (selectedEditJob) {
        newJobObj["updatedJob"] = selectedEditJob["DbId"];
    }

    showNewJobPage(1, selectedEditJob);

    //register close button handler
    $(".overlayClose").click(function () {
        $("#newJobOverlay").css("display", "none");
    });

    //register esc key press handler
    $(document).on('keydown', function (event) {
        if (event.key == "Escape") {
            $("#newJobOverlay").css("display", "none");
        }
    });
}

//shows a given page number when adding a new job
function showNewJobPage(pageNumber, selectedEditJob) {
    //load page
    $.ajax({
        url: "Templates/newJobPage" + pageNumber
    })
        .done(function (data) {
            //replace language markups
            data = replaceLanguageMarkups(data)

            switch (pageNumber) {
                case 1:
                    $("#newJobPage").html(data);
                    registerNextPageClickHandler(pageNumber, selectedEditJob);
                    //click handler for encryption checkBox
                    $("#cbEncryption").click(function () {
                        if ($("#cbEncryption").prop("checked")) {
                            $("#txtEncryptionPassword").css("display", "block");
                        } else {
                            $("#txtEncryptionPassword").css("display", "none");
                        }
                    });

                    //click handler for encryption checkBox
                    $("#cbLiveBackup").click(function () {
                        if ($("#cbLiveBackup").prop("checked")) {
                            $("#lbSize").css("display", "block");
                        } else {
                            $("#lbSize").css("display", "none");
                        }
                    });

                    //enable input number spinner for lbSize
                    $("input[type='number']").inputSpinner();

                    //show current settings when editing a job
                    if (selectedEditJob) {
                        showCurrentSettings(pageNumber, selectedEditJob);
                    }

                    //set focus to txtJobName
                    $("#txtJobName").focus();

                    break;
                case 2:
                    //load hyperv hosts
                    $.ajax({
                        url: "api/HyperVHosts"
                    })
                        .done(function (hostsData) {
                            configuredHosts = JSON.parse(hostsData);
                            var renderedData = Mustache.render(data, { hosts: configuredHosts });
                            $("#newJobPage").html(renderedData);

                            //disable host select when lb is enabled
                            if (newJobObj["livebackup"]) {
                                $('.availableHost[data-hostid="1"]').attr('selected', 'selected');
                                $("#sbHyperVHost").attr('disabled', 'disabled');
                                $("#hostHint").css("display", "block");
                            }

                            //get first selected host id
                            var selectedHostID = $("#sbHyperVHost").find(":selected").data("hostid");

                            //load vms for selected host
                            loadVMs(selectedHostID, selectedEditJob, false);

                            //on host change event handler
                            $("#sbHyperVHost").on("change", function (event) {
                                var selectedHostID = $(this).find(":selected").data("hostid");

                                //set loading spinner to visible
                                $("#vmLoadingSpinner").show();

                                //clear current list
                                $("#newJobvmList").html("");

                                //load vms for selected host
                                loadVMs(selectedHostID, selectedEditJob, true);
                            });

                            registerNextPageClickHandler(pageNumber, selectedEditJob);
                        });


                    break;
                case 3:
                    $("#newJobPage").html(data);
                    registerNextPageClickHandler(pageNumber, selectedEditJob);

                    //enable input number spinner
                    $("input[type='number']").inputSpinner();

                    //set interval select change event handler
                    $("#sbJobInterval").on("change", function (event) {
                        var interval = $(this).children("option:selected").data("interval");

                        //disable/enable controls
                        switch (interval) {
                            case "hourly":
                                $("#spJobIntervalMinute").removeAttr("disabled");
                                $("#spJobIntervalHour").prop("disabled", true);
                                $("#sbJobDay").prop("disabled", true);
                                break;
                            case "daily":
                                $("#spJobIntervalMinute").removeAttr("disabled");
                                $("#spJobIntervalHour").removeAttr("disabled");
                                $("#sbJobDay").prop("disabled", true);
                                break;
                            case "weekly":
                                $("#spJobIntervalMinute").removeAttr("disabled");
                                $("#spJobIntervalHour").removeAttr("disabled");
                                $("#sbJobDay").removeAttr("disabled");
                                break;
                            case "never":
                                $("#spJobIntervalHour").prop("disabled", true);
                                $("#sbJobDay").prop("disabled", true);
                                $("#spJobIntervalMinute").prop("disabled", true);
                                break;
                        }
                    });

                    //show current settings when editing a job
                    if (selectedEditJob) {
                        showCurrentSettings(pageNumber, selectedEditJob);
                    }

                    break;
                case 4:
                    $("#newJobPage").html(data);
                    registerNextPageClickHandler(pageNumber, selectedEditJob);

                    //disable options for non-incremental jobs
                    if (!newJobObj["incremental"]) {
                        $("#incrementalOptions").css("display", "none");
                    }

                    //enable input number spinner
                    $("input[type='number']").inputSpinner();

                    //set "rotation type" select event handler
                    $("#sbRotationType").on("change", function (event) {
                        var rotationType = $(this).children("option:selected").data("rotationtype");

                        switch (rotationType) {
                            case "merge":
                                $("#lblMaxElements").html(languageStrings["backups_number"]);
                                break;
                            case "blockrotation":
                                $("#lblMaxElements").html(languageStrings["blocks_number"]);
                                break;
                        }

                    });

                    //show current settings when editing a job
                    if (selectedEditJob) {
                        showCurrentSettings(pageNumber, selectedEditJob);
                    }
                    break;

                case 5:
                    $("#newJobPage").html(data);
                    registerNextPageClickHandler(pageNumber, selectedEditJob);
                    buildFolderBrowser();

                    

                    //register checkCredentialsButton click handler   
                    $("#checkCredentialsButton").click(checkCredentials);
      

                    //register change on target type selection
                    $("#sbTargetType").on("change", function (event) {
                        var targetType = $(this).children("option:selected").data("targettype");

                        switch (targetType) {
                            case "local":
                                buildFolderBrowser();
                                $("#checkCredentialsButton").css("display", "none");
                                break;
                            case "smb":
                                $('#folderBrowser').jstree("destroy");
                                $("#checkCredentialsButton").css("display", "inline");
                                buildSMBForm();
                                break;
                            case "nxmstorage":
                                $('#folderBrowser').jstree("destroy");
                                $("#checkCredentialsButton").css("display", "inline");
                                buildnxmStorageForm();
                                break;
                        }
                    });

                    //show current settings when editing a job
                    if (selectedEditJob) {
                        showCurrentSettings(pageNumber, selectedEditJob);
                    }

                    break;
            }



        });

}

//load vms for a given host id when creating a new job
function loadVMs(hostID, selectedEditJob, showErrorBox) {
    $.ajax({
        url: "api/vms?hostid=" + hostID
    })
        .done(function (vmdata) {
            var parsedJSON = jQuery.parseJSON(vmdata);

            //load host item template
            $.ajax({
                url: "Templates/vmListItem",
                success: function (data) {
                    data = Mustache.render(data, { vms: parsedJSON });

                    //set loading spinner to unvisible
                    $("#vmLoadingSpinner").hide();

                    //set host list to gui
                    $("#newJobvmList").html(data);

                    //vm click handler                    
                    $(".availablevm").click(function (event) {
                        $(this).toggleClass("active");

                        //enable next-button
                        if ($(".availablevm.active").length > 0) {
                            $("#newJobNextButton").removeAttr("disabled");
                        } else {
                            $("#newJobNextButton").attr("disabled", "disabled");
                        }

                    });

                    //set next-button to disabled
                    $("#newJobNextButton").attr("disabled", "disabled");

                    //show current settings when editing a job
                    if (selectedEditJob) {
                        showCurrentSettings(pageNumber, selectedEditJob);

                        //activate next button when vm is selected
                        if ($(".availablevm.active")) {
                            $("#newJobNextButton").attr("disabled", false);
                        }
                    }

                }
            });

            
        })
        .fail(function () {
            //hide loading anim
            $("#vmLoadingSpinner").hide();

            //show message on error while loading vms
            if (showErrorBox) {
                Swal.fire(
                    languageStrings["error"],
                    languageStrings["error_connecting_host"],
                    'error'
                );
            }
        });
}


//generic funktion for checking credentials
function checkCredentials() {
    var targetType = $("#sbTargetType").children("option:selected").data("targettype");

    if (targetType == "smb") {
        checkSMBCredentials();
    } else if (targetType == "nxmstorage") {
        checkNxmStorageCredentials();
    }
}

//checks given nxmStorage credentials
function checkNxmStorageCredentials() {
    var username = $("#inputUsername").val();
    var password = $("#inputPassword").val();

    $.ajax({
        url: "api/ChecknxmStorageCredentials",
        type: "post",
        contentType: "application/json; charset=utf-8",
        data: JSON.stringify({Username: username, Password: password })
    })
        .done(function (data) {
            Swal.fire(
                languageStrings["successful_capital"],
                languageStrings["check_success"],
                'success'
            );

        }).fail(function (data) {
            Swal.fire(
                languageStrings["error"],
                languageStrings["check_error"],
                'error'
            );

    });
}


//checks given smb credentials
function checkSMBCredentials() {
    var path = $("#smbPath").val();
    var username = $("#inputUsername").val();
    var password = $("#inputPassword").val();

    //check if valid smb path
    if (!checkForValidSMBPath(path)) {
        $("#smbPath").css("background-color", "#ff4d4d");
        return;
    } else {
        $("#smbPath").css("background-color", "initial");
    }

    $.ajax({
        url: "api/CheckSMBCredentials",
        type: "post",
        contentType: "application/json; charset=utf-8",
        data: JSON.stringify({ Path: path, Username: username, Password: password })
    })
        .done(function (data) {
            Swal.fire(
                languageStrings["successful_capital"],
                languageStrings["check_success"],
                'success'
            );

        }).fail(function (data) {
            Swal.fire(
                languageStrings["error"],
                languageStrings["check_error"],
                'error'
            );

        });
}

//builds the form for nxmStorage credentials
function buildnxmStorageForm() {
    $.ajax({
        url: "Templates/nxmStorageCredentials"
    })
        .done(function (data) {
            $("#folderBrowser").html(replaceLanguageMarkups(data));
        });
}

//builds the form fpr smb credentials
function buildSMBForm() {
    $.ajax({
        url: "Templates/smbCredentials"
    })
        .done(function (data) {
            $("#folderBrowser").html(replaceLanguageMarkups(data));
        });
}

//builds the folder browser elements on creating a new job
function buildFolderBrowser() {
    $('#folderBrowser').jstree({
        'core': {
            'check_callback': true,
            'data': null
        },
        types: {
            "drive": {
                "icon": "fa fa-hdd-o"
            },
            "folder": {
                "icon": "fa fa-folder-open-o"
            },
            "default": {
            }
        }, plugins: ["types"]
    });

    //init treeview
    maxNodeID = 0;
    navigateToDirectory("/", "drive", "#");
    selectedDirectory = "";

    //node select handler
    $("#folderBrowser").on("select_node.jstree", function (e, data) {
        var selectedPath = data.instance.get_path(data.node, '\\');
        selectedDirectory = selectedPath;
        navigateToDirectory(selectedPath, "folder", data.node.id);
    });
}

//shows the current settings on a given page when editing a job
function showCurrentSettings(pageNumber, selectedEditJob) {
    switch (pageNumber) {
        case 1:
            $("#txtJobName").val(selectedEditJob["Name"]);
            $("#cbMail").prop("checked", selectedEditJob["MailNotifications"])
            $("#cbIncremental").prop("checked", selectedEditJob["Incremental"]);
            $("#cbLiveBackup").prop("checked", selectedEditJob["LiveBackup"]);
            $("#spLiveBackupSize").val(selectedEditJob["LiveBackupSize"]);
            $("#cbDedupe").prop("checked", selectedEditJob["UsingDedupe"]);
            $("#cbEncryption").prop("checked", selectedEditJob["UseEncryption"]);
            $("#cbEncryption").prop("disabled", true); //encrpytion setting not changeable

            //show lb size area if necessary
            if (selectedEditJob["LiveBackup"]) {
                $("#lbSize").css("display", "block");
            }

            break;

        case 2:
            for (var i = 0; i < selectedEditJob["JobVMs"].length; i++) {
                $(".availablevm").each(function () {
                    if ($(this).data("vmid") == selectedEditJob["JobVMs"][i]["vmID"]) {
                        $(this).addClass("active");
                    }
                });
            }
            break;
        case 3:
            //set interval base
            switch (selectedEditJob["Interval"]["intervalBase"]) {
                case 0: //hourly
                    $('option[data-interval="hourly"]').prop("selected", true);
                    $("#sbJobInterval").change(); //trigger change-event manually
                    $("#spJobIntervalMinute").val(selectedEditJob["Interval"]["minute"]);
                    break;
                case 1: //daily
                    $('option[data-interval="daily"]').prop("selected", true);
                    $("#sbJobInterval").change(); //trigger change-event manually
                    $("#spJobIntervalMinute").val(selectedEditJob["Interval"]["minute"]);
                    $("#spJobIntervalHour").val(selectedEditJob["Interval"]["hour"]);
                    break;
                case 2: //weekly
                    $('option[data-interval="weekly"]').prop("selected", true);
                    $("#sbJobInterval").change(); //trigger change-event manually
                    $("#spJobIntervalMinute").val(selectedEditJob["Interval"]["minute"]);
                    $("#spJobIntervalHour").val(selectedEditJob["Interval"]["hour"]);
                    $('option[data-day="' + selectedEditJob["Interval"]["day"] + '"]').prop("selected", true);
                    break;
                case 3: //never
                    $('option[data-interval="never"]').prop("selected", true);
                    $("#sbJobInterval").change(); //trigger change-event manually
                    break;
            }
            break;

        case 4:
            $("#spBlockSize").val(selectedEditJob["BlockSize"]);

            //set rotation type
            if (selectedEditJob["Rotation"]["type"] == 0) { //merge
                $('option[data-rotationtype="merge"]').prop("selected", true);
                $("#lblMaxElements").html(languageStrings["backups_number"]);
            } else if (selectedEditJob["Rotation"]["type"] == 1) { //blockrotation
                $('option[data-rotationtype="blockrotation"]').prop("selected", true);
                $("#lblMaxElements").html(languageStrings["blocks_number"]);
            }

            $("#spMaxElements").val(selectedEditJob["Rotation"]["maxElementCount"]);
            break;

        case 5:

            break;

    }
}

//folder browser: navigate to directory
function navigateToDirectory(directory, nodeType, currentNodeID) {

    //when data already loaded, do not load again
    if (currentNodeID != "#") {
        if ($("#" + currentNodeID).data("loaded")) {
            return;
        }
    }

    $.ajax({
        url: 'api/Directory',
        contentType: "application/json; charset=utf-8",
        data: JSON.stringify({ path: directory }),
        type: 'POST',
        cache: false,
        success: function (result) {
            var directories = JSON.parse(result);
            for (var i = 0; i < directories.length; i++) {
                $('#folderBrowser').jstree().create_node(currentNodeID, { id: "dirnode" + (i + maxNodeID), text: directories[i], type: nodeType }, "last", false, false);
            }
            maxNodeID += directories.length;



            //set loaded attrib to current node
            if (currentNodeID != "#") {
                $("#" + currentNodeID).data("loaded", true);
            }

            //open current node
            if (currentNodeID != "#") {
                $("#folderBrowser").jstree("open_node", $("#" + currentNodeID));
            }

        }
    });
}

//click handler for nextPageButton
function registerNextPageClickHandler(currentPage, selectedEditJob) {
    $("#newJobNextButton").click(function () {

        //parse inputs
        switch (currentPage) {
            case 1:
                var jobName = $("#txtJobName").val();

                //no valid jobname given
                if (!jobName) {
                    $("#txtJobName").css("background-color", "#ff4d4d");
                    return;
                } else { //valid jobname given
                    $("#txtJobName").css("background-color", "initial");
                    newJobObj["name"] = jobName;
                }

                //using encryption but no password given?
                if (!selectedEditJob && $("#cbEncryption").prop("checked") && !$("#txtEncryptionPassword").val()) {
                    $("#txtEncryptionPassword").css("background-color", "#ff4d4d");
                    return;
                } else {
                    $("#txtEncryptionPassword").css("background-color", "initial");
                }

                //mail notifications enabled?
                newJobObj["mailnotifications"] = $("#cbMail").prop("checked");

                //use dedupe?
                newJobObj["usingdedupe"] = $("#cbDedupe").prop("checked");

                //use live backup?
                newJobObj["livebackup"] = $("#cbLiveBackup").prop("checked");
                newJobObj["livebackupsize"] = $("#spLiveBackupSize").val();

                //use encryption?
                newJobObj["useencryption"] = $("#cbEncryption").prop("checked");
                newJobObj["encpassword"] = $("#txtEncryptionPassword").val();

                //use incremental backups?
                newJobObj["incremental"] = $("#cbIncremental").prop("checked");

                break;
            case 2:

                //get selected vms
                var selectedVMs = $(".availablevm.active");
                newJobObj["vms"] = [];

                //get selected host
                var selectedHostID = $("#sbHyperVHost").find(":selected").data("hostid");
                newJobObj["hostid"] = selectedHostID;

                //lb just possible on localhost and only when one vm got selected
                if (selectedVMs.length > 1 && newJobObj["livebackup"]) {
                    Swal.fire(
                        languageStrings["error"],
                        languageStrings["error_one_vm"],
                        'error'
                    );
                    return;
                }

                //add vm IDs to newJob object
                for (var i = 0; i < selectedVMs.length; i++) {
                    var vm = {};
                    vm.id = $(selectedVMs[i]).data("vmid");
                    vm.name = $(selectedVMs[i]).data("vmname");
                    vm.hostid = selectedHostID;
                    newJobObj["vms"].push(vm);
                }

                break;

            case 3:
                //get job interval
                newJobObj["interval"] = $("#sbJobInterval option:selected").data("interval");

                //get minute
                newJobObj["minute"] = $("#spJobIntervalMinute").val();

                //get hour
                newJobObj["hour"] = $("#spJobIntervalHour").val();

                //get day
                newJobObj["day"] = $("#sbJobDay option:selected").data("day");

                break;

            case 4:
                //get block-size
                newJobObj["blocksize"] = $("#spBlockSize").val();

                //get rotation-type
                newJobObj["rotationtype"] = $("#sbRotationType option:selected").data("rotationtype");

                //get max-elements
                newJobObj["maxelements"] = $("#spMaxElements").val();

                //valid input?
                if (newJobObj["rotationtype"] == "merge" && newJobObj["blocksize"] > newJobObj["maxelements"]) {
                    Swal.fire(
                        languageStrings["invalid_input"],
                        languageStrings["error_retention"],
                        'error'
                    );
                    return;
                }

                break;

            case 5:
                var targetType = $("#sbTargetType").children("option:selected").data("targettype");

                //set target type
                newJobObj["targetType"] = targetType;

                if (targetType == "smb") {
                    //get and set smb path, username and password
                    var username = $("#inputUsername").val();
                    var password = $("#inputPassword").val();
                    var smbDirectory = $("#smbPath").val();

                    //check if valid smb path
                    if (!checkForValidSMBPath(smbDirectory)) {
                        $("#smbPath").css("background-color", "#ff4d4d");
                        return;
                    }

                    newJobObj["targetUsername"] = username;
                    newJobObj["targetPassword"] = password;
                    newJobObj["targetPath"] = smbDirectory;

                    //check access to smb path


                } else if (targetType == "local") {

                    //get and set selected node
                    newJobObj["targetPath"] = selectedDirectory;

                } else if (targetType == "nxmstorage") {

                    //read user and password for nxmstorage
                    var username = $("#inputUsername").val();
                    var password = $("#inputPassword").val();
                    newJobObj["targetUsername"] = username;
                    newJobObj["targetPassword"] = password;
                }




                //done creating new job, send to server
                saveNewJob();
                $("#newJobOverlay").css("display", "none");
                return;
                break;
        }


        currentPage += 1;
        showNewJobPage(currentPage, selectedEditJob);
    });
}

//checks if a given string is a valid smb path
function checkForValidSMBPath(path) {
    return /^\\\\.{1,}\\.{1,}$/.test(path);
}


//sends the new job data to server
function saveNewJob() {
    var jobJSON = JSON.stringify(newJobObj);
    $.ajax({
        url: 'api/JobCreate',
        contentType: "application/json; charset=utf-8",
        data: jobJSON,
        type: 'POST',
        cache: false,
        success: function (result) {
            location.reload();
        },
        error: function (result) {
            Swal.fire(
                languageStrings["failed"],
                languageStrings["job_create_failed"],
                'error'
            );
            $("#newJobOverlay").css("display", "none");
        }
    });
}

//performs logout
function logOut() {
    $.ajax({
        url: "api/Logout"
    })
        .done(function (data) {
            Cookies.remove("session_id");
            location.reload();
        });
}

//builds the jobs sidebar
function buildJobsList() {

    //clear the content first
    $("#jobsList").html("");

    //load html template first
    var jobTemplate;
    $.ajax({
        url: "Templates/navJobItem"
    })
        .done(function (data) {
            jobTemplate = replaceLanguageMarkups(data);

            //iterate through all jobs
            for (var i = 0; i < configuredJobs.length; i++) {

                //deep-copy job template
                var currentTemplate = jobTemplate.slice();

                //add job to sidebar
                $("#jobsList").append(Mustache.render(currentTemplate, { JobName: configuredJobs[i].Name, JobDbId: configuredJobs[i].DbId, JobEnabled: configuredJobs[i].Enabled }));

            }

            //register job click handler
            $(".jobLink").click(function () {
                var JobDbId = $(this).data("jobdbid");
                selectedJob = JobDbId;

                //look for job
                for (var i = 0; i < configuredJobs.length; i++) {
                    if (configuredJobs[i].DbId == JobDbId) {
                        lastJobStateData = "";
                        selectedJobObj = configuredJobs[i];
                        buildJobDetailsPanel();
                    }
                }

            });

        });
}

//builds the vm list
function buildJobDetailsPanel() {

    //disable chart
    ratesChart = null;
    transferrates = []

    //load vm details table
    $.ajax({
        url: "Templates/jobDetailsPanel"
    })
        .done(function (tableData) {

            //remove language markups
            tableData = replaceLanguageMarkups(tableData);

            var jobHeaderString;
            jobHeaderString = languageStrings["job_details_caption"] + " (" + selectedJobObj.Name + ")";
            if (!selectedJobObj.Enabled) {
                jobHeaderString += " - " + languageStrings["job_disabled"];
            }

            $("#mainPanelHeader").html(jobHeaderString);


            //set details panel and vms list
            var vms = [];
            for (var i = 0; i < selectedJobObj.JobVMs.length; i++) {
                vms[i] = { vmid: selectedJobObj.JobVMs[i].vmID, name: selectedJobObj.JobVMs[i].vmName };
            }



            var details = Mustache.render(tableData, { vms: vms });
            $("#mainPanel").html(details);

            //set vm click handler
            $(".vm").click(vmClickHandler);

            //set start/stop job button click handler
            $("#startStopJobButton").click(startStopJobHandler);

            //set delete job button click handler
            $("#deleteJobButton").click(deleteJobHandler);

            //set enable job button click handler
            $("#enableJobButton").click(enableJobHandler);

            //set edit job button click handler
            $("#editJobButton").click(editJobHandler);

            //set restore button click handler
            $("#restoreButton").click(startRestoreHandler); //startRestoreHandler function is defined within nxmRestore.js

            //set stop LB Button click handler
            $("#stopLBButton").click(stopLBHandler);

            //edit enableJobButton caption
            $("#enableJobButtonCaption").html(selectedJobObj["Enabled"] ? languageStrings["disable_job_button"] : languageStrings["enable_job_button"]);

            //select first vm
            $(".vm").first().click();

        });

}

//stops running lb
function stopLBHandler() {
    $.ajax({
        url: 'api/StopLB',
        contentType: "application/json; charset=utf-8",
        data: String(selectedJob),
        type: 'POST',
        cache: false,
        success: function (result) {
            $("#stopLBButton").css("display", "none");
        }
    });
}


//changes a given int to two digits
function buildTwoDigitsInt(input) {
    return input.toLocaleString('en-US', { minimumIntegerDigits: 2, useGrouping: false });
}


//click handler for editing job
function editJobHandler(event) {

    var selectedEditJob = [];
    //look for job
    for (var i = 0; i < configuredJobs.length; i++) {
        if (configuredJobs[i].DbId == selectedJob) {
            selectedEditJob = configuredJobs[i];
        }
    }

    startNewJobProcess(selectedEditJob);
}

//click handler for enabling/disabling job
function enableJobHandler(event) {
    var postObj = {};

    postObj["jobID"] = selectedJob;
    postObj["enabled"] = selectedJobObj["Enabled"] ? "false" : "true";

    $.ajax({
        url: 'api/JobEnable',
        contentType: "application/json; charset=utf-8",
        data: JSON.stringify(postObj),
        type: 'POST',
        cache: false,
        success: function (result) {
            selectedJobObj["Enabled"] = !selectedJobObj["Enabled"];

            //edit enableJobButton caption
            $("#enableJobButtonCaption").html(selectedJobObj["Enabled"] ? languageStrings["disable_job_button"] : languageStrings["enable_job_button"]);

            //refresh the details panel header
            var jobHeaderString;
            jobHeaderString = languageStrings["job_details_caption"] + " (" + selectedJobObj.Name + ")";
            if (!selectedJobObj.Enabled) {
                jobHeaderString += " - " + languageStrings["job_disabled"];
            }
            $("#mainPanelHeader").html(jobHeaderString);

            //refresh job state table
            renderJobStateTable();

            //build jobs list panel
            buildJobsList();
        }
    });
}

//click handler for deleting job
function deleteJobHandler(event) {
    //api call
    Swal.fire({
        title: languageStrings["delete_job_caption"],
        text: languageStrings["delete_job_question"],
        icon: 'question',
        showCancelButton: true,
        confirmButtonColor: '#3085d6',
        cancelButtonColor: '#d33',
        confirmButtonText: languageStrings["delete_confirm_button"],
        cancelButtonText: languageStrings["cancel"]
    }).then((result) => {
        if (result.isConfirmed) {
            $.ajax({
                url: 'api/JobDelete',
                contentType: "application/json; charset=utf-8",
                data: String(selectedJob),
                type: 'POST',
                cache: false,
                complete: function (data) {
                    if (data.status == 200) { //success
                        Swal.fire(
                            languageStrings["job_deleted_caption"],
                            languageStrings["job_deleted_text"],
                            'success'
                        );
                        location.reload();
                    } else if (data.status == 400) { //job currently running
                        Swal.fire(
                            languageStrings["not_possible"],
                            languageStrings["job_delete_error"],
                            'error'
                        );
                    }
                }
            });


        }
    });
}

//click handler for starting/stopping job manually
function startStopJobHandler(event) {
    //api call
    $.ajax({
        url: "api/JobStartStop/" + selectedJob
    })
        .done(function (data) {

        });
}

//click handler for clicking a vm within main panel
function vmClickHandler(event) {
    //stop refresh timer
    clearInterval(eventRefreshTimer);

    selectedVM = $(this).data("vmid");

    //remove active-class from all vms
    $(".vm").removeClass("active");

    //set current element in GUI
    $(this).addClass("active");

    //load required templates
    loadJobTemplates();

    showCurrentEvents();

    //start refresh timer
    eventRefreshTimer = setInterval(showCurrentEvents, 4000);
}

//loads the required templates
function loadJobTemplates() {
    $.ajax({
        url: "Templates/eventsListItem"
    })
        .done(function (data) {
            eventsListItemTemplate = data;
        });

    $.ajax({
        url: "Templates/jobStateTable"
    })
        .done(function (data) {

            //remove language markups
            data = replaceLanguageMarkups(data);

            jobStateTableTemplate = data;
        });

}

//refresh handler for clicking vm in main panel
function showCurrentEvents() {

    //api call
    $.ajax({
        url: "api/BackupJobEvent?id=" + selectedJob + "&jobType=backup"
    })
        .done(function (data) {


            data = jQuery.parseJSON(data);

            //iterate through all events
            var eventsList = [];
            for (var i = 0; i < data.length; i++) {
                //ignore events if wrong vmid
                if (data[i]["vmid"] != selectedVM) {
                    continue;
                }

                //build event object
                var oneEvent = {};
                oneEvent.text = data[i].info;

                switch (data[i].status) {
                    case "successful":
                        oneEvent.successful = true;
                        break;
                    case "inProgress":
                        oneEvent.inProgress = true;
                        break;
                    case "error":
                        oneEvent.error = true;
                        break;
                    case "warning":
                        oneEvent.warning = true;
                        break;
                    case "info":
                        oneEvent.info = true;
                        break;
                }

                //add event to eventsList
                eventsList.unshift(oneEvent);

            }

            //display events
            $("#jobEventList").html(Mustache.render(eventsListItemTemplate, { events: eventsList }));

            //step two: refresh backup job state panel
            renderJobStateTable();

        });


}

//builds the transferrates chart
function buildTransferrateChart() {
    var labels = [];
    //build labels
    for (var i = 0; i < transferrates.length; i++) {
        labels.push("");
    }

    const data = {
        labels: labels,
        datasets: [{
            label: languageStrings["transferrate"],
            fill: true,
            backgroundColor: 'rgb(0, 123, 255)',
            borderColor: 'rgb(0, 123, 255)',
            data: transferrates,
        }, {
                label: languageStrings["processingrate"],
            fill: true,
            backgroundColor: 'rgb(255, 26, 26)',
            borderColor: 'rgb(255, 26, 26)',
            data: processrates,
            }]
    };

    const config = {
        type: 'line',
        data: data,
        options: {
            elements: {
                point: {
                    radius: 0
                }
            }
        }
    };

    if (!ratesChart) { //init chart
        ratesChart = new Chart(document.getElementById('chartCanvas'), config);
    } else { //update chart
        ratesChart.data.labels = labels;
        ratesChart.data.datasets[0].data = transferrates;
        ratesChart.data.datasets[1].data = processrates;
        ratesChart.update();

    }
}


//render job state table
function renderJobStateTable() {

    $.ajax({
        url: "api/BackupJobState?jobId=" + selectedJob
    })
        .done(function (data) {

            //nothing to refresh?
            if (lastJobStateData == data) {
                return;
            } else {
                lastJobStateData = data;
            }

            data = JSON.parse(data);

            //build next run string
            var intervalString;

            if (selectedJobObj["Enabled"]) {
                switch (data["Interval"]["intervalBase"]) {
                    case 0: //hourly
                        intervalString = languageStrings["hourly_at"] + " " + buildTwoDigitsInt(data["Interval"]["minute"]);
                        break;
                    case 1: //daily
                        intervalString = languageStrings["daily_at"] + " " + buildTwoDigitsInt(data["Interval"]["hour"]) + ":" + buildTwoDigitsInt(data["Interval"]["minute"]);
                        break;
                    case 2: //weekly
                        var dayForGui = languageStrings[data["Interval"]["day"]];

                        intervalString = dayForGui + "s " + languageStrings["at_time"] + " " + buildTwoDigitsInt(data["Interval"]["hour"]) + ":" + buildTwoDigitsInt(data["Interval"]["minute"]);
                        break;
                    case 3: //nie
                        intervalString = languageStrings["only_manually"];
                        $("#enableJobButton").attr("disabled", "disabled");
                        break;
                }
            } else {
                //job is disabled
                intervalString = languageStrings["job_disabled_text"];
            }

            //livebackup working
            if (data["LiveBackupActive"]) {
                $("#stopLBButton").css("display", "inline-block");
            } else {
                $("#stopLBButton").css("display", "none");
            }

            var lastRunString = data["LastRun"];
            if (data["IsRunning"]) {
                lastRunString = languageStrings["currently_running"];
            } else if (lastRunString != "") {
                //check if last last run date has to be translated
                //date gets stored at german format. Have to translate it?
                if (languageStrings["language_name"] == "en") {
                    var lastRunDate = moment(data["LastRun"], "DD.MM.YYYY HH:mm:ss");
                    lastRunString = lastRunDate.format("MM/DD/YYYY HH:mm:ss");
                }

            }

            var successString = languageStrings[data["Successful"]];
            if (data["LastRun"] == "") {
                successString = languageStrings["not_applicable"];
            }

            processrates = [];
            transferrates = [];
            if (data["Rates"] && data["Rates"].length > 0) {
                var currentTransferrateString = prettyPrintBytes(data["Rates"][data["Rates"].length - 1]["transfer"]) + "/s";
                var tempRates = data["Rates"];

                //convert data rates to MB
                for (var i = 0; i < tempRates.length; i++) {
                    if (tempRates[i]["process"] > 0 || tempRates[i]["transfer"] > 0) {
                        transferrates.push(tempRates[i]["transfer"] / 1000000);
                        processrates.push(tempRates[i]["process"] / 1000000);
                    }
                }
            }

            $("#jobStateTable").html(Mustache.render(jobStateTableTemplate, { running: data["IsRunning"], interval: intervalString, lastRun: lastRunString, lastState: successString, lastTransferrate: currentTransferrateString }));


            //register click handler for clicking link to show last execution details
            $("#lastExecutionDetailsLink").click(showLastRunDetails);

            //set state color and set start/stop button caption
            if (data["LastRun"] == "" && !data["IsRunning"]) {
                $("#jobDetailsRow").css("background-color", "#e6e6e6");
                $("#jobDetailsRow").removeClass("detailsRowRunning");

                //change start/stop button caption
                $("#startStopButtonCaption").html(languageStrings["start_job_button"]);
                $("#startStoJobpButton").addClass("btn-primary");
                $("#startStopJobButton").removeClass("btn-danger");
            } else {
                if (data["Successful"] == "successful") {
                    $("#jobDetailsRow").css("background-color", "#ccffcc");
                    $("#jobDetailsRow").removeClass("detailsRowRunning");

                    //change start/stop button caption
                    $("#startStopButtonCaption").html(languageStrings["start_job_button"]);
                    $("#startStopJobButton").addClass("btn-primary");
                    $("#startStopJobButton").removeClass("btn-danger");
                } else {
                    $("#jobDetailsRow").css("background-color", "#ffb3b3");
                    $("#jobDetailsRow").removeClass("detailsRowRunning");

                    //change start/stop button caption
                    $("#startStopButtonCaption").html(languageStrings["start_job_button"]);
                    $("#startStopJobButton").addClass("btn-primary");
                    $("#startStopJobButton").removeClass("btn-danger");
                }

                if (data.IsRunning) {
                    $("#jobDetailsRow").addClass("detailsRowRunning");

                    //change start/stop button caption
                    $("#startStopButtonCaption").html(languageStrings["stop_job_button"]);
                    $("#startStopJobButton").addClass("btn-danger");
                    $("#startStopJobButton").removeClass("btn-primary");
                }
            }

            //build the chart for transferrates
            buildTransferrateChart();

        });
}

//click handler for showing stats for last execution
function showLastRunDetails() {
    //show dialog box
    Swal.fire({
        title: languageStrings["last_run_caption"],
        html: "<div id='executionDetailsPopUp'></div>",
        confirmButtonColor: '#3085d6',
        allowOutsideClick: true,
        allowEscapeKey: true,
        confirmButtonText: languageStrings["close"],
    });

    //load form
    $.ajax({
        url: "Templates/JobExecutionDetailsForm"
    })
        .done(function (detailsForm) {

            //replace language markups
            detailsForm = replaceLanguageMarkups(detailsForm);

            //parse job data
            var jobData = JSON.parse(lastJobStateData);

            var bytesTransfered = prettyPrintBytes(jobData["LastBytesTransfered"]);
            var bytesProcessed = prettyPrintBytes(jobData["LastBytesProcessed"]);

            if (jobData["LastStop"]) {
                //calculate compression efficiency
                var compressionEfficiency = parseFloat(((jobData["LastBytesProcessed"] - jobData["LastBytesTransfered"]) / jobData["LastBytesProcessed"]) * 100).toFixed(2);

                //calculate execution duration
                var startDate = moment(jobData["LastRun"], "DD.MM.YYYY HH:mm:ss");
                var endDate = moment(jobData["LastStop"], "DD.MM.YYYY HH:mm:ss");

                var duration = prettyPrintDuration(endDate.diff(startDate));
                duration = duration.replace(".", ",");

                var renderedDetailsForm = Mustache.render(detailsForm, { bytesTransfered: bytesTransfered, bytesProcessed: bytesProcessed, compressionEfficiency: compressionEfficiency, duration: duration });

                $("#executionDetailsPopUp").html(renderedDetailsForm)

            } else { //job not started yet
                var renderedDetailsForm = Mustache.render(detailsForm, { bytesTransfered: bytesTransfered, bytesProcessed: bytesProcessed, compressionEfficiency: "0", duration: "-" });

                $("#executionDetailsPopUp").html(renderedDetailsForm)
            }



        });
}

//pretty print milliseconds
function prettyPrintDuration(ms) {
    let seconds = (ms / 1000).toFixed(1);
    let minutes = (ms / (1000 * 60)).toFixed(1);
    let hours = (ms / (1000 * 60 * 60)).toFixed(1);
    let days = (ms / (1000 * 60 * 60 * 24)).toFixed(1);
    if (seconds < 60) return seconds + " " + languageStrings["seconds"];
    else if (minutes < 60) return minutes + " " + languageStrings["minutes"];
    else if (hours < 24) return hours + " " + languageStrings["hours"];
    else return days + " " + languageStrings["days"];
}

//pretty prints file size
function prettyPrintBytes(bytes, si = true, dp = 2) {
    const thresh = si ? 1000 : 1024;

    if (Math.abs(bytes) < thresh) {
        return bytes + ' B';
    }

    const units = si
        ? ['kB', 'MB', 'GB', 'TB', 'PB', 'EB', 'ZB', 'YB']
        : ['KiB', 'MiB', 'GiB', 'TiB', 'PiB', 'EiB', 'ZiB', 'YiB'];
    let u = -1;
    const r = 10 ** dp;

    do {
        bytes /= thresh;
        ++u;
    } while (Math.round(Math.abs(bytes) * r) / r >= thresh && u < units.length - 1);


    return bytes.toFixed(dp) + ' ' + units[u];
}


//init login form
function initLoginForm(showError) {
    //chech whether 2fa is activated or not
    $.ajax({
        url: "api/MFAActivated",
        success: function (result) {
            //2fa activated
            showLoginForm(true, showError);
        },
        error: function (jqXHR, exception) {
            //2fa not activated
            showLoginForm(false, showError);
        }
    });    

}

//shows the login form with or without mfa
function showLoginForm(mfa, showError) {
    //load login form
    $.ajax({
        url: "Templates/loginForm"
    }).done(function (loginForm) {
        //replace language markups
        loginForm = replaceLanguageMarkups(loginForm);

        Swal.fire({
            title: 'Login',
            html: loginForm,
            showLoaderOnConfirm: true,
            confirmButtonText: languageStrings["login"],
            allowEnterKey: true,
            didOpen: () => {
                $("#loginText").focus();

                //show otp input field?
                if (mfa) {
                    $("#otpText").css("display", "block");
                }
            },
            preConfirm: () => {
                const login = Swal.getPopup().querySelector('#loginText').value;
                const password = Swal.getPopup().querySelector('#passwordText').value;
                const otp = Swal.getPopup().querySelector('#otpText').value;

                if (!login || !password) {
                    Swal.showValidationMessage(languageStrings["login_failed"]);
                    return;
                }

                if (mfa && !otp) {
                    Swal.showValidationMessage(languageStrings["login_failed"]);
                    return;
                }
                var encodedLogin;

                //mfa activated? build login string
                if (mfa) {
                    encodedLogin = btoa(login) + ":" + btoa(password) + ":" + btoa(otp);
                } else {
                    encodedLogin = btoa(login) + ":" + btoa(password);
                }               


                return encodedLogin;
            }
        }).then((result) => {
            Swal.fire({
                title: languageStrings["login_title"],
                text: languageStrings["login_text"],
                allowOutsideClick: false,
                allowEscapeKey: false,
                allowEnterKey: false,
                onOpen: () => {
                    swal.showLoading()
                }
            })
            ajaxLogin(result.value);
        });

        //register enter handler
        $(".swal2-popup").on("keypress", function (event) {
            if (event.which == 13) {
                $(".swal2-confirm").click();
            }
        });

        if (showError) {
            Swal.showValidationMessage(languageStrings["login_failed"]);
        }
    });
}

//async function for login ajax call
function ajaxLogin(encodedLogin) {
    try {
        $.ajax({
            url: 'api/Login',
            contentType: "application/json",
            data: "'" + encodedLogin + "'",
            type: 'POST',
            cache: false,
            success: function (result) {
                location.reload();
            },
            error: function (jqXHR, exception) {
                initLoginForm(true);
            }
        });
    } catch (error) {
        console.error(error);
    }
}