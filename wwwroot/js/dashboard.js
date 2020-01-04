$(async function () {
    var MapClass = function () {
        let data_function_table = {};
        let maps = {};

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
                init: function (maps_to_to_attach) {
                    maps = maps_to_to_attach
                },
                getMarker: _getMarker,
                setMarker: _setMarker,
                setListener: function (device_type, device_serial, event_name, listener_func) {
                    let marker = getMarker(device_type, device_serial);
                    if (!marker)
                        throw new Error("Cannot set a listener on non-existing marker");
                    google.maps.event.addListener(marker, event_name, listener_func);
                },
                renderPositionMarker: function (_, element) {
                    let device_coords = JSON.parse(element.attr('data-telemetry'));
                    let device_type = element.data('device-type');
                    let device_serial = element.data('serial');
                    let device_svg_marker = element.data('marker-svg-path');

                    if (device_coords == null || device_type == null || device_serial == null)
                        return;

                    let icon_url = `${window.location.origin}/${device_svg_marker}`
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
                            map: maps[device_serial],
                            serial: device_serial,
                            device_type: device_type
                        });
                        _setMarker(marker, device_type, device_serial);
                    }
                }
            }
        }();
        _MarkerStoreClass.init(maps);

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
            init: function () {
                function perElementInit(index, element) {
                    function _initializeMapMarkerActionButton(index, element) {
                        element = $(element);
                        element.click(data_function_table[element.data('toggle')].bind(null, index, element));
                    }
                    jquery_element = $(element);
                    serial_number = jquery_element.data('serial');
                    maps[serial_number] = new google.maps.Map(element, {
                        zoom: 8,
                        serial: serial_number,
                        center: { lat: 40.5, lng: -7 },
                        mapTypeId: 'satellite'
                    });

                    maps[serial_number].addListener("rightclick",
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
            getMap: function (device_serial) {
                return maps[device_serial];
            }
        }
    }();

    ElementClassRangeClass = function () {
        let _available_classes = new Set();
        let _classes = [];
        function _makeClassActive(element, classes_to_activate) {
            //TODO consolidate this with a filter. What a mess..
            _available_classes.forEach(function (item) {
                element.removeClass(item);
            });
            element.addClass(classes_to_activate.join(' '));
        }
        return {
            addStep: function (min, max, element_classes) {
                split_classes = element_classes.split(/\s+/).filter(Boolean);
                _classes.push({ min: min, max: max, class: split_classes });
                split_classes.forEach(item => _available_classes.add(item));
            },
            addRegularSteps: function (min, max, classes) {
                let step_size = (max - min) / classes.length;
                for (i = 0; i < classes.length; i++) {
                    this.addStep(min + step_size * i, min + step_size * (i + 1), classes[i]);
                }
            },
            addClassToElement: function (element, value) {
                for (i = 0; i < _classes.length; i++) {
                    if (value >= _classes[i].min && value < _classes[i].max) {
                        _makeClassActive(element, _classes[i].class);
                        return;
                    }
                }
            }
        }
    };

    TelemetryClass = function () {
        var _FunctionTable = {};
        _FunctionTable["renderBatteryLevel"] = _renderBatteryLevel;
        _FunctionTable["renderBatteryLevelIcon"] = _renderBatteryLevelIcon;
        _FunctionTable["renderRadioSignal"] = _renderRadioSignal;
        _FunctionTable["renderRadioSignalIcon"] = _renderRadioSignalIcon;
        _FunctionTable["renderFlyingState"] = _renderFlyingState;
        _FunctionTable["renderPlaneIcon"] = _renderPlaneIcon;
        _FunctionTable["renderPositionText"] = _renderPositionText;
        _signalr_connection = null;
        _map_class_instance = null;

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

            flying_state_class_range = ElementClassRangeClass();
            flying_state_class_range.addStep(0, 1, "text-success");
            flying_state_class_range.addStep(1, 8, "text-warning blinking");
            flying_state_class_range.addClassToElement(element, flying_state.State);
        }

        function _renderBatteryLevelIcon(_, element) {
            element = $(element);
            battery_level = JSON.parse(element.attr('data-telemetry'));

            if (!battery_level)
                return;
            battery_level_class_range = ElementClassRangeClass();
            battery_level_class_range.addRegularSteps(0, 100, [
                    'fas fa-battery-empty blinking text-danger blinking',
                    'fas fa-battery-quarter text-warning blinking',
                    'fas fa-battery-half text-warning',
                    'fas fa-battery-three-quarters text-success',
                    'fas fa-battery-full text-success',
                ]
            );

            battery_level_class_range.addClassToElement(element, battery_level.BatteryLevelPercent);
        }

        function _renderRadioSignalIcon(_, element) {
            element = $(element);

            let rssi_class_range = ElementClassRangeClass();
            rssi_class_range.addRegularSteps(-96, -40, ["fas fa-wifi-1", "fas fa-wifi-2", "fas fa-wifi"]);

            let signal_quality_class_range = ElementClassRangeClass();
            signal_quality_class_range.addStep(1, 2, "text-danger");
            signal_quality_class_range.addStep(2, 3, "text-warning");
            signal_quality_class_range.addStep(3, 5, "text-success");

            let radio_signal = JSON.parse(element.attr('data-telemetry'));
            if (!radio_signal)
                return;

            if (radio_signal.__isset.rssi == true && radio_signal.Rssi) {
                console.warn(radio_signal.Rssi)
                rssi_class_range.addClassToElement(element, radio_signal.Rssi);
            }

            if (radio_signal.__isset.signal_quality && radio_signal.SignalQuality) {
                signal_quality_class_range.addClassToElement(element, radio_signal.SignalQuality);
            }
        }

        function _renderBatteryLevel(_, element) {
            element = $(element);
            battery_level = JSON.parse(element.attr('data-telemetry'));
            if (!battery_level)
                return;
            battery_level_class_range = ElementClassRangeClass();
            battery_level_class_range.addStep(0, 35, 'label-danger');
            battery_level_class_range.addStep(35, 75, 'label-warning');
            battery_level_class_range.addStep(75, 100, 'label-success');
            battery_level_class_range.addClassToElement(element, battery_level.BatteryLevelPercent);
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
                    _updateTelemetry('.position-text', message);
                    _map_class_instance.updatePositionMapMarker();
                });

                _signalr_connection.on("DroneBatteryLevel", function (message) {
                    _updateTelemetry('.battery-level-text', message);
                    _updateTelemetry('.battery-level-icon', message);
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
        function _initJanus(index, element) {
            if (index > 1)
                throw new Error("Cannot have multiple elements with janus");
            element = $(element);
            initJanus(element.data('janus-url'), element.data('stun-server-url'));
        }

        function makeElementFullScreen(webrtc_video_element) {
            if (document.fullscreenElement
                || document.webkitFullscreenElement
                || document.mozFullScreenElement
                || document.msFullscreenElement)
            {
                if (document.exitFullscreen) {
                    document.exitFullscreen();
                } else if (document.mozCancelFullScreen) {
                    document.mozCancelFullScreen();
                } else if (document.webkitExitFullscreen) {
                    document.webkitExitFullscreen();
                } else if (document.msExitFullscreen) {
                    document.msExitFullscreen();
                }
            }
            else
            {
                if (webrtc_video_element.requestFullscreen) {
                    webrtc_video_element.requestFullscreen();
                } else if (webrtc_video_element.mozRequestFullScreen) {
                    webrtc_video_element.mozRequestFullScreen();
                } else if (webrtc_video_element.webkitRequestFullscreen) {
                    webrtc_video_element.webkitRequestFullscreen(Element.ALLOW_KEYBOARD_INPUT);
                } else if (webrtc_video_element.msRequestFullscreen) {
                    webrtc_video_element.msRequestFullscreen();
                }
            }
        }

        function _initFullscreenButton(index, element) {
            element = $(element);
            fullscreen_serial = element.data('serial');
            if (!fullscreen_serial)
                throw Error("Fullscreen does not hava data about which device it refers to");

            let webrtc_video_element = $(`.webrtc-video[data-serial="${fullscreen_serial}"]`).first();
            element.on('click', makeElementFullScreen.bind(null, webrtc_video_element.get(0)));
        }

        return {
            init: function () {
                $('.janus-section').each(_initJanus);
                $('button[data-toggle="video-fullscreen"]').each(_initFullscreenButton)
                $('a[data-toggle="video-fullscreen"]').each(_initFullscreenButton)
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
    TelemetryClass.updateTelemetry('.battery-level-text', '{ "__isset": { "battery_level_percent": true, "serial": false, "timestamp": false }, "Id": 12, "BatteryLevelPercent": 50, "Serial": "000000000000000000", "Timestamp": 1577126258 }');
    TelemetryClass.updateTelemetry('.battery-level-icon', '{ "__isset": { "battery_level_percent": true, "serial": false, "timestamp": false }, "Id": 12, "BatteryLevelPercent": 50, "Serial": "000000000000000000", "Timestamp": 1577126258 }');
    // TelemetryClass.updateTelemetry('.radio-signal-text', '{ "__isset": { "battery_level_percent": true, "serial": false, "timestamp": false }, "Id": 12, "SignalQuality": 5, "Serial": "000000000000000000", "Timestamp": 1577126258 }');
    // TelemetryClass.updateTelemetry('.flying-state-text', '{ "__isset": { "battery_level_percent": true, "serial": false, "timestamp": false }, "Id": 12, "State": 0, "Serial": "000000000000000000", "Timestamp": 1577126258 }');
})