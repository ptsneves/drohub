import { HubConnectionBuilder, LogLevel } from '@aspnet/signalr'
export default {
    install (Vue) {
        const telemetryHub = new Vue();
        Vue.prototype.$telemetryhub = telemetryHub;

        let connection = null;
        let startedPromise = null;
        let manuallyClosed = false;

        Vue.prototype.startSignalR = function () {
            connection = new HubConnectionBuilder()
                .withUrl("/telemetryhub")
                .configureLogging(LogLevel.Information)
                .build();

            function setupTelemetryListeners() {
                connection.on("DroneBatteryLevel", message => {
                    telemetryHub.$emit('drone-battery-level-changed', JSON.parse(message));
                });
                connection.on("DroneRadioSignal", message => {
                    telemetryHub.$emit('drone-radio-signal-changed', JSON.parse(message));
                });
                connection.on("CameraState", message => {
                    telemetryHub.$emit('camera-state-changed', JSON.parse(message));
                });
                connection.on("GimbalState", message => {
                    telemetryHub.$emit('gimbal-state-changed', JSON.parse(message));
                });
            }

            setupTelemetryListeners();

            function start () {
                startedPromise = connection.start().catch(err => {
                    console.error('Failed to connect with hub', err);
                    return new Promise((resolve, reject) =>
                        setTimeout(() => start().then(resolve).catch(reject), 5000));
                })
                return startedPromise
            }
            connection.onclose(() => {
                if (!manuallyClosed) start();
            })

            // Start everything
            manuallyClosed = false;
            start();
        }

        Vue.prototype.stopSignalR = () => {
            if (!startedPromise) return

            manuallyClosed = true
            return startedPromise
                .then(() => connection.stop())
                .then(() => { startedPromise = null })
        }
    }
}
