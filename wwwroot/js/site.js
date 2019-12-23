// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

$(function () {

    function initializeMap(index, element) {
        let map = new google.maps.Map(element, {
            zoom: 8,
            center: { lat: 40.5, lng: -7 },
            mapTypeId: 'satellite'
        });

        map.addListener("rightclick",
            function (event) {
                var lat = event.latLng.lat();
                var lng = event.latLng.lng();
                // populate yor box/field with lat, lng
                console.log("Lat=" + lat + "; Lng=" + lng);
                $('#modal-longitude-value').text(lng);
                $('#modal-latitude-value').text(lat);
                $('#modal-move-to-position').modal({ focus: true, show: true, keyboard: true });
            }
        );

        $('.initial-position').each(
            function () {
                updatePositionData($(this).html(), map);
            }
        );
    }
    $('.google-map').each(initializeMap);

    function followActionAndDoNothing(event) {
        var url = $(this).data('url');
        $.get(url);
    }
    $('a[data-toggle="ajax-request"]').click(followActionAndDoNothing);
    $('button[data-toggle="ajax-request"]').click(followActionAndDoNothing);

    var placeholderElement = $('#modal-placeholder');
    function makeModal (event) {
        var url = $(this).data('url');
        $.get(url).done(function (data) {
            placeholderElement.html(data);
            placeholderElement.find('.modal').modal({ focus: true, show: true, keyboard: true });
        });
    }
    $('a[data-toggle="ajax-modal"]').click(makeModal);
    $('button[data-toggle="ajax-modal"]').click(makeModal);

    placeholderElement.on('click', '[data-save="modal"]', function (event) {
        event.preventDefault();

        var form = $(this).parents('.modal').find('form');
        var actionUrl = form.attr('action');
        var dataToSend = form.serialize();

        $.post(actionUrl, dataToSend).done(function (data) {
            var newBody = $('.modal-body', data);
            placeholderElement.find('.modal-body').replaceWith(newBody);

            var isValid = newBody.find('[name="IsValid"]').val() == 'True';
            if (isValid) {
                placeholderElement.find('.modal').modal('hide');
            }
        });
    });
});