<template>
    <div>
        <gallery-settings-drop-down
            v-on:update:selection-event="onGallerySettingsSelection"
            ref="settings"/>

        <gallery-add-tag-modal
            v-if="allowSettings"
        />
        <gallery-delete-files-modal
            v-if="allowSettings"
        />
        <div
            class="selection-confirmation-bar"
            v-if="isSelectionOn"
        >
            <a class="btn btn-success btn-flat"
                    v-on:click="onSelectionConfirmed"
                    v-bind:href="getDownloadsURL"
            >
                <i class="fa fa-check"></i>
            </a>
            <a class="btn btn-danger btn-flat"
                    v-on:click="onCancelSelection"
            ><i class="fa fa-times"></i></a>
        </div>
        <ul class="timeline" v-for="unix_timestamp in getEventList" v-bind:key="unix_timestamp" >
            <li class="drohub-time-label">
                <time-label class='time'
                            v-bind:unix-time-stamp="unix_timestamp"
                            unix-time-stamp-units="ms"
                            v-bind:show-only-date="true">
                </time-label>
            </li>

            <li v-for="(device_files, device_name, index) in last_filtered_model[unix_timestamp]" v-bind:key="index">

                <span class="drohub-glyphicon">
                    <inline-svg
                        v-bind:src="require('../../../../wwwroot/images/assets/device-id-drone.svg')"
                    />
                </span>
                <div class="drohub-gallery-content-margin timeline-item">
                    <h3 class="drohub-timeline-header">
                        {{ device_name }}
                        <span class="pull-right">
                            <button
                                v-on:click="toggleTags"
                                class="btn btn-link"
                            >
                                <inline-svg
                                    v-show="show_tags"
                                    v-bind:src="require('../../../../wwwroot/images/assets/timeline-tag-off.svg')"
                                />
                                <inline-svg
                                    v-show="!show_tags"
                                    v-bind:src="require('../../../../wwwroot/images/assets/timeline-tag-on.svg')"
                                />
                            </button>
                            <button
                                v-on:click="openGallerySettings"
                                v-if="allowSettings"
                                class="btn btn-link"
                            >
                                <inline-svg
                                    v-bind:src="require('../../../../wwwroot/images/assets/device-window-settings.svg')"
                                />
                            </button>
                    </span>
                    </h3>
                    <div class="container-fluid content-centered timeline-body">
                        <div class="row equal-row-height">
                            <div
                                class="col-lg-3 col-md-6 col-sm-12 gallery-item"
                                v-bind:class="{'selectable-item': isSelectionOn}"
                                v-for="(file, file_index) in device_files"
                                v-bind:key="file.media_object.MediaPath"
                            >
                                <gallery-item-select
                                    v-on:update:selection-event="onGalleryItemSelected"
                                    v-bind:item-id="file.media_object.MediaPath"
                                    v-bind:is-enabled="isSelectionOn"
                                    v-bind:is-item-selected="isItemSelected(file.media_object.MediaPath)"
                                />
                                <gallery-video-player
                                    v-bind:videoSrc="file.media_object.MediaPath"
                                    v-bind:allow-settings="allowSettings"
                                />
                                <media-tag-label
                                    v-show="show_tags"
                                    v-for="tag in file.media_object.Tags"
                                    v-bind:key="tag"
                                    v-bind:media-id="file.media_object.MediaPath"
                                    v-bind:allow-delete="allowSettings"
                                    v-bind:text="tag">
                                </media-tag-label>
                            </div>
                        </div>
                    </div>
                </div>
            </li>
        </ul>
        <div v-waypoint="{ active: true, callback: onWaypoint }">
            <span v-show="entries_visible < entries_available">There are more medias available. Keep scrolling or select a tag to load more videos.</span>
        </div>
    </div>
</template>

<script>
    import { createPopper } from '@popperjs/core';
    import InlineSvg from 'vue-inline-svg';
    import TimeLabel from "../../components/TimeLabel";
    import GalleryVideoPlayer from "./GalleryVideoPlayer";
    import GallerySettingsDropDown from "./GallerySettingsDropDown";
    import GalleryItemSelect from "./GalleryItemSelect";
    import GalleryAddTagModal from "./GalleryAddTagModal";
    import GalleryDeleteFilesModal from "./GalleryDeleteFilesModal";
    import qs from 'qs';

    export default {
        name: "GalleryTimeLine",
        components: {
            GalleryDeleteFilesModal,
            GalleryAddTagModal,
            GalleryItemSelect,
            InlineSvg,
            GalleryVideoPlayer,
            GallerySettingsDropDown,
            TimeLabel,
        },
        props: {
            allowSettings: {
                type: Boolean,
                required: true,
            },
        },
        data() {
            return {
                DOWNLOADS_POST_LOCATION: "/DHub/DeviceRepository/DownloadMedias",
                gallery_model: window.init_data,
                last_filtered_model: {},
                last_filtered_tags: [],
                entries_visible: 0,
                entries_available: 0,
                max_entries_visible: 5,
                MAX_ENTRIES_INCREMENT: 5,
                show_tags: true,
                sorted_keys: [],
                selection_model: this.getResetSelectionModel(),

            };
        },
        methods: {
            getResetSelectionModel() {
                return {
                    type: "INACTIVE",
                    selected_medias: [],
                };
            },
            resetSelectionModel() {
                this.selection_model = this.getResetSelectionModel();
            },
            toggleTags() {
                this.show_tags = !this.show_tags;
            },
            onWaypoint ({ element, going, direction }) {
                // going: in, out
                // direction: top, right, bottom, left
                if (going === this.$waypointMap.GOING_IN) {
                    this.max_entries_visible += this.MAX_ENTRIES_INCREMENT;
                }
            },
            onGalleryItemSelected(event) {
                const selected_media = event.itemId
                const index = this.selection_model.selected_medias.indexOf(selected_media);
                if (index > -1)
                    this.selection_model.selected_medias.splice(index, 1);
                else
                    this.selection_model.selected_medias.push(selected_media);
            },
            onGallerySettingsSelection(settings_type) {
                this.selection_model.type = settings_type
            },
            openGallerySettings(event) {
                event.stopPropagation();
                this.$refs.settings.showOptions();
                createPopper(event.target, this.$refs.settings.$el, {
                    placement: 'left',
                    closeOnClickOutside: true,
                });
            },
            setModalModel(payload) {
                this.$store.commit('SET_MODAL_MODEL', payload);
                this.resetSelectionModel()
            },
            onSelectionConfirmed(event) {
                switch(this.selection_model.type) {
                    case 'ADD_TAGS':
                        this.setModalModel({
                            type: 'ADD_TAGS',
                            TimeStampInSeconds: '00:00',
                            MediaIdListJSON: this.selection_model.selected_medias,
                            UseTimeStamp: false,
                        });
                        event.preventDefault();
                        break;
                    case 'DELETE':
                        this.setModalModel({
                            type: 'DELETE_FILES',
                            MediaIdList: this.selection_model.selected_medias,
                        });
                        event.preventDefault();
                        break;
                    case 'DOWNLOAD':
                        this.resetSelectionModel();
                }
            },
            onCancelSelection() {
                this.resetSelectionModel();
            },
            isItemSelected(item_id) {
                return this.isSelectionOn && this.selection_model.selected_medias.indexOf(item_id) > -1;
            },
        },
        computed: {
            getDownloadsURL() {
                if (this.selection_model.type === 'DOWNLOAD' && this.hasMoreThan1Selection)
                    return '/DHub/DeviceRepository/DownloadMedias?' + qs.stringify({
                        'MediaIdList': this.selection_model.selected_medias
                    });
                else
                    return '#'
            },
            isSelectionOn() {
                return this.selection_model.type !== "INACTIVE";
            },
            hasMoreThan1Selection() {
                return this.isSelectionOn && this.selection_model.selected_medias.length > 0;
            },
            getEventList() {
                this.entries_visible = 0;
                this.entries_available = 0;
                const selected_tags = this.$store.state.filtered_tags;

                let new_model = JSON.parse(JSON.stringify(this.gallery_model));

                const unix_times = Object.keys(this.gallery_model).sort(function(a,b) {
                    return Number(a) - Number(b);
                }).reverse();

                for (let unix_date of unix_times) {
                    for(let device_name in this.gallery_model[unix_date]) {
                        let new_device_file_list = [];

                        for (let file_index = 0; file_index < this.gallery_model[unix_date][device_name].length; file_index++) {
                            this.entries_available++;
                            if (this.entries_visible < this.max_entries_visible &&
                                    hasInterSection(selected_tags, this.gallery_model[unix_date][device_name][file_index]["media_object"]["Tags"]) === true) {

                                new_device_file_list.push(this.gallery_model[unix_date][device_name][file_index]);
                                this.entries_visible++;
                            }
                        }

                        if (new_device_file_list.length === 0)
                            delete new_model[unix_date][device_name];
                        else
                            new_model[unix_date][device_name] = new_device_file_list;
                    }
                    if (Object.keys(new_model[unix_date]).length === 0)
                        delete new_model[unix_date];
                }


                this.last_filtered_model = new_model;

                return Object.keys(new_model).sort(function(a,b) {
                    return Number(a) - Number(b);
                }).reverse();
            },
        }
    }

    function hasInterSection(set, super_set) {
        return set.filter(x => super_set.includes(x)).length === set.length;
    }

</script>

<style scoped>
.drohub-glyphicon {
    width: 52px;
    height: 52px;
    position: absolute;
    border-radius: 50%;
    text-align: center;
    line-height: 68px;
    left: 7px;
    top: 0;
    fill: white;
    background: #1099FF;
}

.equal-row-height {
    display: flex;
    flex-wrap: wrap;
    display: -webkit-flex;
    display: -webkit-box;
    display: -webkit-flex;
    display: -ms-flexbox;
}

.gallery-item {
    padding-block: 10px;
}

.selection-confirmation-bar {
    position: fixed;
    top: 0;
    left: 0;
    width: 100%;
    z-index: 9998;
    background: #1099ff;
    text-align: center;
}

</style>
