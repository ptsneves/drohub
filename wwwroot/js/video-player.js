$(window).on("load", function() {
    let VideoPlayerClass = (function() {
        let InputRangeProgressBar = (function () {
            let _e_slider = undefined;
            let _video_player_class = undefined;

            function _updateSlider(data) {
                let time = data['current_time'];
                let duration = data['duration'];

                if (isNaN(time) || isNaN(duration))
                    return;

                $(_e_slider).prop('value', time);
                $(_e_slider).prop('max', duration);
            }

            function _reset() {
                _e_slider.prop('min', 0);
                _e_slider.prop('value', 0);
                _e_slider.prop('step', 0.05);
            }

            function _onChange() {
                _video_player_class.seekPosition($(this).val());
            }

            return {
                init: function (e_slider, video_player_class) {
                    _e_slider = $(e_slider);
                    _e_slider.change(_onChange)
                    _reset()
                    _video_player_class = video_player_class;
                    _video_player_class.addOnTimeUpdate(_updateSlider);
                    _video_player_class.addOnEnd(_reset);
                }
            }
        });

        let VideoTimeTextClass = (function () {
            let _e_text = undefined;
            let _video_player_class = undefined;

            function _setCurrentTime(data) {
                let time_seconds_float = data['current_time'];
                let time_seconds_int = Math.floor(time_seconds_float);
                let time_minutes = Math.floor(time_seconds_int / 60);
                let time_seconds = Math.floor(time_seconds_int % 60);
                _e_text.text(`${String(time_minutes).padStart(2, '0')}:${String(time_seconds).padStart(2, '0')}`);
            }

            return {
                init: function (e_text, video_player_class) {
                    _e_text = $(e_text);
                    _e_text.text('00:00');
                    _video_player_class = video_player_class;
                    _video_player_class.addOnTimeUpdate(_setCurrentTime)
                }
            }
        });

        let DetatchVideoButtonClass = (function () {
            let _e_button = undefined;
            let _e_video = undefined;
            let _video_player_container = undefined;
            let _video_player_class = undefined;

            let new_window_title = "Video Player"

            function _generateHTML() {
                let conf = _video_player_class.getConfiguration();
                return `
        <body style="padding: 0; margin: 0;">
            ${_video_player_container.get(0).outerHTML}
        </body>`;
            }

            function _openDetachedVideo(extra_features) {
                let features = `menubar=no,location=no,width=${_video_player_container.outerWidth(true)},height=${_video_player_container.outerHeight(true)}`;

                if(extra_features !== undefined && extra_features !== '')
                    features = features + ',' + extra_features;

                let new_window = window.open(window.location.href, '_blank', features);

                new_window.onload = function() {
                    new_window.document.body.innerHTML = _generateHTML();
                }

                new_window.document.close();
            }

            return {
                init: function (e_button, e_video, video_player_container, video_player_class) {
                    _e_button = $(e_button);
                    _e_video = $(e_video);
                    _video_player_container = $(video_player_container) ;
                    _video_player_class = video_player_class;
                    _e_button.click(_openDetachedVideo);
                }
            }
        });

        let PlayPauseButtonClass = (function () {
            let _e_button = undefined;
            let _video_player_class = undefined;
            let _playing_state = false;
            function _togglePlayPause() {
                // _e_button
                if (!_playing_state)
                    _video_player_class.startPlayback();
                else
                    _video_player_class.pausePlayback()
            }

            function _onPlaybackOn() {
                _playing_state = true;
                _e_button.find('.video-player-play-icon').hide();
                _e_button.find('.video-player-pause-icon').show();
            }

            function _onPlaybackOff() {
                _playing_state = false;
                _e_button.find('.video-player-play-icon').show();
                _e_button.find('.video-player-pause-icon').hide();
            }

            return {
                init: function (button, video_player_class) {
                    _e_button = $(button);
                    _e_button.click(_togglePlayPause)
                    _video_player_class = video_player_class;

                    _onPlaybackOff();

                    _video_player_class.addOnPlay(_onPlaybackOn);
                    _video_player_class.addOnEnd(_onPlaybackOff);
                    _video_player_class.addOnPause(_onPlaybackOff);
                }
            }
        });

        let _e_video = undefined;
        let _video_player_container = undefined;

        let _on_play_functions_to_call = [];
        let _on_pause_functions_to_call = [];
        let _on_end_functions_to_call = [];
        let _on_time_update_functions_to_call = [];

        let _jquery_js_href = "/AdminLTE/bower_components/jquery/dist/jquery.min.js"
        let _video_player_js_href = "/js/video-player.js"
        let _video_player_css_href = "/css/video-player.css"

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

        function setVolume(volume_percent) {
            let MIN = 0;
            let MAX = 100;
            volume_percent = Math.min(Math.max(volume_percent, MIN), MAX)
            _e_video.get(0).muted = false;
            _e_video.get(0).volume = volume_percent/100;
        }

        //if the mute button is pushed, the player wilwill toggle between Mute and Unmute state
        function toggleMute() {
            _e_video.get(0).muted = !_e_video.get(0).muted;
        }

        return {
            init: function init(video_player_container) {
                _video_player_container = video_player_container
                _e_video = $(_video_player_container).find('video.video-player');
                if (!_e_video.get(0).canPlayType) {
                    console.error("Cannot play video. Not initializing");
                    return;
                }

                _e_video.on("timeupdate", function () {
                    let data = {
                        'current_time': _e_video.get(0).currentTime,
                        'duration': _e_video.get(0).duration,
                    }
                    callFunc(_on_time_update_functions_to_call, data)
                });

                _e_video.on("ended", function () {
                    callFunc(_on_end_functions_to_call);
                });

                _e_video.on("play", function () {
                    callFunc(_on_play_functions_to_call);
                });

                _e_video.on("pause", function () {
                    callFunc(_on_pause_functions_to_call);
                });

                let e_slider = $(video_player_container).find('input.video-player-progressbar[type="range"]');
                if (e_slider)
                    InputRangeProgressBar().init(e_slider, this);

                let b_playpause = $(video_player_container).find('button.video-player-play-pause');
                if (b_playpause)
                    PlayPauseButtonClass().init(b_playpause, this);

                let t_curtime = $(video_player_container).find('.video-player-curtime');
                if (t_curtime && t_curtime.hasClass('video-player-text'))
                    VideoTimeTextClass().init(t_curtime, this);

                let b_detatchvideo = $(video_player_container).find('button.video-player-detatch');
                if (b_detatchvideo)
                    DetatchVideoButtonClass().init(b_detatchvideo, _e_video, _video_player_container, this);

            },
            getConfiguration: function getConfiguration() {
                return {
                    'jquery_js_href': _jquery_js_href,
                    'video_player_js_href': _video_player_js_href,
                    'video_player_css_href': _video_player_css_href,
                }
            },
            addOnPlay: function (f) {
                addFunctionToArray(_on_play_functions_to_call, f);
            },
            addOnPause: function (f) {
                addFunctionToArray(_on_pause_functions_to_call, f);
            },
            addOnEnd: function (f) {
                addFunctionToArray(_on_end_functions_to_call, f);
            },
            addOnTimeUpdate : function(f) {
                addFunctionToArray(_on_time_update_functions_to_call, f);
            },
            seekPosition: function(position_in_float_seconds) {
                _e_video.get(0).currentTime = position_in_float_seconds;
            },
            startPlayback: function () {
                _e_video.get(0).play()
                    .catch((e) => console.error("Cannot play video" + JSON.stringify(e)));
            },
            pausePlayback: function() {
                _e_video.get(0).pause();
            },
            toggleMute: toggleMute,
            setVolume: setVolume,
        }
    });

    $('.video-player-container').each(function() {
        let d = VideoPlayerClass();
        d.init($(this));
    });

});