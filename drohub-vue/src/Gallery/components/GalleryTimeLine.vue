<template>
    <div>
        <gallery-settings-drop-down
            v-on:update:selection-event="onGallerySettingsSelection"
            ref="settings"/>

        <gallery-add-tag-modal
            v-if="allowSettings"
            v-bind:add-tags-post-url="addTagsPostUrl"
        />
        <gallery-delete-files-modal
            v-if="allowSettings"
            v-bind:delete-media-objects-post-url="deleteMediaObjectsPostUrl"
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
        <ul class="timeline" v-for="unix_day_timestamp in getEventList" v-bind:key="unix_day_timestamp" >
            <li class="drohub-time-label">
                <time-label class='time'
                            v-bind:unix-time-stamp="unix_day_timestamp"
                            unix-time-stamp-units="ms"
                            v-bind:show-only-date="true">
                </time-label>
            </li>

            <li v-for="(session, session_start_timestamp, index) in last_filtered_model[unix_day_timestamp]" v-bind:key="index">

                <span class="drohub-glyphicon">
                    <inline-svg
                        v-bind:src="require('../../../../wwwroot/images/assets/device-id-drone.svg')"
                    />
                </span>
                <div class="drohub-gallery-content-margin timeline-item">
                    <h3 class="drohub-timeline-header">
                        {{ session.DeviceName }}  [{{ _MIXIN_TIMESTAMP_getTimeText(session.StartTime, 'ms', false) }} - {{ _MIXIN_TIMESTAMP_getTimeText(session.EndTime, 'ms', false) }}]
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
                                v-for="(file, _) in session.SessionMedia"
                                v-bind:key="file.PreviewMediaPath"
                            >
                                <gallery-item-select
                                    v-on:update:selection-event="onGalleryItemSelected"
                                    v-bind:item-id="file.MediaPath"
                                    v-bind:preview-id="file.PreviewMediaPath"
                                    v-bind:is-enabled="isSelectionOn"
                                    v-bind:is-item-selected="isItemSelected(file.PreviewMediaPath, file.MediaPath)"
                                />
                                <inline-svg
                                    v-if="isImage(file.PreviewMediaPath)"
                                    class="media-type-thumbnail"
                                    title="Photo"
                                    v-bind:src="require('../../../../wwwroot/images/assets/timeline-thumbnail-photo.svg')"

                                />
                                <inline-svg
                                    v-if="isVideo(file.PreviewMediaPath)"
                                    title="Recording"
                                    class="media-type-thumbnail"
                                    v-bind:src="require('../../../../wwwroot/images/assets/timeline-thumbnail-recording.svg')"
                                    v-bind:transform-source="transform"
                                />

                                <gallery-video-player
                                    v-if="isVideo(file.PreviewMediaPath)"
                                    v-bind:get-live-stream-recording-video-url="getLiveStreamRecordingVideoUrl"
                                    v-bind:download-video-url="downloadVideoUrl"
                                    v-bind:video-preview-id="file.PreviewMediaPath"
                                    v-bind:allow-settings="allowSettings"
                                    v-bind:video-id="file.MediaPath"
                                />
                                <img
                                    class="gallery-image"
                                    v-else-if="isImage(file.PreviewMediaPath)"
                                    v-bind:src="getImageSrcURL(file.PreviewMediaPath)"
                                    alt="Captured picture"
                                />
                                <media-tag-label
                                    v-show="show_tags"
                                    v-for="tag in file.Tags"
                                    v-bind:key="tag"
                                    v-bind:media-id="file.PreviewMediaPath"
                                    v-bind:allow-delete="allowSettings"
                                    v-bind:delete-tags-post-url="deleteTagsPostUrl"
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
    import Timestamp from './../../components/mixins/timestamp-mixin';

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
            addTagsPostUrl: {
                type: String,
                required: true,
            },
            allowSettings: {
                type: Boolean,
                required: true,
            },
            deleteMediaObjectsPostUrl: {
                type: String,
                required: true,
            },
            deleteTagsPostUrl: {
                type: String,
                required: true,
            },
            downloadMediasUrl: {
                type: String,
                required: true,
            },
            downloadVideoUrl: {
                type: String,
                required: true,
            },
            getLiveStreamRecordingVideoUrl: {
                type: String,
                required: true,
            },
            getPhotoUrl: {
                type: String,
                required: true,
            },
        },
        data() {
            return {
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
            transform(svg) {
                let ids = svg.querySelectorAll('[id]');

                Array.prototype.forEach.call( ids, function( el, _ ) {
                    let classes = el.id;
                    if (el.class !== undefined)
                        classes = el.class + " " + classes;
                    el.removeAttributeNS(null, 'id')
                    el.setAttributeNS(null, 'class', classes);
                });
                return svg;
            },
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
            isItemSelected(preview_item_id, item_id) {
                return this.isSelectionOn
                    && (this.selection_model.selected_medias.indexOf(preview_item_id) > -1
                        || (item_id !== "" && this.selection_model.selected_medias.indexOf(item_id) > -1));
            },
            isVideo(file_path) {
                return file_path.endsWith('.webm') || file_path.endsWith('.mp4');
            },
            isImage(file_path) {
                return file_path.endsWith('.jpeg') || file_path.endsWith('.png');
            },
            getImageSrcURL(photo_id) {
                return this.getPhotoUrl + photo_id;
            },
        },
        computed: {
            getDownloadsURL() {
                if (this.selection_model.type === 'DOWNLOAD' && this.hasMoreThan1Selection)
                    return this.downloadMediasUrl + qs.stringify({
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

                const unix_days = Object.keys(this.gallery_model).sort(function(a,b) {
                    return Number(a) - Number(b);
                }).reverse();

                for (let unix_date of unix_days) {
                    for(let session_timestamp in this.gallery_model[unix_date]) {
                        let new_session_file_list = [];
                        for (let media_index = 0; media_index < this.gallery_model[unix_date][session_timestamp]["SessionMedia"].length; media_index++) {
                            this.entries_available++;
                            if (this.entries_visible < this.max_entries_visible
                                && hasInterSection(selected_tags, this.gallery_model[unix_date][session_timestamp]["SessionMedia"][media_index]["Tags"]) === true) {

                                new_session_file_list.push(this.gallery_model[unix_date][session_timestamp]["SessionMedia"][media_index]);
                                this.entries_visible++;
                            }
                        }

                        if (new_session_file_list.length === 0)
                            delete new_model[unix_date][session_timestamp];
                        else
                            new_model[unix_date][session_timestamp]["SessionMedia"] = new_session_file_list;
                    }
                    if (Object.keys(new_model[unix_date]).length === 0)
                        delete new_model[unix_date];
                }


                this.last_filtered_model = new_model;

                return Object.keys(new_model).sort(function(a,b) {
                    return Number(a) - Number(b);
                }).reverse();
            },
        },
        mixins: [Timestamp],
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

img.gallery-image {
    width: 100%;
    border-radius: 4px;
}

.timeline > li > .timeline-item > .drohub-timeline-header {
    margin: 0;
    color: #555;
    border-bottom: 2px solid #d7d7d7;
    padding: 10px;
    font-size: 20px;
    line-height: 1.1;
    border-top: 4px solid;
    border-top-right-radius: 4px;
    border-top-left-radius: 4px;
    border-top-color: #1099ff;
}

.timeline > .drohub-time-label > span {
    font-weight: 700;
    padding: 7px 18px 7px 18px;
    font-size: 18px;
    display: inline-block;
    background-color: #384156;
    border-radius: 19px;
    color: white;
}

.media-type-thumbnail {
    position: absolute;
    z-index: 10;
    top: 5%;
    left: 8%;
    opacity: 80%;
}
</style>
