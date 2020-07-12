<template>
            <ul
                v-click-outside="hideOptions"
                class="popper"
                v-show="areOptionsVisible"
            >
            <li>
                <a href="#"
                   v-on:click="deleteFiles"
                >
                    <inline-svg
                        v-bind:src="require('../../../../wwwroot/images/assets/system-thrash.svg')">
                    </inline-svg>
                    Delete...
                </a>
            </li>
            <li>
                <a href="#"
                   v-on:click="downloadFiles"
                >
                    <inline-svg
                        v-bind:src="require('../../../../wwwroot/images/assets/timeline-download.svg')">
                    </inline-svg>
                    Download...
                </a>
            </li>
            <li>
                <a href="#"
                   v-on:click="addTags"
                >
                    <inline-svg
                        v-bind:src="require('../../../../wwwroot/images/assets/timeline-tag-on.svg')">
                    </inline-svg>
                    Add Tags...
                </a>
            </li>
        </ul>
</template>

<script>
    import InlineSvg from 'vue-inline-svg';
    import GalleryAddTagModal from "./GalleryAddTagModal";
    import DropDown from './../../components/mixins/dropdown-mixin';

    export default {
        name: "GallerySettingsDropDown",
        components: {
            InlineSvg,
            GalleryAddTagModal,
        },
        methods: {
            deleteFiles() {
                this.hideOptions();
                this.$emit('update:selection-event', 'DELETE') ;
            },
            downloadFiles() {
                this.hideOptions();
                this.$emit('update:selection-event', 'DOWNLOAD') ;
            },
            addTags() {
                this.hideOptions();
                this.$emit('update:selection-event', 'ADD_TAGS') ;
            }
        },
        mixins: [DropDown],
    }
</script>

<style scoped>
    .popper {
        position: absolute;
        z-index: 9998;
        min-width: 160px;
        padding: 5px 0;
        font-size: 14px;
        text-align: left;
        list-style: none;
        background-color: #fff;
        -webkit-background-clip: padding-box;
        background-clip: padding-box;
        border: 1px solid rgba(0, 0, 0, 0.15);
        border-radius: 4px;
    }

    .popper > li > a {
        display: block;
        padding: 3px 20px;
        clear: both;
        font-weight: 400;
        line-height: 1.42857143;
        color: #333;
        white-space: nowrap;
    }
</style>
