// We make use of this 'server' variable to provide the address of the
// REST Janus API. By default, in this example we assume that Janus is
// co-located with the web server hosting the HTML pages but listening
// on a different port (8088, the default for HTTP in Janus), which is
// why we make use of the 'window.location.hostname' base address. Since
// Janus can also do HTTPS, and considering we don't really want to make
// use of HTTP for Janus if your demos are served on HTTPS, we also rely
// on the 'window.location.protocol' prefix to build the variable, in
// particular to also change the port used to contact Janus (8088 for
// HTTP and 8089 for HTTPS, if enabled).
// In case you place Janus behind an Apache frontend (as we did on the
// online demos at http://janus.conf.meetecho.com) you can just use a
// relative path for the variable, e.g.:
//
// 		var server = "/janus";
//
// which will take care of this on its own.
//
//
// If you want to use the WebSockets frontend to Janus, instead, you'll
// have to pass a different kind of address, e.g.:
//
// 		var server = "ws://" + window.location.hostname + ":8188";
//
// Of course this assumes that support for WebSockets has been built in
// when compiling the server. WebSockets support has not been tested
// as much as the REST API, so handle with care!
//
//
// If you have multiple options available, and want to let the library
// autodetect the best way to contact your server (or pool of servers),
// you can also pass an array of servers, e.g., to provide alternative
// means of access (e.g., try WebSockets first and, if that fails, fall
// back to plain HTTP) or just have failover servers:
//
//		var server = [
//			"ws://" + window.location.hostname + ":8188",
//			"/janus"
//		];
//
// This will tell the library to try connecting to each of the servers
// in the presented order. The first working server will be used for
// the whole session.
//
var janus = null;
var streaming = null;
var opaqueId = "streamingtest-"+Janus.randomString(12);

var bitrateTimer = null;
var spinner = null;
var attached = false;
var locked_id = null;

var simulcastStarted = false, svcStarted = false;

function initJanus(server_url, stun_server_url) {
	Janus.init({debug: "all", callback: function() {
		// Make sure the browser supports WebRTC
		if(!Janus.isWebrtcSupported()) {
			alert("No WebRTC support... ");
			return;
		}
		$('.video-play').each(function () {
			$(this).one('click', function () { startStream($(this), $(this).attr("device-id")); });
		});
		console.log("inited Janus");
		janus = new Janus(
			{
				server: server_url,
				iceServers: [
					{ url: stun_server_url }
					],
				// iceTransportPolicy: "relay"
				success: function() {
					// Attach to streaming plugin
					janus.attach(
						{
							plugin: "janus.plugin.streaming",
							opaqueId: opaqueId,
							success: function (pluginHandle) {
								$('#details').remove();
								streaming = pluginHandle;
								Janus.log("Plugin attached! (" + streaming.getPlugin() + ", id=" + streaming.getId() + ")");
								// Setup streaming session
								attached = true;
							},
							error: function(error) {
								Janus.error("  -- Error attaching plugin... ", error);
								alert("Error attaching plugin... " + error);
								attached = false;
							},
							onmessage: function(msg, jsep) {
								Janus.log(" ::: Got a message :::");
								Janus.log(msg);
								var result = msg["result"];
								if(result !== null && result !== undefined) {
									if (result["status"] !== undefined && result["status"] !== null) {
										var status = result["status"];
										Janus.log(`Status received ${status} ${locked_id}`);
										if(status == 'starting')
											$(`#status-${locked_id}`).removeClass('hide').text("Starting, please wait...").show();
										else if (status == 'started') {
											$(`#status-${locked_id}`).removeClass('hide').text("Started").show();
											locked_id = null;
										}
										else if(status == 'stopped')
											stopStream();
									}
								} else if(msg["error"] !== undefined && msg["error"] !== null) {
									alert(msg["error"]);
									//stopStream();
									return;
								}
								if(jsep !== undefined && jsep !== null) {
									Janus.log("Handling SDP as well...");
									Janus.log(jsep);
									// Offer from the plugin, let's answer
									streaming.createAnswer(
										{
											jsep: jsep,
											// We want recvonly audio/video and, if negotiated, datachannels
											media: { audioSend: false, videoSend: false, data: true },
											success: function(jsep) {
												Janus.log("Got SDP!");
												Janus.log(jsep);
												var body = { "request": "start" };
												streaming.send({"message": body, "jsep": jsep});
											},
											error: function(error) {
												Janus.error("WebRTC error:", error);
												alert("WebRTC error... " + JSON.stringify(error));
											}
										});
								}
							},
							onremotestream: function(stream) {
								Janus.log(" ::: Got a remote stream :::");
								Janus.log(stream);
								var addButtons = false;
								if($(`#remotevideo-${locked_id}`).length === 0) {
									$(`#remotevideo-${locked_id}`).bind("playing", function (locked_id) {
										if(this.videoWidth)
											$(`#remotevideo-${locked_id}`).removeClass(`hide-${locked_id}`).show();
										if(spinner !== null && spinner !== undefined)
										var videoTracks = stream.getVideoTracks();
										if(videoTracks === null || videoTracks === undefined || videoTracks.length === 0)
											return;
										var width = this.videoWidth;
										var height = this.videoHeight;
										$(`#curres-${locked_id}`).removeClass('hide').text(width+'x'+height).show();
										if(Janus.webRTCAdapter.browserDetails.browser === "firefox") {
											// Firefox Stable has a bug: width and height are not immediately available after a playing
											setTimeout(function() {
												var width = $(`#remotevideo-${locked_id}`).get(0).videoWidth;
												var height = $(`#remotevideo-${locked_id}`).get(0).videoHeight;
												$(`#curres-${locked_id}`).removeClass(`hide`).text(width+'x'+height).show();
											}, 2000);
										}
										
									}.bind(null, locked_id));
								}
								Janus.attachMediaStream($(`#remotevideo-${locked_id}`).get(0), stream);
								var videoTracks = stream.getVideoTracks();
								if(videoTracks === null || videoTracks === undefined || videoTracks.length === 0) {
									// No remote video action
									$(`#remotevideo-${locked_id}`).hide();
								} else {
									$(`#remotevideo-${locked_id}`).removeClass('hide').show();
								}
								if(videoTracks && videoTracks.length) {
									$(`#curbitrate-${locked_id}`).removeClass('hide').show();
									bitrateTimer = setInterval(function(locked_id) {
										// Display updated bitrate, if supported
										var bitrate = streaming.getBitrate();
										//~ Janus.log("Current bitrate is " + streaming.getBitrate());
										$(`#curbitrate-${locked_id}`).text(bitrate);
										// Check if the resolution changed too
										var width = $(`#remotevideo-${locked_id}`).get(0).videoWidth;
										var height = $(`#remotevideo-${locked_id}`).get(0).videoHeight;
										if(width > 0 && height > 0)
											$(`#curres-${locked_id}`).removeClass('hide').text(width+'x'+height).show();
									}.bind(null, locked_id), 1000);
								}
							},
							ondataopen: function (data) {
								if (locked_id == null)
									return;
								Janus.log("The DataChannel is available!");
							},
							ondata: function(data) {
								Janus.log("We got data from the DataChannel! " + data);
								$(`#datarecv-${locked_id}`).val(data);
							},
							oncleanup: function() {
								Janus.log(" ::: Got a cleanup notification :::");
								
								locked_id = null;
							}
						});
				},
				error: function(error) {
					Janus.error(error);
					alert(error, function() {
						window.location.reload();
					});
				},
				destroyed: function() {
					window.location.reload();
				}
			});
	}});
}

function startStream(element, selected_stream) {
	element.one("click", function () {
		startStream(element, selected_stream);
	});
	if (!attached || locked_id != null) {
		console.log("Not attached or action pending so not starting");
		return;
	}

	locked_id = selected_stream;
	Janus.log("Asking to play video id #" + selected_stream);
	var body = { "request": "watch", id: parseInt(selected_stream) };
	streaming.send({ "message": body });

	element.children().toggleClass("fa-play fa-stop");
	box_stream_element = $(`#devices-box-${selected_stream}`);
	box_stream_element.boxWidget('expand');
	box_stream_element.find('.no-video-placeholder').each(function () {
		$(this).toggleClass('hide');
	});
	box_stream_element.find('.webrtc-video').each(function () {
		$(this).toggleClass('hide');
	});
	element.one("click", function () {
		stopStream(element, selected_stream);
	});
}

function stopStream(element, selected_stream) {
	element.one("click", function () {
		stopStream(element, selected_stream);
	});
	if (!attached || locked_id != null) {
		console.log("Not Attached so not stopping");
		return;
	}
	Janus.log("Asking to stop video id #" + selected_stream);
	var body = { "request": "stop", id: parseInt(selected_stream) };
	streaming.send({"message": body});
	streaming.hangup();
	if(bitrateTimer !== null && bitrateTimer !== undefined)
		clearInterval(bitrateTimer);
	bitrateTimer = null;
	element.children().toggleClass("fa-play fa-stop");
	box_stream_element.find('.no-video-placeholder').each(function () {
		$(this).toggleClass('hide');
	});
	box_stream_element.find('.webrtc-video').each(function () {
		$(this).toggleClass('hide');
	});
	element.one("click", function () {
		startStream(element, selected_stream);
	});
}