"use strict";

async function initNotifications() {
    let signalr_script_url = "/lib/signalr/dist/browser/signalr.js";
    let signalr_script = document.createElement("script");
    signalr_script.src = signalr_script_url;

    signalr_script.onload = function () {
        var connection = new signalR.HubConnectionBuilder().withUrl("/notificationsHub").
            configureLogging(signalR.LogLevel.Information).build();

        connection.on("notification", function (message) {
            var encodedMsg = message;
            alert(message);
        });

        connection.start().then(function () {
            console.log("Notifications started SIGNALR");
        }).catch(function (err) {
            return console.error(err.toString());
        });
    };

    document.head.appendChild(signalr_script);
}
initNotifications();