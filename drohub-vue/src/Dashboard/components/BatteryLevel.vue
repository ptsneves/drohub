<template>
    <div class="minimum-size">
        <inline-svg
            v-bind:src="getBatteryIcon"
        ></inline-svg>
        <span>{{ this.level }}%</span>
    </div>
</template>

<script>
    import InlineSvg from 'vue-inline-svg';
    import TelemetryMixin from '../mixins/telemetry-mixin';
    export default {
        name: "BatteryLevel",
        mixins: [TelemetryMixin],
        components: {
            InlineSvg,
        },
        props: {
            initialLevel: {
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
                level: this.initialLevel
            }
        },
        created() {
            this.$telemetryhub.$on('drone-battery-level-changed', this.onBatteryLevelChanged);
        },
        computed: {
            getBatteryIcon() {
                if (this.inRange(this.level, 0, 24))
                    return require('../../../../wwwroot/images/assets/video-info-battery-empty.svg');
                else if (this.inRange(this.level, 25, 49))
                    return require('../../../../wwwroot/images/assets/video-info-battery-halfempty.svg');
                else if (this.inRange(this.level, 50, 74))
                    return require('../../../../wwwroot/images/assets/video-info-battery-halffull.svg');
                else if (this.inRange(this.level, 75, 100))
                    return require('../../../../wwwroot/images/assets/video-info-battery-full.svg');

                return require('../../../../wwwroot/images/assets/video-info-battery-empty.svg')
            },
        },
        methods: {
            onBatteryLevelChanged(msg) {
                if (msg.Serial === this.serial)
                    this.level = msg.BatteryLevelPercent;
            },
        }
    }
</script>

<style scoped>
</style>
