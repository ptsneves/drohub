$(function () {

    $('input').iCheck({
        checkboxClass: 'icheckbox_square-blue',
        radioClass: 'iradio_square-blue',
        increaseArea: '20%' /* optional */
    });

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
                    if (device_coords == null || device_type == null || device_serial == null || device_svg_marker == null) {
                        console.warn("A rendering was requested but not all fields are set");
                        return;
                    }

                    let icon_url = `${window.location.origin}/${device_svg_marker}`
                    let marker_icon = {
                        url: icon_url, // url
                        rotation: 45,
                        scaledSize: new google.maps.Size(35, 25), // scaled size
                        origin: new google.maps.Point(0, 0), // origin
                        anchor: new google.maps.Point(0, 0) // anchorscaledSize: new google.maps.Size(50, 50), // scaled size
                    };
                    let marker = null;
                    if ((marker = _getMarker(device_type, device_serial)) != null) {
                        marker.setPosition({ lat: device_coords.Latitude, lng: device_coords.Longitude });
                    }
                    else {
                        marker = new google.maps.Marker({
                            position: new google.maps.LatLng(device_coords.Latitude, device_coords.Longitude),
                            icon: marker_icon,
                            map: maps[device_serial],
                            serial: device_serial,
                            device_type: device_type
                        });
                        _setMarker(marker, device_type, device_serial);
                    }
                    $('img[src="' + icon_url + '"]').css({
                        'transform': 'rotate(' + (45) + 'deg)'
                    });
                    if (element.hasClass('main-marker')) {
                        marker.map.panTo(marker.position);
                        marker.map.setZoom(20);
                    }
                }
            }
        }();
        _MarkerStoreClass.init(maps);

        return {
            renderMapMarker: _MarkerStoreClass.renderPositionMarker,
            init: function () {
                function perElementInit(index, element) {
                    function _initializeMapMarkerActionButton(index, element) {
                        element = $(element);
                        element.click(data_function_table[element.data('toggle')].bind(null, index, element));
                    }
                    let jquery_element = $(element);
                    let serial_number = jquery_element.data('serial');
                    if (serial_number == null)
                        console.error("Could not initialize map because we found not data-serial");
                    styles = [
                        {
                            "elementType": "geometry",
                            "stylers": [
                                {
                                    "color": "#f5f5f5"
                                }
                            ]
                        },
                        {
                            "elementType": "labels.icon",
                            "stylers": [
                                {
                                    "visibility": "off"
                                }
                            ]
                        },
                        {
                            "elementType": "labels.text.fill",
                            "stylers": [
                                {
                                    "color": "#616161"
                                }
                            ]
                        },
                        {
                            "elementType": "labels.text.stroke",
                            "stylers": [
                                {
                                    "color": "#f5f5f5"
                                }
                            ]
                        },
                        {
                            "featureType": "administrative.land_parcel",
                            "elementType": "labels.text.fill",
                            "stylers": [
                                {
                                    "color": "#bdbdbd"
                                }
                            ]
                        },
                        {
                            "featureType": "poi",
                            "elementType": "geometry",
                            "stylers": [
                                {
                                    "color": "#eeeeee"
                                }
                            ]
                        },
                        {
                            "featureType": "poi",
                            "elementType": "labels.text.fill",
                            "stylers": [
                                {
                                    "color": "#757575"
                                }
                            ]
                        },
                        {
                            "featureType": "poi.park",
                            "elementType": "geometry",
                            "stylers": [
                                {
                                    "color": "#e5e5e5"
                                }
                            ]
                        },
                        {
                            "featureType": "poi.park",
                            "elementType": "labels.text.fill",
                            "stylers": [
                                {
                                    "color": "#9e9e9e"
                                }
                            ]
                        },
                        {
                            "featureType": "road",
                            "elementType": "geometry",
                            "stylers": [
                                {
                                    "color": "#ffffff"
                                }
                            ]
                        },
                        {
                            "featureType": "road.arterial",
                            "elementType": "labels.text.fill",
                            "stylers": [
                                {
                                    "color": "#757575"
                                }
                            ]
                        },
                        {
                            "featureType": "road.highway",
                            "elementType": "geometry",
                            "stylers": [
                                {
                                    "color": "#dadada"
                                }
                            ]
                        },
                        {
                            "featureType": "road.highway",
                            "elementType": "labels.text.fill",
                            "stylers": [
                                {
                                    "color": "#616161"
                                }
                            ]
                        },
                        {
                            "featureType": "road.local",
                            "elementType": "labels.text.fill",
                            "stylers": [
                                {
                                    "color": "#9e9e9e"
                                }
                            ]
                        },
                        {
                            "featureType": "transit.line",
                            "elementType": "geometry",
                            "stylers": [
                                {
                                    "color": "#e5e5e5"
                                }
                            ]
                        },
                        {
                            "featureType": "transit.station",
                            "elementType": "geometry",
                            "stylers": [
                                {
                                    "color": "#eeeeee"
                                }
                            ]
                        },
                        {
                            "featureType": "water",
                            "elementType": "geometry",
                            "stylers": [
                                {
                                    "color": "#c9c9c9"
                                }
                            ]
                        },
                        {
                            "featureType": "water",
                            "elementType": "labels.text.fill",
                            "stylers": [
                                {
                                    "color": "#9e9e9e"
                                }
                            ]
                        }
                    ];

                    maps[serial_number] = new google.maps.Map(element, {
                        zoom: 8,
                        serial: serial_number,
                        center: { lat: 40.5, lng: -7 },
                        styles: styles
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
                    $('.map-marker-action-button').each(_initializeMapMarkerActionButton);
                }
                $('.google-map').each(perElementInit);
            },
            getMap: function (device_serial) {
                return maps[device_serial];
            }
        }
    }();

    let ElementClassRangeClass = function () {
        let _available_classes = new Set();
        let _classes = [];

        function _makeClassActive(element, classes_to_activate) {
            //TODO consolidate this with a filter. What a mess..
            _available_classes.forEach(function (item) {
                element.removeClass(item);
            });
            element.addClass(classes_to_activate.join(' '));
        }

        function _addStep (min, max, element_classes) {
            let split_classes = element_classes.split(/\s+/).filter(Boolean);
            _classes.push({ min: min, max: max, class: split_classes });
            split_classes.forEach(item => { _available_classes.add(item); });
        }

        _addStep(null, null, "fa-empty-set");

        return {
            addStep: _addStep,
            addRegularSteps: function (min, max, classes) {
                let step_size = (max - min) / classes.length;
                for (i = 0; i < classes.length; i++) {
                    this.addStep(min + step_size * i, min + step_size * (i + 1), classes[i]);
                }
            },
            addClassToElement: function (element, value = null) {
                if (value == null) {
                    _makeClassActive(element, _classes[0].class);
                    return;
                }
                for (i = 1; i < _classes.length; i++) {
                    if (value >= _classes[i].min && value < _classes[i].max) {
                        _makeClassActive(element, _classes[i].class);
                        return;
                    }
                }
                console.warn(`Unreachable situation. The range has been violated\
    Value: ${value} and classes ${JSON.stringify(_classes)}`);
            }
        }
    };

    let WatchDogClass = function() {
        let _timeout_in_milliseconds = -1;
        let _is_active = false;
        let _functions_for_timeout = [];
        let _functions_for_watchdog_start = [];
        let _interval_id = -1;
        let _has_been_toggled = false;

        function _onWatchDogFail() {
            _has_been_toggled = false;
            _is_active = false;
            if (_interval_id > 0)
                window.clearTimeout(_interval_id);
            _functions_for_timeout.forEach(f => { f(); });
        }

        function _runWatchDog() {
            if (_is_active) {
                if (_has_been_toggled) {
                    _has_been_toggled = false;
                    _interval_id = window.setTimeout(_runWatchDog, _timeout_in_milliseconds)
                }
                else {
                    _onWatchDogFail();
                }
            }
        }

        return {
            addFunctionForWatchDogTimeout: function (new_func) {
                addFunctionToArray(_functions_for_timeout, new_func);
            },
            addFunctionForWatchDogStart: function(new_func) {
                addFunctionToArray(_functions_for_watchdog_start, new_func);
            },
            isActive: function() {
                return _is_active;
            },
            start: function() {
                if (_is_active)
                    return;
                _is_active = true;
                _has_been_toggled = true;
                _functions_for_watchdog_start.forEach(f => { f(); });
                _runWatchDog();
            },
            stop: function() {
                if (_is_active)
                    _onWatchDogFail();
            },
            toggle: function() {
                _has_been_toggled = true
            },
            init: function(timeout_in_milliseconds) {
                _is_active = false;
                _has_been_toggled = false;
                _timeout_in_milliseconds = timeout_in_milliseconds;
            }
        }
    };

    let RenderRefreshClass = function() {
        let _refresh_period_in_millis = -1;
        let _on_repeat_functions_to_call = [];
        let _on_start_functions_to_call = [];
        let _on_stop_functions_to_call = [];
        let _interval_id = null;
        let _is_started = false;

        function _refreshCallback() {
            if (_is_started)
                _on_repeat_functions_to_call.forEach(func => { func(); });
        }

        return {
            addOnRefreshFunction: function(f) {
                addFunctionToArray(_on_repeat_functions_to_call, f);
            },
            addOnStartFunction: function(f) {
                addFunctionToArray(_on_start_functions_to_call, f);
            },
            addOnStopFunction: function(f) {
                addFunctionToArray(_on_stop_functions_to_call, f);
            },
            stop: function() {
                _is_started = false;
                window.clearInterval(_interval_id);
                _on_stop_functions_to_call.forEach(f => { f(); } );
            },
            start: function() {
                _is_started = true;
                _on_start_functions_to_call.forEach(f => { f(); } );
                _interval_id = window.setInterval(_refreshCallback, _refresh_period_in_millis);
            },
            init: function(refresh_period_in_millis) {
                _refresh_period_in_millis = refresh_period_in_millis;
            }
        }
    };

    function Static_millisToMinutesAndSeconds(millis) {
        let hours =  Math.floor(millis / (60000*60));
        let minutes = Math.floor(millis / 60000);
        let seconds = ((millis % 60000) / 1000).toFixed(0);
        return `${minutes}m ${seconds}s`;
    }

    let TelemetryClass = function () {
        let _FunctionTable = {};

        let _signalr_connection = null;
        let _map_class_instance = null;
        let _telemetry_watchdog = null;
        let _render_refresher = null;

        function _renderFlightTimeText(index, element) {
            _render_refresher.addOnStartFunction(function() {
                $.getJSON(element.attr('data-url'), function (data) {
                    element.attr('data-telemetry', data);
                })
            });

            _render_refresher.addOnStopFunction(function() {
                element.text("No active flight");
            });

            _render_refresher.addOnRefreshFunction(function() {
                let data_telemetry = element.attr('data-telemetry')
                if (data_telemetry === undefined)
                    return;
                let flight_start = JSON.parse(data_telemetry);
                if (flight_start == null)
                    return;

                let time_diff = Date.now() - element.attr('data-telemetry');
                element.text(Static_millisToMinutesAndSeconds(time_diff));
            });
        }

        function _renderTelemetry(index, element) {
            element = $(element);
            let fn = element.data('renderer');
            if (!fn)
                return;
            if (fn in _FunctionTable)
                _FunctionTable[fn](index, element);
            else
                console.error("Cannot find renderer " + fn);
        }

        function _renderMuteButton(element, is_muted) {
            const child = element.children('i').eq(0);
            if(is_muted)
                child.removeClass('fa-microphone-slash').addClass('fa-microphone');
            else
                child.removeClass('fa-microphone').addClass('fa-microphone-slash');
        }

        function _renderLiveVideo(_, element) {
            function _toggleMute(element, sfu) {
                if (sfu === undefined)
                    return;
                let is_audio_muted = sfu.isAudioMuted();
                if(is_audio_muted)
                    sfu.unmuteAudio();
                else
                    sfu.muteAudio();
                is_audio_muted = sfu.isAudioMuted();
                _renderMuteButton(element, is_audio_muted);
            }

            element = $(element);
            let live_video_result = JSON.parse(element.attr('data-telemetry'));
            let serial = element.data('serial');
            switch (live_video_result.State) {
                case 0: //Live
                    if (element.data("render-state") === "stopped") {
                        element.data("render-state", "starting");
                        if (window.opaqueId === undefined || window.opaqueId == null)
                            return;
                        const janus_instance = attachToRoom(element.data('room-id'), window.opaqueId, function (sfu) {
                            $(`button[data-toggle="video-mute"][data-serial=${serial}]`).each(function (index, element) {
                                element = $(element);
                                element.on('click', _toggleMute.bind(this, element, sfu));
                            });
                        },
                            function (sfu) {
                            $(`button[data-toggle="video-mute"][data-serial=${serial}]`).each(function (index, e) {
                                e = $(e);
                                e.off('click', _toggleMute.bind(this, e, sfu));
                            });
                        })
                        janus.attach(janus_instance);
                    }
                    break;
                default:
                    element.data("render-state", "stopped");
                    console.log("Maybe we should disconnect if necessary")
                    break;
            }
        }

        function _renderSatelliteCount(_, element) {
            $(element).text("8");
        }

        function _renderAltitude(_, element) {
            element = $(element);
            let position = JSON.parse(element.attr('data-telemetry'));
            if (position == null)
                return;
            element.text(`${position.Altitude.toFixed(1)}m`);
        }

        function _renderFlyingState(_, element) {
            element = $(element);
            let flying_state = JSON.parse(element.attr('data-telemetry'));
            if (!flying_state)
                return;

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

            flying_state_class_range = ElementClassRangeClass();
            flying_state_class_range.addStep(0, 1, "text-success");
            flying_state_class_range.addStep(1, 8, "text-warning blinking");

            flying_state = JSON.parse(element.attr('data-telemetry'));
            if (!flying_state)
                flying_state_class_range.addClassToElement(element);
            else
                flying_state_class_range.addClassToElement(element, flying_state.State);
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
            let telemetry = JSON.parse(telemetry_json);
            if (!telemetry)
                return;

            $(`${selector}[data-serial="${telemetry.Serial}"]`).each(
                function (index, element) {
                    element = $(element);
                    element.attr('data-telemetry', telemetry_json);
                    _renderTelemetry(index, element);
                }
            );
            if (!_telemetry_watchdog.isActive()) {
                _telemetry_watchdog.start();
            }
            else
                _telemetry_watchdog.toggle();
        }

        return {
            updateTelemetry: _updateTelemetry,
            init: function (signalr_connection, map_class_instance) {
                _map_class_instance = map_class_instance;
                _signalr_connection = signalr_connection;

                _render_refresher = RenderRefreshClass();
                _render_refresher.init(1000);

                _telemetry_watchdog = WatchDogClass();
                _telemetry_watchdog.init(5000);
                _telemetry_watchdog.addFunctionForWatchDogStart(_render_refresher.start);
                _telemetry_watchdog.addFunctionForWatchDogTimeout(_render_refresher.stop);

                _FunctionTable["renderFlyingState"] = _renderFlyingState;
                _FunctionTable["renderPlaneIcon"] = _renderPlaneIcon;
                _FunctionTable["renderPositionText"] = _renderPositionText;
                _FunctionTable["renderFlightTimeText"] = _renderFlightTimeText;
                _FunctionTable["renderLiveVideo"] = _renderLiveVideo;
                _FunctionTable["renderMapMarker"] = _map_class_instance.renderMapMarker;
                _FunctionTable["renderSatelliteCount"] = _renderSatelliteCount;
                _FunctionTable["renderAltitude"] = _renderAltitude;

                _signalr_connection.on("DronePosition", function (message) {
                    _updateTelemetry('.main-marker', message);
                    _updateTelemetry('.position', message);
                });

                _signalr_connection.on("DroneBatteryLevel", function (message) {
                    _updateTelemetry('.battery-level', message);
                });

                _signalr_connection.on("DroneRadioSignal", function (message) {
                    _updateTelemetry('.radio-signal', message);
                });

                _signalr_connection.on("DroneFlyingState", function (message) {
                    _updateTelemetry('.flying-state-text', message);
                });

                _signalr_connection.on("DroneReply", function (message) {
                    //pingService
                });

                _signalr_connection.on("DroneLiveVideoStateResult", function (message) {
                    _updateTelemetry('video.janus-video', message)
                });
                $('*[data-renderer]').filter('*[data-telemetry]').each(_renderTelemetry);
            }
        }
    }();

    let JanusVideoClass = function () {
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
            let fullscreen_serial = element.data('serial');
            if (!fullscreen_serial)
                throw Error("Fullscreen does not hava data about which device it refers to");

            let webrtc_video_element = $(`.webrtc-video[data-serial="${fullscreen_serial}"]`).first();
            element.on('click', makeElementFullScreen.bind(null, webrtc_video_element.get(0)));
        }

        return {
            init: function () {
                $('button[data-toggle="video-fullscreen"]').each(_initFullscreenButton);

                const section_element = $('section.janus-section');
                const janus_url = section_element.data('janus-url');
                const stun_server_url = section_element.data('stun-server-url');
                initJanus(janus_url, stun_server_url);
            }
        }
    }();
    JanusVideoClass.init();
    MapClass.init();
    TelemetryClass.init(SignalRConnectionClass.getConnection(), MapClass);


    $('a.in-relocatable-box-move-up').each(function () {
       $(this).click(function() {
           let current_box = $(this).closest('div.box');
           current_box.insertBefore(current_box.prev('div.box'));
       });
    });

    $('a.in-relocatable-box-move-down').each(function () {
        $(this).click(function() {
            let current_box = $(this).closest('div.box');
            current_box.insertAfter(current_box.next('div.box'));
        });
    });

    $('a[data-toggle="drone-control-box"]').each(function () {
        $(this).click(function () {
            $(this).toggleClass('plus-right');
            $(this).toggleClass('minus-right');
            $(`div.drone-control-box[data-serial="${$(this).data('serial')}"]`).each(
                function () {
                    $(this).boxWidget('toggle');
                }
            )
        });
    })
    //test
    // test_connection = SignalRConnectionClass.getConnection();
    // for (i = 0; i < 10; i++) {
    //     setTimeout(function () {
    //     }, 1000);
    //     position = `{"__isset":{"latitude":false,"longitude":false,"altitude":false,"serial":false,"timestamp":false},"Id":412,"Latitude":48.878692719478146,"Longitude":2.4971${i},"Altitude":5.1912965774536133,"Serial":"000000000000000000","Timestamp":1577126313}`;
    //     test_connection.invoke("DronePosition", position)
    // }
    // TelemetryClass.updateTelemetry('.position', `{"__isset":{"latitude":false,"longitude":false,"altitude":false,"serial":false,"timestamp":false},"Id":1,"Latitude":48.878692719478146,"Longitude":2.4971,"Altitude":5.1912965774536133, "Heading": 100, "Serial":"PI040416BA8H083705","Timestamp":1577126313}`);
    // TelemetryClass.updateTelemetry('.marker', `{"__isset":{"latitude":false,"longitude":false,"altitude":false,"serial":false,"timestamp":false},"Id":1,"Latitude":48.878692719478146,"Longitude":2.4971,"Altitude":5.1912965774536133, "Heading": 100, "Serial":"PI040416BA8H083705","Timestamp":1577126313}`);
    // TelemetryClass.updateTelemetry('.battery-level', '{ "__isset": { "battery_level_percent": true, "serial": false, "timestamp": false }, "Id": 12, "BatteryLevelPercent": 10, "Serial": "PI040416BA8H083705", "Timestamp": 1577126258 }');
    // TelemetryClass.updateTelemetry('.battery-level-icon', '{ "__isset": { "battery_level_percent": true, "serial": false, "timestamp": false }, "Id": 12, "BatteryLevelPercent": 99, "Serial": "000000000000000000", "Timestamp": 1577126258 }');
    // TelemetryClass.updateTelemetry('.satellite-count', '{ "__isset": { "battery_level_percent": true, "serial": false, "timestamp": false }, "Id": 12, "BatteryLevelPercent": 99, "Serial": "000000000000000000", "Timestamp": 1577126258 }');
    // // TelemetryClass.updateTelemetry('.battery-level-icon', '{ "__isset": { "battery_level_percent": true, "serial": false, "timestamp": false }, "Id": 12, "BatteryLevelPercent": 29, "Serial": "000000000000000000", "Timestamp": 1577126258 }');
    // TelemetryClass.updateTelemetry('.radio-signal', '{ "__isset": { "rssi": true, "signal_quality": true, "serial": false, "timestamp": false }, "Id": 12, "Rssi": -50, "SignalQuality": 5, "Serial": "PI040416BA8H083705", "Timestamp": 1577126258 }');
    // TelemetryClass.updateTelemetry('.flying-state-text', '{ "__isset": { "battery_level_percent": true, "serial": false, "timestamp": false }, "Id": 12, "State": 6, "Serial": "PI040416BA8H083705", "Timestamp": 1577126258 }');
})