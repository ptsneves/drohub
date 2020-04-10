function initJanus(server_url, stun_server_url, room_id) {
	var opaqueId = "videoroomtest-" + Janus.randomString(12);
	server = server_url;
	console.log(room_id);
	// Initialize the library (all console debuggers enabled)
	Janus.init({debug: "all", callback: function() {
		if(!Janus.isWebrtcSupported()) {
			alert("No WebRTC support... ");
			return;
		}
		// Create session
		janus = new Janus(
			{
				server: server,
				iceServers: [{ urls: ["stun:w1.xirsys.com"] }, { username: "XXXXX", credential: "XXXXXX", urls: ["turn:w1.xirsys.com:80?transport=udp", "turn:eu-turn5.xirsys.com:80?transport=udp", "turn:w1.xirsys.com:80?transport=tcp", "turn:eu-turn5.xirsys.com:80?transport=tcp", "turns:w1.xirsys.com:443?transport=tcp", "turns:w1.xirsys.com:5349?transport=tcp"] }],
				success: function() {
					// Attach to video room test plugin
					janus.attach(
						{
							plugin: "janus.plugin.videoroom",
							opaqueId: opaqueId,
							success: function(pluginHandle) {
								sfutest = pluginHandle;
								Janus.log("Plugin attached! (" + sfutest.getPlugin() + ", id=" + sfutest.getId() + ")");
								sfutest.send(
									{
										"message": {
											"request": "listparticipants",
											"room": room_id
										},
										success: function (listparticipants_result) {
											listparticipants_result.participants.forEach(function (participants) {
												Janus.log(participants)
												newRemoteFeed(participants.id, participants.display, room_id);
											});
										}
									}
								);
								Janus.log("  -- This is a publisher/manager");
							},
							error: function(error) {
								Janus.error("  -- Error attaching plugin...", error);
								alert("Error attaching plugin... " + error);
							},
							consentDialog: function(on) {
								Janus.log("Consent requested but we should not be broadcasting any video");
							},
							mediaState: function(medium, on) {
								Janus.log("Janus " + (on ? "started" : "stopped") + " receiving our " + medium);
							},
							webrtcState: function(on) {
								Janus.log("Janus says our WebRTC PeerConnection is " + (on ? "up" : "down") + " now");
							},
							onmessage: function (msg, jsep) {
								console.log("Message" + msg);
							},
							onlocalstream: function(stream) {
								Janus.log(" ::: Got a local stream :::");
							},
							onremotestream: function (stream) {
								Janus.log(" ::: Got a local stream :::");
								// The publisher stream is sendonly, we don't expect anything here
							},
							oncleanup: function() {
								Janus.log(" ::: Got a cleanup notification: we are unpublished now :::");
								mystream = null;
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
	}
	});


	function newRemoteFeed(id, display, room_id) {
		// A new feed has been published, create a new plugin handle and attach to it as a subscriber
		var remoteFeed = null;
		Janus.log("New remote feed");
		janus.attach(
			{
				plugin: "janus.plugin.videoroom",
				opaqueId: opaqueId,
				success: function (pluginHandle) {
					remoteFeed = pluginHandle;
					Janus.log("Plugin attached! (" + remoteFeed.getPlugin() + ", id=" + remoteFeed.getId() + ")");
					Janus.log("  -- This is a subscriber to room "+ room_id);
					// We wait for the plugin to send us an offer
					var subscribe = {
						"request": "join",
						"ptype": "subscriber",
						"room": room_id,
						"feed": id
					};
					remoteFeed.send({ "message": subscribe });
				},
				error: function (error) {
					Janus.error("  -- Error attaching plugin...", error);
					alert("Error attaching plugin... " + error);
				},
				onmessage: function (msg, jsep) {
					Janus.log(" ::: Got a message (subscriber) :::");
					Janus.log(msg);
					var event = msg["videoroom"];
					Janus.log("Event: " + event);
					if (msg["error"] !== undefined && msg["error"] !== null) {
						alert(msg["error"]);
					} else if (event != undefined && event != null) {
						if (event === "attached") {
							Janus.log("Successfully attached to feed " + remoteFeed.rfid + " (" + remoteFeed.rfdisplay + ") in room " + msg["room"]);
						} else if (event === "event") {
							// Check if we got an event on a simulcast-related event from this publisher
							;
						} else {
							// What has just happened?
						}
					}
					if (jsep !== undefined && jsep !== null) {
						Janus.log("Handling SDP as well...");
						Janus.log(jsep);
						// Answer and attach
						remoteFeed.createAnswer(
							{
								jsep: jsep,
								// Add data:true here if you want to subscribe to datachannels as well
								// (obviously only works if the publisher offered them in the first place)
								media: { audioRecv: false, videoRecv: true, audioSend: false, videoSend: false },	// We want recvonly audio/video
								success: function (jsep) {
									Janus.log("Got SDP!");
									Janus.log(jsep);
									var body = { "request": "start", "room": room_id };
									Janus.log(jsep);
									remoteFeed.send({ "message": body, "jsep": jsep });
								},
								error: function (error) {
									Janus.error("WebRTC error:", error);
									alert("WebRTC error... " + JSON.stringify(error));
								}
							});
					}
				},
				webrtcState: function (on) {
					Janus.log("Janus says this WebRTC PeerConnection feed is " + (on ? "up" : "down") + " now");
				},
				onremotestream: function (stream) {
					element = $(`video.device-video[data-room-id=${room_id}]`);
					element.data('render-state', "playing");
					Janus.attachMediaStream(element.get(0), stream);
				},
				oncleanup: function () {
					element.data('render-state', "stopped");
					Janus.log(" ::: Got a cleanup notification (remote feed " + id + ") :::");// TODO Add notification that there is no video signal
				}
			});
	}
};
