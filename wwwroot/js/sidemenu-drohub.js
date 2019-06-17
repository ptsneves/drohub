function generateRows(device) {
}
function populateDevices(get_device_list_url, gallery_url) {
    $.getJSON(get_device_list_url, function (device_list) {
        $.each(device_list, function (i, device) {
            device_gallery_url = gallery_url + '/' + device.Id +
            $('#devices-list').append('<li><a href=' + device_gallery_url + '> <i class="fa fa-circle-o"></i>' + device.Name + ' </a ></li > ')
        })
    })
}