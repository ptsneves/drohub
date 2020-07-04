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
                                <h4 class="modal-title">Delete files</h4>
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
        data() {
            return {
                MODAL_TYPE: 'DELETE_FILES',
                POST_LOCATION: '/DHub/DeviceRepository/DeleteMediaObjects',
            };
        },
        computed: {
            isActiveModal() {
                return this.$store.state.modal_model.type === this.MODAL_TYPE;
            },
            selectedFiles() {
                return this.$store.state.modal_model.MediaIdList;
            },
        },
        methods: {
            hideModal() {
                return this.$store.commit('SET_MODAL_MODEL', {type:'INACTIVE'});
            },
            submit() {
                if (this.selectedFiles.length === 0 || ! this.isActiveModal)
                    console.error("Delete modal open but no item selected or modal inactive.");

                axios
                    .post(this.POST_LOCATION, qs.stringify({
                        'MediaIdList': this.selectedFiles,
                        '__RequestVerificationToken': this.$store.state.anti_forgery_token,
                    }))
                    .then(function() {
                        window.location.reload(true);
                    });
            },
        },
    }
</script>

<style scoped>
    @import '../assets/modal.css';
</style>
