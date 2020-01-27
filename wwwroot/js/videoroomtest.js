function initJanus(server_url, stun_server_url, room_ids) {
	var opaqueId = "videoroomtest-" + Janus.randomString(12);
	server = server_url;
	console.log(room_ids);
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
				iceServers: [
					{ url: stun_server_url }
				],
				success: function() {
					// Attach to video room test plugin
					janus.attach(
						{
							plugin: "janus.plugin.videoroom",
							opaqueId: opaqueId,
							success: function(pluginHandle) {
								sfutest = pluginHandle;
								Janus.log("Plugin attached! (" + sfutest.getPlugin() + ", id=" + sfutest.getId() + ")");
								// console.log(room);
								room_ids.forEach(room => {
									console.log(room);
									sfutest.send(
										{
											"message": { "request": "exists", "room": room },
											success: function (exists_result) {
												if (exists_result.exists == true) {
													sfutest.send(
														{
															"message": {
																"request": "listparticipants",
																"room": exists_result.room
															},
															success: function (listparticipants_result) {
																listparticipants_result.participants.forEach(function (participants) {
																	newRemoteFeed(participants.id, participants.display, exists_result.room);
																});
															}
														}
													);
												}
											}
										}
									);
								});
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
							onmessage: function(msg, jsep) {
								Janus.log(" ::: Got a message (publisher) :::");
								Janus.log(msg);
								var event = msg["videoroom"];
								Janus.log("Event: " + event);
								if(event != undefined && event != null) {
									if(event === "joined") {
										// Publisher/manager created, negotiate WebRTC and attach to existing feeds, if any
										myid = msg["id"];
										mypvtid = msg["private_id"];
										Janus.log("Successfully joined room " + msg["room"] + " with ID " + myid);
										// Any new feed to attach to?
										if(msg["publishers"] !== undefined && msg["publishers"] !== null) {
											var list = msg["publishers"];
											Janus.log("Got a list of available publishers/feeds:");
											Janus.log(list);
											for(var f in list) {
												var id = list[f]["id"];
												var display = list[f]["display"];
												var audio = list[f]["audio_codec"];
												var video = list[f]["video_codec"];
												Janus.log("  >> [" + id + "] " + display + " (audio: " + audio + ", video: " + video + ")");
												newRemoteFeed(id, display, audio, video);
											}
										}
									} else if(event === "destroyed") {
										// The room has been destroyed
										Janus.warn("The room has been destroyed!");
										alert("The room has been destroyed", function() {
											window.location.reload();
										});
									} else if(event === "event") {
										// Any new feed to attach to?
										if(msg["publishers"] !== undefined && msg["publishers"] !== null) {
											var list = msg["publishers"];
											Janus.log("Got a list of available publishers/feeds:");
											Janus.log(list);
											for(var f in list) {
												var id = list[f]["id"];
												var display = list[f]["display"];
												var audio = list[f]["audio_codec"];
												var video = list[f]["video_codec"];
												Janus.log("  >> [" + id + "] " + display + " (audio: " + audio + ", video: " + video + ")");
												newRemoteFeed(id, display, audio, video);
											}
										} else if(msg["leaving"] !== undefined && msg["leaving"] !== null) {
											// One of the publishers has gone away?
											var leaving = msg["leaving"];
											Janus.log("Publisher left: " + leaving);
											var remoteFeed = null;
											for(var i=1; i<6; i++) {
												if(feeds[i] != null && feeds[i] != undefined && feeds[i].rfid == leaving) {
													remoteFeed = feeds[i];
													break;
												}
											}
											if(remoteFeed != null) {
												Janus.log("Feed " + remoteFeed.rfid + " (" + remoteFeed.rfdisplay + ") has left the room, detaching");
												$('#remote'+remoteFeed.rfindex).empty().hide();
												$('#videoremote'+remoteFeed.rfindex).empty();
												feeds[remoteFeed.rfindex] = null;
												remoteFeed.detach();
											}
										} else if(msg["unpublished"] !== undefined && msg["unpublished"] !== null) {
											// One of the publishers has unpublished?
											var unpublished = msg["unpublished"];
											Janus.log("Publisher left: " + unpublished);
											if(unpublished === 'ok') {
												// That's us
												sfutest.hangup();
												return;
											}
											var remoteFeed = null;
											for(var i=1; i<6; i++) {
												if(feeds[i] != null && feeds[i] != undefined && feeds[i].rfid == unpublished) {
													remoteFeed = feeds[i];
													break;
												}
											}
											if(remoteFeed != null) {
												Janus.log("Feed " + remoteFeed.rfid + " (" + remoteFeed.rfdisplay + ") has left the room, detaching");
												remoteFeed.detach();
											}
										} else if(msg["error"] !== undefined && msg["error"] !== null) {
											if(msg["error_code"] === 426) {
												// This is a "no such room" error: give a more meaningful description
												alert(
													"<p>Apparently room <code>" + myroom + "</code> (the one this demo uses as a test room) " +
													"does not exist...</p><p>Do you have an updated <code>janus.plugin.videoroom.cfg</code> " +
													"configuration file? If not, make sure you copy the details of room <code>" + myroom + "</code> " +
													"from that sample in your current configuration file, then restart Janus and try again."
												);
											} else {
												alert(msg["error"]);
											}
										}
									}
								}
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
					// remoteFeed.videoCodec = video;
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
					element = $(`video.device-video[data-room-id=${room_id}][data-display-name=${display}]`);
					Janus.attachMediaStream(element.get(0), stream);
				},
				oncleanup: function () {
					Janus.log(" ::: Got a cleanup notification (remote feed " + id + ") :::");// TODO Add notification that there is no video signal
				}
			});
	}
};
