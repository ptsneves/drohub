

function getCoordinate(file, coord_type) {
    if (typeof(file.media_info.metadata.location) != 'undefined')
        return eval("file.media_info.metadata.location." + coord_type)
}

function isPictureFile(file_response) {
    return !(file_response[".tag"] != "file" || typeof(file_response.media_info) === 'undefined' ||
        file_response.media_info[".tag"] != 'metadata' ||
        file_response.media_info.metadata[".tag"] != 'photo')
}

async function getFolders(dbx, folder_path, folder_callback) {
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

async function appendThumbnailsToElement(dbx, thumbnail_path_list, element_id_prefix) {
    dbx.filesGetThumbnailBatch({ "entries": thumbnail_path_list }).then(function (response) {
        entries = response["entries"]
        for (var key in entries) {
            current_entry = entries[key]

            if (current_entry[".tag"] == "success") {
                var img = document.createElement('img');
                img.src = `data:image/jpeg;base64, ${current_entry["thumbnail"]}`;
                document.getElementById(element_id_prefix + current_entry["metadata"]["id"]).appendChild(img)
            }
        }
    })
    .catch(function (error) {
        console.log("When trying to get the thumbnail got error:");
        console.log(error);
    })
}

async function getFile(dbx, path, callback_function) {
    dbx.filesListFolder({ "path": path, include_media_info: true })
        .then(function (response) {
            for (let i = 0; i < response.entries.length; i++) {
                if (isPictureFile(response.entries[i]))
                    callback_function(response.entries[i]);
            }
        })
        .catch(function (error) {
            console.error(error);
        });
}
