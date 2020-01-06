
LiveStreamRecordingClass = function (signalr_connection) {
    let _signalr_connection = signalr_connection;
    function _playVideo() {
        let video_id = $(this).data('video-id');
        let video_element = $(this).find('video.livestream-recording-video').first();
    }

    function _initElement(index, element) {
        element = $(element);
        element.click(_playVideo);
    }
    _signalr_connection.on("DroneVideoStateResult", function (message) {
        console.warn("New video");
    });
    // $('.livestream-recording-btn').each(_initElement);
    return {

    }
}(SignalRConnectionClass.getConnection());