//starts the import backup process
function startImportBackupProcess() {
    //load content for overlay
    $.ajax({
        url: "Templates/selectImportSource"
    }).done(function (data) {
        data = replaceLanguageMarkups(data);

        Swal.fire({
            title: languageStrings["import_backup"],
            html: data,
            confirmButtonColor: '#3085d6',
            allowOutsideClick: true,
            allowEscapeKey: true,
            confirmButtonText: languageStrings["close"],
        });

        //build folder browser
        buildFolderBrowser();

        //register change on backup location type selection
        $("#sbLocationType").on("change", function (event) {
            var targetType = $(this).children("option:selected").data("locationtype");

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
            }
        });
    });
}