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
    });
}