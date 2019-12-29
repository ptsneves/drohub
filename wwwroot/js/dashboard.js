$(async function () {
    var MapClass = function () {
        let data_function_table = {};
        let map = null;

        let _MarkerStoreClass = function () {
            marker_dict = {};
            function getDictKey(device_type, device_serial) {
                return `${device_type}-${device_serial}`;
            }
            function _getMarker(device_type, device_serial) {
                let key = getDictKey(device_type, device_serial);
                if (marker_dict.hasOwnProperty(key))
                    return marker_dict[key];
                else
                    return null;
            }

            function _setMarker(marker, device_type, device_serial) {
                if (device_type == null || device_serial == null)
                    throw new Error(`Cannot set marker with with following key ${device_type} ${device_serial}`);
                marker_dict[getDictKey(device_type, device_serial)] = marker;
            }

            return {
                init: function(map_to_to_attach) {
                    map = map_to_to_attach
                },
                getMarker: _getMarker,
                setMarker: _setMarker,
                setListener: function(device_type, device_serial, event_name, listener_func) {
                    let marker = getMarker(device_type, device_serial);
                    if (!marker)
                        throw new Error("Cannot set a listener on non-existing marker");
                    google.maps.event.addListener(marker, event_name, listener_func);
                },
                renderPositionMarker: function(_, element) {
                    let device_coords = JSON.parse(element.attr('data-telemetry'));
                    let device_type = element.data('device-type');
                    let device_serial = element.data('serial');

                    if (device_coords == null || device_type == null || device_serial == null)
                        return;

                    let icon_url = `${window.location.origin}/images/drone-svgrepo-com.svg`
                    let marker_icon = {
                        url: icon_url, // url
                        scaledSize: new google.maps.Size(50, 50), // scaled size
                        origin: new google.maps.Point(0, 0), // origin
                        anchor: new google.maps.Point(0, 0) // anchorscaledSize: new google.maps.Size(50, 50), // scaled size
                    }

                    if ((marker = _getMarker(device_type, device_serial)) != null) {
                        marker.setPosition({ lat: device_coords.Latitude, lng: device_coords.Longitude });
                        return;
                    }
                    else {
                        let marker = new google.maps.Marker({
                            position: { lat: device_coords.Latitude, lng: device_coords.Longitude },
                            icon: marker_icon,
                            map: map,
                            serial: device_serial,
                            device_type: device_type
                        });
                        _setMarker(marker, device_type, device_serial);
                    }
                }
            }
        }();

        _MarkerStoreClass.init(map);

        function _centerMapOnDevice(index, element) {
            element = $(element);
            let device_marker = _MarkerStoreClass.getMarker(element.data('device-type'), element.data('serial'));
            if (device_marker)
                _centerMapOnMarker(device_marker);
        }
        data_function_table["centerMapOnDevice"] = _centerMapOnDevice;

        function _centerMapOnMarker(marker) {
            marker.map.panTo(marker.position);
            marker.map.setZoom(20);
        }

        function _updatePositionMapMarker() {
            $('.position-text').each(
                function (_, element) {
                    element = $(element);
                    _MarkerStoreClass.renderPositionMarker(_, element);
                }
            );
        }

        return {
            updatePositionMapMarker: _updatePositionMapMarker,
            init: function() {
                function perElementInit (index, element) {
                    function _initializeMapMarkerActionButton(index, element) {
                        element = $(element);
                        element.click(data_function_table[element.data('toggle')].bind(null, index, element));
                    }

                    map = new google.maps.Map(element, {
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

                    _updatePositionMapMarker();

                    $('.map-marker-action-button').each(_initializeMapMarkerActionButton);
                }
                $('.google-map').each(perElementInit);
            },
            getMap: function(device_serial) {
                return map;
            }
        }
    }();

    TelemetryClass = function () {
        var _FunctionTable = {};
        _FunctionTable["renderBatteryLevel"] = _renderBatteryLevel;
        _FunctionTable["renderRadioSignal"] = _renderRadioSignal;
        _FunctionTable["renderFlyingState"] = _renderFlyingState;
        _FunctionTable["renderPlaneIcon"] = _renderPlaneIcon;
        _FunctionTable["renderPositionText"] = _renderPositionText;
        _signalr_connection = null;
        _map_class_instance = null;

        function _makeClassActive(element, class_to_activate, AvailableClasses) {
            let classes_to_delete = new Set(AvailableClasses);
            classes_to_delete.delete(class_to_activate)
            classes_to_delete.forEach(function (item) {
                element.removeClass(item);
            });
            element.addClass(class_to_activate);
        }

        function _renderTelemetry(index, element) {
            element = $(element);
            fn = element.data('renderer');
            if (!fn)
                return;
            _FunctionTable[fn](index, element);
        }

        function _renderRadioSignal(_, element) {
            element = $(element);
            radio_signal = JSON.parse(element.attr('data-telemetry'));
            if (!radio_signal)
                return;

            if (radio_signal.__isset['rssi'] == true) {
                element.removeClass('label-default');
                element.addClass('label-primary');
                element.text(radio_signal.Rssi + " dbm");
            }
            else {
                element.removeClass('label-default');
                if (radio_signal.SignalQuality > 3) {
                    element.addClass('label-success');
                }
                else if (radio_signal.SignalQuality >= 2 && radio_signal.SignalQuality <= 3)
                    element.addClass('label-warning');
                else
                    element.addClass('label-danger');

                element.text(radio_signal.SignalQuality + " Level");
            }
        }

        function _renderFlyingState(_, element) {
            element = $(element);
            flying_state = JSON.parse(element.attr('data-telemetry'));
            if (!flying_state)
                return;

            element.removeClass('label-default');
            element.addClass('label-primary');
            switch (flying_state.State) {
                case 0:
                    element.text("Landed");
                    break;
                case 1:
                    element.text("Taking Off");
                    break;
                case 2:
                    element.text("Hovering");
                    break;
                case 3:
                    element.text("Flying");
                    break;
                case 4:
                    element.text("Landing");
                    break;
                case 5:
                    element.text("Emergency");
                    break;
                case 6:
                    element.text("User Takeoff");
                    break;
                case 7:
                    element.text("Motor Ramping");
                    break;
                case 8:
                    element.text("Emergency Landing");
                    break;
                default:
                    console.log("")
            }
        }

        function _renderPlaneIcon(_, element) {
            element = $(element);
            flying_state = JSON.parse(element.attr('data-telemetry'));
            if (!flying_state)
                return;

            const AvailableClasses = new Set(["text-muted", 'text-warning', "text-success", "blinking"]);
            if (flying_state.State == 0) {
                _makeClassActive(element, 'text-success', AvailableClasses);
            }
            else {
                _makeClassActive(element, 'text-warning', AvailableClasses);
                element.addClass('blinking');
            }
        }

        function _renderBatteryLevel(_, element) {
            element = $(element);
            battery_level = JSON.parse(element.attr('data-telemetry'));
            if (!battery_level)
                return;
            console.log(battery_level)
            element.removeClass('label-default');
            if (battery_level.BatteryLevelPercent > 75) {
                element.addClass('label-success');
            }
            else if (battery_level.BatteryLevelPercent > 35 && battery_level.BatteryLevelPercent <= 75)
                element.addClass('label-warning');
            else
                element.addClass('label-danger');
            element.text(battery_level.BatteryLevelPercent);
        }

        function _renderPositionText(_, element) {
            element = $(element);
            drone_coords = JSON.parse(element.attr('data-telemetry'));
            if (drone_coords == null)
                return;

            console.log(drone_coords);
            element.text(`${drone_coords.Latitude.toFixed(4)}N ${drone_coords.Longitude.toFixed(4)}W ${drone_coords.Altitude.toFixed(3)}m`);

        }

        function _updateTelemetry(selector, telemetry_json) {
            telemetry = JSON.parse(telemetry_json);
            if (!telemetry)
                return;

            $(`${selector}[data-serial="${telemetry.Serial}"]`).each(
                function (index, element) {
                    element = $(element);
                    element.attr('data-telemetry', telemetry_json);
                    _renderTelemetry(index, element);
                }
            );
        }

        return {
            updateTelemetry: _updateTelemetry,
            init: function (signalr_connection, map_class_instance) {
                _map_class_instance = map_class_instance;
                _signalr_connection = signalr_connection;

                _signalr_connection.on("DronePosition", function (message) {
                    console.warn("hello");
                    _updateTelemetry('.position-text', message);
                    _map_class_instance.updatePositionMapMarker();
                });

                _signalr_connection.on("DroneBatteryLevel", function (message) {
                    _updateTelemetry('.battery-level-text', message);
                });

                _signalr_connection.on("DroneRadioSignal", function (message) {
                    radio_signal = JSON.parse(message);
                    _updateTelemetry('.radio-signal-text', message);
                });

                _signalr_connection.on("DroneFlyingState", function (message) {
                    flying_state = JSON.parse(message);
                    _updateTelemetry('.flying-state-text', message);
                });

                _signalr_connection.on("DroneReply", function (message) {
                    drone_reply = JSON.parse(message);
                    //pingService
                });
                $('.telemetry-single-data').each(_renderTelemetry);
            }
        }
    }();

    JanusVideoClass = function () {
        function _init(index, element) {
            if (index > 1)
                throw new Error("Cannot have multiple elements with janus");
            element = $(element);
            initJanus(element.data('janus-url'), element.data('stun-server-url'));
        }
        return {
            init: function () {
                $('.janus-section').each(_init);
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
    JanusVideoClass.init();
    MapClass.init();
    TelemetryClass.init(SignalRConnectionClass.getConnection(), MapClass);
    
    //test
    // test_connection = SignalRConnectionClass.getConnection();
    // for (i = 0; i < 10; i++) {
    //     setTimeout(function () {
    //     }, 1000);
    //     position = `{"__isset":{"latitude":false,"longitude":false,"altitude":false,"serial":false,"timestamp":false},"Id":412,"Latitude":48.878692719478146,"Longitude":2.4971${i},"Altitude":5.1912965774536133,"Serial":"000000000000000000","Timestamp":1577126313}`;
    //     test_connection.invoke("DronePosition", position)
    //     MapClass.updatePositionMapMarker();
    // }
    // TelemetryClass.updateTelemetry('.battery-level-text', '{ "__isset": { "battery_level_percent": true, "serial": false, "timestamp": false }, "Id": 12, "BatteryLevelPercent": 50, "Serial": "000000000000000000", "Timestamp": 1577126258 }');
    // TelemetryClass.updateTelemetry('.radio-signal-text', '{ "__isset": { "battery_level_percent": true, "serial": false, "timestamp": false }, "Id": 12, "SignalQuality": 5, "Serial": "000000000000000000", "Timestamp": 1577126258 }');
    // TelemetryClass.updateTelemetry('.flying-state-text', '{ "__isset": { "battery_level_percent": true, "serial": false, "timestamp": false }, "Id": 12, "State": 0, "Serial": "000000000000000000", "Timestamp": 1577126258 }');
})