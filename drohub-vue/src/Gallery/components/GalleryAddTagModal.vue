<template>
    <div class="gallery-add-tag-modal" v-if="isActiveModal">
        <transition name="modal">
            <div class="modal-mask">
                <div class="modal-wrapper">
                    <div class="modal-dialog">
                        <div class="modal-content">
                            <div class="modal-header">
                                <button type="button" class="close" v-on:click="hideModal">
                                    <span aria-hidden="true">&times;</span>
                                </button>
                                <h4 class="modal-title">Add Tags</h4>
                            </div>
                            <div class="modal-body">
                                <ul >
                                    <li v-for="files in selectedFiles">
                                        {{files}}
                                    </li>
                                </ul>

                                <span>
                                    Insert ',' (comma) separated Tag List, eg: Tag,TagA,Tag DF. At least one tag needs to be added.
                                </span>

                                <vue-tags-input
                                    v-model="tag"
                                    v-bind:tags="tags"
                                    v-on:tags-changed="newTags => this.tags = newTags"
                                    v-bind:add-on-key="[13, ',', ';']"
                                ></vue-tags-input>
                            </div>
                            <div class="modal-footer">
                                <button
                                    class="btn btn-primary"
                                    v-bind:class="{ disabled: noTags }"
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
    import VueTagsInput from '@johmun/vue-tags-input';
    import axios from 'axios';
    import qs from 'qs';

    export default {
        name: "GalleryAddTagModal",
        props: {
            addTagsPostUrl: {
                type: String,
                required: true,
            },
            antiForgeryToken: {
                type: String,
                required: true
            },
        },
        components: {
            VueTagsInput,
        },
        data() {
            return {
                MODAL_TYPE: 'ADD_TAGS',
                tag: '',
                tags: [],
            };
        },
        computed: {
            noTags() {
                return this.tags.length === 0 ? 'disabled' : '';
            },
            isActiveModal() {
                return this.$store.state.modal_model.type === this.MODAL_TYPE;
            },
            selectedFiles() {
                return this.$store.state.modal_model.MediaIdListJSON;
            },
        },
        methods: {
            submit() {
                if (this.noTags === true || !this.isActiveModal)
                    return;

                axios
                    .post(this.addTagsPostUrl, qs.stringify({
                        'TagList': this.tags.map(t => t.text),
                        'TimeStampInSeconds': this.$store.state.modal_model.TimeStampInSeconds,
                        '__RequestVerificationToken': this.antiForgeryToken,
                        'MediaIdList': this.selectedFiles,
                        'UseTimeStamp': this.$store.state.modal_model.UseTimeStamp,
                    }))
                    .then(function() {
                        window.location.reload(true);
                    });
            },
            hideModal() {
                return this.$store.commit('SET_MODAL_MODEL', {type:'INACTIVE'});
            }
        }
    }
</script>

<style lang="css" scoped>
    @import '../../assets/modal.css';
    .vue-tags-input {
        max-width: 450px;
        position: relative;
        background-color: #fff;
    }

</style>
