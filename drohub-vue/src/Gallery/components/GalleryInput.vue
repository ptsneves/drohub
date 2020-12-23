<template>
    <div class="gallery-input-container">
        <inline-svg
            v-bind:src="require('../../../../wwwroot/images/assets/timeline-search.svg')"
            class="search-image-helper"
        ></inline-svg>
        <vue-tags-input
            v-model="tag"
            v-bind:placeholder="placeholder"
            v-bind:tags="getTags"
            v-bind:add-on-key="[13, ',', ';']"
            v-on:tags-changed="trackTagsChanged"
        />
    </div>
</template>

<script>
import VueTagsInput from '@johmun/vue-tags-input';
import InlineSvg from 'vue-inline-svg';

export default {
    name: 'GalleryInput',
    props: {
        placeholder: {
            type: String,
            required: true,
        }
    },
    components: {
        VueTagsInput,
        InlineSvg,
    },
    computed: {
      getTags() {
          this.tags =this.$store.state.filtered_tags;
          return this.$store.state.filtered_tags.map(t => ({ "text": t}))
      }
    },
    methods: {
        trackTagsChanged(raw_tag_array) {
            let tag_array = raw_tag_array.map(t => t.text);
            let diff = [];
            if (tag_array.length > this.tags.length) {
                diff = tag_array.filter(t => !this.tags.includes(t));
                this.$store.commit('ADD_TRACKED_TAG', diff[0])
            }
            else {
                diff = this.tags.filter(t => !tag_array.includes(t));
                this.$store.commit('REMOVE_TRACKED_TAG', diff[0])
            }
            this.tags = tag_array;
        }
    },
    data() {
        return {
            tag: '',
            tags: [],
        };
    },
};
</script>

<style lang="css">
    @import "../../assets/vue-tags-input-custom.css";
    .search-image-helper {
        background: white;
        height: auto;
    }
    .gallery-input-container {
        display: flex;
        background: white;
        padding: 0 4px 0 7px;
        border-radius: 2px;
    }
</style>
