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
								var register = { "request": "join", "room": room_id, "ptype": "publisher", "display": "mydrohub" };
								sfutest.send({"message": register});
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
								var event = msg["videoroom"];
								if(event != undefined && event != null) {
									if(event === "joined") {
											// Publisher/manager created, negotiate WebRTC and attach to existing feeds, if any
											myid = msg["id"];
											mypvtid = msg["private_id"];
											Janus.log("Successfully joined room " + msg["room"] + " with ID " + myid);
											// Any new feed to attach to?
											if(msg["publishers"] !== undefined && msg["publishers"] !== null) {
												var list = msg["publishers"];
												Janus.debug("Got a list of available publishers/feeds:");
												Janus.debug(list);
												publishOwnFeed();
												for(var f in list) {

													var id = list[f]["id"];
													var display = list[f]["display"];
													var audio = list[f]["audio_codec"];
													var video = list[f]["video_codec"];
													Janus.debug("  >> [" + id + "] " + display + " (audio: " + audio + ", video: " + video + ")");
													newRemoteFeed(id, "mydrohub", room_id);
												}
											}
									}
									else if(event === "destroyed") {
										// The room has been destroyed
										Janus.warn("The room has been destroyed!");
										window.location.reload();
									}
									else if(event === "event") {
										// Any new feed to attach to?
										if(msg["publishers"] !== undefined && msg["publishers"] !== null) {
											var list = msg["publishers"];
											Janus.debug("Got a list of available publishers/feeds:");
											Janus.debug(JSON.stringify(list));
											publishOwnFeed();
											for(var f in list) {
												var id = list[f]["id"];
												var display = list[f]["display"];
												var audio = list[f]["audio_codec"];
												var video = list[f]["video_codec"];
												Janus.debug("  >> [" + id + "] " + display + " (audio: " + audio + ", video: " + video + ")");
												newRemoteFeed(id, "mydrohub", room_id);
											}
									  }
								  }
								}
								console.log("Message" + JSON.stringify(msg));
								if(jsep !== undefined && jsep !== null) {
									Janus.debug("Handling SDP as well...");
									Janus.debug(jsep);
									sfutest.handleRemoteJsep({jsep: jsep});
									// Check if any of the media we wanted to publish has
									// been rejected (e.g., wrong or unsupported codec)
									var audio = msg["audio_codec"];
									if(mystream && mystream.getAudioTracks() && mystream.getAudioTracks().length > 0 && !audio) {
										// Audio has been rejected
										toastr.warning("Our audio stream has been rejected, viewers won't hear us");
									}
								}
							},
							onlocalstream: function(stream) {
								Janus.log(" ::: Got a local stream :::");
								element = $(`video.device-video[data-room-id=${room_id}]`);
								// element.data('render-state', "playing");
								Janus.attachMediaStream(element.get(0), stream);
								if(sfutest.webrtcStuff.pc.iceConnectionState !== "completed" &&
									sfutest.webrtcStuff.pc.iceConnectionState !== "connected") {
									console.warn("Publishing")
								}

							},
							onremotestream: function (stream) {
								Janus.log(" ::: Got a remote stream :::");
								// The publisher stream is sendonly, we don't expect anything here
							},
							oncleanup: function() {
								Janus.log(" ::: Got a cleanup notification: we are unpublished now ::: publisher");
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

  function publishOwnFeed(useAudio) {
    // Publish our stream
    sfutest.createOffer(
      {
        // Add data:true here if you want to publish datachannels as well
        media: { audioRecv: false, videoRecv: false, audioSend: true, videoSend: false},	// Publishers are sendonly
        // If you want to test simulcasting (Chrome and Firefox only), then
        // pass a ?simulcast=true when opening this demo page: it will turn
        // the following 'simulcast' property to pass to janus.js to true
        success: function(jsep) {
          Janus.debug("Got publisher SDP!");
          Janus.debug(jsep);
          var publish = { "request": "configure", "record": true, "bitrate": 128000, "audio": true, "video": false, "audiocodec": "opus" };
          // You can force a specific codec to use when publishing by using the
          // audiocodec and videocodec properties, for instance:
          // 		publish["audiocodec"] = "opus"
          // to force Opus as the audio codec to use, or:
          // 		publish["videocodec"] = "vp9"
          // to force VP9 as the videocodec to use. In both case, though, forcing
          // a codec will only work if: (1) the codec is actually in the SDP (and
          // so the browser supports it), and (2) the codec is in the list of
          // allowed codecs in a room. With respect to the point (2) above,
          // refer to the text in janus.plugin.videoroom.cfg for more details
          sfutest.send({"message": publish, "jsep": jsep});
        },
        error: function(error) {
          Janus.error("WebRTC error:", JSON.stringify(error));
        }
      });
  }

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
					Janus.log(" ::: Got a message (subscriber) :::" + JSON.stringify(msg));
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
								media: { audioRecv: true, videoRecv: true, audioSend: false, videoSend: false },	// We want recvonly audio/video
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
				webrtcState: function (on, message) {
					Janus.log("Janus says this WebRTC PeerConnection feed is " + (on ? "up" : "down") + " now\n" + JSON.stringify(message));
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
