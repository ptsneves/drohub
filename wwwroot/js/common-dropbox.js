

function getCoordinate(file, coord_type) {
    if (typeof(file.media_info.metadata.location) != 'undefined')
        return eval("file.media_info.metadata.location." + coord_type)
}

function isPictureFile(file_response) {
    return !(file_response[".tag"] != "file" || typeof(file_response.media_info) === 'undefined' ||
        file_response.media_info[".tag"] != 'metadata' ||
        file_response.media_info.metadata[".tag"] != 'photo')
}

function getFolders(folder_path, folder_callback) {
    dbx.filesListFolder({ path: folder_path })
        .then(function (response) {

            for (var i = 0; i < response.entries.length; i++) {
                if (response.entries[i][".tag"] == "folder") {
                    folder_callback(response.entries[i]);
                }
            }
        })
        .catch(function (error) {
            console.error(error);
        });
}