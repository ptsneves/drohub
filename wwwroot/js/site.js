// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

$(async function () {
    ModalClass = function () {
        let PlaceholderElement = $('#modal-placeholder');
        let should_reload = false;
        function makeModal(event) {
            let url = $(this).data('url');
            should_reload = $(this).data('reload') === true;
            $.get(url).done(function (data) {
                PlaceholderElement.html(data);
                PlaceholderElement.find('.modal').modal({ focus: true, show: true, keyboard: true });
            });
        }

        function _submit (event) {
            event.preventDefault();

            let form = $(this).parents('.modal').find('form');
            let actionUrl = form.attr('action');
            let dataToSend = form.serialize();

            $.post(actionUrl, dataToSend).done(function (data) {
                let newBody = $('.modal-body', data);
                PlaceholderElement.find('.modal-body').replaceWith(newBody);

                let isValid = newBody.find('[name="IsValid"]').val() == 'True';
                if (isValid) {
                    if (should_reload === true)
                        location.reload();
                    else
                        PlaceholderElement.find('.modal').modal('hide');
                }
            });
        }
        return {
            "init": function () {
                PlaceholderElement.on('click', '[data-save="modal"]', _submit);
                $('a[data-toggle="ajax-modal"]').click(makeModal);
                $('button[data-toggle="ajax-modal"]').click(makeModal);
            }
        }
    }();

    AJAXRequestClass = function () {
        function _followActionAndDoNothing(event) {

            if ($(this).is("form")) {
                var url = $(this).attr('action');
                $.post($(this).attr('action'), $(this).serialize());
                return false;
            }
            else
                $.get($(this).data('url'));
        }
        $('form[data-toggle="ajax-request"]').submit(_followActionAndDoNothing);
        $('a[data-toggle="ajax-request"]').click(_followActionAndDoNothing);
        $('button[data-toggle="ajax-request"]').click(_followActionAndDoNothing);
    }();


    SignalRConnectionClass = function () {
        _connection = null
        return {
            init: async function () {
                _connection = new signalR.HubConnectionBuilder().withUrl("/telemetryhub").
                    configureLogging(signalR.LogLevel.Debug).build();
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
            let date = new Date(unix_timestamp);
            return date.toLocaleString();
        }

        function _convertUnixToLocalDate(unix_timestamp) {
            let date = new Date(unix_timestamp);
            return date.toLocaleDateString();
        }

        function _setTextToLocalTime(index, element) {
            element = $(element);
            let unix_time = element.data('unix-time');
            if (unix_time)
                element.text(_convertUnixToLocalTime(unix_time));
        }

        function _setTextToLocalDate(index, element) {
            element = $(element);
            let unix_time = element.data('unix-time');
            if (unix_time)
                element.text(_convertUnixToLocalDate(unix_time));
        }

        $('.local-time-text').each(_setTextToLocalTime);
        $('.local-date-text').each(_setTextToLocalDate);

        return {
            convertUnixToLocalTime: _convertUnixToLocalTime,
            convertUnixToLocalDate: _convertUnixToLocalDate,
        }
    }();

    ModalClass.init();
});

function isFunction(o) {
    return typeof o === "function";
}

function addFunctionToArray(array, new_f) {
    if (isFunction(new_f))
        array.push(new_f);
    else
        console.error("Tried to add non function when a function was expected");
}

function callFunc(a, data) {
    a.forEach(func => {
        if (data)
            func(data);
        else
            func();
    });
}