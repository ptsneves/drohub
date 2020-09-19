<template>
    <div class="telemetry-element">
        <button class="btn-transparent-bg" v-on:click="toggleVisible">
            <i class="fas fa-arrows-v"></i>
        </button>
        <div class="slider-container"
             v-click-outside="toggleVisible"
             v-if="is_slider_visible">
            <span class="slider-label">-</span>
            <vue-slider
                ref="slider"
                v-model="pitch_level"
                direction="ttb"
                v-bind:height="height"
                v-bind:min="min_pitch"
                v-bind:max="max_pitch"
                v-bind:interval="interval"
                v-bind:tooltip-formatter="txt => getToolTipText(txt)"
                v-bind:lazy="true"
                v-on:drag-end="onPitchSet"
            ></vue-slider>
            <span class="slider-label">+</span>
        </div>
    </div>
</template>

<script>
    import VueSlider from 'vue-slider-component'
    import 'vue-slider-component/theme/default.css'
    import axios from 'axios';
    import qs from 'qs';

    export default {
        name: "GimbalPitchSlider",
        components: {
            VueSlider
        },
        props: {
            height: {
                type: Number,
                required: true,
            },
            serial: {
                type: String,
                required: true,
            },
            attitudeSetUrl: {
                type: String,
                required: true,
            },
            antiForgeryToken: {
                type: String,
                required: true,
            },
            initialGimbalState: {
                type: Object,
                required: false,
                default: () => ({
                    Mode: 0,
                    Pitch: 1.0,
                    MaxPitch: 1.0,
                    MinPitch: 1.0,
                })
            }
        },
        created() {
            this.$telemetryhub.$on('gimbal-state-changed', this.onGimbalStateChanged);
        },
        data() {
            return {
                ATTITUDE_SET_URL: this.attitudeSetUrl,
                is_slider_visible: false,
                pitch_level: this.centimate(this.initialGimbalState.Pitch),
                max_pitch: this.centimate(this.initialGimbalState.MaxPitch),
                min_pitch: this.centimate(this.initialGimbalState.MinPitch),
                interval: 0.01,
            };
        },
        methods: {
            centimate(val) {
                return Math.round(val * 100) / 100;
            },
            getToolTipText(txt) {
                return `${txt} deg`;
            },
            toggleVisible() {
                if (this.max_pitch === this.min_pitch)
                    return;
                this.is_slider_visible = !this.is_slider_visible;
            },
            onGimbalStateChanged(msg) {
                if (msg.Serial !== this.serial)
                    return;
                this.min_pitch = this.centimate(msg.MinPitch);
                this.max_pitch = this.centimate(msg.MaxPitch);
                this.pitch_level = this.centimate(msg.Pitch);
            },
            onPitchSet(index) {
                axios.post(this.ATTITUDE_SET_URL, qs.stringify({
                    'serial': this.serial,
                    'pitch': this.pitch_level,
                    '__RequestVerificationToken': this.antiForgeryToken,
                }));
            }
        }
    }
</script>

<style scoped>
    @import "../css/telemetry.css";
</style>
