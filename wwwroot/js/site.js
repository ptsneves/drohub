// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

$(async function () {
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


    SignalRConnectionClass = function () {
        _connection = null
        return {
            init: async function () {
                _connection = new signalR.HubConnectionBuilder().withUrl("/telemetryHub").
                    configureLogging(signalR.LogLevel.Information).build();

                await _connection.start().then(function () {
                    console.log("Notifications started SIGNALR");

                }).catch(function (err) {
                    return console.error(err.toString());
                });
            },
            getConnection: function () {
                if (!_connection)
                    throw new Error("Cannot get a connection that does not exist");
                return _connection;
            }
        }
    }();

    await SignalRConnectionClass.init();

    TimePresenterClass = function () {
        function _convertUnixToLocalTime(unix_timestamp) {
            // https://stackoverflow.com/a/847196/227990
            // Create a new JavaScript Date object based on the timestamp
            // multiplied by 1000 so that the argument is in milliseconds, not seconds.
            var date = new Date(unix_timestamp);
            return date.toLocaleString();
        }

        function _initElement(index, element) {
            element = $(element);
            let unix_time = element.data('unix-time');
            if (!unix_time)
                return;
            element.text(_convertUnixToLocalTime(unix_time));
        }

        $('.local-time-text').each(_initElement);

        return {
            convertUnixToLocalTime: _convertUnixToLocalTime,
        }
    }();

    ModalClass.init();
});