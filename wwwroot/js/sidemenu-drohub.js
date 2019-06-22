function generateRows(device) {
}
function populateDevices(get_device_list_url, device_gallery_url) {
    $.getJSON(get_device_list_url)
        .done(function (device_list) {
            $.each(device_list, function (i, device) {
                device_gallery_url = gallery_url + '/' + device.id
                li = '<li><a href=' + device_gallery_url + '> <i class="fa fa-plug"></i>' + device.name + ' </a ></li > '
                    $('#devices-list').append(li)
            })
        })
        .fail(function () {
            console.log("dsadd")
        })
}