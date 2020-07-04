<template>
    <ul
        v-click-outside="hideOptions"
        style= "display:block; top: -170%; left: 30%;"
        class="dropdown-menu"
        v-if="areOptionsVisible"
    >
        <li>
            <a
               v-on:click="showDeleteFilesModal"
               >
                <inline-svg
                    v-bind:src="require('../../../wwwroot/images/assets/system-thrash.svg')">
                </inline-svg>
                Delete
            </a>
        </li>
        <li>
            <a v-bind:href="getDownloadURL">
                <inline-svg
                    v-bind:src="require('../../../wwwroot/images/assets/timeline-download.svg')">
                </inline-svg>
                Download
            </a>
        </li>
        <li>
            <a
                v-on:click="showAddTagModal">
                <inline-svg
                    v-bind:src="require('../../../wwwroot/images/assets/timeline-tag-on.svg')">
                </inline-svg>
                Add Tags
            </a>
        </li>
    </ul>
</template>

<script>
    import InlineSvg from 'vue-inline-svg';
    import GalleryAddTagModal from "./GalleryAddTagModal";
    import DropDown from './mixins/dropdown-mixin.js';

    export default {
        name: "VideoDropUpMenu",
        mixins: [DropDown],
        components: {
            InlineSvg,
            GalleryAddTagModal,
        },
        props: {
            videoSrc: {
                type: String,
                required: true,
            },
        },
        computed: {
            getDownloadURL() {
                return "/DHub/DeviceRepository/DownloadVideo?video_id=" + this.videoSrc;
            },
        },
        methods: {
            showAddTagModal() {
                this.$store.commit('SET_MODAL_MODEL', {
                    type: 'ADD_TAGS',
                    TimeStampInSeconds: '00:00',
                    MediaIdListJSON: [this.videoSrc],
                    UseTimeStamp: false,
                });
            },
            showDeleteFilesModal() {
                this.$store.commit('SET_MODAL_MODEL', {
                    type: 'DELETE_FILES',
                    MediaIdList: [this.videoSrc],
                    }
                );
            },
        }
    }
</script>

<style scoped>
    a {
        cursor: pointer;
    }
</style>
