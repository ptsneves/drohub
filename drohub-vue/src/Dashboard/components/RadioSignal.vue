<template>
    <div class="minimum-size">
        <inline-svg
            v-bind:src="getRadioSignalIcon"
        ></inline-svg>
    </div>
</template>

<script>
    import InlineSvg from 'vue-inline-svg';
    import TelemetryMixin from '../mixins/telemetry-mixin';
    export default {
        name: "RadioSignal",
        mixins: [TelemetryMixin],
        components: {
            InlineSvg,
        },
        created() {
            this.$telemetryhub.$on('drone-radio-signal-changed', this.onRadioSignalChanged);
        },
        props: {
            initialRssi: {
                type: Number,
                required: false,
                default: 0,
            },
            initialQuality: {
                type: Number,
                required: false,
                default: 0,
            },
            serial: {
                type: String,
                required: true,
            }
        },
        data() {
            return {
                rssi: this.initialRssi,
                quality: this.initialQuality
            }
        },
        computed: {
            getRadioSignalIcon() {
                if (this.inRange(this.quality, -1, 2))
                    return require('../../../../wwwroot/images/assets/video-info-controlsignal-bad.svg');
                else if (this.inRange(this.quality, 2, 3))
                    return require('../../../../wwwroot/images/assets/video-info-controlsignal-med.svg');
                else if (this.inRange(this.quality, 4, 6))
                    return require('../../../../wwwroot/images/assets/video-info-controlsignal-good.svg');

                return require('../../../../wwwroot/images/assets/video-info-controlsignal-disconnected.svg')
            }
        },
        methods: {
            onRadioSignalChanged(msg) {
                if (msg.Serial === this.serial) {
                    this.rssi = msg.Rssi;
                    this.quality = msg.SignalQuality;
                }
            },
        }
    }
</script>

<style scoped>

</style>
