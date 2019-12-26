// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

$(function () {
    ModalClass = function () {
        function followActionAndDoNothing(event) {
            var url = $(this).data('url');
            $.get(url);
        }

        var PlaceholderElement = $('#modal-placeholder');
        function makeModal(event) {
            var url = $(this).data('url');
            $.get(url).done(function (data) {
                PlaceholderElement.html(data);
                PlaceholderElement.find('.modal').modal({ focus: true, show: true, keyboard: true });
            });
        }
        return {
            "init": function () {
                PlaceholderElement.on('click', '[data-save="modal"]', function (event) {
                    event.preventDefault();

                    var form = $(this).parents('.modal').find('form');
                    var actionUrl = form.attr('action');
                    var dataToSend = form.serialize();

                    $.post(actionUrl, dataToSend).done(function (data) {
                        var newBody = $('.modal-body', data);
                        PlaceholderElement.find('.modal-body').replaceWith(newBody);

                        var isValid = newBody.find('[name="IsValid"]').val() == 'True';
                        if (isValid) {
                            PlaceholderElement.find('.modal').modal('hide');
                        }
                    });
                });

                $('a[data-toggle="ajax-request"]').click(followActionAndDoNothing);
                $('button[data-toggle="ajax-request"]').click(followActionAndDoNothing);
                $('a[data-toggle="ajax-modal"]').click(makeModal);
                $('button[data-toggle="ajax-modal"]').click(makeModal);
            }
        }
    }();
    ModalClass.init();
});