

function getCoordinate(file, coord_type) {
    if (typeof(file.media_info.metadata.location) != 'undefined')
        return eval("file.media_info.metadata.location." + coord_type)
}

function isPictureFile(file_response) {
    return !(file_response[".tag"] != "file" || typeof(file_response.media_info) === 'undefined' ||
        file_response.media_info[".tag"] != 'metadata' ||
        file_response.media_info.metadata[".tag"] != 'photo')
}