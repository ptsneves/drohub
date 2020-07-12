<template>
    <vue-player
        ref="player"
        v-bind:src="getSrcURL"
        v-bind:show-volume="false"
        v-bind:playsinline="true"
        video-playing-overlay="vue-video-player-overlay"
        controls-class="video-player-bottom"
        play-button-class="main-controls-left"
        timer-class="main-controls-left"
        fullscreen-button-class="main-controls-right"
        range-controls-class="range-bar"
    >
        <template v-slot:play-button-content>
            <inline-svg
                class="overlay-button"
                v-bind:src="require('../../../../wwwroot/images/assets/video-play.svg')"
            />
        </template>
        <template v-slot:pause-button-content>
            <inline-svg
                class="overlay-button"
                v-bind:src="require('../../../../wwwroot/images/assets/video-pause.svg')"
            />
        </template>
        <template v-slot:fullscreen-button-content>
            <inline-svg
                class="overlay-button"
                v-bind:src="require('../../../../wwwroot/images/assets/expand-solid.svg')"
            />
        </template>
        <template v-slot:play-button-overlay-content>
            <inline-svg
                class="overlay-button"
                v-bind:src="require('../../../../wwwroot/images/assets/video-play.svg')"
            />
        </template>
        <template v-slot:optional-settings-controls>
            <p-button
                class="main-controls-right"
                v-on:click="activateVideoDropUp"
            >
                <inline-svg
                    class="overlay-button"
                    v-bind:src="require('../../../../wwwroot/images/assets/3-vertical-dots.svg')"
                />
            </p-button>
            <video-drop-up-menu
                v-bind:video-src="videoSrc"
                ref="dropdown">
            </video-drop-up-menu>
        </template>

    </vue-player>
</template>

<script>
import vuePlayer, { pButton } from '@algoz098/vue-player'
import InlineSvg from 'vue-inline-svg';
import VideoDropUpMenu from "./GalleryVideoDropUpMenu";

export default {
    name: "GalleryVideoPlayer",
    components: {
        vuePlayer,
        InlineSvg,
        pButton,
        VideoDropUpMenu,
    },
    props: {
        videoSrc: {
            type: String,
            required: true,
        },
    },
    methods: {
        activateVideoDropUp() {
            this.$refs.player.doPause();
            this.$refs.dropdown.showOptions();
        }
    },
    computed: {
        getSrcURL() {
            return "/DHub/DeviceRepository/GetLiveStreamRecordingVideo?video_id=" + this.videoSrc;
        },
    }
}
</script>

<style>
    svg.overlay-button {
        fill: white;
    }
    .range-bar {
        display: inline-block;
        width: 100%;
    }

    .main-controls-left {
        float: left;
    }

    .main-controls-right {
        float: right;
    }

    .vue-video-player-overlay {
        height: 100%;
        z-index: 1;
        color: white;
        font-size: 16px;
        margin:auto;
        text-align: center;
        left: 0;
        right: 0;
    }

    .video-player-bottom {
        position: absolute;
        bottom: 5%;
        width: 100%;
        background-color: rgba(0,0,0, 0.6);
        box-shadow: 0 0 5px black;
    }

    .vue-settings-box {

    }
</style>
