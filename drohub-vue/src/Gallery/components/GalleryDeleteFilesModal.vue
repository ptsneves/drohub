<template>
    <div v-if="isActiveModal">
        <transition name="modal">
            <div class="modal-mask">
                <div class="modal-wrapper">
                    <div class="modal-dialog">
                        <div class="modal-content">
                            <div class="modal-header">
                                <button type="button" class="close" v-on:click="hideModal">
                                    <span aria-hidden="true">&times;</span>
                                </button>
                                <h4 class="modal-title">Delete files in DROHUB</h4>
                            </div>
                            <div class="modal-body">
                                <span>
                                    The following files are to be deleted.
                                </span>
                                <ul >
                                    <li v-for="files in selectedFiles">
                                        {{files}}
                                    </li>

                                </ul>
                            </div>
                            <div class="modal-footer">
                                <span class="text-danger" v-show="isRemovingInBothPlaces">
                                    File(s) will be removed in DROHUB when removed in the device/drone.
                                </span>
                                <span class="text-danger" v-show="isRemovingOnlyInDROHUB">
                                    Files may still exist in the drone
                                </span>
                                <div class="form-group">
                                    <div class="checkbox">
                                        <label for="checkbox_remove_in_device">
                                            Mark for removal in the drone/device?
                                            <input
                                                id="checkbox_remove_in_device"
                                                v-model = "remove_in_device"
                                                type="checkbox">
                                        </label>
                                    </div>
                                    <div class="checkbox">
                                        <label for="checkbox_remove_in_drohub">
                                            Mark for removal in DROHUB?
                                            <input
                                                id="checkbox_remove_in_drohub"
                                                v-model = "remove_in_drohub"
                                                type="checkbox">
                                        </label>
                                    </div>
                                </div>
                                <button
                                    class="btn btn-danger"
                                    v-on:click="submit"
                                >Submit</button>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </transition>
    </div>
</template>

<script>
    import axios from "axios";
    import qs from "qs";

    export default {
        name: "GalleryDeleteFilesModal",
        props: {
            deleteMediaObjectsPostUrl: {
                type: String,
                required: true,
            },
            addTagsPostUrl: {
                type: String,
                required: true,
            },
            droneMediaRemovalTag: {
                type: String,
                required: true,
            },
            delayedMediaRemovalTag: {
                type: String,
                required: true,
            },
        },
        data() {
            return {
                MODAL_TYPE: 'DELETE_FILES',
                remove_in_drohub: false,
                remove_in_device: false,
            };
        },
        computed: {
            isActiveModal() {
                return this.$store.state.modal_model.type === this.MODAL_TYPE;
            },
            selectedFiles() {
                return this.$store.state.modal_model.MediaIdList;
            },
            isRemovingInBothPlaces() {
                return this.remove_in_drohub === true && this.remove_in_device === true;
            },
            isRemovingOnlyInDROHUB() {
                return this.remove_in_drohub === true && this.remove_in_device === false;
            }
        },
        methods: {
            hideModal() {
                return this.$store.commit('SET_MODAL_MODEL', {type:'INACTIVE'});
            },
            submit() {
                function markFilesForDeviceDelete(selected_files, delete_metadata) {
                    axios
                        .post(this.addTagsPostUrl, qs.stringify({
                            'TagList': delete_metadata,
                            'TimeStampInSeconds': 0,
                            '__RequestVerificationToken': this.$store.anti_forgery_token,
                            'MediaIdList': selected_files,
                            'UseTimeStamp': false,
                        }))
                        .then(function() {
                            window.location.reload(true);
                        });
                }

                if (this.selectedFiles.length === 0 || ! this.isActiveModal)
                    console.error("Delete modal open but no item selected or modal inactive.");

                if (this.remove_in_device === true) {
                    let metadata_tags = [];
                    metadata_tags.push(this.droneMediaRemovalTag);
                    if (this.remove_in_drohub === true) {
                        metadata_tags.push(this.delayedMediaRemovalTag);
                    }
                    markFilesForDeviceDelete(this.selectedFiles, metadata_tags);
                }
                else if (this.remove_in_drohub === true) {
                    axios
                        .post(this.deleteMediaObjectsPostUrl, qs.stringify({
                            'MediaIdList': this.selectedFiles,
                            '__RequestVerificationToken': this.$store.state.anti_forgery_token,
                        }))
                        .then(function() {
                            window.location.reload(true);
                        });
                }
            },
        },
    }
</script>

<style scoped>
    @import '../../assets/modal.css';
</style>
