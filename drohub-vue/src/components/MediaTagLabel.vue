<template>
        <div
            class="media-tag-label"
            v-if="is_active"
        >
                <span
                    style="cursor: pointer"
                    v-on:click="$store.commit('ADD_TRACKED_TAG', text)"
                >{{ this.text }}</span>
                <i
                    v-if="allowDelete"
                    style="cursor: pointer; padding-left: 10px"
                    v-on:click="deleteTag"
                    class="fa fa-times"
                ></i>
        </div>

</template>

<script>
    import axios from "axios";
    import qs from "qs";

    export default {
        name: "VueTagsInput",
        props: {
            text: {
                type: String,
            },
            mediaId: {
                type: String,
            },
            allowDelete: {
                type: Boolean,
                required: true,
            },
            deleteTagsPostUrl: {
                type: String,
                required: true,
            },
        },
        data() {
            return {
                is_active: true,
            }
        },
        methods: {
            deleteTag() {
                axios
                    .post(this.deleteTagsPostUrl, qs.stringify({
                        'tag_name': this.text,
                        'media_id': this.mediaId,
                    }))
                    .then(_ => {
                        this.is_active = false

                    });
            }
        }

    }
</script>

<style scoped>
    /* default styles for all the tags */
    .media-tag-label {
        width: auto;
        border-radius: 20px;
        padding-left: 11px;
        padding-right: 11px;
        margin: 8px 3px 8px 3px;
        color:white;
        background: #758093;
        display: inline-block;
        border: 0;
        text-transform: uppercase;
        font-weight: bold;
    }
</style>
