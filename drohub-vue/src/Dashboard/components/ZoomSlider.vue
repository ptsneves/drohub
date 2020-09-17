<template>
    <div class="telemetry-element">
        <button class="btn-transparent-bg" v-on:click="toggleVisible">
            <i class="fas fa-search-plus"></i>
        </button>
        <div class="slider-container"
             v-click-outside="toggleVisible"
             v-if="is_slider_visible">
            <span class="slider-label">-</span>
            <vue-slider
                ref="slider"
                v-model="zoom_level"
                direction="ttb"
                v-bind:height="height"
                v-bind:min="min_zoom"
                v-bind:max="max_zoom"
                v-bind:interval="interval"
                v-bind:tooltip-formatter="txt => getToolTipText(txt)"
                v-bind:lazy="true"
                v-on:drag-end="onZoomSet"
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
        name: "ZoomSlider",
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
            zoomSetUrl: {
                type: String,
                required: true,
            },
            antiForgeryToken: {
                type: String,
                required: true,
            },
            initialCameraState: {
                type: Object,
                required: false,
                default: () => ({
                    Mode: 0,
                    ZoomLevel: 1.0,
                    MaxZoom: 1.0,
                    MinZoom: 1.0,
                })
            }
        },
        created() {
            this.$telemetryhub.$on('camera-state-changed', this.onCameraStateChanged);
        },
        data() {
            return {
                ZOOM_SET_URL: this.zoomSetUrl,
                is_slider_visible: false,
                zoom_level: this.centimate(this.initialCameraState.ZoomLevel),
                max_zoom: this.centimate(this.initialCameraState.MaxZoom),
                min_zoom: this.centimate(this.initialCameraState.MinZoom),
                interval: 0.01,
            };
        },
        methods: {
            centimate(val) {
                return Math.round(val * 100) / 100;
            },
            getToolTipText(txt) {
                return `x${txt}`;
            },
            toggleVisible() {
                if (this.max_zoom === this.min_zoom)
                    return;
                this.is_slider_visible = !this.is_slider_visible;
            },
            onCameraStateChanged(msg) {
                if (msg.Serial !== this.serial)
                    return;
                this.min_zoom = this.centimate(msg.MinZoom);
                this.max_zoom = this.centimate(msg.MaxZoom);
                this.zoom_level = this.centimate(msg.ZoomLevel);
            },
            onZoomSet(index) {
                axios.post(this.ZOOM_SET_URL, qs.stringify({
                    'serial': this.serial,
                    'zoom_level': this.zoom_level,
                    '__RequestVerificationToken': this.antiForgeryToken,
                }));
            }
        }
    }
</script>

<style scoped>
    @import "../css/telemetry.css";
    .slider-container {
        background-color: #282828;
        padding: 0 13px;
        border-radius: 25px;
        position: absolute;
    }
    .slider-label {
        width: 100%;
        padding-bottom: 4px;
        padding-top: 4px;
        display: inline-block;
        text-align: center;
    }
</style>
