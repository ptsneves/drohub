<template>
    <div>
        <vue-tags-input
            v-model="tag"
            v-bind:tags="getTags"
            v-bind:add-on-key="[13, ',', ';']"
            v-on:tags-changed="trackTagsChanged"
        />
    </div>
</template>

<script>
import VueTagsInput from '@johmun/vue-tags-input';

export default {
    name: 'GalleryInput',
    components: {
        VueTagsInput,
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
</style>
